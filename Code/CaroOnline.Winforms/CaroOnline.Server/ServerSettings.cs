using System.Net;
using System.Text.Json;

namespace CaroOnline.Server
{
    public sealed class ServerSettings
    {
        private static readonly JsonSerializerOptions LoadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public string ListenAddress { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 9999;
        public int MaxConnections { get; set; } = 15;

        // Số trận (phòng) được chạy cùng lúc
        public int MaxRooms { get; set; } = 7;

        public int MaxFrameBytes { get; set; } = 65536;
        public int HandshakeTimeoutSeconds { get; set; } = 10;
        public int TurnSeconds { get; set; } = 30;
        public int ReconnectSeconds { get; set; } = 60;
        public int MaxSpectatorsPerRoom { get; set; } = 13;
        public string HistoryFile { get; set; } = "match-history.json";
        public string ClientAddress { get; set; } = "127.0.0.1";

        public static ServerSettings Load(string? filePath = null)
        {
            string? path = filePath ?? FindSettingsFile();
            if (path == null)
                return new ServerSettings();

            ServerSettings? settings;
            try
            {
                settings = JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(path), LoadOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Không đọc được file cấu hình '{path}': {ex.Message}");
            }

            settings ??= new ServerSettings();
            settings.Validate();
            return settings;
        }

        // Tìm appsettings.json cạnh file chạy, hoặc trong thư mục data
        private static string? FindSettingsFile()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                Path.Combine(AppContext.BaseDirectory, "data", "appsettings.json")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        public void Validate()
        {
            if (!IPAddress.TryParse(ListenAddress, out _))
                throw new InvalidDataException("ListenAddress must be a valid IP address.");
            if (Port is < 1 or > 65535)
                throw new InvalidDataException("Port must be between 1 and 65535.");
            if (MaxConnections is < 2 or > 256)
                throw new InvalidDataException("MaxConnections must be between 2 and 256.");
            if (MaxRooms is < 1 or > 128)
                throw new InvalidDataException("MaxRooms must be between 1 and 128.");
            if (MaxFrameBytes is < 1024 or > 1_048_576)
                throw new InvalidDataException("MaxFrameBytes must be between 1024 and 1048576.");
            if (HandshakeTimeoutSeconds is < 1 or > 60)
                throw new InvalidDataException("HandshakeTimeoutSeconds must be between 1 and 60.");
            if (TurnSeconds is < 5 or > 3600)
                throw new InvalidDataException("TurnSeconds must be between 5 and 3600.");
            if (ReconnectSeconds is < 0 or > 3600)
                throw new InvalidDataException("ReconnectSeconds must be between 0 and 3600.");
            if (MaxSpectatorsPerRoom is < 0 or > 254)
                throw new InvalidDataException("MaxSpectatorsPerRoom must be between 0 and 254.");
            if (string.IsNullOrWhiteSpace(HistoryFile))
                throw new InvalidDataException("HistoryFile cannot be empty.");
            if (string.IsNullOrWhiteSpace(ClientAddress))
                throw new InvalidDataException("ClientAddress cannot be empty.");
        }
    }
}