using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Player = CaroOnline.Winforms.Player;
using Cons = CaroOnline.Winforms.Cons;
using CaroOnline.Winforms;

namespace GameCaro
{
    public class ChessBoardManager
    {
        private static readonly Color CellColor = Color.FromArgb(250, 248, 245);
        private static readonly Color CellHoverColor = Color.FromArgb(231, 240, 249);
        private static readonly Color CellBorderColor = Color.FromArgb(204, 216, 228);
        private static readonly Color RecentMoveBorderColor = Color.FromArgb(224, 145, 44);
        private Button? lastMoveButton;

        #region Properties
        private Panel chessBoard = null!;
        public Panel ChessBoard
        {
            get { return chessBoard; }
            set { chessBoard = value; }
        }

        public bool IsOnlineMode { get; set; }

        public bool CanMakeOnlineMove { get; set; } = true;

        public event Action<Point, int>? OnlineMoveRequested;

        private List<Player> player = null!;
        public List<Player> Player
        {
            get { return player; }
            set { player = value; }
        }

        private int currentPlayer;
        public int CurrentPlayer
        {
            get { return currentPlayer; }
            set { currentPlayer = value; }
        }

        private TextBox playerName = null!;
        public TextBox PlayerName
        {
            get { return playerName; }
            set { playerName = value; }
        }

        private PictureBox markPlayer = null!;
        public PictureBox MarkPlayer
        {
            get { return markPlayer; }
            set { markPlayer = value; }
        }

        private List<List<Button>> matrix = null!;
        public List<List<Button>> Matrix
        {
            get { return matrix; }
            set { matrix = value; }
        }

        private event EventHandler? playerMarked;
        public event EventHandler PlayerMarked
        {
            add { playerMarked += value; }
            remove { playerMarked -= value; }
        }

        private event EventHandler? endeGame;
        public event EventHandler EndeGame
        {
            add { endeGame += value; }
            remove { endeGame -= value; }
        }
        #endregion

        private Stack<PlayInfo> PlayTimeLine;

        public int MoveCount => PlayTimeLine.Count;

        #region Initialize
        public ChessBoardManager(Panel chessBoard, TextBox playerName, PictureBox markPlayer)
        {
            this.ChessBoard = chessBoard;
            this.PlayerName = playerName;
            this.MarkPlayer = markPlayer;
            this.MarkPlayer.SizeMode = PictureBoxSizeMode.StretchImage;

            string resourcePath = Path.Combine(Application.StartupPath, "Resources");

            this.Player = new List<Player>()
            {
                new Player("Player 1", Image.FromFile(Path.Combine(resourcePath, "iconX.jpg"))),
                new Player("Player 2", Image.FromFile(Path.Combine(resourcePath, "iconO.jpg")))
            };
            currentPlayer = 0;
            PlayTimeLine = new Stack<PlayInfo>();
            ChangePlayer();

        }
        #endregion

        #region Methods
        public void DrawChessBoard()
        {
            ChessBoard.SuspendLayout();
            ChessBoard.Enabled = true;
            ChessBoard.AutoScroll = false;
            ChessBoard.BackColor = CellColor;
            ChessBoard.Controls.Clear();
            Matrix = new List<List<Button>>();
            lastMoveButton = null;

            for (int i = 0; i < Cons.CHESS_BOARD_HEIGHT; i++)
            {
                Matrix.Add(new List<Button>());
                for (int j = 0; j < Cons.CHESS_BOARD_WIDTH; j++)
                {
                    Button btn = new Button()
                    {
                        Width = Cons.CHESS_WIDTH,
                        Height = Cons.CHESS_HEIGHT,
                        Location = new Point(j * Cons.CHESS_WIDTH, i * Cons.CHESS_HEIGHT),
                        BackgroundImageLayout = ImageLayout.Zoom,
                        FlatStyle = FlatStyle.Flat,
                        Margin = Padding.Empty,
                        Padding = Padding.Empty,
                        BackColor = CellColor,
                        UseVisualStyleBackColor = false,
                        Cursor = Cursors.Hand,
                        Tag = i.ToString()
                    };

                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.BorderColor = CellBorderColor;
                    btn.FlatAppearance.MouseOverBackColor = CellHoverColor;
                    btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(220, 232, 244);
                    btn.Click += btn_Click;
                    btn.MouseEnter += (_, _) =>
                    {
                        if (btn.BackgroundImage == null && btn != lastMoveButton)
                            btn.BackColor = CellHoverColor;
                    };
                    btn.MouseLeave += (_, _) =>
                    {
                        if (btn != lastMoveButton)
                            btn.BackColor = CellColor;
                    };
                    chessBoard.Controls.Add(btn);
                    Matrix[i].Add(btn);
                }
            }
            ChessBoard.ResumeLayout(true);
        }

