
using System;
using System.Collections.Concurrent;
using System.IO;
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
        private TcpListener? listener;

        private readonly ConcurrentDictionary<int, TcpClient> clients
            = new ConcurrentDictionary<int, TcpClient>();

        private readonly ConcurrentDictionary<int, SemaphoreSlim> sendLocks
            = new ConcurrentDictionary<int, SemaphoreSlim>();

        private readonly GameManager game = new GameManager();

        private readonly SemaphoreSlim gameLock = new SemaphoreSlim(1, 1);

        private CancellationTokenSource? cancellation;

        public event Action<string>? LogReceived;

        public event Action<int>? ClientCountChanged;

        public bool IsRunning { get; private set; }

        public async Task StartServer()
        {
            if (IsRunning)
                return;

            try
            {
                cancellation = new CancellationTokenSource();

                listener = new TcpListener(IPAddress.Any, 9999);

                listener.Start();

                IsRunning = true;

                Log("Server đang chạy tại cổng 9999.");

                while (!cancellation.IsCancellationRequested)
                {
                    TcpClient client =
                        await listener.AcceptTcpClientAsync(
                            cancellation.Token);

                    _ = HandleClient(client);
                }
            }
            catch (OperationCanceledException)
            {
                Log("Server đã dừng nhận kết nối.");
            }
            catch (ObjectDisposedException)
            {
                // Listener đã được đóng.
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

        private async Task HandleClient(TcpClient client)
        {
            int playerIndex = -1;

            try
            {
                playerIndex = GetFreePlayer();

                if (playerIndex == -1)
                {
                    Log("Server đã đủ 2 người chơi.");
                    client.Close();
                    return;
                }

                clients[playerIndex] = client;

                sendLocks[playerIndex] = new SemaphoreSlim(1, 1);

                Log($"Player {playerIndex + 1} đã kết nối.");

                ClientCountChanged?.Invoke(clients.Count);

                using NetworkStream stream = client.GetStream();

                using StreamReader reader =
                    new StreamReader(stream, Encoding.UTF8);

                await SendToClient(
                    playerIndex,
                    new Message
                    {
                        Type = "WELCOME",
                        PlayerIndex = playerIndex
                    });

                await Broadcast(new Message
                {
                    Type = "PLAYER_COUNT",
                    Data = clients.Count.ToString()
                });

                while (IsRunning && client.Connected)
                {
                    string? line = await reader.ReadLineAsync();

                    if (line == null)
                        break;

                    Message? message =
                        JsonSerializer.Deserialize<Message>(line);

                    if (message == null)
                        continue;

                    await ProcessMessage(playerIndex, message);
                }
            }
            catch (IOException ex)
            {
                Log("Lỗi truyền dữ liệu: " + ex.Message);
            }
            catch (JsonException ex)
            {
                Log("Dữ liệu JSON không hợp lệ: " + ex.Message);
            }
            catch (Exception ex)
            {
                Log("Lỗi Client: " + ex.Message);
            }
            finally
            {
                if (playerIndex != -1)
                {
                    if (clients.TryRemove(playerIndex, out _))
                    {
                        sendLocks.TryRemove(playerIndex, out _);

                        Log($"Player {playerIndex + 1} đã ngắt kết nối.");

                        ClientCountChanged?.Invoke(clients.Count);

                        await Broadcast(new Message
                        {
                            Type = "PLAYER_COUNT",
                            Data = clients.Count.ToString()
                        });
                    }
                }

                client.Close();
            }
        }

        private int GetFreePlayer()
        {
            if (!clients.ContainsKey(0))
                return 0;

            if (!clients.ContainsKey(1))
                return 1;

            return -1;
        }

        private async Task ProcessMessage(
            int playerIndex,
            Message message)
        {
            await gameLock.WaitAsync();

            try
            {
                switch (message.Type)
                {
                    case "MOVE":

                        if (clients.Count != 2)
                            return;

                        if (!game.MakeMove(
                            message.X,
                            message.Y,
                            playerIndex))
                        {
                            Log("Nước đi không hợp lệ.");
                            return;
                        }

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

                            Log($"Player {playerIndex + 1} đã chiến thắng.");
                        }
                        else if (game.IsBoardFull())
                        {
                            await Broadcast(new Message
                            {
                                Type = "GAME_OVER",
                                Data = "DRAW"
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

                    case "UNDO":

                        if (game.Undo())
                        {
                            await Broadcast(new Message
                            {
                                Type = "UNDO",
                                PlayerIndex = game.CurrentPlayer
                            });

                            Log("Đã thực hiện Undo.");
                        }

                        break;

                    case "NEW_GAME":

                        game.NewGame();

                        await Broadcast(new Message
                        {
                            Type = "NEW_GAME",
                            PlayerIndex = 0
                        });

                        Log("Đã bắt đầu ván mới.");

                        break;
                }
            }
            finally
            {
                gameLock.Release();
            }
        }

        private async Task Broadcast(Message message)
        {
            string json = JsonSerializer.Serialize(message);

            foreach (var item in clients)
            {
                try
                {
                    await SendToClient(item.Key, message);
                }
                catch (Exception ex)
                {
                    Log("Không thể gửi dữ liệu đến Player "
                        + (item.Key + 1) + ": " + ex.Message);
                }
            }
        }

        private async Task SendToClient(
            int playerIndex,
            Message message)
        {
            if (!clients.TryGetValue(playerIndex, out TcpClient? client))
                return;

            if (!sendLocks.TryGetValue(playerIndex, out SemaphoreSlim? sendLock))
                return;

            await sendLock.WaitAsync();

            try
            {
                if (!client.Connected)
                    return;

                string json = JsonSerializer.Serialize(message);

                byte[] data = Encoding.UTF8.GetBytes(json + "\n");

                await client.GetStream().WriteAsync(data);
            }
            finally
            {
                sendLock.Release();
            }
        }

        public void StopServer()
        {
            if (!IsRunning)
                return;

            IsRunning = false;

            cancellation?.Cancel();

            listener?.Stop();

            foreach (var item in clients)
            {
                item.Value.Close();
            }

            clients.Clear();

            ClientCountChanged?.Invoke(0);

            Log("Server đã dừng.");
        }

        private void Log(string message)
        {
            LogReceived?.Invoke(
                $"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
