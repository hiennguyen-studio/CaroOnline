using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CaroOnline.Server
{
    public sealed class RoomServerManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
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
        public bool IsRunning { get; private set; }
        public int ConnectedClientCount => clients.Count;

        public RoomServerManager(ServerSettings? serverSettings = null)
        {
            settings = serverSettings ?? ServerSettings.Load();
            settings.Validate();
        }

        public async Task StartServer()
        {
            if (IsRunning)
                return;

            try
            {
                serverCancellation = new CancellationTokenSource();
                listener = new TcpListener(IPAddress.Parse(settings.ListenAddress), settings.Port);
                listener.Start();
                IsRunning = true;
                Log($"Server listening on {settings.ListenAddress}:{settings.Port}.");

                while (!serverCancellation.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(serverCancellation.Token);
                    if (clients.Count >= settings.MaxConnections)
                    {
                        client.Close();
                        Log("Rejected connection: configured connection limit reached.");
                        continue;
                    }

                    int connectionId = Interlocked.Increment(ref nextConnectionId);
                    clients[connectionId] = client;
                    ClientCountChanged?.Invoke(clients.Count);
                    _ = HandleConnectionAsync(connectionId, client, serverCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Log("Server stopped accepting connections.");
            }
            catch (ObjectDisposedException)
            {
                // Listener was closed during shutdown.
            }
            catch (SocketException ex)
            {
                if (IsRunning)
                    Log("Socket error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Log("Server error: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

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
                Log($"{session.Name} connected.");

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
                // Server shutdown or connection handshake timeout.
            }
            catch (InvalidDataException ex)
            {
                if (session != null)
                    await SendErrorAsync(session, "INVALID_FRAME", ex.Message, serverToken);
                Log("Invalid network frame: " + ex.Message);
            }
            catch (JsonException ex)
            {
                if (session != null)
                    await SendErrorAsync(session, "INVALID_JSON", "Message payload is not valid JSON.", serverToken);
                Log("Invalid JSON received: " + ex.Message);
            }
            catch (IOException ex)
            {
                Log("Connection I/O ended: " + ex.Message);
            }
            catch (SocketException ex)
            {
                Log("Connection socket ended: " + ex.Message);
            }
            catch (Exception ex)
            {
                Log("Client handler error: " + ex.Message);
            }
            finally
            {
                clients.TryRemove(connectionId, out _);
                if (session != null)
                    await HandleSessionDisconnectedAsync(connectionId, session);
                client.Close();
                ClientCountChanged?.Invoke(clients.Count);
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
                    error = "There is no matching pending challenge.";
                else if (!sessionsByName.TryGetValue(challengerName, out challenger) || challenger.Client == null)
                    error = "The challenger is no longer online.";
                else if (!string.IsNullOrEmpty(accepter.RoomId) || !string.IsNullOrEmpty(challenger.RoomId))
                    error = "One of the players is already in a room.";
                else
                {
                    string roomId = Guid.NewGuid().ToString("N")[..8];
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
            Log($"Room {room.RoomId} started: {room.PlayerOneName} vs {room.PlayerTwoName}.");
        }

        private async Task RejectChallengeAsync(ServerSession rejecter, Message message, CancellationToken token)
        {
            string challengerName = (message.TargetName ?? string.Empty).Trim();
            pendingChallenges.TryRemove(rejecter.Name, out _);
            if (sessionsByName.TryGetValue(challengerName, out ServerSession? challenger))
                await SendToSessionAsync(challenger, new Message { Type = "CHALLENGE_REJECT", Name = rejecter.Name }, token);
        }

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
                await SendRoomListAsync(null, token);
                return;
            }

            MatchHistoryRecord? record = null;
            await room.SyncLock.WaitAsync(token);
            try
            {
                record = FinishRoom(room, session.PlayerIndex == 0 ? "PLAYER_TWO_WIN_BY_FORFEIT" : "PLAYER_ONE_WIN_BY_FORFEIT");
            }
            finally
            {
                room.SyncLock.Release();
            }
            if (record != null)
            {
                await BroadcastRoomAsync(room, new Message { Type = "GAME_OVER", RoomId = room.RoomId, Result = record.Result, Data = record.Result }, token);
                await SaveMatchHistoryAsync(record, token);
                await BroadcastPlayerListAsync(token);
                await SendRoomListAsync(null, token);
            }
        }

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
            List<ServerSession> recipients;
            List<Message> outgoing = new();
            await room.SyncLock.WaitAsync(token);
            try
            {
                int playerIndex = session.PlayerIndex ?? -1;
                if (room.IsPaused || (playerIndex != 0 && playerIndex != 1) ||
                    !ReferenceEquals(playerIndex == 0 ? room.PlayerOne : room.PlayerTwo, session))
                {
                    outgoing.Add(CreateError("NOT_A_PLAYER", "Only a connected player in this room can move."));
                }
                else if (!room.Game.MakeMove(message.X, message.Y, playerIndex))
                {
                    outgoing.Add(CreateError("INVALID_MOVE", "Move must be in bounds, on an empty cell, and made by the player whose turn it is."));
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
                        if (draw)
                            room.Game.FinishInDraw();
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
                recipients = GetRoomSessions(room);
            }
            finally
            {
                room.SyncLock.Release();
            }

            foreach (Message update in outgoing)
                await BroadcastToSessionsAsync(recipients, update, token);
            if (completedRecord != null)
            {
                await SaveMatchHistoryAsync(completedRecord, token);
                await BroadcastPlayerListAsync(token);
                await SendRoomListAsync(null, token);
                Log($"Room {room.RoomId} completed: {completedRecord.Result}.");
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
                List<MatchMove> moves = room.Game.GetMoveHistory();
                if (moves.Count > 0 && moves[^1].PlayerIndex == session.PlayerIndex && room.Game.Undo())
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
                room.IsPaused = false;
                StartTurnTimer(room);
            }
            finally
            {
                room.SyncLock.Release();
            }
            await BroadcastRoomAsync(room, CreateRoomStateMessage(room), token);
        }

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
                    await room.SyncLock.WaitAsync(timer.Token);
                    try
                    {
                        if (!ReferenceEquals(room.TurnTimer, timer) || room.Game.IsGameOver || room.IsPaused)
                            return;
                        int remaining = Math.Max(0, (int)Math.Ceiling((room.TurnDeadlineUtc!.Value - DateTime.UtcNow).TotalSeconds));
                        if (remaining == 0)
                        {
                            int winner = 1 - room.Game.CurrentPlayer;
                            room.Game.FinishGame();
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
                        recipients = GetRoomSessions(room);
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
                            PlayerIndex = 1 - room.Game.CurrentPlayer,
                            Result = completedRecord.Result,
                            Data = completedRecord.Result
                        }, CancellationToken.None);
                        await SaveMatchHistoryAsync(completedRecord, CancellationToken.None);
                        await BroadcastPlayerListAsync(CancellationToken.None);
                        await SendRoomListAsync(null, CancellationToken.None);
                        Log($"Room {room.RoomId} ended on turn timeout.");
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // A move, reconnect, or server shutdown replaced this timer.
            }
            catch (Exception ex)
            {
                Log("Turn timer error: " + ex.Message);
            }
        }

        private MatchHistoryRecord? FinishRoom(GameRoom room, string result)
        {
            if (room.Game.IsGameOver && !rooms.ContainsKey(room.RoomId))
                return null;
            room.Game.FinishGame();
            room.TurnTimer?.Cancel();
            room.TurnTimer?.Dispose();
            room.TurnTimer = null;
            room.TurnDeadlineUtc = null;
            rooms.TryRemove(room.RoomId, out _);
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

        private async Task HandleSessionDisconnectedAsync(int connectionId, ServerSession session)
        {
            await lobbyLock.WaitAsync();
            try
            {
                if (!activeSessions.TryGetValue(connectionId, out ServerSession? current) || !ReferenceEquals(current, session))
                    return;
                activeSessions.TryRemove(connectionId, out _);
                session.Client = null;
                session.ConnectionId = -1;
                session.DisconnectedAtUtc = DateTime.UtcNow;

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

            Log($"{session.Name} disconnected; reconnect grace is {settings.ReconnectSeconds} seconds.");
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
                room.IsPaused = false;
                StartTurnTimer(room);
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
                await lobbyLock.WaitAsync(expiry.Token);
                try
                {
                    if (!ReferenceEquals(session.ReconnectExpiry, expiry) || session.Client != null)
                        return;
                    sessionsByToken.TryRemove(session.ResumeToken, out _);
                    sessionsByName.TryRemove(session.Name, out _);
                    if (!string.IsNullOrEmpty(session.RoomId) && rooms.TryGetValue(session.RoomId, out affectedRoom) && session.PlayerIndex.HasValue)
                    {
                        await affectedRoom.SyncLock.WaitAsync(expiry.Token);
                        try
                        {
                            int winner = 1 - session.PlayerIndex.Value;
                            affectedRoom.Game.FinishGame();
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
                    await BroadcastRoomAsync(affectedRoom, new Message
                    {
                        Type = "GAME_OVER",
                        RoomId = affectedRoom.RoomId,
                        Result = record.Result,
                        Data = record.Result
                    }, CancellationToken.None);
                    await SaveMatchHistoryAsync(record, CancellationToken.None);
                }
                await BroadcastPlayerListAsync(CancellationToken.None);
                await SendRoomListAsync(null, CancellationToken.None);
                Log($"Reconnect grace expired for {session.Name}.");
            }
            catch (OperationCanceledException)
            {
                // The player resumed before the grace period expired.
            }
            catch (Exception ex)
            {
                Log("Reconnect expiry error: " + ex.Message);
            }
        }

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
                await BroadcastToSessionsAsync(activeSessions.Values.ToList(), message, token);
            else
                await SendToSessionAsync(recipient, message, token);
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
                await BroadcastToSessionsAsync(activeSessions.Values.ToList(), message, token);
            else
                await SendToSessionAsync(recipient, message, token);
        }

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
                // Connection cleanup is handled by the receive loop.
            }
            catch (SocketException)
            {
                // Connection cleanup is handled by the receive loop.
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

        private async Task SaveMatchHistoryAsync(MatchHistoryRecord record, CancellationToken token)
        {
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
                await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }), token);
                File.Move(temporaryPath, path, true);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                Log("Unable to persist match history: " + ex.Message);
            }
            finally
            {
                historyLock.Release();
            }
        }

        public void StopServer()
        {
            if (!IsRunning)
                return;
            IsRunning = false;
            serverCancellation?.Cancel();
            listener?.Stop();
            foreach (TcpClient client in clients.Values)
                client.Close();
            clients.Clear();
            foreach (GameRoom room in rooms.Values)
                room.TurnTimer?.Cancel();
            ClientCountChanged?.Invoke(0);
            Log("Server stopped.");
        }

        private void Log(string text)
        {
            LogReceived?.Invoke($"[{DateTime.Now:HH:mm:ss}] {text}");
        }
    }
}