        public void SetCellSize(int cellSize)
        {
            if (cellSize <= 0 || Matrix == null)
                return;

            Cons.CHESS_WIDTH = cellSize;
            Cons.CHESS_HEIGHT = cellSize;
            ChessBoard.SuspendLayout();
            for (int y = 0; y < Matrix.Count; y++)
            {
                for (int x = 0; x < Matrix[y].Count; x++)
                {
                    Button cell = Matrix[y][x];
                    cell.Size = new Size(cellSize, cellSize);
                    cell.Location = new Point(x * cellSize, y * cellSize);
                }
            }
            ChessBoard.Size = new Size(cellSize * Cons.CHESS_BOARD_WIDTH,
                cellSize * Cons.CHESS_BOARD_HEIGHT);
            ChessBoard.ResumeLayout(true);
        }

        private void btn_Click(object? sender, EventArgs e)
        {
            if (sender is not Button btn)
                return;

            if (btn.BackgroundImage != null)
                return;

            if (IsOnlineMode)
            {
                if (CanMakeOnlineMove)
                    OnlineMoveRequested?.Invoke(GetChessPoint(btn), CurrentPlayer);
                return;
            }

            Mark(btn);

            PlayTimeLine.Push(new PlayInfo(GetChessPoint(btn), CurrentPlayer));

            HighlightRecentMove(btn);

            currentPlayer = (currentPlayer == 1) ? 0 : 1;

            ChangePlayer();

            if (playerMarked != null)
                playerMarked(this, new EventArgs());

            if (isEndGame(btn))
            {
                EndGame();
            }
        }

        public void EndGame()
        {
            if (endeGame != null)
                endeGame(this, new EventArgs());
        }

        public void NewGame()
        {
            PlayTimeLine.Clear();

            lastMoveButton = null;

            CurrentPlayer = 0;

            ChangePlayer();

            DrawChessBoard();
        }

        public bool Undo()
        {
            if (PlayTimeLine.Count == 0)
                return false;

            PlayInfo playInfo = PlayTimeLine.Pop();

            Button btn = Matrix[playInfo.Point.Y][playInfo.Point.X];

            btn.BackgroundImage = null;

            HighlightRecentMove(PlayTimeLine.Count > 0
                ? Matrix[PlayTimeLine.Peek().Point.Y][PlayTimeLine.Peek().Point.X]
                : null);

            CurrentPlayer = playInfo.currentPlayer;

            ChangePlayer();

            return true;
        }

        public void ApplyRemoteMove(Point point, int playerIndex)
        {
            if (point.X < 0 || point.X >= Cons.CHESS_BOARD_WIDTH)
                return;

            if (point.Y < 0 || point.Y >= Cons.CHESS_BOARD_HEIGHT)
                return;

            if (Matrix[point.Y][point.X].BackgroundImage != null)
                return;

            CurrentPlayer = playerIndex;

            Button btn = Matrix[point.Y][point.X];

            Mark(btn);

            PlayTimeLine.Push(new PlayInfo(point, CurrentPlayer));

            HighlightRecentMove(btn);

            if (isEndGame(btn))
            {
                EndGame();
                return;
            }

            CurrentPlayer = (CurrentPlayer == 0) ? 1 : 0;

            ChangePlayer();
        }

        public void ApplyRemoteSnapshot(int[] board, int currentPlayer)
        {
            if (board == null || board.Length != Cons.CHESS_BOARD_WIDTH * Cons.CHESS_BOARD_HEIGHT)
                return;

            PlayTimeLine.Clear();
            lastMoveButton = null;
            for (int y = 0; y < Cons.CHESS_BOARD_HEIGHT; y++)
            {
                for (int x = 0; x < Cons.CHESS_BOARD_WIDTH; x++)
                {
                    int cell = board[y * Cons.CHESS_BOARD_WIDTH + x];
                    if (cell is < 1 or > 2)
                        continue;

                    CurrentPlayer = cell - 1;
                    Button button = Matrix[y][x];
                    Mark(button);
                    PlayTimeLine.Push(new PlayInfo(new Point(x, y), CurrentPlayer));
                }
            }

            CurrentPlayer = currentPlayer is 0 or 1 ? currentPlayer : 0;
            ChangePlayer();
        }

        private bool isEndGame(Button btn)
        {
            Point point = GetChessPoint(btn);
            if (point.X < 0 || point.Y < 0)
                return false;

            return isEndHorizontal(btn) || isEndVertical(btn) || isEndPrimary(btn) || isEndSub(btn);
        }

