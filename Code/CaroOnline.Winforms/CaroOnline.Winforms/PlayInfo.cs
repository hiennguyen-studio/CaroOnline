using System;
using System.Collections.Generic;
using System.Text;

namespace CaroOnline.Winforms
{
    public class PlayInfo
    {
        private Point point;
        public Point Point
        {
            get { return point; }
            set { point = value; }
        }

        private int CurrentPlayer;
        public int currentPlayer
        {
            get { return CurrentPlayer; }
            set { CurrentPlayer = value; }
        }

        public PlayInfo(Point point, int currentPlayer)
        {
            this.point = point;
            this.CurrentPlayer = currentPlayer;
        }
    }
}