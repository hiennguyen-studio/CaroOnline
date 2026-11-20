
using System;
using System.Collections.Generic;

namespace CaroOnline.Server
{
    public class Message
    {
        // Loại thông điệp: HELLO, WELCOME, MOVE, ROOM_STATE...
        public string Type { get; set; } = "";

        // Nội dung thông điệp hoặc thông tin bổ sung
        public string Data { get; set; } = "";

        // Tọa độ nước đi
        public int X { get; set; }

        public int Y { get; set; }

        // Người chơi hiện tại
        public int PlayerIndex { get; set; } = -1;

        // Thông tin người chơi
        public string Name { get; set; } = "";

        public string TargetName { get; set; } = "";

        public string ResumeToken { get; set; } = "";

        // Thông tin phòng
        public string RoomId { get; set; } = "";

        public string Result { get; set; } = "";

        // Trạng thái bàn cờ
        public int[] Board { get; set; } = Array.Empty<int>();

        public List<MatchMove> Moves { get; set; } = new();

        // Thông tin lượt chơi
        public DateTime? TurnDeadlineUtc { get; set; }

        public int YourPlayerIndex { get; set; } = -1;

        public bool IsSpectator { get; set; }

        // Thông tin lỗi
        public string ErrorCode { get; set; } = "";

        // Danh sách người chơi
        public List<PlayerSummary> Players { get; set; } = new();

        // Danh sách phòng
        public List<RoomSummary> Rooms { get; set; } = new();

        public Message()
        {
        }
    }

    // Thông tin người chơi trong danh sách online
    public class PlayerSummary
    {
        public string Name { get; set; } = "";

        public bool InRoom { get; set; }
    }

    // Thông tin phòng chơi
    public class RoomSummary
    {
        public string RoomId { get; set; } = "";

        public string PlayerOne { get; set; } = "";

        public string PlayerTwo { get; set; } = "";

        public int SpectatorCount { get; set; }

        public bool IsGameOver { get; set; }
    }
}
