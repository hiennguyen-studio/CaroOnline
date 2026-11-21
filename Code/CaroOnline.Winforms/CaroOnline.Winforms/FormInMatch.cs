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
        private static readonly Color Amber = ColorTranslator.FromHtml("#FACC15");

        private readonly ChessBoardManager ChessBoard;

        // SocketManager DÙNG CHUNG với FormLobby (không tự tạo, không tự đóng)
        private readonly SocketManager socketManager;

        // Trạng thái trận (do server quyết định)
        private bool isSpectator;
        private int myIndex = -1;
        private string roomId = string.Empty;
        private string playerOneName = "PLAYER 1";
        private string playerTwoName = "PLAYER 2";
        private bool isGameEnded;
        private bool isPaused;
        private bool connectionLost;
        private bool boardLocked;
        private bool isLeavingRoom;
        private bool leaveRequested;
        private int timerMaxSeconds;

        private RoundedPanel? mainSurface;
        private Label? roomTitleLabel;
        private Label? roomInfoLabel;
        private Label? roleLabel;
        private Label? headerStatus;
        private Label? playerOneNameLabel;
        private Label? playerTwoNameLabel;
        private Label? playerOneStatus;
        private Label? playerTwoStatus;
        private Panel? activePlayerUnderline;
        private Label? turnLabel;
        private Label? moveCountLabel;
        private Label? lastMoveLabel;
        private Label? lastMoveTurnLabel;
        private Label? matchMessageLabel;
        private Label? timerHintLabel;
        private CircularTimerControl? circularTimer;
        private CyberButton? surrenderButton;

        public FormInMatch(SocketManager socketManager, Message initialState)
        {
            InitializeComponent();
            DoubleBuffered = true;

            this.socketManager = socketManager;

            ChessBoard = new ChessBoardManager(pnlChessBoard, txbPlayerName, pctbMark, 15, 15, true);
            ChessBoard.IsOnlineMode = true;
            ChessBoard.CanMakeOnlineMove = false;
            ChessBoard.OnlineMoveRequested += ChessBoard_OnlineMoveRequested;
            ChessBoard.EndeGame += ChessBoard_EndeGame;

            // Đồng hồ lượt do SERVER điều khiển (TURN_TIMER), không dùng timer cục bộ
            tmCoolDown.Stop();

            BuildMatchUI();

            // Trạng thái đầu tiên lấy từ ROOM_STATE mà FormLobby đã nhận
            ApplyRoomState(initialState);

            socketManager.RoomStateReceived += OnRoomState;
            socketManager.MoveReceived += OnMove;
            socketManager.TurnChanged += OnTurn;
            socketManager.TurnTimerTick += OnTurnTimer;
            socketManager.GameOver += OnGameOver;
            socketManager.PlayerDisconnected += OnPlayerDisconnected;
            socketManager.PlayerReconnected += OnPlayerReconnected;
            socketManager.ErrorReceived += OnError;
            socketManager.Disconnected += OnDisconnected;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // SocketManager sống lâu hơn form này nên phải hủy đăng ký sự kiện
            socketManager.RoomStateReceived -= OnRoomState;
            socketManager.MoveReceived -= OnMove;
            socketManager.TurnChanged -= OnTurn;
            socketManager.TurnTimerTick -= OnTurnTimer;
            socketManager.GameOver -= OnGameOver;
            socketManager.PlayerDisconnected -= OnPlayerDisconnected;
            socketManager.PlayerReconnected -= OnPlayerReconnected;
            socketManager.ErrorReceived -= OnError;
            socketManager.Disconnected -= OnDisconnected;

            base.OnFormClosed(e);
        }

        // ==========================================
        // NHẬN DỮ LIỆU TỪ SERVER
        // ==========================================

        // ROOM_STATE: vào phòng, kết nối lại, sau UNDO / NEW_GAME, khán giả vào xem...
        private void OnRoomState(Message state)
        {
            if (IsDisposed || isGameEnded)
                return;

            ApplyRoomState(state);
        }

        private void ApplyRoomState(Message state)
        {
            string previousRoomId = roomId;
            myIndex = state.YourPlayerIndex;
            isSpectator = state.IsSpectator;
            roomId = state.RoomId ?? string.Empty;
            playerOneName = string.IsNullOrWhiteSpace(state.Name) ? "PLAYER 1" : state.Name;
            playerTwoName = string.IsNullOrWhiteSpace(state.TargetName) ? "PLAYER 2" : state.TargetName;
            isPaused = string.Equals(state.Data, "PAUSED", StringComparison.OrdinalIgnoreCase);
            boardLocked = false;
            connectionLost = false;

            // Chỉ vẽ lại hoàn toàn khi mới vào phòng. Với cùng một phòng, ApplyRemoteSnapshot
            // chỉ đồng bộ ô cờ để tránh làm reset UI hoặc mất khả năng thao tác khi khán giả vào/rời.
            if (!string.Equals(previousRoomId, roomId, StringComparison.OrdinalIgnoreCase) || ChessBoard.Matrix == null)
            {
                ChessBoard.NewGame();
                ChessBoard.SetCellSize(30);
            }

            if (state.Board is { Length: > 0 } board)
                ChessBoard.ApplyRemoteSnapshot(board, state.PlayerIndex);
            else
                ChessBoard.CurrentPlayer = state.PlayerIndex is 0 or 1 ? state.PlayerIndex : 0;

            string shortRoom = roomId.ToUpperInvariant();
            if (roomTitleLabel != null)
                roomTitleLabel.Text = $"PHÒNG {shortRoom}";
            if (roomInfoLabel != null)
                roomInfoLabel.Text = $"Phòng {shortRoom}";
            if (roleLabel != null)
            {
                roleLabel.Text = isSpectator
                    ? "Khán giả"
                    : $"Bạn là {(myIndex == 0 ? "X" : "O")}\n{NameOf(myIndex)}";
            }

            if (isSpectator)
                SetMessage("Bạn đang xem trận đấu với tư cách khán giả.");
            else if (isPaused)
                SetMessage("Đối thủ đang mất kết nối. Ván đấu tạm dừng.");
            else if (ChessBoard.MoveCount == 0)
                SetMessage($"Ván mới bắt đầu. {playerOneName} (X) đi trước.");
            else
                SetMessage("Đã đồng bộ trạng thái ván đấu.");

            RefreshInteractivity();
            UpdateMatchUI();
        }

        // MOVE: server gửi cho CẢ HAI người chơi và khán giả, kể cả người vừa đánh
        private void OnMove(Message move)
        {
            if (IsDisposed || isGameEnded)
                return;

            Point point = new(move.X, move.Y);

            ChessBoard.ApplyRemoteMove(point, move.PlayerIndex);

            SetMessage($"{NameOf(move.PlayerIndex)} vừa đi tại {FormatCoordinate(point)}.");
            RefreshInteractivity();
            UpdateMatchUI();
        }

        // TURN: lượt chơi tiếp theo
        private void OnTurn(Message turn)
        {
            if (IsDisposed || isGameEnded)
                return;

            ChessBoard.CurrentPlayer = turn.PlayerIndex is 0 or 1 ? turn.PlayerIndex : 0;

            if (!isSpectator)
            {
                SetMessage(ChessBoard.CurrentPlayer == myIndex
                    ? "Đến lượt bạn. Hãy chọn nước đi."
                    : $"Đang chờ {NameOf(ChessBoard.CurrentPlayer)} đi...");
            }

            RefreshInteractivity();
            UpdateMatchUI();
        }

        // TURN_TIMER: mỗi giây server gửi số giây còn lại trong Data
        private void OnTurnTimer(Message tick)
        {
            if (IsDisposed || isGameEnded)
                return;

            if (!int.TryParse(tick.Data, out int remaining))
                return;

            timerMaxSeconds = Math.Max(timerMaxSeconds, remaining + 1);

            if (circularTimer != null)
            {
                circularTimer.Maximum = timerMaxSeconds * 1000;
                circularTimer.Value = Math.Clamp((timerMaxSeconds - remaining) * 1000, 0, circularTimer.Maximum);
                circularTimer.Invalidate();
            }
        }

        // GAME_OVER: server quyết định thắng / thua / hòa. Phòng đã bị đóng sau khi gửi.
        private void OnGameOver(Message over)
        {
            if (IsDisposed || isGameEnded)
                return;

            isGameEnded = true;
            boardLocked = true;
            ChessBoard.CanMakeOnlineMove = false;

            string result = string.IsNullOrWhiteSpace(over.Result) ? over.Data ?? string.Empty : over.Result;
            string text = BuildResultText(result);

            SetMessage(text.Replace("\n", " "));
            RefreshInteractivity();
            UpdateMatchUI();

            MessageBox.Show(this, text, "Trận đấu kết thúc", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Phòng đã đóng trên server, quay về sảnh
            isLeavingRoom = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnPlayerDisconnected(Message info)
        {
            if (IsDisposed || isGameEnded)
                return;

            isPaused = true;
            string seconds = string.IsNullOrWhiteSpace(info.Data) ? string.Empty : $" Chờ kết nối lại tối đa {info.Data} giây.";
            SetMessage($"{info.Name} mất kết nối.{seconds}");
            RefreshInteractivity();
            UpdateMatchUI();
        }

        private void OnPlayerReconnected(Message info)
        {
            if (IsDisposed || isGameEnded)
                return;

            isPaused = false;
            SetMessage($"{info.Name} đã kết nối lại. Ván đấu tiếp tục.");
            RefreshInteractivity();
            UpdateMatchUI();
        }

        private void OnError(string code, string text)
        {
            if (IsDisposed || isGameEnded)
                return;

            SetMessage(string.IsNullOrWhiteSpace(text) ? "Lỗi từ server." : text);

            // Nước đi bị từ chối: mở khóa lại để thử nước khác
            RefreshInteractivity();
        }

        private void OnDisconnected(string? reason)
        {
            if (IsDisposed || isGameEnded)
                return;

            connectionLost = true;
            SetMessage("Mất kết nối server. Đang thử kết nối lại...");
            RefreshInteractivity();
            UpdateMatchUI();
        }

        // ==========================================
        // GỬI LÊN SERVER
        // ==========================================

        // Người chơi bấm vào một ô: gửi nước đi, server kiểm tra rồi phát MOVE cho cả phòng
        private async void ChessBoard_OnlineMoveRequested(Point point, int playerIndex)
        {
            if (isSpectator || isGameEnded || !socketManager.IsConnected)
                return;

            // Khóa ngay để không gửi trùng khi bấm nhanh; sẽ mở lại khi có TURN / lỗi
            ChessBoard.CanMakeOnlineMove = false;

            try
            {
                await socketManager.SendMoveAsync(point.X, point.Y);
            }
            catch (Exception ex)
            {
                SetMessage("Không gửi được nước đi: " + ex.Message);
                RefreshInteractivity();
            }
        }

        private async void UndoMove()
        {
            if (isSpectator || isGameEnded || isPaused || connectionLost || !socketManager.IsConnected)
                return;

            try
            {
                // Server chỉ cho người vừa đi nước cuối được đi lại
                await socketManager.UndoAsync();
            }
            catch (Exception ex)
            {
                SetMessage("Không gửi được yêu cầu đi lại: " + ex.Message);
            }
        }

        private void Surrender()
        {
            RequestLeave("Đầu hàng", "Đầu hàng sẽ bị xử thua. Bạn có chắc chắn không?");
        }

        private void LeaveRoomToLobby()
        {
            RequestLeave("Rời phòng", "Rời phòng lúc này sẽ bị xử thua. Bạn có chắc chắn không?");
        }

        private async void RequestLeave(string caption, string confirmText)
        {
            // Khán giả, ván đã xong hoặc mất kết nối: thoát thẳng về sảnh
            if (isSpectator || isGameEnded || !socketManager.IsConnected)
            {
                isLeavingRoom = true;
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            if (leaveRequested)
                return;

            if (MessageBox.Show(this, confirmText, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                != DialogResult.Yes)
                return;

            leaveRequested = true;
            SetMessage("Đang rời phòng...");

            try
            {
                // Server xử đối thủ thắng và gửi GAME_OVER, OnGameOver sẽ đóng form
                await socketManager.LeaveRoomAsync();
            }
            catch (Exception ex)
            {
                leaveRequested = false;
                SetMessage("Không rời được phòng: " + ex.Message);
            }
        }

        // ==========================================
        // GIAO DIỆN
        // ==========================================

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
            roomTitleLabel = MakeLabel("PHÒNG", new Font("Segoe UI", 20F, FontStyle.Bold), White,
                new Point(60, 8), new Size(300, 40), ContentAlignment.MiddleLeft);
            headerStatus = MakeLabel("Đang thi đấu", new Font("Segoe UI", 10F, FontStyle.Bold), Green,
                new Point(914, 15), new Size(130, 24), ContentAlignment.MiddleRight);
            var onlineDot = new StatusDotControl(Green) { Location = new Point(897, 22), Size = new Size(8, 8) };
            var close = MakeLabel("×", new Font("Segoe UI", 16F), Muted,
                new Point(1036, 8), new Size(24, 26), ContentAlignment.MiddleCenter);
            close.Cursor = Cursors.Hand;
            close.Click += (_, _) => Close();
            step.MouseDown += DragWindow;
            roomTitleLabel.MouseDown += DragWindow;
            surface.MouseDown += DragWindow;
            surface.Controls.Add(step);
            surface.Controls.Add(roomTitleLabel);
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
            playerOneNameLabel = MakeLabel("PLAYER 1", new Font("Segoe UI", 13F, FontStyle.Bold), White,
                new Point(82, 12), new Size(290, 24), ContentAlignment.MiddleLeft);
            playerOneNameLabel.AutoEllipsis = true;
            banner.Controls.Add(playerOneNameLabel);
            playerOneStatus = MakeLabel("Đang chờ", new Font("Segoe UI", 9.5F), Cyan,
                new Point(82, 38), new Size(290, 20), ContentAlignment.MiddleLeft);
            banner.Controls.Add(playerOneStatus);
            banner.Controls.Add(MakeLabel("VS", new Font("Segoe UI", 12F, FontStyle.Bold), Subtle,
                new Point(475, 24), new Size(78, 24), ContentAlignment.MiddleCenter));
            playerTwoNameLabel = MakeLabel("PLAYER 2", new Font("Segoe UI", 13F, FontStyle.Bold), White,
                new Point(656, 12), new Size(288, 24), ContentAlignment.MiddleRight);
            playerTwoNameLabel.AutoEllipsis = true;
            banner.Controls.Add(playerTwoNameLabel);
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
            roomInfoLabel = MakeLabel("Phòng", new Font("Segoe UI", 11F, FontStyle.Bold), White,
                new Point(12, 39), new Size(140, 22), ContentAlignment.MiddleLeft);
            card.Controls.Add(roomInfoLabel);
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
            card.Controls.Add(MakeLabel("Vai trò", new Font("Segoe UI", 10F, FontStyle.Bold), White,
                new Point(12, 214), new Size(140, 20), ContentAlignment.MiddleLeft));
            roleLabel = MakeLabel("—", new Font("Segoe UI", 9.5F), Cyan,
                new Point(12, 238), new Size(140, 40), ContentAlignment.TopLeft);
            card.Controls.Add(roleLabel);
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
                Maximum = 0,
                Value = 0
            };
            card.Controls.Add(circularTimer);
            timerHintLabel = MakeLabel("Đang chờ", new Font("Segoe UI", 9.5F), Muted,
                new Point(14, 190), new Size(189, 24), ContentAlignment.MiddleCenter);
            card.Controls.Add(timerHintLabel);

            surrenderButton = MakeActionButton("ĐẦU HÀNG", new Point(12, 230), new Size(193, 42),
                ColorTranslator.FromHtml("#351A28"), Coral, ColorTranslator.FromHtml("#5C2638"));
            surrenderButton.Click += (_, _) => Surrender();
            var leaveButton = MakeActionButton("RỜI PHÒNG", new Point(12, 282), new Size(193, 42),
                ColorTranslator.FromHtml("#3A3215"), Amber, ColorTranslator.FromHtml("#75621B"));
            leaveButton.Click += (_, _) => LeaveRoomToLobby();
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
            banner.Controls.Add(MakeLabel("Trận đấu trực tuyến", new Font("Segoe UI", 10F, FontStyle.Bold), White,
                new Point(31, 11), new Size(360, 24), ContentAlignment.MiddleLeft));
            matchMessageLabel = MakeLabel("Đang đồng bộ với server...", new Font("Segoe UI", 9.5F), Muted,
                new Point(16, 38), new Size(990, 24), ContentAlignment.MiddleLeft);
            banner.Controls.Add(matchMessageLabel);
        }

        private string NameOf(int index) => index == 1 ? playerTwoName : playerOneName;

        // "Bạn" nếu là mình, ngược lại là tên người chơi
        private string Subject(int index) => !isSpectator && index == myIndex ? "Bạn" : NameOf(index);

        private void SetMessage(string text)
        {
            if (matchMessageLabel != null)
                matchMessageLabel.Text = text;
        }

        // Quyết định ai được bấm ô, nút nào sáng
        private void RefreshInteractivity()
        {
            bool locked = isGameEnded || boardLocked || isPaused || connectionLost;
            bool myTurn = !isSpectator && !locked && myIndex >= 0 && ChessBoard.CurrentPlayer == myIndex;

            ChessBoard.IsOnlineMode = true;
            ChessBoard.CanMakeOnlineMove = myTurn;
            pnlChessBoard.Enabled = !(isSpectator || isGameEnded);

            // Lượt luôn đổi qua lại, nên "chưa phải lượt mình" nghĩa là mình vừa đi nước cuối
            bool canUndo = !isSpectator && !locked && ChessBoard.MoveCount > 0 && ChessBoard.CurrentPlayer != myIndex;
            undoToolStripMenuItem.Enabled = canUndo;

            if (surrenderButton != null)
            {
                surrenderButton.Visible = !isSpectator;
                surrenderButton.Enabled = !isSpectator && !isGameEnded && !connectionLost;
            }

            if (timerHintLabel != null)
            {
                timerHintLabel.Text = isSpectator ? "Bạn đang xem trận"
                    : isGameEnded ? "Ván đã kết thúc"
                    : isPaused || connectionLost ? "Tạm dừng"
                    : myTurn ? "Hãy chọn nước đi"
                    : "Đang chờ đối thủ";
            }
        }

        private string DescribeSide(int side)
        {
            if (isGameEnded)
                return "Đã kết thúc";
            if (side != ChessBoard.CurrentPlayer)
                return "Đang chờ";
            if (isSpectator)
                return "Đang suy nghĩ";
            return side == myIndex ? "Đến lượt của bạn" : "Đối thủ đang nghĩ";
        }

        private void UpdateMatchUI()
        {
            int currentPlayer = ChessBoard.CurrentPlayer;
            bool playerOneActive = currentPlayer == 0;

            if (playerOneNameLabel != null)
                playerOneNameLabel.Text = playerOneName;
            if (playerTwoNameLabel != null)
                playerTwoNameLabel.Text = playerTwoName;

            if (playerOneStatus != null)
            {
                playerOneStatus.Text = DescribeSide(0);
                playerOneStatus.ForeColor = playerOneActive && !isGameEnded ? Cyan : Subtle;
            }
            if (playerTwoStatus != null)
            {
                playerTwoStatus.Text = DescribeSide(1);
                playerTwoStatus.ForeColor = !playerOneActive && !isGameEnded ? Coral : Subtle;
            }
            if (activePlayerUnderline != null)
            {
                activePlayerUnderline.Location = new Point(playerOneActive ? 82 : 656, 70);
                activePlayerUnderline.Size = new Size(playerOneActive ? 290 : 288, 3);
                activePlayerUnderline.BackColor = playerOneActive ? Cyan : Coral;
            }

            if (turnLabel != null)
            {
                turnLabel.Text = $"Lượt của {NameOf(currentPlayer)}";
                turnLabel.ForeColor = playerOneActive ? Cyan : Coral;
            }
            if (moveCountLabel != null)
                moveCountLabel.Text = $"Nước đi {ChessBoard.MoveCount:00}";

            PlayInfo? lastMove = ChessBoard.LastMove;
            if (lastMoveLabel != null)
                lastMoveLabel.Text = lastMove == null ? "Nước vừa đi: --" : $"Nước vừa đi: {FormatCoordinate(lastMove.Point)}";
            if (lastMoveTurnLabel != null)
                lastMoveTurnLabel.Text = "X đi trước";

            if (headerStatus != null)
            {
                if (isGameEnded)
                {
                    headerStatus.Text = "Trận đấu kết thúc";
                    headerStatus.ForeColor = Coral;
                }
                else if (connectionLost)
                {
                    headerStatus.Text = "Mất kết nối";
                    headerStatus.ForeColor = Coral;
                }
                else if (isPaused)
                {
                    headerStatus.Text = "Tạm dừng";
                    headerStatus.ForeColor = Amber;
                }
                else if (isSpectator)
                {
                    headerStatus.Text = "Khán giả";
                    headerStatus.ForeColor = Cyan;
                }
                else
                {
                    headerStatus.Text = "Đang thi đấu";
                    headerStatus.ForeColor = Green;
                }
            }
        }

        // Nội dung thông báo kết quả từ chuỗi Result của server
        private string BuildResultText(string result)
        {
            string upper = result.ToUpperInvariant();

            if (upper.StartsWith("DRAW", StringComparison.Ordinal))
                return "Ván cờ hòa!";

            int winner = upper.StartsWith("PLAYER_ONE_WIN", StringComparison.Ordinal) ? 0
                : upper.StartsWith("PLAYER_TWO_WIN", StringComparison.Ordinal) ? 1
                : -1;

            if (winner < 0)
                return "Trận đấu đã kết thúc.";

            int loser = 1 - winner;

            string headline = isSpectator ? $"{NameOf(winner)} thắng!"
                : winner == myIndex ? "Bạn đã thắng!"
                : "Bạn đã thua.";

            string reason = upper.Contains("BY_FORFEIT", StringComparison.Ordinal)
                ? $"{Subject(loser)} đã rời phòng hoặc đầu hàng."
                : upper.Contains("BY_TIMEOUT", StringComparison.Ordinal)
                    ? $"{Subject(loser)} hết giờ."
                    : upper.Contains("BY_DISCONNECT", StringComparison.Ordinal)
                        ? $"{Subject(loser)} mất kết nối quá lâu."
                        : string.Empty;

            return reason.Length > 0 ? $"{headline}\n{reason}" : headline;
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

        // ==========================================
        // SỰ KIỆN CỦA BÀN CỜ / FORM
        // ==========================================

        // Bàn cờ phát hiện 5 quân liên tiếp: chỉ khóa bàn cờ,
        // kết quả chính thức chờ GAME_OVER từ server
        private void ChessBoard_EndeGame(object? sender, EventArgs e)
        {
            boardLocked = true;
            ChessBoard.CanMakeOnlineMove = false;
        }

        // Đồng hồ do server điều khiển, hai hàm này giữ lại vì Designer đang gắn sự kiện
        private void timer1_Tick(object sender, EventArgs e)
        {
        }

        private void tmCoolDown_Tick(object sender, EventArgs e)
        {
        }

        // Ván mới do server quản lý (phòng đóng khi hết ván), nên menu này không làm gì
        private void newGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e) => UndoMove();

        private void quitToolStripMenuItem_Click(object sender, EventArgs e) => Close();

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Đã có kết quả, đang chủ động rời hoặc khán giả: đóng luôn.
            // FormLobby sẽ gửi LEAVE_ROOM nếu vẫn còn trong phòng.
            if (isLeavingRoom || isGameEnded || isSpectator)
                return;

            if (e.CloseReason != CloseReason.UserClosing)
                return;

            if (MessageBox.Show(this, "Thoát lúc này sẽ bị xử thua. Bạn có chắc chắn không?", "Thông báo",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                e.Cancel = true;
            }
        }

        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z)) { UndoMove(); return true; }
            if (keyData == (Keys.Control | Keys.Q)) { Close(); return true; }
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

        // ==========================================
        // CÁC CONTROL TỰ VẼ
        // ==========================================

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

            protected override void OnEnabledChanged(EventArgs e)
            {
                base.OnEnabledChanged(e);
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
