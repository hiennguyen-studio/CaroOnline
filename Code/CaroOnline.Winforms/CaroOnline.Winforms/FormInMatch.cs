using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GameCaro;

namespace CaroOnline.Winforms
{
    public partial class FormInMatch : Form
    {
        private static readonly Color SurfaceColor = ColorTranslator.FromHtml("#0C131F");
        private static readonly Color CardColor = ColorTranslator.FromHtml("#101926");
        private static readonly Color BoardCardColor = ColorTranslator.FromHtml("#0E1622");
        private static readonly Color White = ColorTranslator.FromHtml("#F8FAFC");
        private static readonly Color Muted = ColorTranslator.FromHtml("#94A3B8");
        private static readonly Color Subtle = ColorTranslator.FromHtml("#64748B");
        private static readonly Color Cyan = ColorTranslator.FromHtml("#38BDF8");
        private static readonly Color Coral = ColorTranslator.FromHtml("#F87171");
        private static readonly Color Green = ColorTranslator.FromHtml("#34D399");

        private readonly ChessBoardManager ChessBoard;
        private readonly SocketManager socketManager;
        private bool isGameEnded;
        private bool isLeavingRoom;
        private readonly bool isSpectator;
        private RoundedPanel? mainSurface;
        private Label? headerStatus;
        private Label? playerOneStatus;
        private Label? playerTwoStatus;
        private Panel? activePlayerUnderline;
        private Label? turnLabel;
        private Label? moveCountLabel;
        private Label? lastMoveLabel;
        private Label? lastMoveTurnLabel;
        private Label? matchMessageLabel;
        private CircularTimerControl? circularTimer;
        private CyberButton? undoButton;

        public FormInMatch(bool isSpectator = false)
        {
            InitializeComponent();
            DoubleBuffered = true;
            this.isSpectator = isSpectator;
            socketManager = new SocketManager();
            ChessBoard = new ChessBoardManager(pnlChessBoard, txbPlayerName, pctbMark, 15, 15, true);
            ChessBoard.EndeGame += ChessBoard_EndeGame;
            ChessBoard.PlayerMarked += ChessBoard_PlayerMarked;

            prcbCoolDown.Step = Cons.COOL_DOWN_STEP;
            prcbCoolDown.Maximum = 30_000;
            prcbCoolDown.Value = 0;
            tmCoolDown.Interval = Cons.COOL_DOWN_INTERVAL;

            BuildMatchUI();
            NewGame();
            if (isSpectator)
            {
                pnlChessBoard.Enabled = false;
                if (headerStatus != null)
                {
                    headerStatus.Text = "Khán giả";
                    headerStatus.ForeColor = Cyan;
                }
                if (matchMessageLabel != null)
                    matchMessageLabel.Text = "Bạn đang xem trận đấu với tư cách khán giả.";
            }
            ChessBoard.SetCellSize(30);
            if (!isSpectator)
                tmCoolDown.Start();
            UpdateMatchUI();
        }

        private void BuildMatchUI()
        {
            var surface = new RoundedPanel
            {
                Location = new Point(16, 16),
                Size = new Size(1068, 828),
                BackColor = SurfaceColor,
                BorderColor = ColorTranslator.FromHtml("#1E2D40"),
                BorderWidth = 1.5F,
                Radius = 24F
            };
            mainSurface = surface;
            Controls.Add(surface);

            var step = MakeLabel("03", new Font("Segoe UI", 12F, FontStyle.Bold), Cyan,
                new Point(24, 15), new Size(32, 24), ContentAlignment.MiddleLeft);
            var title = MakeLabel("PHÒNG 01", new Font("Segoe UI", 20F, FontStyle.Bold), White,
                new Point(60, 8), new Size(235, 40), ContentAlignment.MiddleLeft);
            headerStatus = MakeLabel("Đang thi đấu", new Font("Segoe UI", 10F, FontStyle.Bold), Green,
                new Point(914, 15), new Size(130, 24), ContentAlignment.MiddleRight);
            var onlineDot = new StatusDotControl(Green) { Location = new Point(897, 22), Size = new Size(8, 8) };
            var close = MakeLabel("×", new Font("Segoe UI", 16F), Muted,
                new Point(1036, 8), new Size(24, 26), ContentAlignment.MiddleCenter);
            close.Cursor = Cursors.Hand;
            close.Click += (_, _) => Close();
            step.MouseDown += DragWindow;
            title.MouseDown += DragWindow;
            surface.MouseDown += DragWindow;
            surface.Controls.Add(step);
            surface.Controls.Add(title);
            surface.Controls.Add(headerStatus);
            surface.Controls.Add(onlineDot);
            surface.Controls.Add(close);
            surface.Controls.Add(new Panel
            {
                Location = new Point(20, 59),
                Size = new Size(1028, 1),
                BackColor = ColorTranslator.FromHtml("#1E293B")
            });

            BuildPlayerBanner(surface);
            BuildInformationCard(surface);
            BuildBoardCard(surface);
            BuildTimerCard(surface);
            BuildMatchMessage(surface);
            menuStrip1.Visible = false;
        }

