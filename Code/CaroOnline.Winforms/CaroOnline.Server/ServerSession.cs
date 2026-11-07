using System.Collections.Concurrent;
using System.Net.Sockets;

namespace CaroOnline.Server
{
    internal sealed class ServerSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string ResumeToken { get; set; } = "";
        public string RoomId { get; set; } = "";
        public int? PlayerIndex { get; set; }
        public bool IsSpectator { get; set; }
        public int ConnectionId { get; set; } = -1;
        public TcpClient? Client { get; set; }
        public DateTime? DisconnectedAtUtc { get; set; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
        public CancellationTokenSource? ReconnectExpiry { get; set; }
    }

    internal sealed class GameRoom
    {
        public string RoomId { get; init; } = "";
        public string PlayerOneName { get; init; } = "";
        public string PlayerTwoName { get; init; } = "";
        public ServerSession PlayerOne { get; init; } = null!;
        public ServerSession PlayerTwo { get; init; } = null!;
        public ConcurrentDictionary<Guid, ServerSession> Spectators { get; } = new();
        public GameManager Game { get; } = new();
        public SemaphoreSlim SyncLock { get; } = new(1, 1);
        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
        public DateTime? TurnDeadlineUtc { get; set; }
        public CancellationTokenSource? TurnTimer { get; set; }
        public bool IsPaused { get; set; }
    }

    internal sealed class MatchHistoryRecord
    {
        public string RoomId { get; set; } = "";
        public string PlayerOne { get; set; } = "";
        public string PlayerTwo { get; set; } = "";
        public string Result { get; set; } = "";
        public DateTime StartedAtUtc { get; set; }
        public DateTime FinishedAtUtc { get; set; }
        public List<MatchMove> Moves { get; set; } = new();
    }
}
