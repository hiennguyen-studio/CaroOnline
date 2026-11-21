using System;
using System.Collections.Generic;

namespace CaroOnline.Winforms
{
    // Khung tin nhắn dùng chung giữa Client và Server (JSON).
    public class Message
    {
        public string Type { get; set; } = string.Empty;

        public string? Name { get; set; }
        public string? TargetName { get; set; }
        public string? RoomId { get; set; }
        public string? ResumeToken { get; set; }
        public string? Data { get; set; }
        public string? ErrorCode { get; set; }
        public string? Result { get; set; }

        public int X { get; set; }
        public int Y { get; set; }

        // Lượt hiện tại (ROOM_STATE/TURN) hoặc người đi nước vừa rồi (MOVE)
        public int PlayerIndex { get; set; }

        // Chỉ số của CHÍNH người nhận trong phòng (-1 nếu là khán giả)
        public int YourPlayerIndex { get; set; } = -1;

        public bool IsSpectator { get; set; }

        // Bàn cờ dạng mảng phẳng: Board[y * COLS + x]; 0 = trống, 1 = người chơi 1, 2 = người chơi 2
        public int[]? Board { get; set; }

        public List<MatchMove>? Moves { get; set; }
        public List<PlayerSummary>? Players { get; set; }
        public List<RoomSummary>? Rooms { get; set; }
        public List<HistoryItem>? History { get; set; }

        public DateTime? TurnDeadlineUtc { get; set; }
    }

    public class MatchMove
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int PlayerIndex { get; set; }
        public DateTime PlayedAtUtc { get; set; }
    }

    public class PlayerSummary
    {
        public string Name { get; set; } = string.Empty;

        // true = đang ở trong một phòng (đang đấu hoặc đang xem)
        public bool InRoom { get; set; }
    }

    public class RoomSummary
    {
        public string RoomId { get; set; } = string.Empty;
        public string PlayerOne { get; set; } = string.Empty;
        public string PlayerTwo { get; set; } = string.Empty;
        public int SpectatorCount { get; set; }
        public bool IsGameOver { get; set; }
    }

    public class HistoryItem
    {
        public string RoomId { get; set; } = string.Empty;
        public string PlayerOne { get; set; } = string.Empty;
        public string PlayerTwo { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
        public DateTime FinishedAtUtc { get; set; }
        public int TotalMoves { get; set; }
    }
}