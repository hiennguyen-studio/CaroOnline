
using System;

namespace CaroOnline.Server
{
    public class MatchMove
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int PlayerIndex { get; set; }

        public DateTime PlayedAtUtc { get; set; }
    }
}