        private void BuildPlayerBanner(RoundedPanel surface)
        {
            var banner = MakeCard(new Point(20, 75), new Size(1028, 76), CardColor,
                ColorTranslator.FromHtml("#1E2D3D"), 14F);
            surface.Controls.Add(banner);

            var xMark = new PlayerMarkControl("X", ColorTranslator.FromHtml("#112838"), Cyan)
            {
                Location = new Point(16, 13),
                Size = new Size(48, 48)
            };
            var oMark = new PlayerMarkControl("O", ColorTranslator.FromHtml("#2A1822"), Coral)
            {
                Location = new Point(960, 13),
                Size = new Size(48, 48)
            };
            banner.Controls.Add(xMark);
            banner.Controls.Add(MakeLabel("PLAYER 1", new Font("Segoe UI", 13F, FontStyle.Bold), White,
                new Point(82, 12), new Size(290, 24), ContentAlignment.MiddleLeft));
            playerOneStatus = MakeLabel("Đến lượt của bạn", new Font("Segoe UI", 9.5F), Cyan,
                new Point(82, 38), new Size(290, 20), ContentAlignment.MiddleLeft);
            banner.Controls.Add(playerOneStatus);
            banner.Controls.Add(MakeLabel("VS", new Font("Segoe UI", 12F, FontStyle.Bold), Subtle,
                new Point(475, 24), new Size(78, 24), ContentAlignment.MiddleCenter));
            banner.Controls.Add(MakeLabel("PLAYER 2", new Font("Segoe UI", 13F, FontStyle.Bold), White,
                new Point(656, 12), new Size(288, 24), ContentAlignment.MiddleRight));
            playerTwoStatus = MakeLabel("Đang chờ", new Font("Segoe UI", 9.5F), Subtle,
                new Point(656, 38), new Size(288, 20), ContentAlignment.MiddleRight);
            banner.Controls.Add(playerTwoStatus);
            banner.Controls.Add(oMark);
            activePlayerUnderline = new Panel
            {
                Location = new Point(82, 70),
                Size = new Size(290, 3),
                BackColor = Cyan
            };
            banner.Controls.Add(activePlayerUnderline);
        }

