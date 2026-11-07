namespace CaroOnline.Server
{
    public class Message
    {
        public string Type { get; set; } = "";

        public string Data { get; set; } = "";

        public int X { get; set; }

        public int Y { get; set; }

        public int PlayerIndex { get; set; }

        public int YourPlayerIndex { get; set; } = -1;

        public bool IsSpectator { get; set; }

        public string RoomId { get; set; } = "";

        public string Name { get; set; } = "";

        public string TargetName { get; set; } = "";

        public string ResumeToken { get; set; } = "";

        public string ErrorCode { get; set; } = "";

        public string Result { get; set; } = "";

        public int[] Board { get; set; } = Array.Empty<int>();

        public List<MatchMove> Moves { get; set; } = new();

        public List<PlayerSummary> Players { get; set; } = new();

        public List<RoomSummary> Rooms { get; set; } = new();

        public DateTime? TurnDeadlineUtc { get; set; }

        public Message()
        {
        }
    }

    public sealed class MatchMove
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int PlayerIndex { get; set; }
        public DateTime PlayedAtUtc { get; set; }
    }

    public sealed class PlayerSummary
    {
        public string Name { get; set; } = "";
        public bool InRoom { get; set; }
    }

    public sealed class RoomSummary
    {
        public string RoomId { get; set; } = "";
        public string PlayerOne { get; set; } = "";
        public string PlayerTwo { get; set; } = "";
        public int SpectatorCount { get; set; }
        public bool IsGameOver { get; set; }
    }
}