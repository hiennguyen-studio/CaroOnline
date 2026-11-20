using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CaroOnline.Winforms
{
    public sealed class SocketManager : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private const int MaxFrameBytes = 65536;

        // [SỬA 1] Thêm IP và PORT mặc định (FormLogin đang dùng)
        private static readonly ClientNetworkSettings defaultSettings =
            ClientNetworkSettings.Load();

        public static string IP = defaultSettings.ServerAddress;

        public static int PORT = defaultSettings.ServerPort;

        private readonly SynchronizationContext? uiContext;
        private readonly SemaphoreSlim sendLock = new(1, 1);

        private TcpClient? client;
        private NetworkStream? stream;
        private CancellationTokenSource? cancellation;
        private int disconnectRaised;

        // ==========================================
        // SỰ KIỆN (luôn chạy trên luồng giao diện)
        // ==========================================

        public event Action<Message>? Welcomed;
        public event Action<List<PlayerSummary>>? PlayerListReceived;
        public event Action<List<RoomSummary>>? RoomListReceived;

        // Có người thách đấu mình: tham số là tên người thách
        public event Action<string>? ChallengeReceived;

        // Lời thách đấu của mình bị từ chối: tham số là tên người từ chối
        public event Action<string>? ChallengeRejected;

        // Vào phòng / cập nhật toàn bộ trạng thái phòng (người chơi lẫn khán giả)
        public event Action<Message>? RoomStateReceived;

        public event Action<Message>? MoveReceived;
        public event Action<Message>? TurnChanged;
        public event Action<Message>? TurnTimerTick;
        public event Action<Message>? GameOver;
        public event Action<Message>? PlayerDisconnected;
        public event Action<Message>? PlayerReconnected;

        // (errorCode, nội dung)
        public event Action<string, string>? ErrorReceived;
        public event Action<string>? InfoReceived;

        // Mất kết nối; tham số là lý do (có thể null)
        public event Action<string?>? Disconnected;

        // ==========================================
        // TRẠNG THÁI
        // ==========================================

        public bool IsConnected => client?.Connected == true && cancellation is { IsCancellationRequested: false };
        public string PlayerName { get; private set; } = string.Empty;
        public string? ResumeToken { get; private set; }

        // Phòng đang ở ("" nếu đang ở sảnh)
        public string CurrentRoomId { get; private set; } = string.Empty;

        // 0 hoặc 1 nếu là người chơi, -1 nếu là khán giả / ở sảnh
        public int MyPlayerIndex { get; private set; } = -1;

        public bool IsSpectator { get; private set; }

        public bool IsInRoom => !string.IsNullOrEmpty(CurrentRoomId);

        public SocketManager()
        {
            uiContext = SynchronizationContext.Current;
        }

        // ==========================================
        // KẾT NỐI
        // ==========================================

        // Kết nối, gửi HELLO và chờ WELCOME. Ném InvalidOperationException nếu Server từ chối.
        public async Task<Message> ConnectAsync(
            string host,
            int port,
            string playerName,
            string? resumeToken = null,
            CancellationToken token = default)
        {
            Disconnect();

            TcpClient tcp = new() { NoDelay = true };
            CancellationTokenSource cts = new();

            try
            {
                await tcp.ConnectAsync(host, port, token);

                client = tcp;
                stream = tcp.GetStream();
                cancellation = cts;
                disconnectRaised = 0;
                PlayerName = playerName;

                // [SỬA 2] Lưu lại IP/PORT vừa kết nối thành công
                IP = host;
                PORT = port;

                ResetRoomState();

                await SendAsync(new Message
                {
                    Type = "HELLO",
                    Name = playerName,
                    ResumeToken = resumeToken
                }, token);

                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));

                Message? reply = await ReadFrameAsync(stream, timeout.Token);

                if (reply == null)
                    throw new InvalidOperationException("Server đã đóng kết nối.");

                if (!string.Equals(reply.Type, "WELCOME", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(reply.Data ?? reply.ErrorCode ?? "Server từ chối kết nối.");

                ResumeToken = reply.ResumeToken;
                ApplyWelcome(reply);

                _ = ReceiveLoopAsync(tcp, stream, cts);

                Raise(() => Welcomed?.Invoke(reply));
                return reply;
            }
            catch
            {
                cts.Cancel();
                cts.Dispose();
                tcp.Close();
                client = null;
                stream = null;
                cancellation = null;
                throw;
            }
        }

        public void Disconnect()
        {
            Interlocked.Exchange(ref disconnectRaised, 1);

            try { cancellation?.Cancel(); } catch { }
            try { client?.Close(); } catch { }

            client = null;
            stream = null;
            cancellation = null;
            ResetRoomState();
        }

        // [SỬA 3] Giữ lại tên Close() để FormInMatch vẫn gọi được
        public void Close() => Disconnect();

        public void Dispose()
        {
            Disconnect();
            sendLock.Dispose();
        }

        // ==========================================
        // LỆNH GỬI LÊN SERVER
        // ==========================================

        public Task RequestPlayersAsync() => SendAsync(new Message { Type = "GET_PLAYERS" });

        public Task RequestRoomsAsync() => SendAsync(new Message { Type = "GET_ROOMS" });

        public Task ChallengeAsync(string targetName) =>
            SendAsync(new Message { Type = "CHALLENGE", TargetName = targetName });

        // challengerName: tên người đã thách mình
        public Task AcceptChallengeAsync(string challengerName) =>
            SendAsync(new Message { Type = "CHALLENGE_ACCEPT", TargetName = challengerName });

        public Task RejectChallengeAsync(string challengerName) =>
            SendAsync(new Message { Type = "CHALLENGE_REJECT", TargetName = challengerName });

        // Vào xem trận với tư cách khán giả
        public Task JoinSpectatorAsync(string roomId) =>
            SendAsync(new Message { Type = "JOIN_SPECTATOR", RoomId = roomId });

        public async Task LeaveRoomAsync()
        {
            bool wasSpectator = IsSpectator;

            await SendAsync(new Message { Type = "LEAVE_ROOM" });

            // Server không báo lại cho khán giả vừa rời, nên tự xóa trạng thái phòng
            if (wasSpectator)
                ResetRoomState();
        }

        public Task SendMoveAsync(int x, int y) =>
            SendAsync(new Message { Type = "MOVE", X = x, Y = y });

        public Task UndoAsync() => SendAsync(new Message { Type = "UNDO" });

        public Task NewGameAsync() => SendAsync(new Message { Type = "NEW_GAME" });

        public Task PingAsync() => SendAsync(new Message { Type = "PING" });

        public async Task SendAsync(Message message, CancellationToken token = default)
        {
            NetworkStream? currentStream = stream;

            if (currentStream == null)
                throw new InvalidOperationException("Chưa kết nối tới Server.");

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

            if (payload.Length > MaxFrameBytes)
                throw new InvalidDataException("Tin nhắn vượt quá giới hạn kích thước.");

            byte[] header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));

            await sendLock.WaitAsync(token);
            try
            {
                await currentStream.WriteAsync(header, token);
                await currentStream.WriteAsync(payload, token);
                await currentStream.FlushAsync(token);
            }
            finally
            {
                sendLock.Release();
            }
        }

        // ==========================================
        // NHẬN DỮ LIỆU
        // ==========================================

        private async Task ReceiveLoopAsync(TcpClient tcp, NetworkStream netStream, CancellationTokenSource cts)
        {
            string? reason = null;

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    Message? message = await ReadFrameAsync(netStream, cts.Token);

                    if (message == null)
                    {
                        reason = "Server đã đóng kết nối.";
                        break;
                    }

                    Dispatch(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Chủ động ngắt kết nối.
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException
                                           or InvalidDataException or JsonException)
            {
                reason = ex.Message;
            }
            finally
            {
                // Chỉ báo mất kết nối nếu đây vẫn là kết nối hiện tại và chưa tự ngắt
                if (ReferenceEquals(client, tcp) && Interlocked.Exchange(ref disconnectRaised, 1) == 0)
                {
                    ResetRoomState();
                    Raise(() => Disconnected?.Invoke(reason));
                }

                try { tcp.Close(); } catch { }
            }
        }

        private void Dispatch(Message message)
        {
            switch ((message.Type ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "PLAYER_LIST":
                    List<PlayerSummary> players = message.Players ?? new List<PlayerSummary>();
                    Raise(() => PlayerListReceived?.Invoke(players));
                    break;

                case "ROOM_LIST":
                    List<RoomSummary> rooms = message.Rooms ?? new List<RoomSummary>();
                    Raise(() => RoomListReceived?.Invoke(rooms));
                    break;

                case "CHALLENGE":
                    string challenger = message.Name ?? string.Empty;
                    Raise(() => ChallengeReceived?.Invoke(challenger));
                    break;

                case "CHALLENGE_REJECT":
                    string rejecter = message.Name ?? string.Empty;
                    Raise(() => ChallengeRejected?.Invoke(rejecter));
                    break;

                case "ROOM_STATE":
                    CurrentRoomId = message.RoomId ?? string.Empty;
                    MyPlayerIndex = message.YourPlayerIndex;
                    IsSpectator = message.IsSpectator;
                    Raise(() => RoomStateReceived?.Invoke(message));
                    break;

                case "MOVE":
                    Raise(() => MoveReceived?.Invoke(message));
                    break;

                case "TURN":
                    Raise(() => TurnChanged?.Invoke(message));
                    break;

                case "TURN_TIMER":
                    Raise(() => TurnTimerTick?.Invoke(message));
                    break;

                case "GAME_OVER":
                    Raise(() => GameOver?.Invoke(message));
                    ResetRoomState();
                    break;

                case "PLAYER_DISCONNECTED":
                    Raise(() => PlayerDisconnected?.Invoke(message));
                    break;

                case "PLAYER_RECONNECTED":
                    Raise(() => PlayerReconnected?.Invoke(message));
                    break;

                case "ERROR":
                    string code = message.ErrorCode ?? string.Empty;
                    string text = message.Data ?? string.Empty;
                    Raise(() => ErrorReceived?.Invoke(code, text));
                    break;

                case "INFO":
                    string info = message.Data ?? string.Empty;
                    Raise(() => InfoReceived?.Invoke(info));
                    break;

                case "WELCOME":
                case "PONG":
                default:
                    break;
            }
        }

        private void ApplyWelcome(Message welcome)
        {
            CurrentRoomId = welcome.RoomId ?? string.Empty;
            MyPlayerIndex = string.IsNullOrEmpty(CurrentRoomId) ? -1 : welcome.PlayerIndex;
            IsSpectator = false;
        }

        private void ResetRoomState()
        {
            CurrentRoomId = string.Empty;
            MyPlayerIndex = -1;
            IsSpectator = false;
        }

        // ==========================================
        // KHUNG DỮ LIỆU: 4 byte độ dài (big-endian) + JSON UTF-8
        // ==========================================

        private static async Task<Message?> ReadFrameAsync(NetworkStream netStream, CancellationToken token)
        {
            byte[] header = new byte[sizeof(int)];

            int headerBytes = await ReadExactlyAsync(netStream, header, token);

            if (headerBytes == 0)
                return null;

            if (headerBytes != header.Length)
                throw new EndOfStreamException("Kết nối đóng giữa chừng.");

            int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));

            if (length <= 0 || length > MaxFrameBytes)
                throw new InvalidDataException("Độ dài khung dữ liệu không hợp lệ.");

            byte[] payload = new byte[length];

            if (await ReadExactlyAsync(netStream, payload, token) != length)
                throw new EndOfStreamException("Kết nối đóng giữa chừng.");

            return JsonSerializer.Deserialize<Message>(payload, JsonOptions)
                ?? throw new JsonException("Tin nhắn rỗng.");
        }

        private static async Task<int> ReadExactlyAsync(NetworkStream netStream, byte[] buffer, CancellationToken token)
        {
            int total = 0;

            while (total < buffer.Length)
            {
                int read = await netStream.ReadAsync(buffer.AsMemory(total), token);

                if (read == 0)
                    return total;

                total += read;
            }

            return total;
        }

        // Chuyển về luồng giao diện nếu có
        private void Raise(Action action)
        {
            if (uiContext != null)
                uiContext.Post(_ => action(), null);
            else
                action();
        }
    }
}