        private Point GetChessPoint(Button btn)
        {
            int vertical = Convert.ToInt32(btn.Tag);
            int horizontal = Matrix[vertical].IndexOf(btn);

            if (horizontal == -1)
            {
                for (int i = 0; i < Cons.CHESS_BOARD_HEIGHT; i++)
                {
                    for (int j = 0; j < Cons.CHESS_BOARD_WIDTH; j++)
                    {
                        if (Matrix[i][j] == btn)
                            return new Point(j, i);
                    }
                }
            }

            return new Point(horizontal, vertical);
        }

        private bool isEndHorizontal(Button btn)
        {
            Point point = GetChessPoint(btn);
            int countLeft = 0;
            for (int i = point.X; i >= 0; i--)
            {
                if (Matrix[point.Y][i].BackgroundImage == btn.BackgroundImage)
                    countLeft++;
                else
                    break;
            }

            int contRight = 0;
            for (int i = point.X + 1; i < Cons.CHESS_BOARD_WIDTH; i++)
            {
                if (Matrix[point.Y][i].BackgroundImage == btn.BackgroundImage)
                    contRight++;
                else
                    break;
            }

            return (countLeft + contRight) >= 5;
        }

        private bool isEndVertical(Button btn)
        {
            Point point = GetChessPoint(btn);
            int countTop = 0;
            for (int i = point.Y; i >= 0; i--)
            {
                if (Matrix[i][point.X].BackgroundImage == btn.BackgroundImage)
                    countTop++;
                else
                    break;
            }

            int countBottom = 0;
            for (int i = point.Y + 1; i < Cons.CHESS_BOARD_HEIGHT; i++)
            {
                if (Matrix[i][point.X].BackgroundImage == btn.BackgroundImage)
                    countBottom++;
                else
                    break;
            }

            return (countTop + countBottom) >= 5;
        }

        // SỬA LỖI 2: Xóa điều kiện (i <= point.X) làm giới hạn sai chéo chính
        private bool isEndPrimary(Button btn)
        {
            Point point = GetChessPoint(btn);
            int countTop = 0;
            for (int i = 0; i < Cons.CHESS_BOARD_WIDTH; i++)
            {
                if (point.Y - i < 0 || point.X - i < 0)
                    break;
                if (Matrix[point.Y - i][point.X - i].BackgroundImage == btn.BackgroundImage)
                    countTop++;
                else
                    break;
            }

            int countBottom = 0;
            for (int i = 1; i < Cons.CHESS_BOARD_WIDTH; i++)
            {
                if (point.Y + i >= Cons.CHESS_BOARD_HEIGHT || point.X + i >= Cons.CHESS_BOARD_WIDTH)
                    break;
                if (Matrix[point.Y + i][point.X + i].BackgroundImage == btn.BackgroundImage)
                    countBottom++;
                else
                    break;
            }

            return (countTop + countBottom) >= 5;
        }

        private bool isEndSub(Button btn)
        {
            Point point = GetChessPoint(btn);
            int countTop = 0;
            for (int i = 0; i < Cons.CHESS_BOARD_WIDTH; i++)
            {
                if (point.Y - i < 0 || point.X + i >= Cons.CHESS_BOARD_WIDTH)
                    break;
                if (Matrix[point.Y - i][point.X + i].BackgroundImage == btn.BackgroundImage)
                    countTop++;
                else
                    break;
            }

            int countBottom = 0;
            for (int i = 1; i < Cons.CHESS_BOARD_WIDTH; i++)
            {
                if (point.Y + i >= Cons.CHESS_BOARD_HEIGHT || point.X - i < 0)
                    break;
                if (Matrix[point.Y + i][point.X - i].BackgroundImage == btn.BackgroundImage)
                    countBottom++;
                else
                    break;
            }

            return (countTop + countBottom) >= 5;
        }

        private void Mark(Button btn)
        {
            btn.BackgroundImage = Player[currentPlayer].Mark;
            btn.BackColor = CellColor;

        }

        private void HighlightRecentMove(Button? button)
        {
            if (lastMoveButton != null)
            {
                lastMoveButton.FlatAppearance.BorderColor = CellBorderColor;
                lastMoveButton.FlatAppearance.BorderSize = 1;
            }

            lastMoveButton = button;
            if (lastMoveButton != null)
            {
                lastMoveButton.FlatAppearance.BorderColor = RecentMoveBorderColor;
                lastMoveButton.FlatAppearance.BorderSize = 3;
            }
        }

        private void ChangePlayer()
        {
            PlayerName.Text = Player[currentPlayer].Name;
            MarkPlayer.Image = Player[currentPlayer].Mark;
        }
        #endregion
    }
}