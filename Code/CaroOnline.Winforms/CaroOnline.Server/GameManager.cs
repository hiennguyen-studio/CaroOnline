using System.Collections.Generic;
using System.Drawing;

namespace CaroOnline.Server
{
    public class GameManager
    {
        public const int ROWS = 14;
        public const int COLS = 17;

        private int[,] board = new int[ROWS, COLS];

        private readonly Stack<Point> history = new Stack<Point>();

        private readonly List<MatchMove> moveHistory = new List<MatchMove>();

        public int CurrentPlayer { get; private set; } = 0;

        public bool IsGameOver { get; private set; } = false;

        public bool MakeMove(int x, int y, int player)
        {
            if (IsGameOver)
                return false;

            if (player != CurrentPlayer)
                return false;

            if (player < 0 || player > 1)
                return false;

            if (x < 0 || x >= COLS ||
                y < 0 || y >= ROWS)
                return false;

            if (board[y, x] != 0)
                return false;

            board[y, x] = player + 1;

            history.Push(new Point(x, y));

            moveHistory.Add(new MatchMove
            {
                X = x,
                Y = y,
                PlayerIndex = player,
                PlayedAtUtc = DateTime.UtcNow
            });

            CurrentPlayer = 1 - CurrentPlayer;

            return true;
        }

        public bool Undo()
        {
            if (history.Count == 0 || IsGameOver)
                return false;

            Point p = history.Pop();

            board[p.Y, p.X] = 0;

            if (moveHistory.Count > 0)
                moveHistory.RemoveAt(moveHistory.Count - 1);

            CurrentPlayer = 1 - CurrentPlayer;

            return true;
        }

        public void NewGame()
        {
            board = new int[ROWS, COLS];

            history.Clear();

            moveHistory.Clear();

            CurrentPlayer = 0;

            IsGameOver = false;
        }

        public bool CheckWinner(int x, int y, int player)
        {
            if (player < 0 || player > 1 || x < 0 || x >= COLS ||
                y < 0 || y >= ROWS || board[y, x] != player + 1)
                return false;

            int value = player + 1;

            int[,] directions =
            {
                { 1, 0 },
                { 0, 1 },
                { 1, 1 },
                { 1, -1 }
            };

            for (int i = 0; i < 4; i++)
            {
                int dx = directions[i, 0];
                int dy = directions[i, 1];

                int count = 1;

                count += CountDirection(x, y, dx, dy, value);

                count += CountDirection(x, y, -dx, -dy, value);

                if (count >= 5)
                {
                    IsGameOver = true;
                    return true;
                }
            }

            return false;
        }

        public bool IsBoardFull()
        {
            return history.Count == ROWS * COLS;
        }

        public int[] GetBoardSnapshot()
        {
            int[] snapshot = new int[ROWS * COLS];
            for (int y = 0; y < ROWS; y++)
            {
                for (int x = 0; x < COLS; x++)
                    snapshot[y * COLS + x] = board[y, x];
            }

            return snapshot;
        }

        public List<MatchMove> GetMoveHistory()
        {
            return moveHistory.Select(move => new MatchMove
            {
                X = move.X,
                Y = move.Y,
                PlayerIndex = move.PlayerIndex,
                PlayedAtUtc = move.PlayedAtUtc
            }).ToList();
        }

        public void FinishInDraw()
        {
            FinishGame();
        }

        public void FinishGame()
        {
            IsGameOver = true;
        }

        private int CountDirection(
            int x,
            int y,
            int dx,
            int dy,
            int value)
        {
            int count = 0;

            x += dx;
            y += dy;

            while (x >= 0 && x < COLS &&
                   y >= 0 && y < ROWS &&
                   board[y, x] == value)
            {
                count++;

                x += dx;
                y += dy;
            }

            return count;
        }
    }
}