        private void BuildInformationCard(RoundedPanel surface)
        {
            var card = MakeCard(new Point(20, 166), new Size(165, 548), CardColor,
                ColorTranslator.FromHtml("#1E2D3D"), 14F);
            surface.Controls.Add(card);
            card.Controls.Add(MakeLabel("THÔNG TIN", new Font("Segoe UI", 8.5F, FontStyle.Bold), Subtle,
                new Point(12, 12), new Size(140, 18), ContentAlignment.MiddleLeft));
            card.Controls.Add(MakeLabel("Phòng 01", new Font("Segoe UI", 11F, FontStyle.Bold), White,
                new Point(12, 39), new Size(140, 22), ContentAlignment.MiddleLeft));
            card.Controls.Add(MakeLabel("Cờ Caro", new Font("Segoe UI", 9.5F), Muted,
                new Point(12, 64), new Size(140, 19), ContentAlignment.MiddleLeft));
            card.Controls.Add(MakeLabel("15 × 15", new Font("Segoe UI", 9.5F), Muted,
                new Point(12, 85), new Size(140, 19), ContentAlignment.MiddleLeft));
            AddDivider(card, 114);
            card.Controls.Add(MakeLabel("Luật chơi", new Font("Segoe UI", 10F, FontStyle.Bold), White,
                new Point(12, 127), new Size(140, 20), ContentAlignment.MiddleLeft));
            card.Controls.Add(MakeLabel("5 quân liên\ntiếp để thắng", new Font("Segoe UI", 9F), Muted,
                new Point(12, 151), new Size(140, 38), ContentAlignment.MiddleLeft));
            AddDivider(card, 201);
            card.Controls.Add(MakeLabel("Khán giả", new Font("Segoe UI", 10F, FontStyle.Bold), White,
                new Point(12, 214), new Size(140, 20), ContentAlignment.MiddleLeft));
            card.Controls.Add(MakeLabel("04 đang xem", new Font("Segoe UI", 9F), Subtle,
                new Point(12, 238), new Size(140, 18), ContentAlignment.MiddleLeft));

            string[] spectators = { "Minh Anh", "Player 5", "Hải Đăng", "Player 8" };
            for (int i = 0; i < spectators.Length; i++)
                card.Controls.Add(new SpectatorRow(spectators[i])
                {
                    Location = new Point(8, 264 + i * 36),
                    Size = new Size(148, 29),
                    BackColor = CardColor
                });

            card.Controls.Add(MakeLabel("Công khai", new Font("Segoe UI", 9F), Subtle,
                new Point(12, 509), new Size(140, 20), ContentAlignment.MiddleLeft));
        }

        private void BuildBoardCard(RoundedPanel surface)
        {
            var card = MakeCard(new Point(203, 166), new Size(610, 548), BoardCardColor,
                ColorTranslator.FromHtml("#1E2D3D"), 14F);
            surface.Controls.Add(card);
            card.Controls.Add(MakeLabel("BÀN CỜ", new Font("Segoe UI", 10F, FontStyle.Bold), Muted,
                new Point(16, 12), new Size(180, 22), ContentAlignment.MiddleLeft));
            card.Controls.Add(MakeLabel("15 × 15", new Font("Segoe UI", 9.5F), Subtle,
                new Point(498, 12), new Size(94, 22), ContentAlignment.MiddleRight));

            Controls.Remove(pnlChessBoard);
            pnlChessBoard.Location = new Point(80, 46);
            pnlChessBoard.Size = new Size(450, 450);
            pnlChessBoard.BackColor = ColorTranslator.FromHtml("#131D2B");
            pnlChessBoard.Visible = true;
            card.Controls.Add(pnlChessBoard);

            var turnDot = new StatusDotControl(Cyan) { Location = new Point(16, 510), Size = new Size(8, 8) };
            turnLabel = MakeLabel("Lượt của PLAYER 1", new Font("Segoe UI", 10.5F, FontStyle.Bold), Cyan,
                new Point(31, 501), new Size(310, 24), ContentAlignment.MiddleLeft);
            moveCountLabel = MakeLabel("Nước đi 00", new Font("Segoe UI", 9.5F), Muted,
                new Point(458, 501), new Size(132, 24), ContentAlignment.MiddleRight);
            lastMoveLabel = MakeLabel("Nước vừa đi: --", new Font("Segoe UI", 9.5F), Muted,
                new Point(16, 527), new Size(310, 18), ContentAlignment.MiddleLeft);
            lastMoveTurnLabel = MakeLabel("X đi trước", new Font("Segoe UI", 9.5F), Muted,
                new Point(418, 527), new Size(172, 18), ContentAlignment.MiddleRight);
            card.Controls.Add(turnDot);
            card.Controls.Add(turnLabel);
            card.Controls.Add(moveCountLabel);
            card.Controls.Add(lastMoveLabel);
            card.Controls.Add(lastMoveTurnLabel);
        }

