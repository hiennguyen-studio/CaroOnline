using System.Net.Sockets;
using System.Text;

namespace CaroOnline.Winforms
{
    public static class AuthService
    {
        public const int AuthPort = 9000;

        public static string CurrentUsername { get; private set; } = "";
        public static string CurrentRole { get; private set; } = "";
        public static string CurrentDisplayName { get; private set; } = "";

        private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        private static string UnB64(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));

        private static string ParseHost(string endpoint)
        {
            endpoint = endpoint.Trim();
            int i = endpoint.LastIndexOf(':');
            return i > 0 ? endpoint[..i] : endpoint;
        }

        private static async Task<string[]> SendAsync(string endpoint, string request)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new TcpClient();
            await client.ConnectAsync(ParseHost(endpoint), AuthPort, cts.Token);

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await writer.WriteLineAsync(request.AsMemory(), cts.Token);
            string? line = await reader.ReadLineAsync(cts.Token);
            if (line == null) throw new IOException("Server đóng kết nối.");
            return line.Split('|');
        }

        public static async Task<(bool ok, string error)> LoginAsync(string endpoint, string username, string password)
        {
            try
            {
                var r = await SendAsync(endpoint, $"LOGIN|{B64(username)}|{B64(password)}");
                if (r[0] == "OK" && r.Length >= 3)
                {
                    CurrentUsername = username;
                    CurrentRole = UnB64(r[1]);
                    CurrentDisplayName = UnB64(r[2]);
                    return (true, "");
                }
                return (false, r.Length > 1 ? UnB64(r[1]) : "Lỗi không xác định.");
            }
            catch (Exception ex)
            {
                return (false, "Không kết nối được server: " + ex.Message);
            }
        }

        public static async Task<(bool ok, string error)> RegisterAsync(string endpoint, string username, string password)
        {
            try
            {
                var r = await SendAsync(endpoint, $"REGISTER|{B64(username)}|{B64(password)}|{B64(username)}");
                if (r[0] == "OK") return (true, "");
                return (false, r.Length > 1 ? UnB64(r[1]) : "Lỗi không xác định.");
            }
            catch (Exception ex)
            {
                return (false, "Không kết nối được server: " + ex.Message);
            }
        }
    }
}