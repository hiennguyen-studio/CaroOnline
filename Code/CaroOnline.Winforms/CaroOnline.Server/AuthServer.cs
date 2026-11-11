using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CaroOnline.Server;

public class AuthServer
{
    public const int Port = 9000;  

    private readonly Database db;
    private TcpListener? listener;
    private CancellationTokenSource? cts;

    public event Action<string>? Log;

    public AuthServer(Database database) => db = database;

    private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    private static string UnB64(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));

    public void Start()
    {
        if (listener != null) return;
        cts = new CancellationTokenSource();
        listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        Log?.Invoke($"Auth server đang lắng nghe cổng {Port}");
        _ = Task.Run(() => AcceptLoop(cts.Token));
    }

    public void Stop()
    {
        cts?.Cancel();
        listener?.Stop();
        listener = null;
        Log?.Invoke("Auth server đã dừng");
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await listener!.AcceptTcpClientAsync(token);
                _ = Task.Run(() => Handle(client));
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { Log?.Invoke("Accept lỗi: " + ex.Message); }
        }
    }

    private async Task Handle(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

                string? line = await reader.ReadLineAsync(timeout.Token);
                string response = Process(line);
                await writer.WriteLineAsync(response.AsMemory(), timeout.Token);
            }
            catch (Exception ex)
            {
                Log?.Invoke("Client lỗi: " + ex.Message);
            }
        }
    }

    private string Process(string? line)
    {
        try
        {
            if (string.IsNullOrEmpty(line)) return Err("Yêu cầu rỗng.");
            var p = line.Split('|');

            if (p[0] == "LOGIN" && p.Length == 3)
            {
                string username = UnB64(p[1]);
                var user = db.LoginUser(username, UnB64(p[2]), out string msg);
                Log?.Invoke($"LOGIN {username}: {(user != null ? "OK" : msg)}");
                return user != null
                    ? $"OK|{B64(user.Role)}|{B64(user.DisplayName ?? "")}"
                    : Err(msg);
            }

            if (p[0] == "REGISTER" && p.Length == 4)
            {
                string username = UnB64(p[1]).Trim();
                string password = UnB64(p[2]);
                string display = UnB64(p[3]).Trim();

                if (username.Length < 3) return Err("Tên đăng nhập tối thiểu 3 ký tự.");
                if (password.Length < 6) return Err("Mật khẩu tối thiểu 6 ký tự.");
                if (display.Length == 0) display = username;

                bool ok = db.RegisterUser(username, password, display, out string msg);
                Log?.Invoke($"REGISTER {username}: {msg}");
                return ok ? "OK|" + B64(msg) : Err(msg);
            }

            return Err("Yêu cầu không hợp lệ.");
        }
        catch (Exception ex)
        {
            return Err("Lỗi server: " + ex.Message);
        }
    }

    private static string Err(string msg) => "ERR|" + B64(msg);
}