        private void BuildTimerCard(RoundedPanel surface)
        {
            var card = MakeCard(new Point(831, 166), new Size(217, 548), ColorTranslator.FromHtml("#0B1421"),
                ColorTranslator.FromHtml("#1E2D3D"), 14F);
            surface.Controls.Add(card);
            card.Controls.Add(MakeLabel("THỜI GIAN LƯỢT", new Font("Segoe UI", 9.5F, FontStyle.Bold), Muted,
                new Point(10, 18), new Size(197, 22), ContentAlignment.MiddleCenter));
            circularTimer = new CircularTimerControl
            {
                Location = new Point(43, 55),
                Size = new Size(130, 130),
                BackColor = card.BackColor,
                Maximum = prcbCoolDown.Maximum,
                Value = 0
            };
            card.Controls.Add(circularTimer);
            card.Controls.Add(MakeLabel("Hãy chọn nước đi", new Font("Segoe UI", 9.5F), Muted,
                new Point(14, 190), new Size(189, 24), ContentAlignment.MiddleCenter));

            undoButton = MakeActionButton("ĐI LẠI", new Point(12, 230), new Size(193, 42),
                ColorTranslator.FromHtml("#123447"), Cyan, ColorTranslator.FromHtml("#1B4D65"));
            undoButton.Click += (_, _) => UndoMove();
            var surrenderButton = MakeActionButton("ĐẦU HÀNG", new Point(12, 282), new Size(193, 42),
                ColorTranslator.FromHtml("#351A28"), Coral, ColorTranslator.FromHtml("#5C2638"));
            surrenderButton.Click += (_, _) => Surrender();
            var leaveButton = MakeActionButton("RỜI PHÒNG", new Point(12, 334), new Size(193, 42),
                ColorTranslator.FromHtml("#3A3215"), ColorTranslator.FromHtml("#FACC15"), ColorTranslator.FromHtml("#75621B"));
            leaveButton.Click += (_, _) => LeaveRoomToLobby();
            card.Controls.Add(undoButton);
            card.Controls.Add(surrenderButton);
            card.Controls.Add(leaveButton);
            card.Controls.Add(MakeLabel("Bình tĩnh.\nNước hay sẽ đến.", new Font("Segoe UI", 9F), Muted,
                new Point(16, 422), new Size(185, 48), ContentAlignment.MiddleLeft));
        }

        private void BuildMatchMessage(RoundedPanel surface)
        {
            var banner = MakeCard(new Point(20, 734), new Size(1028, 74), ColorTranslator.FromHtml("#152438"),
                Color.Transparent, 12F);
            surface.Controls.Add(banner);
            var dot = new StatusDotControl(Green) { Location = new Point(16, 20), Size = new Size(8, 8) };
            banner.Controls.Add(dot);
            banner.Controls.Add(MakeLabel("Trận đấu đang diễn ra", new Font("Segoe UI", 10F, FontStyle.Bold), White,
                new Point(31, 11), new Size(360, 24), ContentAlignment.MiddleLeft));
            banner.Controls.Add(MakeLabel("Kết nối ổn định · 24 ms", new Font("Segoe UI", 9F, FontStyle.Bold), Green,
                new Point(780, 11), new Size(230, 22), ContentAlignment.MiddleRight));
            matchMessageLabel = MakeLabel("Ván mới bắt đầu. PLAYER 1 đi trước.", new Font("Segoe UI", 9.5F), Muted,
                new Point(16, 38), new Size(990, 24), ContentAlignment.MiddleLeft);
            banner.Controls.Add(matchMessageLabel);
        }

