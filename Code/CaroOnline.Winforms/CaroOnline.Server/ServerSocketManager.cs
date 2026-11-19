
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CaroOnline.Server
{
    public class ServerSocketManager
    {
        private TcpListener listener;

        // Lưu danh sách Client
        private readonly Dictionary<int, ClientSession> clients
            = new Dictionary<int, ClientSession>();

        private readonly object clientLock = new object();

        // Quản lý game
        private readonly GameManager game = new GameManager();

        private readonly SemaphoreSlim gameLock =
            new SemaphoreSlim(1, 1);

        private CancellationTokenSource cancellation;

        // Các sự kiện gửi thông tin về FormServer
        public event Action<string> LogReceived;

        public event Action<int> ClientCountChanged;

        public event Action<string[]> ClientListChanged;

        public bool IsRunning { get; private set; }

        // Mỗi Client có một khóa gửi dữ liệu riêng
        private class ClientSession
        {
            public TcpClient Client { get; }

            public SemaphoreSlim SendLock { get; }
                = new SemaphoreSlim(1, 1);

            public ClientSession(TcpClient client)
            {
                Client = client;
            }
        }

        // ==========================================
        // START SERVER
        // ==========================================

        public async Task StartServer()
        {
            if (IsRunning)
                return;

            cancellation = new CancellationTokenSource();

            try
            {
                listener = new TcpListener(
                    IPAddress.Any, 9999);

                listener.Start();

                IsRunning = true;

                Log("Server đang chạy tại cổng 9999.");
                Log("Đang chờ Client kết nối...");

                while (!cancellation.IsCancellationRequested)
                {
                    TcpClient client =
                        await listener.AcceptTcpClientAsync(
                            cancellation.Token);

                    int playerIndex = -1;

                    lock (clientLock)
                    {
                        if (clients.Count < 2)
                        {
                            playerIndex = clients.ContainsKey(0)
                                ? 1 : 0;

                            clients.Add(
                                playerIndex,
                                new ClientSession(client));
                        }
                    }

                    // Server chỉ cho phép 2 Client
                    if (playerIndex == -1)
                    {
                        Log("Server đã đủ 2 người chơi.");

                        client.Close();

                        continue;
                    }

                    string ip =
                        client.Client.RemoteEndPoint?.ToString()
                        ?? "Unknown";

                    Log($"Player {playerIndex + 1} đã kết nối: {ip}");

                    UpdateClientList();

                    // Xử lý Client riêng biệt
                    _ = HandleClient(client, playerIndex);
                }
            }
            catch (OperationCanceledException)
            {
                // Server được yêu cầu dừng
            }
            catch (ObjectDisposedException)
            {
                // Listener đã đóng
            }
            catch (SocketException ex)
            {
                if (IsRunning)
                    Log("Lỗi Socket: " + ex.Message);
            }
            catch (Exception ex)
            {
                Log("Lỗi Server: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        // ==========================================
        // HANDLE CLIENT
        // ==========================================

        private async Task HandleClient(
            TcpClient client,
            int playerIndex)
        {
            try
            {
                using NetworkStream stream = client.GetStream();

                using StreamReader reader =
                    new StreamReader(stream, Encoding.UTF8);

                // Gửi thông tin Player cho Client
                await SendToClient(client, new Message
                {
                    Type = "WELCOME",
                    PlayerIndex = playerIndex
                });

                // Thông báo số lượng Client
                await Broadcast(new Message
                {
                    Type = "PLAYER_COUNT",
                    Data = GetClientCount().ToString()
                });

                // Nhận dữ liệu từ Client
                while (IsRunning)
                {
                    string line = await reader.ReadLineAsync();

                    if (line == null)
                        break;

                    Message message =
                        JsonSerializer.Deserialize<Message>(line);

                    if (message == null)
                        continue;

                    await ProcessMessage(playerIndex, message);
                }
            }
            catch (IOException)
            {
                // Client mất kết nối
            }
            catch (ObjectDisposedException)
            {
                // Kết nối đã đóng
            }
            catch (Exception ex)
            {
                Log($"Player {playerIndex + 1}: {ex.Message}");
            }
            finally
            {
                bool removed = false;

                lock (clientLock)
                {
                    if (clients.TryGetValue(
                        playerIndex, out ClientSession current)
                        && current.Client == client)
                    {
                        clients.Remove(playerIndex);
                        removed = true;
                    }
                }

                client.Close();

                if (removed)
                {
                    Log($"Player {playerIndex + 1} đã ngắt kết nối.");

                    UpdateClientList();

                    await Broadcast(new Message
                    {
                        Type = "PLAYER_COUNT",
                        Data = GetClientCount().ToString()
                    });
                }
            }
        }

        // ==========================================
        // GET CLIENT COUNT
        // ==========================================

        private int GetClientCount()
        {
            lock (clientLock)
            {
                return clients.Count;
            }
        }

        // ==========================================
        // UPDATE CLIENT LIST
        // ==========================================

        private void UpdateClientList()
        {
            string[] clientNames;

            lock (clientLock)
            {
                clientNames = clients
                    .OrderBy(x => x.Key)
                    .Select(x =>
                    {
                        string ip =
                            x.Value.Client.Client.RemoteEndPoint?.ToString()
                            ?? "Unknown";

                        return $"Player {x.Key + 1} | {ip}";
                    })
                    .ToArray();
            }

            ClientCountChanged?.Invoke(clientNames.Length);

            ClientListChanged?.Invoke(clientNames);
        }

        // ==========================================
        // PROCESS MESSAGE
        // ==========================================

        private async Task ProcessMessage(
            int playerIndex,
            Message message)
        {
            await gameLock.WaitAsync();

            try
            {
                switch (message.Type)
                {
                    // Người chơi đánh cờ
                    case "MOVE":

                        if (GetClientCount() < 2)
                            return;

                        if (!game.MakeMove(
                            message.X,
                            message.Y,
                            playerIndex))
                            return;

                        // Đồng bộ nước đi cho cả 2 Client
                        await Broadcast(new Message
                        {
                            Type = "MOVE",
                            X = message.X,
                            Y = message.Y,
                            PlayerIndex = playerIndex
                        });

                        bool won = game.CheckWinner(
                            message.X,
                            message.Y,
                            playerIndex);

                        if (won)
                        {
                            await Broadcast(new Message
                            {
                                Type = "GAME_OVER",
                                PlayerIndex = playerIndex,
                                Data = "WIN"
                            });
                        }
                        else
                        {
                            await Broadcast(new Message
                            {
                                Type = "TURN",
                                PlayerIndex = game.CurrentPlayer
                            });
                        }

                        break;

                    // Hoàn tác nước đi
                    case "UNDO":

                        if (game.Undo())
                        {
                            await Broadcast(new Message
                            {
                                Type = "UNDO",
                                PlayerIndex = game.CurrentPlayer
                            });
                        }

                        break;

                    // Chơi ván mới
                    case "NEW_GAME":

                        if (GetClientCount() < 2)
                            return;

                        game.NewGame();

                        await Broadcast(new Message
                        {
                            Type = "NEW_GAME",
                            PlayerIndex = 0
                        });

                        break;
                }
            }
            finally
            {
                gameLock.Release();
            }
        }

        // ==========================================
        // BROADCAST
        // ==========================================

        private async Task Broadcast(Message message)
        {
            string json = JsonSerializer.Serialize(message);

            byte[] data = Encoding.UTF8.GetBytes(
                json + "\n");

            List<ClientSession> clientSnapshot;

            lock (clientLock)
            {
                clientSnapshot = clients.Values.ToList();
            }

            foreach (ClientSession session in clientSnapshot)
            {
                try
                {
                    await session.SendLock.WaitAsync();

                    try
                    {
                        if (!session.Client.Connected)
                            continue;

                        NetworkStream stream =
                            session.Client.GetStream();

                        await stream.WriteAsync(data);
                    }
                    finally
                    {
                        session.SendLock.Release();
                    }
                }
                catch
                {
                    Log("Không thể gửi dữ liệu đến Client.");
                }
            }
        }

        // ==========================================
        // SEND TO CLIENT
        // ==========================================

        private async Task SendToClient(
            TcpClient client,
            Message message)
        {
            string json = JsonSerializer.Serialize(message);

            byte[] data = Encoding.UTF8.GetBytes(
                json + "\n");

            ClientSession session = null;

            lock (clientLock)
            {
                session = clients.Values.FirstOrDefault(
                    x => x.Client == client);
            }

            if (session == null)
                return;

            await session.SendLock.WaitAsync();

            try
            {
                await client.GetStream().WriteAsync(data);
            }
            finally
            {
                session.SendLock.Release();
            }
        }

        // ==========================================
        // STOP SERVER
        // ==========================================

        public void StopServer()
        {
            // Không để Server tự bỏ qua việc đóng
            // chỉ vì IsRunning đã thay đổi trạng thái.
            bool wasRunning = IsRunning;

            IsRunning = false;

            cancellation?.Cancel();

            listener?.Stop();

            lock (clientLock)
            {
                foreach (var session in clients.Values)
                {
                    session.Client.Close();
                }

                clients.Clear();
            }

            UpdateClientList();

            if (wasRunning)
                Log("Server đã dừng.");

            listener = null;
        }

        // ==========================================
        // LOG
        // ==========================================

        private void Log(string message)
        {
            LogReceived?.Invoke(
                $"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
