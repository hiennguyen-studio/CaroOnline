using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CaroOnline.Server
{
    // Mỗi phòng có MỘT GameManager riêng, nên trạng thái các trận hoàn toàn độc lập.
    // Mọi thao tác đều được khóa nội bộ để an toàn khi nhiều luồng cùng truy cập.
    public class GameManager
    {
        public const int ROWS = 14;
        public const int COLS = 17;

        private readonly object sync = new object();

        private int[,] board = new int[ROWS, COLS];

        private readonly Stack<Point> history = new Stack<Point>();

        private readonly List<MatchMove> moveHistory =
            new List<MatchMove>();

        private int currentPlayer = 0;

        private bool isGameOver = false;

        public int CurrentPlayer
        {
            get { lock (sync) { return currentPlayer; } }
        }

        public bool IsGameOver
        {
            get { lock (sync) { return isGameOver; } }
        }

        // Số nước đã đi trong ván hiện tại
        public int MoveCount
        {
            get { lock (sync) { return moveHistory.Count; } }
        }

        // Người đi nước gần nhất (-1 nếu chưa có nước nào)
        public int LastMovePlayerIndex
        {
            get
            {
                lock (sync)
                {
                    return moveHistory.Count == 0
                        ? -1
                        : moveHistory[moveHistory.Count - 1].PlayerIndex;
                }
            }
        }

        public bool MakeMove(int x, int y, int player)
        {
            lock (sync)
            {
                if (isGameOver)
                    return false;

                if (player < 0 || player > 1)
                    return false;

                if (player != currentPlayer)
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

                currentPlayer = 1 - currentPlayer;

                return true;
            }
        }

        public bool Undo()
        {
            lock (sync)
            {
                if (history.Count == 0 || isGameOver)
                    return false;

                Point p = history.Pop();

                board[p.Y, p.X] = 0;

                if (moveHistory.Count > 0)
                    moveHistory.RemoveAt(moveHistory.Count - 1);

                currentPlayer = 1 - currentPlayer;

                return true;
            }
        }

        public void NewGame()
        {
            lock (sync)
            {
                board = new int[ROWS, COLS];

                history.Clear();
                moveHistory.Clear();

                currentPlayer = 0;
                isGameOver = false;
            }
        }

        // Kiểm tra thắng và đánh dấu ván kết thúc nếu có người thắng
        public bool CheckWinner(int x, int y, int player)
        {
            lock (sync)
            {
                if (player < 0 || player > 1 ||
                    x < 0 || x >= COLS ||
                    y < 0 || y >= ROWS ||
                    board[y, x] != player + 1)
                {
                    return false;
                }

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
                        isGameOver = true;
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsBoardFull()
        {
            lock (sync)
            {
                return history.Count == ROWS * COLS;
            }
        }

        public int[] GetBoardSnapshot()
        {
            lock (sync)
            {
                int[] snapshot = new int[ROWS * COLS];

                for (int y = 0; y < ROWS; y++)
                {
                    for (int x = 0; x < COLS; x++)
                    {
                        snapshot[y * COLS + x] = board[y, x];
                    }
                }

                return snapshot;
            }
        }

        public List<MatchMove> GetMoveHistory()
        {
            lock (sync)
            {
                return moveHistory.Select(move => new MatchMove
                {
                    X = move.X,
                    Y = move.Y,
                    PlayerIndex = move.PlayerIndex,
                    PlayedAtUtc = move.PlayedAtUtc
                }).ToList();
            }
        }

        public void FinishInDraw()
        {
            FinishGame();
        }

        public void FinishGame()
        {
            lock (sync)
            {
                isGameOver = true;
            }
        }

        // Chỉ được gọi bên trong lock(sync)
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