        private void UpdateMatchUI()
        {
            int currentPlayer = ChessBoard.CurrentPlayer;
            bool playerOneActive = currentPlayer == 0;
            if (playerOneStatus != null)
            {
                playerOneStatus.Text = playerOneActive ? "Đến lượt của bạn" : "Đang chờ";
                playerOneStatus.ForeColor = playerOneActive ? Cyan : Subtle;
            }
            if (playerTwoStatus != null)
            {
                playerTwoStatus.Text = playerOneActive ? "Đang chờ" : "Đến lượt của bạn";
                playerTwoStatus.ForeColor = playerOneActive ? Subtle : Coral;
            }
            if (activePlayerUnderline != null)
            {
                activePlayerUnderline.Location = new Point(playerOneActive ? 82 : 656, 70);
                activePlayerUnderline.Size = new Size(playerOneActive ? 290 : 288, 3);
                activePlayerUnderline.BackColor = playerOneActive ? Cyan : Coral;
            }

            if (turnLabel != null)
            {
                turnLabel.Text = $"Lượt của PLAYER {currentPlayer + 1}";
                turnLabel.ForeColor = playerOneActive ? Cyan : Coral;
            }
            if (moveCountLabel != null)
                moveCountLabel.Text = $"Nước đi {ChessBoard.MoveCount:00}";

            PlayInfo? lastMove = ChessBoard.LastMove;
            if (lastMoveLabel != null)
                lastMoveLabel.Text = lastMove == null ? "Nước vừa đi: --" : $"Nước vừa đi: {FormatCoordinate(lastMove.Point)}";
            if (lastMoveTurnLabel != null)
                lastMoveTurnLabel.Text = "X đi trước";
            if (matchMessageLabel != null && lastMove != null)
                matchMessageLabel.Text = $"PLAYER {lastMove.currentPlayer + 1} vừa đi tại {FormatCoordinate(lastMove.Point)}. Đến lượt PLAYER {currentPlayer + 1}.";

            if (undoButton != null)
                undoButton.Enabled = ChessBoard.MoveCount > 0 && !isGameEnded;
            undoToolStripMenuItem.Enabled = ChessBoard.MoveCount > 0 && !isGameEnded;
            if (circularTimer != null)
            {
                circularTimer.Value = prcbCoolDown.Value;
                circularTimer.Maximum = prcbCoolDown.Maximum;
                circularTimer.Invalidate();
            }
        }

        private static string FormatCoordinate(Point point) => $"{(char)('A' + point.X)}{point.Y + 1}";

        private static Label MakeLabel(string text, Font font, Color color, Point location, Size size,
            ContentAlignment alignment) => new()
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Text = text,
            Font = font,
            ForeColor = color,
            Location = location,
            Size = size,
            TextAlign = alignment
        };

        private static RoundedPanel MakeCard(Point location, Size size, Color background, Color border, float radius) => new()
        {
            Location = location,
            Size = size,
            BackColor = background,
            BorderColor = border,
            BorderWidth = 1.2F,
            Radius = radius
        };

