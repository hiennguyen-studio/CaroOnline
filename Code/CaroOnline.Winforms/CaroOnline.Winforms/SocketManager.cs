
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CaroOnline.Winforms
{
    public class SocketManager
    {
        #region Properties

        private static readonly ClientNetworkSettings defaultSettings = ClientNetworkSettings.Load();

        private readonly ClientNetworkSettings settings = ClientNetworkSettings.Load();

        public static string IP = defaultSettings.ServerAddress;

        public static int PORT = defaultSettings.ServerPort;

        private TcpListener? server;

        private TcpClient? client;

        private readonly ConcurrentDictionary<TcpClient, SemaphoreSlim> clientSendLocks = new();

        private readonly SemaphoreSlim serverClientSendLock = new(1, 1);

        private readonly ConcurrentDictionary<int, TcpClient>
            clients = new ConcurrentDictionary<int, TcpClient>();

        private int clientId = 0;

        private bool isServer = false;

        private CancellationTokenSource? cancellation;

        public event Action<string>? MessageReceived;

        public event Action<int>? ClientConnected;

        public event Action<int>? ClientDisconnected;

        public event Action? Disconnected;

        public bool IsConnected => client?.Connected == true;

        #endregion

        #region Client

        public async Task<bool> ConnectServer() // Ket noi den Server
        {
            return await ConnectServer(IP, PORT, Environment.UserName, string.Empty);
        }

        public async Task<bool> ConnectServer(string host, int port, string playerName, string resumeToken = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
                    return false;

                TcpClient connectedClient = new();
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(settings.ConnectTimeoutSeconds));
                await connectedClient.ConnectAsync(host, port, timeout.Token);
                client?.Close();
                client = connectedClient;
                IP = host;
                PORT = port;

                isServer = false;

                _ = ReceiveClientLoop(connectedClient);

                await Send(JsonSerializer.Serialize(new Message
                {
                    Type = "HELLO",
                    Name = playerName,
                    ResumeToken = resumeToken
                }));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi ket noi: " + ex.Message);

                return false;
            }
        }

        private async Task ReceiveClientLoop(TcpClient connectedClient) // Nhan du lieu tu Server
        {
            try
            {
                while (ReferenceEquals(client, connectedClient) && connectedClient.Connected)
                {
                    string? message = await ReceiveData(connectedClient);

                    if (message == null)
                        break;

                    MessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi nhan du lieu: " + ex.Message);
            }
            finally
            {
                if (ReferenceEquals(client, connectedClient))
                {
                    client = null;
                    connectedClient.Close();
                    Disconnected?.Invoke();
                }
            }
        }

        #endregion

        #region Server

        public async Task CreateServer() // Khoi dong Server
        {
            try
            {
                isServer = true;

                cancellation = new CancellationTokenSource();

                server = new TcpListener(IPAddress.Any, PORT);

                server.Start();

                Console.WriteLine("Server dang chay tai port " + PORT);

                while (!cancellation.Token.IsCancellationRequested)
                {
                    TcpClient newClient = await server.AcceptTcpClientAsync(
                        cancellation.Token
                    );

                    int id = Interlocked.Increment(ref clientId);

                    clients.TryAdd(id, newClient);

                    ClientConnected?.Invoke(id);

                    _ = ReceiveServerLoop(id, newClient);
                }
            }
            catch (OperationCanceledException)
            {
                // Server da dung
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi Server: " + ex.Message);
            }
        }

        private async Task ReceiveServerLoop(int id, TcpClient client) // Nhan du lieu tu Client
        {
            try
            {
                while (client.Connected)
                {
                    string? message = await ReceiveData(client);

                    if (message == null)
                        break;

                    MessageReceived?.Invoke(message);

                    
                    await SendToAll(message); // Chuyen tiep thong diep den cac Client
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi Client: " + ex.Message);
            }
            finally
            {
                clients.TryRemove(id, out _);

                client.Close();

                ClientDisconnected?.Invoke(id);
            }
        }

        public async Task SendToAll(string message) // Gui du lieu den tat ca Client
        {
            foreach (var item in clients.ToArray())
            {
                try
                {
                    await SendData(item.Value, message);
                }
                catch
                {
                    // Bo qua Client bi mat ket noi
                }
            }
        }

        public void StopServer() // Dung Server
        {
            cancellation?.Cancel();

            server?.Stop();

            foreach (var item in clients)
            {
                item.Value.Close();
            }

            clients.Clear();
        }

        #endregion

        #region Both

        public async Task Send(string message) // Gui du lieu
        {
            if (isServer)
            {
                await SendToAll(message);
            }
            else if (client != null && client.Connected)
            {
                await SendData(client, message);
            }
        }

        public string SerializeData<T>(T data) // Gui du lieu theo dinh dang JSON
        {
            return JsonSerializer.Serialize(data);
        }

        public T? DeserializeData<T>(string data) // Giai ma du lieu JSON
        {
            return JsonSerializer.Deserialize<T>(data);
        }

        private async Task SendData(TcpClient target, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            if (data.Length > settings.MaxFrameBytes)
                throw new InvalidDataException("Message exceeds the configured frame limit.");

            byte[] length = BitConverter.GetBytes(
                IPAddress.HostToNetworkOrder(data.Length)
            );

            NetworkStream stream = target.GetStream();
            SemaphoreSlim sendLock = clientSendLocks.GetOrAdd(target, _ => new SemaphoreSlim(1, 1));
            await sendLock.WaitAsync();
            try
            {
                await stream.WriteAsync(length);
                await stream.WriteAsync(data);
                await stream.FlushAsync();
            }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task<string?> ReceiveData(TcpClient target) // Nhan du lieu TCP
        {
            NetworkStream stream = target.GetStream();

            byte[] lengthBuffer = new byte[4];

            int read = await ReadExact(stream, lengthBuffer, 4);

            if (read == 0)
                return null;
            if (read != lengthBuffer.Length)
                throw new EndOfStreamException("Connection closed during the frame header.");

            int length = IPAddress.NetworkToHostOrder(
                BitConverter.ToInt32(lengthBuffer, 0)
            );

            if (length <= 0 || length > settings.MaxFrameBytes)
                throw new InvalidDataException("Du lieu khong hop le");

            byte[] data = new byte[length];

            read = await ReadExact(stream, data, length);

            if (read != length)
                throw new EndOfStreamException();

            return Encoding.UTF8.GetString(data);
        }

        private async Task<int> ReadExact(
            NetworkStream stream,
            byte[] buffer,
            int length)
        {
            int total = 0;

            while (total < length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(total, length - total));

                if (read == 0)
                    return 0;

                total += read;
            }

            return total;
        }

        public string GetLocalIPv4(NetworkInterfaceType type)  // Lay dia chi IPv4 cua may tinh
        {
            foreach (NetworkInterface item in
                NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == type &&
                    item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in
                        item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily ==
                            AddressFamily.InterNetwork)
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }

            return "127.0.0.1";
        }

        public void Close() // Ngat ket noi
        {
            cancellation?.Cancel();

            client?.Close();

            client = null;

            StopServer();
        }

        #endregion
    }
}
