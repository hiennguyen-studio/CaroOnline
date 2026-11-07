using System.Text.Json;

namespace CaroOnline.Winforms
{
    public sealed class ClientNetworkSettings
    {
        public string ServerAddress { get; set; } = "127.0.0.1";
        public int ServerPort { get; set; } = 9999;
        public int ConnectTimeoutSeconds { get; set; } = 10;
        public int MaxFrameBytes { get; set; } = 65536;

        public static ClientNetworkSettings Load(string? filePath = null)
        {
            string path = filePath ?? Path.Combine(AppContext.BaseDirectory, "clientsettings.json");
            if (!File.Exists(path))
                return new ClientNetworkSettings();

            ClientNetworkSettings? settings = JsonSerializer.Deserialize<ClientNetworkSettings>(File.ReadAllText(path));
            settings ??= new ClientNetworkSettings();
            if (string.IsNullOrWhiteSpace(settings.ServerAddress) ||
                settings.ServerPort is < 1 or > 65535 ||
                settings.ConnectTimeoutSeconds is < 1 or > 120 ||
                settings.MaxFrameBytes is < 1024 or > 1_048_576)
                throw new InvalidDataException("Client network configuration contains invalid values.");

            return settings;
        }
    }
}