        private static CyberButton MakeActionButton(string text, Point location, Size size, Color background,
            Color foreground, Color border) => new()
        {
            Text = text,
            Location = location,
            Size = size,
            BackColor = background,
            ForeColor = foreground,
            BorderColor = border,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        private static void AddDivider(Control parent, int y)
        {
            parent.Controls.Add(new Panel
            {
                Location = new Point(12, y),
                Size = new Size(parent.Width - 24, 1),
                BackColor = ColorTranslator.FromHtml("#1E293B")
            });
        }

        private void LeaveRoomToLobby()
        {
            isLeavingRoom = true;
            tmCoolDown.Stop();
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void Surrender()
        {
            if (isGameEnded || isSpectator) return;
            int surrenderingPlayer = ChessBoard.CurrentPlayer + 1;
            int winningPlayer = surrenderingPlayer == 1 ? 2 : 1;
            EndGame(winningPlayer, $"PLAYER {surrenderingPlayer} đầu hàng.");
        }

        void EndGame(int? winningPlayer = null, string? endReason = null)
        {
            if (isGameEnded)
                return;
            isGameEnded = true;
            tmCoolDown.Stop();
            pnlChessBoard.Enabled = false;
            undoToolStripMenuItem.Enabled = false;
            UpdateUndoAvailability();
            if (headerStatus != null)
            {
                headerStatus.Text = "Trận đấu kết thúc";
                headerStatus.ForeColor = Coral;
            }
            UpdateMatchUI();
            if (matchMessageLabel != null)
                matchMessageLabel.Text = endReason == null
                    ? "Ván đấu kết thúc · Chọn ĐI LẠI để chơi tiếp hoặc RỜI PHÒNG."
                    : $"{endReason} PLAYER {winningPlayer} thắng.";

            if (winningPlayer.HasValue)
            {
                int losingPlayer = winningPlayer.Value == 1 ? 2 : 1;
                string resultMessage = endReason == null
                    ? $"PLAYER {winningPlayer.Value} thắng!"
                    : $"{endReason}\nPLAYER {winningPlayer.Value} thắng, PLAYER {losingPlayer} thua.";
                DialogResult rematchChoice = MessageBox.Show(
                    $"{resultMessage}\nBạn có muốn đấu lại không?",
                    "Trận đấu kết thúc",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (rematchChoice == DialogResult.Yes)
                    NewGame();
                else
                    LeaveRoomToLobby();
            }
        }

        void NewGame()
        {
            StartNewGameUI();
        }

        void UndoMove()
        {
            UndoLastMoveUI();
        }

        void Quit()
        {
            socketManager.Close();
            Application.Exit();
        }

        private void StartNewGameUI()
        {
            isGameEnded = false;
            ChessBoard.IsOnlineMode = false;
            ChessBoard.CanMakeOnlineMove = true;
            ChessBoard.NewGame();
            ChessBoard.SetCellSize(30);
            pnlChessBoard.Enabled = true;
            undoToolStripMenuItem.Enabled = false;
            prcbCoolDown.Value = 0;
            if (!isSpectator)
                tmCoolDown.Start();
            if (headerStatus != null)
            {
                headerStatus.Text = "Đang thi đấu";
                headerStatus.ForeColor = Green;
            }
            UpdateMatchUI();
        }

        private void UndoLastMoveUI()
        {
            if (isGameEnded || isSpectator || !ChessBoard.Undo())
                return;
            tmCoolDown.Stop();
            prcbCoolDown.Value = 0;
            UpdateUndoAvailability();
            UpdateMatchUI();
        }

        private void UpdateUndoAvailability()
        {
            bool canUndo = !isGameEnded && ChessBoard.MoveCount > 0;
            undoToolStripMenuItem.Enabled = canUndo;
            if (undoButton != null)
                undoButton.Enabled = canUndo;
        }

        private void ChessBoard_EndeGame(object? sender, EventArgs e)
        {
            int? winningPlayer = ChessBoard.LastMove is PlayInfo lastMove
                ? lastMove.currentPlayer + 1
                : null;
            EndGame(winningPlayer);
        }

        private void ChessBoard_PlayerMarked(object? sender, EventArgs e)
        {
            prcbCoolDown.Value = 0;
            tmCoolDown.Stop();
            tmCoolDown.Start();
            UpdateUndoAvailability();
            UpdateMatchUI();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (prcbCoolDown.Value < prcbCoolDown.Maximum)
                prcbCoolDown.PerformStep();
            if (circularTimer != null)
            {
                circularTimer.Value = prcbCoolDown.Value;
                circularTimer.Invalidate();
            }
            if (prcbCoolDown.Value >= prcbCoolDown.Maximum)
            {
                int losingPlayer = ChessBoard.CurrentPlayer + 1;
                int winningPlayer = losingPlayer == 1 ? 2 : 1;
                EndGame(winningPlayer, $"PLAYER {losingPlayer} hết giờ.");
            }
        }

        private void tmCoolDown_Tick(object sender, EventArgs e) => timer1_Tick(sender, e);

        private void newGameToolStripMenuItem_Click(object sender, EventArgs e) => NewGame();
        private void undoToolStripMenuItem_Click(object sender, EventArgs e) => UndoMove();
        private void quitToolStripMenuItem_Click(object sender, EventArgs e) => Quit();

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isLeavingRoom)
            {
                socketManager.Close();
                return;
            }
            if (MessageBox.Show("Nhấn OK để thoát", "Thông báo", MessageBoxButtons.OKCancel) != DialogResult.OK)
            {
                e.Cancel = true;
                return;
            }
            socketManager.Close();
        }

        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.N)) { NewGame(); return true; }
            if (keyData == (Keys.Control | Keys.Z)) { UndoMove(); return true; }
            if (keyData == (Keys.Control | Keys.Q)) { Quit(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void FormInMatch_Load(object? sender, EventArgs e)
        {
        }

        private sealed class RoundedPanel : Panel
        {
            public Color BorderColor = Color.Transparent;
            public float BorderWidth = 1F;
            public float Radius = 14F;

            public RoundedPanel()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new RectangleF(BorderWidth / 2F, BorderWidth / 2F,
                    Width - BorderWidth, Height - BorderWidth);
                using GraphicsPath path = CreateRoundedPath(bounds, Radius);
                using var brush = new SolidBrush(BackColor);
                e.Graphics.FillPath(brush, path);
                if (BorderColor != Color.Transparent)
                {
                    using var pen = new Pen(BorderColor, BorderWidth);
                    e.Graphics.DrawPath(pen, path);
                }
                base.OnPaint(e);
            }
        }

        private sealed class CyberButton : Control
        {
            public Color BorderColor = Color.Transparent;
            private bool _hovered;
            private bool _pressed;

            public CyberButton()
            {
                DoubleBuffered = true;
                TabStop = true;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                    ControlStyles.Selectable | ControlStyles.StandardClick, true);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hovered = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hovered = false;
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left && Enabled)
                {
                    Focus();
                    _pressed = true;
                    Invalidate();
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _pressed = false;
                Invalidate();
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.KeyCode is Keys.Enter or Keys.Space)
                {
                    _pressed = true;
                    Invalidate();
                    e.Handled = true;
                }
            }

            protected override void OnKeyUp(KeyEventArgs e)
            {
                base.OnKeyUp(e);
                if (_pressed && e.KeyCode is Keys.Enter or Keys.Space)
                {
                    _pressed = false;
                    Invalidate();
                    OnClick(EventArgs.Empty);
                    e.Handled = true;
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
                using GraphicsPath path = CreateRoundedPath(bounds, 10F);
                Color fill = !Enabled ? Color.FromArgb(45, 54, 66, 78)
                    : _pressed ? ControlPaint.Dark(BackColor, 0.08F)
                    : _hovered ? ControlPaint.Light(BackColor, 0.08F)
                    : BackColor;
                using var brush = new SolidBrush(fill);
                e.Graphics.FillPath(brush, path);
                if (BorderColor != Color.Transparent)
                {
                    using var pen = new Pen(BorderColor, 1F);
                    e.Graphics.DrawPath(pen, path);
                }
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        private sealed class PlayerMarkControl : Control
        {
            private readonly Color _fill;
            private readonly Color _accent;

            public PlayerMarkControl(string mark, Color fill, Color accent)
            {
                Text = mark;
                _fill = fill;
                _accent = accent;
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new RectangleF(0.5F, 0.5F, Width - 1, Height - 1);
                using GraphicsPath path = CreateRoundedPath(rect, 12F);
                using var brush = new SolidBrush(_fill);
                using var pen = new Pen(_accent, 1.2F);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
                using var font = new Font("Segoe UI", 20F, FontStyle.Bold);
                TextRenderer.DrawText(e.Graphics, Text, font, ClientRectangle, _accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        private sealed class StatusDotControl : Control
        {
            private readonly Color _color;

            public StatusDotControl(Color color)
            {
                _color = color;
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(_color);
                e.Graphics.FillEllipse(brush, 0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            }
        }

        private sealed class SpectatorRow : Control
        {
            public SpectatorRow(string name)
            {
                Text = name;
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var dotBrush = new SolidBrush(Green);
                e.Graphics.FillEllipse(dotBrush, 3, (Height - 6) / 2, 6, 6);
                using var font = new Font("Segoe UI", 8.5F);
                TextRenderer.DrawText(e.Graphics, Text, font, new Rectangle(16, 0, Width - 18, Height), Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        private sealed class CircularTimerControl : Control
        {
            public int Value;
            public int Maximum = 10000;

            public CircularTimerControl()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                const float thickness = 8F;
                var bounds = new RectangleF(thickness / 2F + 2, thickness / 2F + 2,
                    Width - thickness - 4, Height - thickness - 4);
                using var backgroundPen = new Pen(ColorTranslator.FromHtml("#1A2636"), thickness);
                using var progressPen = new Pen(Cyan, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                e.Graphics.DrawEllipse(backgroundPen, bounds);
                int max = Math.Max(1, Maximum);
                float remaining = Math.Clamp(1F - (float)Value / max, 0F, 1F);
                if (remaining > 0)
                    e.Graphics.DrawArc(progressPen, bounds, -90F, 360F * remaining);

                int seconds = (int)Math.Ceiling(Math.Max(0, Maximum - Value) / 1000F);
                using var font = new Font("Segoe UI", 20F, FontStyle.Bold);
                TextRenderer.DrawText(e.Graphics, $"{seconds}s", font, ClientRectangle, White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        private static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height));
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
