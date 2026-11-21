using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CaroOnline.Server
{
    // Lõi đa phòng: mỗi trận là một GameRoom riêng (GameManager, đồng hồ lượt, khóa, khán giả riêng)
    public sealed class RoomServerManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions HistoryWriteOptions = new()
        {
            WriteIndented = true
        };

        private readonly ServerSettings settings;
        private readonly ConcurrentDictionary<int, TcpClient> clients = new();
        private readonly ConcurrentDictionary<int, ServerSession> activeSessions = new();
        private readonly ConcurrentDictionary<string, ServerSession> sessionsByToken = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ServerSession> sessionsByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, GameRoom> rooms = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> pendingChallenges = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim lobbyLock = new(1, 1);
        private readonly SemaphoreSlim historyLock = new(1, 1);
        private TcpListener? listener;
        private CancellationTokenSource? serverCancellation;
        private int nextConnectionId;

        public event Action<string>? LogReceived;
        public event Action<int>? ClientCountChanged;
        public event Action<string[]>? ClientListChanged;
        public event Action<int>? RoomCountChanged;

        public bool IsRunning { get; private set; }
        public int ConnectedClientCount => clients.Count;
        public int RoomCount => rooms.Count;
        public int Port => settings.Port;
        public int MaxConnections => settings.MaxConnections;
        public int MaxRooms => settings.MaxRooms;
        public Database? Database { get; set; }
        public RoomServerManager(ServerSettings? serverSettings = null)
        {
            settings = serverSettings ?? ServerSettings.Load();
            settings.Validate();
        }

        // ==========================================
        // START / STOP
        // ==========================================

        public async Task StartServer()
        {
            if (IsRunning)
                return;

            ResetState();

            CancellationTokenSource cancellation = new();
            serverCancellation = cancellation;
            CancellationToken token = cancellation.Token;

            TcpListener? tcpListener = null;
            bool listening = false;

            try
            {
                tcpListener = new TcpListener(IPAddress.Parse(settings.ListenAddress), settings.Port);
                tcpListener.Start();
                listener = tcpListener;
                listening = true;
                IsRunning = true;

                Log($"Game Server đang chạy tại {settings.ListenAddress}:{settings.Port} " +
                    $"(tối đa {settings.MaxRooms} phòng cùng lúc).");

                while (!token.IsCancellationRequested)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync(token);

                    if (clients.Count >= settings.MaxConnections)
                    {
                        client.Close();
                        Log("Từ chối kết nối: đã đạt giới hạn số kết nối.");
                        continue;
                    }

                    int connectionId = Interlocked.Increment(ref nextConnectionId);
                    clients[connectionId] = client;
                    ClientCountChanged?.Invoke(clients.Count);
                    _ = HandleConnectionAsync(connectionId, client, token);
                }
            }
            catch (OperationCanceledException)
            {
                Log("Server đã ngừng nhận kết nối.");
            }
            catch (ObjectDisposedException)
            {
                // Listener đã bị đóng khi dừng Server.
            }
            catch (SocketException ex)
            {
                if (!listening)
                    Log($"Không thể mở cổng {settings.Port}: {ex.Message}");
                else if (IsRunning)
                    Log("Lỗi Socket: " + ex.Message);
            }
            catch (Exception ex)
            {
                Log("Lỗi Server: " + ex.Message);
            }
            finally
            {
                // Tránh ghi đè trạng thái nếu Server đã được khởi động lại
                if (ReferenceEquals(serverCancellation, cancellation))
                    IsRunning = false;

                cancellation.Cancel();

                try
                {
                    tcpListener?.Stop();
                }
                catch
                {
                    // Bỏ qua lỗi khi đóng Listener.
                }

                if (ReferenceEquals(listener, tcpListener))
                    listener = null;
            }
        }

        public void StopServer()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            serverCancellation?.Cancel();

            try
            {
                listener?.Stop();
            }
            catch
            {
                // Bỏ qua lỗi khi đóng Listener.
            }

            foreach (TcpClient client in clients.Values)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    // Bỏ qua lỗi khi đóng Client.
                }
            }

            clients.Clear();

            // Hủy đồng hồ của mọi phòng, xóa toàn bộ phòng và phiên
            ResetState();

            ClientCountChanged?.Invoke(0);
            ClientListChanged?.Invoke(Array.Empty<string>());
            RoomCountChanged?.Invoke(0);

            Log("Đã dừng Server.");
        }

        // Hủy mọi bộ đếm và xóa sạch trạng thái phòng/phiên
        private void ResetState()
        {
            foreach (GameRoom room in rooms.Values)
                room.TurnTimer?.Cancel();

            foreach (ServerSession session in sessionsByName.Values)
                session.ReconnectExpiry?.Cancel();

            rooms.Clear();
            pendingChallenges.Clear();
            activeSessions.Clear();
            sessionsByToken.Clear();
            sessionsByName.Clear();
        }

        // ==========================================
        // KẾT NỐI
        // ==========================================

        private async Task HandleConnectionAsync(int connectionId, TcpClient client, CancellationToken serverToken)
        {
            ServerSession? session = null;
            try
            {
                client.NoDelay = true;
                NetworkStream stream = client.GetStream();
                using CancellationTokenSource handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
                handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(settings.HandshakeTimeoutSeconds));
                Message? hello = await ReadMessageAsync(stream, handshakeTimeout.Token);
                if (hello == null || !string.Equals(hello.Type, "HELLO", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteMessageAsync(stream, CreateError("HELLO_REQUIRED", "Send a HELLO message first."), serverToken);
                    return;
                }

                session = await RegisterSessionAsync(connectionId, client, hello);
                if (session == null)
                {
                    await WriteMessageAsync(stream, CreateError("INVALID_LOGIN", "The name or resume token is invalid or already in use."), serverToken);
                    return;
                }

                await SendToSessionAsync(session, new Message
                {
                    Type = "WELCOME",
                    Name = session.Name,
                    ResumeToken = session.ResumeToken,
                    PlayerIndex = session.PlayerIndex ?? -1,
                    RoomId = session.RoomId
                }, serverToken);

                if (!string.IsNullOrEmpty(session.RoomId) && rooms.TryGetValue(session.RoomId, out GameRoom? resumedRoom))
                {
                    await HandlePlayerReconnectAsync(session, resumedRoom, serverToken);
                    await SendRoomSnapshotAsync(session, resumedRoom, serverToken);
                }

                await BroadcastPlayerListAsync(serverToken);
                await SendRoomListAsync(null, serverToken);
                Log($"{session.Name} đã kết nối.");

                while (!serverToken.IsCancellationRequested)
                {
                    Message? message = await ReadMessageAsync(stream, serverToken);
                    if (message == null)
                        break;
                    if (!ValidateMessageEnvelope(message, out string errorCode, out string errorText))
                    {
                        await SendErrorAsync(session, errorCode, errorText, serverToken);
                        continue;
                    }

                    await ProcessMessageAsync(session, message, serverToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Server dừng hoặc hết thời gian chờ handshake.
            }
            catch (InvalidDataException ex)
            {
                if (session != null)
                    await SendErrorAsync(session, "INVALID_FRAME", ex.Message, CancellationToken.None);
                Log("Khung dữ liệu không hợp lệ: " + ex.Message);
            }
            catch (JsonException ex)
            {
                if (session != null)
                    await SendErrorAsync(session, "INVALID_JSON", "Message payload is not valid JSON.", CancellationToken.None);
                Log("JSON không hợp lệ: " + ex.Message);
            }
            catch (IOException ex)
            {
                Log("Kết nối I/O đã kết thúc: " + ex.Message);
            }
            catch (SocketException ex)
            {
                Log("Kết nối socket đã kết thúc: " + ex.Message);
            }
            catch (Exception ex)
            {
                Log("Lỗi xử lý Client: " + ex.Message);
            }
            finally
            {
                clients.TryRemove(connectionId, out _);
                if (session != null)
                    await HandleSessionDisconnectedAsync(connectionId, session);
                client.Close();
                ClientCountChanged?.Invoke(clients.Count);
                PublishState();
            }
        }

        private async Task<ServerSession?> RegisterSessionAsync(int connectionId, TcpClient client, Message hello)
        {
            string name = (hello.Name ?? string.Empty).Trim();
            if (!IsValidPlayerName(name))
                return null;

            await lobbyLock.WaitAsync();
            try
            {
                ServerSession? session = null;
                if (!string.IsNullOrWhiteSpace(hello.ResumeToken) &&
                    sessionsByToken.TryGetValue(hello.ResumeToken, out ServerSession? returningSession))
                {
                    if (returningSession.Client != null || !string.Equals(returningSession.Name, name, StringComparison.OrdinalIgnoreCase))
                        return null;
                    session = returningSession;
                    session.ReconnectExpiry?.Cancel();
                    session.ReconnectExpiry?.Dispose();
                    session.ReconnectExpiry = null;
                }
                else
                {
                    if (sessionsByName.ContainsKey(name))
                        return null;
                    session = new ServerSession
                    {
                        Name = name,
                        ResumeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24))
                    };
                    sessionsByToken[session.ResumeToken] = session;
                    sessionsByName[session.Name] = session;
                }

                session.Client = client;
                session.ConnectionId = connectionId;
                session.DisconnectedAtUtc = null;
                activeSessions[connectionId] = session;
                return session;
            }
            finally
            {
                lobbyLock.Release();
            }
        }

        // ==========================================
        // XỬ LÝ MESSAGE
        // ==========================================

        private async Task ProcessMessageAsync(ServerSession session, Message message, CancellationToken token)
        {
            switch (message.Type.Trim().ToUpperInvariant())
            {
                case "GET_PLAYERS":
                    await SendPlayerListAsync(session, token);
                    break;
                case "GET_ROOMS":
                    await SendRoomListAsync(session, token);
                    break;
                case "GET_HISTORY":
                    await SendHistoryAsync(session, token);
                    break;
                case "CHALLENGE":
                    await SendChallengeAsync(session, message, token);
                    break;
                case "CHALLENGE_ACCEPT":
                    await AcceptChallengeAsync(session, message, token);
                    break;
                case "CHALLENGE_REJECT":
                    await RejectChallengeAsync(session, message, token);
                    break;
                case "JOIN_SPECTATOR":
                    await AddSpectatorToRoomAsync(session, message.RoomId, token);
                    break;
                case "LEAVE_ROOM":
                    await LeaveRoomAsync(session, token);
                    break;
                case "MOVE":
                    await ValidateAndApplyMoveAsync(session, message, token);
                    break;
                case "UNDO":
                    await UndoLastMoveAsync(session, token);
                    break;
                case "NEW_GAME":
                    await StartNewRoomGameAsync(session, token);
                    break;
                case "PING":
                    await SendToSessionAsync(session, new Message { Type = "PONG" }, token);
                    break;
                default:
                    await SendErrorAsync(session, "UNKNOWN_MESSAGE", "The message type is not supported.", token);
                    break;
            }
        }

        // ==========================================
        // THÁCH ĐẤU VÀ TẠO PHÒNG
        // ==========================================

        private async Task SendChallengeAsync(ServerSession challenger, Message message, CancellationToken token)
        {
            string targetName = (message.TargetName ?? string.Empty).Trim();
            if (targetName.Length == 0 || string.Equals(targetName, challenger.Name, StringComparison.OrdinalIgnoreCase))
            {
                await SendErrorAsync(challenger, "INVALID_TARGET", "Choose another online player to challenge.", token);
                return;
            }

            await lobbyLock.WaitAsync(token);
            ServerSession? target = null;
            string? error = null;
            try
            {
                if (!sessionsByName.TryGetValue(targetName, out target) || target.Client == null)
                    error = "The selected player is not online.";
                else if (!string.IsNullOrEmpty(target.RoomId) || !string.IsNullOrEmpty(challenger.RoomId))
                    error = "Both players must be outside a room.";
                else if (rooms.Count >= settings.MaxRooms)
                    error = "The server has reached its room limit. Try again later.";
                else if (!pendingChallenges.TryAdd(target.Name, challenger.Name))
                    error = "The target already has a pending challenge.";
            }
            finally
            {
                lobbyLock.Release();
            }

            if (error != null)
            {
                await SendErrorAsync(challenger, "CHALLENGE_FAILED", error, token);
                return;
            }

            await SendToSessionAsync(target!, new Message { Type = "CHALLENGE", Name = challenger.Name }, token);
            await SendToSessionAsync(challenger, new Message { Type = "INFO", Data = $"Challenge sent to {target!.Name}." }, token);
        }

        private async Task AcceptChallengeAsync(ServerSession accepter, Message message, CancellationToken token)
        {
            string challengerName = (message.TargetName ?? string.Empty).Trim();
            ServerSession? challenger = null;
            GameRoom? room = null;
            string? error = null;

            await lobbyLock.WaitAsync(token);
            try
            {
                if (!pendingChallenges.TryGetValue(accepter.Name, out string? pendingName) ||
                    !string.Equals(pendingName, challengerName, StringComparison.OrdinalIgnoreCase))
                {
                    error = "There is no matching pending challenge.";
                }
                else if (!sessionsByName.TryGetValue(challengerName, out challenger) || challenger.Client == null)
                {
                    error = "The challenger is no longer online.";
                    pendingChallenges.TryRemove(accepter.Name, out _);
                }
                else if (!string.IsNullOrEmpty(accepter.RoomId) || !string.IsNullOrEmpty(challenger.RoomId))
                {
                    error = "One of the players is already in a room.";
                }
                else if (rooms.Count >= settings.MaxRooms)
                {
                    error = "The server has reached its room limit. Try again later.";
                }
                else
                {
                    string roomId;
                    do
                    {
                        roomId = Guid.NewGuid().ToString("N")[..8];
                    }
                    while (rooms.ContainsKey(roomId));

                    room = new GameRoom
                    {
                        RoomId = roomId,
                        PlayerOne = challenger,
                        PlayerTwo = accepter,
                        PlayerOneName = challenger.Name,
                        PlayerTwoName = accepter.Name
                    };
                    challenger.RoomId = roomId;
                    challenger.PlayerIndex = 0;
                    challenger.IsSpectator = false;
                    accepter.RoomId = roomId;
                    accepter.PlayerIndex = 1;
                    accepter.IsSpectator = false;
                    rooms[roomId] = room;
                    pendingChallenges.TryRemove(accepter.Name, out _);
                }
            }
            finally
            {
                lobbyLock.Release();
            }

            if (error != null || room == null)
            {
                await SendErrorAsync(accepter, "CHALLENGE_FAILED", error ?? "Unable to create a room.", token);
                return;
            }

            StartTurnTimer(room);
            await BroadcastRoomAsync(room, CreateRoomStateMessage(room), token);
            await BroadcastPlayerListAsync(token);
            await SendRoomListAsync(null, token);
            Log($"Phòng {room.RoomId} bắt đầu: {room.PlayerOneName} vs {room.PlayerTwoName} " +
                $"({rooms.Count}/{settings.MaxRooms} phòng).");
        }

        private async Task RejectChallengeAsync(ServerSession rejecter, Message message, CancellationToken token)
        {
            string challengerName = (message.TargetName ?? string.Empty).Trim();

            // Chỉ hủy lời thách đấu nếu đúng người thách đấu
            if (pendingChallenges.TryGetValue(rejecter.Name, out string? pendingName) &&
                string.Equals(pendingName, challengerName, StringComparison.OrdinalIgnoreCase))
            {
                pendingChallenges.TryRemove(rejecter.Name, out _);
            }

            if (sessionsByName.TryGetValue(challengerName, out ServerSession? challenger))
                await SendToSessionAsync(challenger, new Message { Type = "CHALLENGE_REJECT", Name = rejecter.Name }, token);
        }

        // ==========================================
        // KHÁN GIẢ VÀ RỜI PHÒNG
        // ==========================================

        private async Task AddSpectatorToRoomAsync(ServerSession spectator, string roomId, CancellationToken token)
        {
            if (!rooms.TryGetValue(roomId ?? string.Empty, out GameRoom? room))
            {
                await SendErrorAsync(spectator, "ROOM_NOT_FOUND", "The requested match is not available for spectating.", token);
                return;
            }

            await room.SyncLock.WaitAsync(token);
            string? error = null;
            try
            {
                if (room.Game.IsGameOver)
                    error = "The match has already ended.";
                else if (!string.IsNullOrEmpty(spectator.RoomId))
                    error = "Leave the current room before joining as a spectator.";
                else if (room.Spectators.Count >= settings.MaxSpectatorsPerRoom)
                    error = "The room has reached its spectator limit.";
                else
                {
                    room.Spectators[spectator.SessionId] = spectator;
                    spectator.RoomId = room.RoomId;
                    spectator.PlayerIndex = null;
                    spectator.IsSpectator = true;
                }
            }
            finally
            {
                room.SyncLock.Release();
            }

            if (error != null)
            {
                await SendErrorAsync(spectator, "SPECTATE_FAILED", error, token);
                return;
            }

            await SendRoomSnapshotAsync(spectator, room, token);
            await BroadcastRoomAsync(room, CreateRoomStateMessage(room), token);
            await BroadcastPlayerListAsync(token);
            await SendRoomListAsync(null, token);
        }

        private async Task LeaveRoomAsync(ServerSession session, CancellationToken token)
        {
            if (string.IsNullOrEmpty(session.RoomId) || !rooms.TryGetValue(session.RoomId, out GameRoom? room))
            {
                await SendErrorAsync(session, "NOT_IN_ROOM", "You are not in a room.", token);
                return;
            }

            if (session.IsSpectator)
            {
                await room.SyncLock.WaitAsync(token);
                try
                {
                    room.Spectators.TryRemove(session.SessionId, out _);
                    session.RoomId = string.Empty;
                    session.IsSpectator = false;
                }
                finally
                {
                    room.SyncLock.Release();
                }
                await BroadcastRoomAsync(room, CreateRoomStateMessage(room), token);
                await BroadcastPlayerListAsync(token);
                await SendRoomListAsync(null, token);
                return;
            }

            MatchHistoryRecord? record = null;
            List<ServerSession> recipients;
            await room.SyncLock.WaitAsync(token);
            try
            {
                // Lấy danh sách người nhận TRƯỚC khi đóng phòng (đóng phòng sẽ xóa khán giả)
                recipients = GetRoomSessions(room);
                record = FinishRoom(room, session.PlayerIndex == 0 ? "PLAYER_TWO_WIN_BY_FORFEIT" : "PLAYER_ONE_WIN_BY_FORFEIT");
            }
            finally
            {
                room.SyncLock.Release();
            }

            if (record != null)
            {
                await BroadcastToSessionsAsync(recipients, new Message
                {
                    Type = "GAME_OVER",
                    RoomId = room.RoomId,
                    Result = record.Result,
                    Data = record.Result
                }, token);
                await SaveMatchHistoryAsync(record, token);
                await BroadcastPlayerListAsync(token);
                await SendRoomListAsync(null, token);
                Log($"Phòng {room.RoomId} kết thúc: {record.Result}.");
            }
        }

        // ==========================================
        // NƯỚC ĐI, UNDO, VÁN MỚI
        // ==========================================

        private async Task ValidateAndApplyMoveAsync(ServerSession session, Message message, CancellationToken token)
        {
            if (session.IsSpectator)
            {
                await SendErrorAsync(session, "SPECTATOR_READ_ONLY", "Spectators cannot submit moves.", token);
                return;
            }
            if (string.IsNullOrEmpty(session.RoomId) || !rooms.TryGetValue(session.RoomId, out GameRoom? room))
            {
                await SendErrorAsync(session, "NOT_IN_ROOM", "Join a match before moving.", token);
                return;
            }

            MatchHistoryRecord? completedRecord = null;
            Message? rejection = null;
            List<ServerSession> recipients;
            List<Message> outgoing = new();
            await room.SyncLock.WaitAsync(token);
            try
            {
                // Lấy danh sách người nhận TRƯỚC khi có thể đóng phòng
                recipients = GetRoomSessions(room);

                int playerIndex = session.PlayerIndex ?? -1;
                if (room.IsPaused || (playerIndex != 0 && playerIndex != 1) ||
                    !ReferenceEquals(playerIndex == 0 ? room.PlayerOne : room.PlayerTwo, session))
                {
                    rejection = CreateError("NOT_A_PLAYER", "Only a connected player in this room can move.");
                }
                else if (!room.Game.MakeMove(message.X, message.Y, playerIndex))
                {
                    rejection = CreateError("INVALID_MOVE", "Move must be in bounds, on an empty cell, and made by the player whose turn it is.");
                }
                else
                {
                    bool won = room.Game.CheckWinner(message.X, message.Y, playerIndex);
                    bool draw = !won && room.Game.IsBoardFull();
                    outgoing.Add(new Message
                    {
                        Type = "MOVE",
                        RoomId = room.RoomId,
                        X = message.X,
                        Y = message.Y,
                        PlayerIndex = playerIndex
                    });

                    if (won || draw)
                    {
                        completedRecord = FinishRoom(room, draw ? "DRAW" : playerIndex == 0 ? "PLAYER_ONE_WIN" : "PLAYER_TWO_WIN");
                        outgoing.Add(new Message
                        {
                            Type = "GAME_OVER",
                            RoomId = room.RoomId,
                            PlayerIndex = playerIndex,
                            Result = completedRecord?.Result ?? (draw ? "DRAW" : "WIN"),
                            Data = completedRecord?.Result ?? (draw ? "DRAW" : "WIN")
                        });
                    }
                    else
                    {
                        StartTurnTimer(room);
                        outgoing.Add(new Message
                        {
                            Type = "TURN",
                            RoomId = room.RoomId,
                            PlayerIndex = room.Game.CurrentPlayer,
                            TurnDeadlineUtc = room.TurnDeadlineUtc
                        });
                    }
                }
            }
            finally
            {
                room.SyncLock.Release();
            }

            // Nước đi bị từ chối chỉ báo lỗi cho người gửi, không gửi cho cả phòng
            if (rejection != null)
            {
                await SendToSessionAsync(session, rejection, token);
                return;
            }

            foreach (Message update in outgoing)
                await BroadcastToSessionsAsync(recipients, update, token);

            if (completedRecord != null)
            {
                await SaveMatchHistoryAsync(completedRecord, token);
                await BroadcastPlayerListAsync(token);
                await SendRoomListAsync(null, token);
                Log($"Phòng {room.RoomId} kết thúc: {completedRecord.Result}.");
            }
        }

        private async Task UndoLastMoveAsync(ServerSession session, CancellationToken token)
        {
            if (session.IsSpectator || string.IsNullOrEmpty(session.RoomId) || !rooms.TryGetValue(session.RoomId, out GameRoom? room))
            {
                await SendErrorAsync(session, "NOT_A_PLAYER", "Only a player in a match can request undo.", token);
                return;
            }

            bool undone = false;
            await room.SyncLock.WaitAsync(token);
            try
            {
                if (!room.IsPaused &&
                    room.Game.LastMovePlayerIndex == session.PlayerIndex &&
                    room.Game.Undo())
                {
                    undone = true;
                    StartTurnTimer(room);
                }
            }
            finally
            {
                room.SyncLock.Release();
            }

            if (undone)
                await BroadcastRoomAsync(room, CreateRoomStateMessage(room), token);
            else
                await SendErrorAsync(session, "UNDO_NOT_ALLOWED", "Only the player who made the latest move can undo it.", token);
        }

        private async Task StartNewRoomGameAsync(ServerSession session, CancellationToken token)
        {
            if (session.IsSpectator || string.IsNullOrEmpty(session.RoomId) || !rooms.TryGetValue(session.RoomId, out GameRoom? room))
            {
                await SendErrorAsync(session, "NOT_A_PLAYER", "Only a player in a match can start a new game.", token);
                return;
            }

            await room.SyncLock.WaitAsync(token);
            try
            {
                room.Game.NewGame();

                // Chỉ tiếp tục khi CẢ HAI người chơi đang online
                bool bothOnline = room.PlayerOne.Client != null && room.PlayerTwo.Client != null;
                room.IsPaused = !bothOnline;

                if (bothOnline)
                {
                    StartTurnTimer(room);
                }
                else
                {
                    room.TurnTimer?.Cancel();
                    room.TurnDeadlineUtc = null;
                }
            }
            finally
            {
                room.SyncLock.Release();
            }
            await BroadcastRoomAsync(room, CreateRoomStateMessage(room), token);
        }

        // ==========================================
        // ĐỒNG HỒ LƯỢT (MỖI PHÒNG MỘT ĐỒNG HỒ)
        // ==========================================

        private void StartTurnTimer(GameRoom room)
        {
            room.TurnTimer?.Cancel();
            room.TurnTimer?.Dispose();
            room.TurnTimer = null;
            if (room.Game.IsGameOver || room.IsPaused || !IsRunning)
                return;

            CancellationTokenSource timer = new();
            room.TurnTimer = timer;
            room.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(settings.TurnSeconds);
            _ = RunTurnTimerAsync(room, timer);
        }

        private async Task RunTurnTimerAsync(GameRoom room, CancellationTokenSource timer)
        {
            try
            {
                while (!timer.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), timer.Token);
                    MatchHistoryRecord? completedRecord = null;
                    Message? timerMessage = null;
                    List<ServerSession> recipients;
                    int winner = -1;
                    await room.SyncLock.WaitAsync(timer.Token);
                    try
                    {
                        if (!ReferenceEquals(room.TurnTimer, timer) || room.Game.IsGameOver || room.IsPaused)
                            return;

                        // Lấy danh sách người nhận TRƯỚC khi có thể đóng phòng
                        recipients = GetRoomSessions(room);

                        int remaining = Math.Max(0, (int)Math.Ceiling((room.TurnDeadlineUtc!.Value - DateTime.UtcNow).TotalSeconds));
                        if (remaining == 0)
                        {
                            winner = 1 - room.Game.CurrentPlayer;
                            completedRecord = FinishRoom(room, winner == 0 ? "PLAYER_ONE_WIN_BY_TIMEOUT" : "PLAYER_TWO_WIN_BY_TIMEOUT");
                        }
                        else
                        {
                            timerMessage = new Message
                            {
                                Type = "TURN_TIMER",
                                RoomId = room.RoomId,
                                PlayerIndex = room.Game.CurrentPlayer,
                                Data = remaining.ToString(),
                                TurnDeadlineUtc = room.TurnDeadlineUtc
                            };
                        }
                    }
                    finally
                    {
                        room.SyncLock.Release();
                    }

                    if (timerMessage != null)
                        await BroadcastToSessionsAsync(recipients, timerMessage, CancellationToken.None);

                    if (completedRecord != null)
                    {
                        await BroadcastToSessionsAsync(recipients, new Message
                        {
                            Type = "GAME_OVER",
                            RoomId = room.RoomId,
                            PlayerIndex = winner,
                            Result = completedRecord.Result,
                            Data = completedRecord.Result
                        }, CancellationToken.None);
                        await SaveMatchHistoryAsync(completedRecord, CancellationToken.None);
                        await BroadcastPlayerListAsync(CancellationToken.None);
                        await SendRoomListAsync(null, CancellationToken.None);
                        Log($"Phòng {room.RoomId} kết thúc do hết giờ.");
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Có nước đi, kết nối lại hoặc Server dừng và đã thay thế đồng hồ này.
            }
            catch (ObjectDisposedException)
            {
                // Đồng hồ đã bị hủy và giải phóng.
            }
            catch (Exception ex)
            {
                Log("Lỗi đồng hồ lượt: " + ex.Message);
            }
        }

        // ==========================================
        // KẾT THÚC PHÒNG
        // ==========================================

        // Gọi khi đang giữ room.SyncLock. Trả về null nếu phòng đã được đóng trước đó.
        private MatchHistoryRecord? FinishRoom(GameRoom room, string result)
        {
            // Chỉ một luồng đóng được phòng (tránh lưu lịch sử hai lần)
            if (!rooms.TryRemove(room.RoomId, out _))
                return null;

            room.Game.FinishGame();
            room.TurnTimer?.Cancel();
            room.TurnTimer?.Dispose();
            room.TurnTimer = null;
            room.TurnDeadlineUtc = null;
            room.PlayerOne.RoomId = string.Empty;
            room.PlayerOne.PlayerIndex = null;
            room.PlayerTwo.RoomId = string.Empty;
            room.PlayerTwo.PlayerIndex = null;
            foreach (ServerSession spectator in room.Spectators.Values)
            {
                spectator.RoomId = string.Empty;
                spectator.IsSpectator = false;
            }
            room.Spectators.Clear();
            return new MatchHistoryRecord
            {
                RoomId = room.RoomId,
                PlayerOne = room.PlayerOneName,
                PlayerTwo = room.PlayerTwoName,
                Result = result,
                StartedAtUtc = room.StartedAtUtc,
                FinishedAtUtc = DateTime.UtcNow,
                Moves = room.Game.GetMoveHistory()
            };
        }

        // ==========================================
        // MẤT KẾT NỐI VÀ KẾT NỐI LẠI
        // ==========================================

        private async Task HandleSessionDisconnectedAsync(int connectionId, ServerSession session)
        {
            List<ServerSession> challengeCancellationRecipients;
            await lobbyLock.WaitAsync();
            try
            {
                if (!activeSessions.TryGetValue(connectionId, out ServerSession? current) || !ReferenceEquals(current, session))
                    return;
                activeSessions.TryRemove(connectionId, out _);
                session.Client = null;
                session.ConnectionId = -1;
                session.DisconnectedAtUtc = DateTime.UtcNow;

                // Lời thách đấu liên quan đến người này không còn hiệu lực
                challengeCancellationRecipients = RemovePendingChallenges(session.Name);

                if (session.IsSpectator && rooms.TryGetValue(session.RoomId, out GameRoom? spectatorRoom))
                {
                    spectatorRoom.Spectators.TryRemove(session.SessionId, out _);
                    session.RoomId = string.Empty;
                    session.IsSpectator = false;
                }
                else if (session.PlayerIndex.HasValue && rooms.TryGetValue(session.RoomId, out GameRoom? room))
                {
                    await room.SyncLock.WaitAsync();
                    try
                    {
                        room.IsPaused = true;
                        room.TurnTimer?.Cancel();
                        room.TurnDeadlineUtc = null;
                    }
                    finally
                    {
                        room.SyncLock.Release();
                    }
                    _ = BroadcastRoomAsync(room, new Message
                    {
                        Type = "PLAYER_DISCONNECTED",
                        RoomId = room.RoomId,
                        Name = session.Name,
                        Data = settings.ReconnectSeconds.ToString()
                    }, CancellationToken.None);
                }

                CancellationTokenSource expiry = new();
                session.ReconnectExpiry?.Cancel();
                session.ReconnectExpiry?.Dispose();
                session.ReconnectExpiry = expiry;
                _ = ExpireDisconnectedSessionAsync(session, expiry);
            }
            finally
            {
                lobbyLock.Release();
            }

            foreach (ServerSession recipient in challengeCancellationRecipients)
            {
                await SendToSessionAsync(recipient, new Message
                {
                    Type = "CHALLENGE_CANCEL",
                    Name = session.Name
                }, CancellationToken.None);
            }

            Log($"{session.Name} mất kết nối; thời gian chờ kết nối lại là {settings.ReconnectSeconds} giây.");
            await BroadcastPlayerListAsync(CancellationToken.None);
            await SendRoomListAsync(null, CancellationToken.None);
        }

        private async Task HandlePlayerReconnectAsync(ServerSession session, GameRoom room, CancellationToken token)
        {
            if (!session.PlayerIndex.HasValue)
                return;

            await room.SyncLock.WaitAsync(token);
            try
            {
                // Chỉ tiếp tục ván khi CẢ HAI người chơi đang online
                if (room.PlayerOne.Client != null && room.PlayerTwo.Client != null)
                {
                    room.IsPaused = false;
                    StartTurnTimer(room);
                }
            }
            finally
            {
                room.SyncLock.Release();
            }

            await BroadcastRoomAsync(room, new Message
            {
                Type = "PLAYER_RECONNECTED",
                RoomId = room.RoomId,
                Name = session.Name
            }, token);
        }

        private async Task ExpireDisconnectedSessionAsync(ServerSession session, CancellationTokenSource expiry)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.ReconnectSeconds), expiry.Token);
                MatchHistoryRecord? record = null;
                GameRoom? affectedRoom = null;
                List<ServerSession> recipients = new();
                await lobbyLock.WaitAsync(expiry.Token);
                try
                {
                    if (!ReferenceEquals(session.ReconnectExpiry, expiry) || session.Client != null)
                        return;
                    sessionsByToken.TryRemove(session.ResumeToken, out _);
                    sessionsByName.TryRemove(session.Name, out _);
                    RemovePendingChallenges(session.Name);
                    if (!string.IsNullOrEmpty(session.RoomId) && rooms.TryGetValue(session.RoomId, out affectedRoom) && session.PlayerIndex.HasValue)
                    {
                        await affectedRoom.SyncLock.WaitAsync(expiry.Token);
                        try
                        {
                            // Lấy danh sách người nhận TRƯỚC khi đóng phòng
                            recipients = GetRoomSessions(affectedRoom);
                            int winner = 1 - session.PlayerIndex.Value;
                            record = FinishRoom(affectedRoom, winner == 0 ? "PLAYER_ONE_WIN_BY_DISCONNECT" : "PLAYER_TWO_WIN_BY_DISCONNECT");
                        }
                        finally
                        {
                            affectedRoom.SyncLock.Release();
                        }
                    }
                }
                finally
                {
                    lobbyLock.Release();
                }

                if (record != null && affectedRoom != null)
                {
                    await BroadcastToSessionsAsync(recipients, new Message
                    {
                        Type = "GAME_OVER",
                        RoomId = affectedRoom.RoomId,
                        Result = record.Result,
                        Data = record.Result
                    }, CancellationToken.None);
                    await SaveMatchHistoryAsync(record, CancellationToken.None);
                    Log($"Phòng {affectedRoom.RoomId} kết thúc: {record.Result}.");
                }
                await BroadcastPlayerListAsync(CancellationToken.None);
                await SendRoomListAsync(null, CancellationToken.None);
                Log($"Hết thời gian chờ kết nối lại của {session.Name}.");
            }
            catch (OperationCanceledException)
            {
                // Người chơi đã kết nối lại trước khi hết thời gian chờ.
            }
            catch (ObjectDisposedException)
            {
                // Bộ đếm đã bị hủy và giải phóng khi người chơi kết nối lại.
            }
            catch (Exception ex)
            {
                Log("Lỗi hết hạn kết nối lại: " + ex.Message);
            }
        }

        private List<ServerSession> RemovePendingChallenges(string playerName)
        {
            List<ServerSession> cancellationRecipients = new();
            foreach (KeyValuePair<string, string> pair in pendingChallenges)
            {
                if (string.Equals(pair.Value, playerName, StringComparison.OrdinalIgnoreCase))
                {
                    if (sessionsByName.TryGetValue(pair.Key, out ServerSession? recipient) && recipient.Client != null)
                        cancellationRecipients.Add(recipient);
                    pendingChallenges.TryRemove(pair.Key, out _);
                }
                else if (string.Equals(pair.Key, playerName, StringComparison.OrdinalIgnoreCase))
                {
                    pendingChallenges.TryRemove(pair.Key, out _);
                }
            }

            return cancellationRecipients;
        }

        // ==========================================
        // TRẠNG THÁI PHÒNG / DANH SÁCH
        // ==========================================

        private async Task SendRoomSnapshotAsync(ServerSession session, GameRoom room, CancellationToken token)
        {
            await room.SyncLock.WaitAsync(token);
            Message snapshot;
            try
            {
                snapshot = CreateRoomStateMessage(room);
            }
            finally
            {
                room.SyncLock.Release();
            }

            // Báo cho người nhận biết họ là người chơi nào hay khán giả
            snapshot.YourPlayerIndex = session.PlayerIndex ?? -1;
            snapshot.IsSpectator = session.IsSpectator;
            await SendToSessionAsync(session, snapshot, token);
        }

        private Message CreateRoomStateMessage(GameRoom room)
        {
            return new Message
            {
                Type = "ROOM_STATE",
                RoomId = room.RoomId,
                Board = room.Game.GetBoardSnapshot(),
                Moves = room.Game.GetMoveHistory(),
                PlayerIndex = room.Game.CurrentPlayer,
                Name = room.PlayerOneName,
                TargetName = room.PlayerTwoName,
                TurnDeadlineUtc = room.TurnDeadlineUtc,
                Data = room.IsPaused ? "PAUSED" : room.Game.IsGameOver ? "GAME_OVER" : "ACTIVE"
            };
        }

        private async Task SendPlayerListAsync(ServerSession? recipient, CancellationToken token)
        {
            List<PlayerSummary> players = sessionsByName.Values
                .Where(player => player.Client != null)
                .OrderBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
                .Select(player => new PlayerSummary { Name = player.Name, InRoom = !string.IsNullOrEmpty(player.RoomId) })
                .ToList();
            Message message = new() { Type = "PLAYER_LIST", Players = players };
            if (recipient == null)
            {
                await BroadcastToSessionsAsync(activeSessions.Values.ToList(), message, token);
                PublishState();
            }
            else
            {
                await SendToSessionAsync(recipient, message, token);
            }
        }

        private async Task BroadcastPlayerListAsync(CancellationToken token)
        {
            await SendPlayerListAsync(null, token);
        }

        private async Task SendRoomListAsync(ServerSession? recipient, CancellationToken token)
        {
            List<RoomSummary> roomSummaries = rooms.Values
                .Where(room => !room.Game.IsGameOver)
                .Select(room => new RoomSummary
                {
                    RoomId = room.RoomId,
                    PlayerOne = room.PlayerOneName,
                    PlayerTwo = room.PlayerTwoName,
                    SpectatorCount = room.Spectators.Count,
                    IsGameOver = room.Game.IsGameOver
                })
                .ToList();
            Message message = new() { Type = "ROOM_LIST", Rooms = roomSummaries };
            if (recipient == null)
            {
                await BroadcastToSessionsAsync(activeSessions.Values.ToList(), message, token);
                PublishState();
            }
            else
            {
                await SendToSessionAsync(recipient, message, token);
            }
        }

        // Báo cho giao diện Server: danh sách Client và số phòng
        private void PublishState()
        {
            try
            {
                ClientListChanged?.Invoke(BuildClientList());
                RoomCountChanged?.Invoke(rooms.Count);
            }
            catch
            {
                // Lỗi ở phía giao diện không được làm hỏng luồng mạng.
            }
        }

        private string[] BuildClientList()
        {
            return sessionsByName.Values
                .OrderBy(session => session.Name, StringComparer.OrdinalIgnoreCase)
                .Select(DescribeSession)
                .ToArray();
        }

        private static string DescribeSession(ServerSession session)
        {
            string location;
            if (string.IsNullOrEmpty(session.RoomId))
                location = "Sảnh chờ";
            else if (session.IsSpectator)
                location = $"Phòng {session.RoomId} (khán giả)";
            else
                location = $"Phòng {session.RoomId} (Người chơi {(session.PlayerIndex ?? 0) + 1})";

            TcpClient? client = session.Client;
            if (client == null)
                return $"{session.Name} | Chờ kết nối lại | {location}";

            string address = "Unknown";
            try
            {
                address = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            }
            catch
            {
                // Socket đã đóng.
            }

            return $"{session.Name} | {address} | {location}";
        }

        // ==========================================
        // GỬI DỮ LIỆU
        // ==========================================

        private Task BroadcastRoomAsync(GameRoom room, Message message, CancellationToken token)
        {
            return BroadcastToSessionsAsync(GetRoomSessions(room), message, token);
        }

        private List<ServerSession> GetRoomSessions(GameRoom room)
        {
            return new[] { room.PlayerOne, room.PlayerTwo }
                .Concat(room.Spectators.Values)
                .Where(session => session.Client != null)
                .DistinctBy(session => session.SessionId)
                .ToList();
        }

        private async Task BroadcastToSessionsAsync(IEnumerable<ServerSession> recipients, Message message, CancellationToken token)
        {
            foreach (ServerSession recipient in recipients)
            {
                if (message.Type == "ROOM_STATE")
                {
                    message.YourPlayerIndex = recipient.PlayerIndex ?? -1;
                    message.IsSpectator = recipient.IsSpectator;
                }
                await SendToSessionAsync(recipient, message, token);
            }
        }

        private async Task SendErrorAsync(ServerSession session, string code, string text, CancellationToken token)
        {
            await SendToSessionAsync(session, CreateError(code, text), token);
        }

        private static Message CreateError(string code, string text)
        {
            return new Message { Type = "ERROR", ErrorCode = code, Data = text };
        }

        private async Task SendToSessionAsync(ServerSession session, Message message, CancellationToken token)
        {
            await session.SendLock.WaitAsync(token);
            try
            {
                TcpClient? client = session.Client;
                if (client == null || !client.Connected)
                    return;
                await WriteMessageAsync(client.GetStream(), message, token);
            }
            catch (IOException)
            {
                // Việc dọn dẹp kết nối do vòng lặp nhận xử lý.
            }
            catch (SocketException)
            {
                // Việc dọn dẹp kết nối do vòng lặp nhận xử lý.
            }
            catch (ObjectDisposedException)
            {
                // Kết nối đã bị đóng.
            }
            catch (InvalidOperationException)
            {
                // Kết nối đã bị đóng.
            }
            finally
            {
                session.SendLock.Release();
            }
        }

        private async Task<Message?> ReadMessageAsync(NetworkStream stream, CancellationToken token)
        {
            byte[] header = new byte[sizeof(int)];
            int headerBytes = await ReadExactlyAsync(stream, header, token);
            if (headerBytes == 0)
                return null;
            int payloadLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
            if (payloadLength <= 0 || payloadLength > settings.MaxFrameBytes)
                throw new InvalidDataException("Frame length is outside the configured limit.");

            byte[] payload = new byte[payloadLength];
            if (await ReadExactlyAsync(stream, payload, token) != payloadLength)
                throw new EndOfStreamException("Connection closed during a framed message.");
            return JsonSerializer.Deserialize<Message>(payload, JsonOptions)
                ?? throw new JsonException("Message payload was empty.");
        }

        private async Task WriteMessageAsync(NetworkStream stream, Message message, CancellationToken token)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            if (payload.Length > settings.MaxFrameBytes)
                throw new InvalidDataException("Outgoing message exceeds the configured frame limit.");
            byte[] header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
            await stream.WriteAsync(header, token);
            await stream.WriteAsync(payload, token);
            await stream.FlushAsync(token);
        }

        private static async Task<int> ReadExactlyAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
        {
            int totalBytes = 0;
            while (totalBytes < buffer.Length)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalBytes), token);
                if (bytesRead == 0)
                    return totalBytes;
                totalBytes += bytesRead;
            }
            return totalBytes;
        }

        private bool ValidateMessageEnvelope(Message message, out string errorCode, out string errorText)
        {
            if (string.IsNullOrWhiteSpace(message.Type) || message.Type.Length > 32)
            {
                errorCode = "INVALID_TYPE";
                errorText = "Message type is required and must be at most 32 characters.";
                return false;
            }
            if (message.Data?.Length > settings.MaxFrameBytes || message.RoomId?.Length > 64 ||
                message.TargetName?.Length > 24 || message.Name?.Length > 24 ||
                message.ResumeToken?.Length > 128)
            {
                errorCode = "INVALID_FIELD";
                errorText = "One or more message fields exceed their allowed length.";
                return false;
            }
            errorCode = string.Empty;
            errorText = string.Empty;
            return true;
        }

        private static bool IsValidPlayerName(string name)
        {
            return name.Length is >= 1 and <= 24 &&
                name.All(character => char.IsLetterOrDigit(character) || character is ' ' or '_' or '-');
        }

        // ==========================================
        // LỊCH SỬ TRẬN ĐẤU
        // ==========================================
        private async Task SendHistoryAsync(ServerSession session, CancellationToken token)
        {
            List<HistoryItem> items = new();
            Database? db = Database;
            if (db != null)
            {
                try
                {
                    items = await Task.Run(() => db.GetMatchHistory(session.Name), token);
                }
                catch (Exception ex)
                {
                    Log("Không đọc được lịch sử từ SQL: " + ex.Message);
                }
            }

            await SendToSessionAsync(session, new Message { Type = "HISTORY_LIST", History = items }, token);
        }


        private async Task SaveMatchHistoryAsync(MatchHistoryRecord record, CancellationToken token)
        {
            // Lưu vào SQL (lỗi SQL không làm hỏng Server, file JSON vẫn được ghi bên dưới)
            Database? db = Database;
            if (db != null)
            {
                try
                {
                    await Task.Run(() => db.SaveMatchHistory(
                        record.RoomId, record.PlayerOne, record.PlayerTwo, record.Result,
                        record.StartedAtUtc, record.FinishedAtUtc, record.Moves.Count,
                        JsonSerializer.Serialize(record.Moves)), token);
                }
                catch (Exception ex)
                {
                    Log("Không lưu được lịch sử vào SQL: " + ex.Message);
                }
            }
            await historyLock.WaitAsync(token);
            try
            {
                string path = Path.IsPathRooted(settings.HistoryFile)
                    ? settings.HistoryFile
                    : Path.Combine(AppContext.BaseDirectory, settings.HistoryFile);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                List<MatchHistoryRecord> records = File.Exists(path)
                    ? JsonSerializer.Deserialize<List<MatchHistoryRecord>>(await File.ReadAllTextAsync(path, token), JsonOptions) ?? new()
                    : new List<MatchHistoryRecord>();
                records.Add(record);
                string temporaryPath = path + ".tmp";
                await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(records, HistoryWriteOptions), token);
                File.Move(temporaryPath, path, true);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                Log("Không thể lưu lịch sử trận đấu: " + ex.Message);
            }
            finally
            {
                historyLock.Release();
            }
        }

        private void Log(string text)
        {
            LogReceived?.Invoke($"[{DateTime.Now:HH:mm:ss}] {text}");
        }
    }
}