
using System;
using System.Drawing;

namespace CaroOnline.Winforms
{
    [Serializable]
    public class PlayInfo
    {
        public Point Point { get; set; }

        public int currentPlayer { get; set; }

        public PlayInfo()
        {
        }

        public PlayInfo(Point point, int currentPlayer)
        {
            Point = point;
            this.currentPlayer = currentPlayer;
        }
    }
}
