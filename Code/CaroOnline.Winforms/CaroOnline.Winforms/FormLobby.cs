using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GameCaro;
using System.Threading.Tasks;

namespace CaroOnline.Winforms
{
    public partial class FormLobby : Form
    {
        #region Properties

        ChessBoardManager ChessBoard;

        SocketManager socketManager;

        bool isOnline = false;

        bool isGameEnded;

        string resumeToken = string.Empty;

        string currentRoomId = string.Empty;

        string localPlayerName = string.Empty;

        private Panel? lobbyViewPanel;
        private Label? roomViewTitle;
        private LocalRoomState? selectedRoom;
        private bool joinedRoomAsPlayer;
        private Button? quickNewGameButton;
        private Button? quickUndoButton;

        // Danh sách người chơi online và các trận đang diễn ra (dữ liệu thật từ server).
        private const int ReconnectIntervalMs = 5000;
        private readonly List<PlayerSummary> onlinePlayers = new();
        private readonly List<RoomSummary> ongoingMatches = new();
        private PlayerListControl? playerListControl;
        private MatchListControl? matchListControl;
        private Label? onlineCountLabel;
        private Label? matchCountLabel;
        private Label? connectionStatusLabel;
        private Label? matchPlayerOneLabel;
        private Label? matchPlayerTwoLabel;
        private MatchStatusControl? matchStatus;
        private LobbyButton? watchMatchButton;
        private RoomSummary? selectedMatch;
        private System.Windows.Forms.Timer? reconnectTimer;
        private bool isConnecting;
        private bool loginRejected;

        // Thách đấu
        private Label? challengeText;
        private LobbyButton? acceptChallengeButton;
        private LobbyButton? declineChallengeButton;
        private RoundedPanel? challengeRequestCard;
        private RoundedPanel? ongoingMatchesCard;
        private string? pendingChallenger;

        // Đang mở cửa sổ trận đấu (tránh mở trùng khi server gửi ROOM_STATE nhiều lần)
        private bool inMatchOpen;

        private readonly ToolTip lobbyToolTip = new();

        public FormLobby()
        {
            InitializeComponent();
            DoubleBuffered = true;
            WindowState = FormWindowState.Normal;
            lobbyToolTip.OwnerDraw = true;
            lobbyToolTip.BackColor = ColorTranslator.FromHtml("#162232");
            lobbyToolTip.ForeColor = White;
            lobbyToolTip.InitialDelay = 450;
            lobbyToolTip.ReshowDelay = 100;
            lobbyToolTip.Popup += LobbyToolTip_Popup;
            lobbyToolTip.Draw += LobbyToolTip_Draw;

            ChessBoard = new ChessBoardManager(
                pnlChessBoard,
                txbPlayerName,
                pctbMark
            );

            // Tạo SocketManager trên luồng giao diện để các event luôn chạy trên luồng này
            socketManager = new SocketManager();

            socketManager.Welcomed += SocketManager_Welcomed;
            socketManager.PlayerListReceived += SocketManager_PlayerListReceived;
            socketManager.RoomListReceived += SocketManager_RoomListReceived;
            socketManager.ChallengeReceived += SocketManager_ChallengeReceived;
            socketManager.ChallengeRejected += SocketManager_ChallengeRejected;
            socketManager.ChallengeCancelled += SocketManager_ChallengeCancelled;
            socketManager.RoomStateReceived += SocketManager_RoomStateReceived;
            socketManager.GameOver += SocketManager_GameOver;
            socketManager.ErrorReceived += SocketManager_ErrorReceived;
            socketManager.InfoReceived += SocketManager_InfoReceived;
            socketManager.Disconnected += SocketManager_Disconnected;

            ChessBoard.EndeGame += ChessBoard_EndeGame;

            ChessBoard.PlayerMarked += ChessBoard_PlayerMarked;

            ChessBoard.OnlineMoveRequested += ChessBoard_OnlineMoveRequested;

            prcbCoolDown.Step = Cons.COOL_DOWN_STEP;

            prcbCoolDown.Maximum = Cons.COOL_DOWN_TIME;

            prcbCoolDown.Value = 0;

            tmCoolDown.Interval = Cons.COOL_DOWN_INTERVAL;

            ClientNetworkSettings networkSettings = ClientNetworkSettings.Load();
            txbIP.Text = $"{networkSettings.ServerAddress}:{networkSettings.ServerPort}";
            localPlayerName = GetLobbyPlayerName();
            txbPlayerName.ReadOnly = false;
            txbPlayerName.Text = localPlayerName;
            Lan.Text = "Rời phòng / Chọn phòng";

            AddLobbyMenuItems();

            InitializeRoomLobbyUI();

            NewGame();
            ShowRoomLobbyView();

            reconnectTimer = new System.Windows.Forms.Timer { Interval = ReconnectIntervalMs };
            reconnectTimer.Tick += async (_, _) => await ConnectLobbyAsync();
        }

        #endregion

        #region Methods

        void EndGame()
        {
            if (isGameEnded)
                return;
            isGameEnded = true;

            tmCoolDown.Stop();

            pnlChessBoard.Enabled = false;

            undoToolStripMenuItem.Enabled = false;
            UpdateUndoAvailability();

            MessageBox.Show("Kết thúc");
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
            reconnectTimer?.Stop();
            socketManager.Dispose();

            Application.Exit();
        }

        #endregion

        #region Kết nối Server

        private async Task ConnectLobbyAsync()
        {
            if (isConnecting || IsDisposed || socketManager.IsConnected)
                return;

            isConnecting = true;
            loginRejected = false;
            SetConnectionStatus("Đang kết nối server...", Muted);
            try
            {
                ClientNetworkSettings networkSettings = ClientNetworkSettings.Load();
                string host = string.IsNullOrWhiteSpace(AuthService.ServerHost)
                    ? networkSettings.ServerAddress
                    : AuthService.ServerHost;
                localPlayerName = GetLobbyPlayerName();
                if (playerListControl != null)
                    playerListControl.SelfName = localPlayerName;

                // Thành công: event Welcomed sẽ cập nhật giao diện,
                // sau đó server tự đẩy PLAYER_LIST và ROOM_LIST.
                await socketManager.ConnectAsync(
                    host,
                    networkSettings.ServerPort,
                    localPlayerName,
                    string.IsNullOrEmpty(resumeToken) ? null : resumeToken);
            }
            catch (InvalidOperationException ex)
            {
                // Server từ chối (tên đang được dùng / không hợp lệ...). Server giữ tên
                // trong thời gian chờ kết nối lại nên vẫn tiếp tục thử.
                loginRejected = true;
                SetConnectionStatus($"{ex.Message} · thử lại sau 5 giây", Coral);
                reconnectTimer?.Start();
            }
            catch (Exception)
            {
                SetConnectionStatus("Không kết nối được server · thử lại sau 5 giây", Coral);
                reconnectTimer?.Start();
            }
            finally
            {
                isConnecting = false;
            }
        }

        private void SocketManager_Welcomed(Message message)
        {
            resumeToken = message.ResumeToken ?? string.Empty;
            isOnline = true;
            loginRejected = false;
            reconnectTimer?.Stop();
            SetConnectionStatus($"● Đã kết nối · {message.Name}", Green);
            RefreshLobbyLists();
        }

        private void SocketManager_Disconnected(string? reason)
        {
            if (IsDisposed)
                return;

            isOnline = false;
            ShowChallenge(null);
            ClearLobbyLists();
            if (!loginRejected)
                SetConnectionStatus("Mất kết nối server · đang thử lại...", Coral);
            reconnectTimer?.Start();
        }

        private void SocketManager_PlayerListReceived(List<PlayerSummary> players)
        {
            onlinePlayers.Clear();
            onlinePlayers.AddRange(players);
            RefreshLobbyLists();
        }

        private void SocketManager_RoomListReceived(List<RoomSummary> rooms)
        {
            ongoingMatches.Clear();
            ongoingMatches.AddRange(rooms.Where(room => !room.IsGameOver));
            RefreshLobbyLists();
        }

        private void SocketManager_InfoReceived(string info)
        {
            if (!string.IsNullOrWhiteSpace(info))
                SetConnectionStatus(info, Muted);
        }

        private void SocketManager_ErrorReceived(string code, string text)
        {
            // Lời thách đấu không còn hiệu lực (người thách đã thoát...)
            if (code == "CHALLENGE_FAILED")
                ShowChallenge(null);

            SetConnectionStatus(string.IsNullOrWhiteSpace(text) ? "Lỗi từ server" : text, Coral);
        }

        private void SocketManager_GameOver(Message message)
        {
            string result = string.IsNullOrWhiteSpace(message.Result) ? message.Data ?? string.Empty : message.Result;
            SetConnectionStatus($"Phòng {message.RoomId} đã kết thúc · {result}", Muted);
            currentRoomId = string.Empty;
            RefreshLobbyLists();
        }

        #endregion

        #region Thách đấu

        // Bấm tên người chơi trong danh sách để thách đấu
        private async void PlayerListControl_PlayerSelected(PlayerSummary player)
        {
            if (!socketManager.IsConnected || socketManager.IsInRoom)
                return;

            if (string.Equals(player.Name, socketManager.PlayerName, StringComparison.OrdinalIgnoreCase))
                return;

            if (player.InRoom)
            {
                SetConnectionStatus($"{player.Name} đang trong trận", Coral);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                $"Thách đấu {player.Name}?",
                "Thách đấu",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                // Server trả INFO "Challenge sent to ..." hoặc ERROR nếu không thách được
                await socketManager.ChallengeAsync(player.Name);
            }
            catch (Exception ex)
            {
                SetConnectionStatus("Không gửi được lời thách đấu: " + ex.Message, Coral);
            }
        }

        // Có người thách đấu mình
        private void SocketManager_ChallengeReceived(string challengerName)
        {
            if (string.IsNullOrWhiteSpace(challengerName))
                return;

            ShowChallenge(challengerName);
        }

        // Lời thách đấu của mình bị từ chối
        private void SocketManager_ChallengeRejected(string rejecterName)
        {
            SetConnectionStatus($"{rejecterName} đã từ chối lời thách đấu", Coral);
        }

        private void SocketManager_ChallengeCancelled(string challengerName)
        {
            if (!string.Equals(pendingChallenger, challengerName, StringComparison.OrdinalIgnoreCase))
                return;

            ShowChallenge(null);
            SetConnectionStatus($"Lời thách đấu từ {challengerName} đã bị hủy", Muted);
        }

        private void ShowChallenge(string? challenger)
        {
            pendingChallenger = challenger;
            bool hasChallenge = !string.IsNullOrEmpty(challenger);
            LayoutChallengeCards(hasChallenge);

            if (challengeText != null)
            {
                challengeText.Text = hasChallenge ? $"{challenger} muốn đấu cùng bạn" : "Chưa có lời thách đấu";
                challengeText.ForeColor = hasChallenge ? White : Muted;
            }

            if (acceptChallengeButton != null)
                acceptChallengeButton.Enabled = hasChallenge;
            if (declineChallengeButton != null)
                declineChallengeButton.Enabled = hasChallenge;
        }

        private void LayoutChallengeCards(bool hasChallenge)
        {
            if (challengeRequestCard != null)
                challengeRequestCard.Visible = hasChallenge;

            if (ongoingMatchesCard != null)
            {
                ongoingMatchesCard.Bounds = hasChallenge
                    ? new Rectangle(712, 266, 356, 442)
                    : new Rectangle(712, 110, 356, 598);
            }

            if (matchListControl != null)
            {
                matchListControl.Bounds = hasChallenge
                    ? new Rectangle(8, 42, 340, 390)
                    : new Rectangle(8, 42, 340, 546);
            }
        }

        private async void AcceptChallenge_Click(object? sender, EventArgs e)
        {
            string? challenger = pendingChallenger;
            if (challenger == null)
                return;

            ShowChallenge(null);
            if (challengeText != null)
            {
                challengeText.Text = "Đang tạo phòng...";
                challengeText.ForeColor = Green;
            }

            try
            {
                // Server tạo phòng và gửi ROOM_STATE cho cả hai người
                await socketManager.AcceptChallengeAsync(challenger);
            }
            catch (Exception ex)
            {
                SetConnectionStatus("Không chấp nhận được: " + ex.Message, Coral);
            }
        }

        private async void DeclineChallenge_Click(object? sender, EventArgs e)
        {
            string? challenger = pendingChallenger;
            if (challenger == null)
                return;

            ShowChallenge(null);
            if (challengeText != null)
            {
                challengeText.Text = "Đã từ chối lời thách đấu";
                challengeText.ForeColor = Coral;
            }

            try
            {
                await socketManager.RejectChallengeAsync(challenger);
            }
            catch (Exception ex)
            {
                SetConnectionStatus("Không gửi được phản hồi: " + ex.Message, Coral);
            }
        }

        #endregion

        #region Vào phòng (người chơi và khán giả)

        // Bấm "VÀO XEM TRẬN ĐẤU"
        private async void WatchMatch_Click()
        {
            RoomSummary? match = selectedMatch;
            if (match == null || !socketManager.IsConnected || socketManager.IsInRoom)
                return;

            try
            {
                // Server trả ROOM_STATE (IsSpectator = true) nếu vào được,
                // hoặc ERROR (ROOM_NOT_FOUND / SPECTATE_FAILED).
                await socketManager.JoinSpectatorAsync(match.RoomId);
            }
            catch (Exception ex)
            {
                SetConnectionStatus("Không vào xem được: " + ex.Message, Coral);
            }
        }

        // Server gửi ROOM_STATE khi người chơi vào phòng, chấp nhận thách đấu,
        // khán giả vào xem hoặc kết nối lại vào phòng cũ.
        private async void SocketManager_RoomStateReceived(Message state)
        {
            if (state == null)
                return;

            currentRoomId = state.RoomId ?? string.Empty;

            // Tránh mở nhiều cửa sổ trận đấu cùng lúc.
            if (inMatchOpen)
                return;

            inMatchOpen = true;
            try
            {
                SetConnectionStatus(
                    state.IsSpectator
                        ? $"Đang xem phòng {state.RoomId}"
                        : $"Đang đấu · phòng {state.RoomId}",
                    Green);

                // FormInMatch yêu cầu SocketManager và trạng thái phòng ban đầu.
                OpenInMatch(state);
            }
            catch (Exception ex)
            {
                SetConnectionStatus(
                    "Không thể mở trận đấu: " + ex.Message,
                    Coral);
            }
            finally
            {
                inMatchOpen = false;
                currentRoomId = string.Empty;
            }

            // Khi đóng FormInMatch, rời phòng hiện tại.
            if (socketManager.IsConnected && socketManager.IsInRoom)
            {
                try
                {
                    await socketManager.LeaveRoomAsync();
                }
                catch
                {
                    // Server tự xử lý khi kết nối bị mất.
                }
            }

            // Làm mới danh sách sau khi rời phòng.
            if (socketManager.IsConnected)
            {
                try
                {
                    await socketManager.RequestPlayersAsync();
                    await socketManager.RequestRoomsAsync();
                }
                catch
                {
                    // Bỏ qua nếu kết nối bị gián đoạn.
                }
            }

            RefreshLobbyLists();
        }

        private void OpenInMatch(Message initialState)
        {
            using var match = new FormInMatch(socketManager, initialState);
            match.ShowDialog(this);
        }

        private async void AvatarButton_Click(object? sender, EventArgs e)
        {
            using var overlay = new FormOverlay { Bounds = Bounds };
            overlay.Show(this);

            string historyPlayerName = string.IsNullOrWhiteSpace(socketManager.PlayerName)
                ? localPlayerName
                : socketManager.PlayerName;
            using var history = new FormMatchHistory(historyPlayerName);
            Action<List<HistoryItem>> historyHandler = items =>
            {
                if (!history.IsDisposed)
                    history.SetHistory(items);
            };
            socketManager.HistoryReceived += historyHandler;
            history.Shown += async (_, _) =>
            {
                if (!socketManager.IsConnected)
                {
                    history.SetError("Chưa kết nối server để tải lịch sử.");
                    return;
                }

                history.SetLoading();
                try
                {
                    await socketManager.RequestHistoryAsync();
                }
                catch (Exception ex)
                {
                    if (!history.IsDisposed)
                        history.SetError("Không tải được lịch sử: " + ex.Message);
                }
            };

            try
            {
                history.ShowDialog(overlay);
            }
            finally
            {
                socketManager.HistoryReceived -= historyHandler;
                if (!overlay.IsDisposed)
                    overlay.Close();
            }
        }

        private void LobbyToolTip_Popup(object? sender, PopupEventArgs e)
        {
            using var font = new Font("Segoe UI", 8.5F);
            string text = lobbyToolTip.GetToolTip(e.AssociatedControl) ?? string.Empty;
            Size textSize = TextRenderer.MeasureText(text, font);
            e.ToolTipSize = new Size(textSize.Width + 20, textSize.Height + 12);
        }

        private void LobbyToolTip_Draw(object? sender, DrawToolTipEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = CreateRoundedPath(new RectangleF(e.Bounds.X, e.Bounds.Y,
                e.Bounds.Width - 1, e.Bounds.Height - 1), 8F);
            using var background = new SolidBrush(ColorTranslator.FromHtml("#162232"));
            using var border = new Pen(ColorTranslator.FromHtml("#2B4055"));
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(border, path);
            using var font = new Font("Segoe UI", 8.5F);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, font,
                Rectangle.Inflate(e.Bounds, -10, -6), White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        #endregion

        private void InitializeRoomLobbyUI()
        {
            menuStrip1.Visible = false;
            pnlChessBoard.Visible = false;
            information.Visible = false;
            pctbAvatar.Visible = false;

            var surface = new RoundedPanel
            {
                Name = "lobbyViewPanel",
                BackColor = ColorTranslator.FromHtml("#0C131F"),
                BorderColor = ColorTranslator.FromHtml("#1E2D40"),
                BorderWidth = 1.5F,
                Radius = 24,
                Location = new Point(16, 16),
                Size = new Size(1088, 728)
            };
            lobbyViewPanel = surface;
            Controls.Add(surface);
            surface.MouseDown += DragWindow;

            var stepNumber = MakeLobbyLabel("02", new Font("Segoe UI", 12F, FontStyle.Bold), Cyan,
                new Point(24, 17), new Size(34, 26), ContentAlignment.MiddleLeft);
            var title = MakeLobbyLabel("SẢNH CHỜ", new Font("Segoe UI", 22F, FontStyle.Bold), White,
                new Point(59, 9), new Size(166, 42), ContentAlignment.MiddleLeft);
            var subtitle = new LobbySubtitleControl
            {
                Location = new Point(232, 24),
                Size = new Size(430, 24),
                BackColor = surface.BackColor
            };
            stepNumber.MouseDown += DragWindow;
            title.MouseDown += DragWindow;
            subtitle.MouseDown += DragWindow;
            var historyAvatar = new HistoryAvatarButton
            {
                Location = new Point(1014, 17),
                Size = new Size(42, 42),
                BackColor = ColorTranslator.FromHtml("#131E2E")
            };
            historyAvatar.Click += AvatarButton_Click;
            lobbyToolTip.SetToolTip(historyAvatar, "Lịch sử đấu");

            surface.Controls.Add(stepNumber);
            surface.Controls.Add(title);
            surface.Controls.Add(subtitle);
            surface.Controls.Add(historyAvatar);
            connectionStatusLabel = MakeLobbyLabel("Chưa kết nối server", new Font("Segoe UI", 9F), Muted,
                new Point(640, 56), new Size(364, 22), ContentAlignment.MiddleRight);
            surface.Controls.Add(connectionStatusLabel);
            surface.Controls.Add(new Panel
            {
                Location = new Point(20, 87),
                Size = new Size(1048, 1),
                BackColor = ColorTranslator.FromHtml("#1E293B")
            });

            var exitButton = MakeLobbyButton("Out · Thoát", new Point(20, 110), new Size(210, 44),
                ColorTranslator.FromHtml("#351A24"), ColorTranslator.FromHtml("#F87171"), ColorTranslator.FromHtml("#5C2638"));
            exitButton.Click += (_, _) => Close();
            surface.Controls.Add(exitButton);

            var playersCard = MakeLobbyCard(new Point(20, 166), new Size(210, 542),
                ColorTranslator.FromHtml("#101926"), ColorTranslator.FromHtml("#1E2D3D"), 16);
            surface.Controls.Add(playersCard);
            playersCard.Controls.Add(MakeLobbyLabel("Danh sách\nplayer online", new Font("Segoe UI", 11F, FontStyle.Bold), White,
                new Point(14, 14), new Size(182, 42), ContentAlignment.MiddleLeft, playersCard.BackColor));
            onlineCountLabel = MakeLobbyLabel("Ngoại tuyến", new Font("Segoe UI", 9F), Muted,
                new Point(14, 58), new Size(182, 20), ContentAlignment.MiddleLeft, playersCard.BackColor);
            playersCard.Controls.Add(onlineCountLabel);
            playerListControl = new PlayerListControl
            {
                Name = "playerListControl",
                Location = new Point(6, 86),
                Size = new Size(198, 418),
                BackColor = playersCard.BackColor,
                EmptyText = "Chưa kết nối server",
                SelfName = localPlayerName,
                Cursor = Cursors.Hand
            };
            playerListControl.PlayerSelected += PlayerListControl_PlayerSelected;
            lobbyToolTip.SetToolTip(playerListControl, "Bấm vào tên người chơi để thách đấu");
            playersCard.Controls.Add(playerListControl);
            playersCard.Controls.Add(MakeLobbyLabel("● Rảnh", new Font("Segoe UI", 8.5F), Green,
                new Point(14, 512), new Size(70, 20), ContentAlignment.MiddleLeft, playersCard.BackColor));
            playersCard.Controls.Add(MakeLobbyLabel("● Đang đấu", new Font("Segoe UI", 8.5F), Coral,
                new Point(92, 512), new Size(100, 20), ContentAlignment.MiddleLeft, playersCard.BackColor));

            var matchCard = MakeLobbyCard(new Point(246, 110), new Size(450, 598),
                ColorTranslator.FromHtml("#0E1622"), ColorTranslator.FromHtml("#1E2D3D"), 16);
            surface.Controls.Add(matchCard);
            matchPlayerOneLabel = MakeLobbyLabel("—", new Font("Segoe UI", 13F, FontStyle.Bold), Cyan,
                new Point(36, 16), new Size(130, 26), ContentAlignment.MiddleRight, matchCard.BackColor);
            matchPlayerOneLabel.AutoEllipsis = true;
            matchCard.Controls.Add(matchPlayerOneLabel);
            matchCard.Controls.Add(MakeLobbyLabel("vs", new Font("Segoe UI", 10F, FontStyle.Bold), Muted,
                new Point(184, 18), new Size(82, 24), ContentAlignment.MiddleCenter, matchCard.BackColor));
            matchPlayerTwoLabel = MakeLobbyLabel("—", new Font("Segoe UI", 13F, FontStyle.Bold), Coral,
                new Point(284, 16), new Size(130, 26), ContentAlignment.MiddleLeft, matchCard.BackColor);
            matchPlayerTwoLabel.AutoEllipsis = true;
            matchCard.Controls.Add(matchPlayerTwoLabel);
            matchCard.Controls.Add(MakeLobbyLabel("BÀN CỜ · 15 × 15", new Font("Segoe UI", 8.5F, FontStyle.Bold), Muted,
                new Point(80, 52), new Size(290, 20), ContentAlignment.MiddleCenter, matchCard.BackColor));
            matchCard.Controls.Add(new BoardPreviewControl
            {
                Location = new Point(60, 82),
                Size = new Size(330, 330),
                BackColor = matchCard.BackColor
            });
            matchStatus = new MatchStatusControl
            {
                Location = new Point(60, 426),
                Size = new Size(330, 24),
                BackColor = matchCard.BackColor
            };
            matchCard.Controls.Add(matchStatus);
            watchMatchButton = MakeLobbyButton("VÀO XEM TRẬN ĐẤU", new Point(30, 480), new Size(390, 44),
                ColorTranslator.FromHtml("#112233"), Cyan, ColorTranslator.FromHtml("#1D3B58"));
            watchMatchButton.Click += (_, _) => WatchMatch_Click();
            matchCard.Controls.Add(watchMatchButton);

            var requestCard = MakeLobbyCard(new Point(712, 110), new Size(356, 140),
                ColorTranslator.FromHtml("#102027"), ColorTranslator.FromHtml("#1E464A"), 14);
            challengeRequestCard = requestCard;
            surface.Controls.Add(requestCard);
            requestCard.Controls.Add(MakeLobbyLabel("Yêu cầu thách đấu", new Font("Segoe UI", 11F, FontStyle.Bold), White,
                new Point(14, 11), new Size(320, 24), ContentAlignment.MiddleLeft, requestCard.BackColor));
            challengeText = MakeLobbyLabel("Chưa có lời thách đấu", new Font("Segoe UI", 9.5F), Muted,
                new Point(14, 38), new Size(320, 22), ContentAlignment.MiddleLeft, requestCard.BackColor);
            requestCard.Controls.Add(challengeText);
            acceptChallengeButton = MakeLobbyButton("✓ Chấp nhận", new Point(12, 82), new Size(154, 38),
                ColorTranslator.FromHtml("#133829"), ColorTranslator.FromHtml("#4ADE80"), ColorTranslator.FromHtml("#1F5E43"));
            declineChallengeButton = MakeLobbyButton("✕ Từ chối", new Point(178, 82), new Size(164, 38),
                ColorTranslator.FromHtml("#361923"), Coral, ColorTranslator.FromHtml("#612637"));
            acceptChallengeButton.Click += AcceptChallenge_Click;
            declineChallengeButton.Click += DeclineChallenge_Click;
            requestCard.Controls.Add(acceptChallengeButton);
            requestCard.Controls.Add(declineChallengeButton);
            acceptChallengeButton.BringToFront();
            declineChallengeButton.BringToFront();

            var roomsCard = MakeLobbyCard(new Point(712, 266), new Size(356, 442),
                ColorTranslator.FromHtml("#0E1622"), ColorTranslator.FromHtml("#1E2D3D"), 16);
            ongoingMatchesCard = roomsCard;
            surface.Controls.Add(roomsCard);
            roomsCard.Controls.Add(MakeLobbyLabel("Trận đang diễn ra", new Font("Segoe UI", 12F, FontStyle.Bold), White,
                new Point(14, 10), new Size(210, 24), ContentAlignment.MiddleLeft, roomsCard.BackColor));
            matchCountLabel = MakeLobbyLabel("0 trận", new Font("Segoe UI", 9.5F), Muted,
                new Point(254, 11), new Size(84, 22), ContentAlignment.MiddleRight, roomsCard.BackColor);
            roomsCard.Controls.Add(matchCountLabel);
            matchListControl = new MatchListControl
            {
                Name = "matchListControl",
                Location = new Point(8, 42),
                Size = new Size(340, 390),
                BackColor = roomsCard.BackColor,
                EmptyText = "Chưa kết nối server"
            };
            matchListControl.MatchSelected += match =>
            {
                selectedMatch = match;
                ShowSelectedMatch(match);
            };
            roomsCard.Controls.Add(matchListControl);
            ShowChallenge(null);

            roomViewTitle = new Label
            {
                Name = "roomViewTitle",
                AutoSize = false,
                Location = new Point(0, 0),
                Size = Size.Empty,
                Visible = false
            };
            Controls.Add(roomViewTitle);
            Controls.SetChildIndex(roomViewTitle, 0);
            ShowSelectedMatch(null);
            RefreshLobbyLists();
        }

        #region Danh sách online & trận đang diễn ra

        /// <summary>Tên hiển thị gửi lên server; server chỉ nhận chữ, số, khoảng trắng, '_' và '-' (tối đa 24 ký tự).</summary>
        private static string GetLobbyPlayerName()
        {
            string name = AuthService.CurrentDisplayName;
            if (string.IsNullOrWhiteSpace(name))
                name = AuthService.CurrentUsername;
            if (string.IsNullOrWhiteSpace(name))
                name = Environment.UserName;

            string cleaned = new string(name.Trim()
                .Select(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' ? c : '_')
                .Take(24)
                .ToArray()).Trim();
            return cleaned.Length == 0 ? "Player" : cleaned;
        }

        private void SetConnectionStatus(string text, Color color)
        {
            if (connectionStatusLabel == null)
                return;
            connectionStatusLabel.Text = text;
            connectionStatusLabel.ForeColor = color;
        }

        private void ClearLobbyLists()
        {
            onlinePlayers.Clear();
            ongoingMatches.Clear();
            RefreshLobbyLists();
        }

        /// <summary>Đẩy dữ liệu mới nhất vào hai danh sách và cập nhật bộ đếm.</summary>
        private void RefreshLobbyLists()
        {
            string emptyText = isOnline ? string.Empty : "Chưa kết nối server";

            if (playerListControl != null)
            {
                playerListControl.SelfName = localPlayerName;
                playerListControl.EmptyText = isOnline ? "Chưa có người chơi nào" : emptyText;
                playerListControl.SetItems(onlinePlayers);
            }

            if (onlineCountLabel != null)
            {
                int inMatch = onlinePlayers.Count(player => player.InRoom);
                onlineCountLabel.Text = isOnline
                    ? $"{onlinePlayers.Count} trực tuyến · {inMatch} đang đấu"
                    : "Ngoại tuyến";
            }

            // Trận đang chọn có thể đã kết thúc: tìm lại theo RoomId để làm mới số người xem.
            if (selectedMatch != null)
                selectedMatch = ongoingMatches.FirstOrDefault(match => match.RoomId == selectedMatch.RoomId);

            if (matchListControl != null)
            {
                matchListControl.SelectedRoomId = selectedMatch?.RoomId;
                matchListControl.EmptyText = isOnline ? "Chưa có trận nào đang diễn ra" : emptyText;
                matchListControl.SetItems(ongoingMatches);
            }

            if (matchCountLabel != null)
                matchCountLabel.Text = $"{ongoingMatches.Count} trận";

            ShowSelectedMatch(selectedMatch);
        }

        private void ShowSelectedMatch(RoomSummary? match)
        {
            selectedMatch = match;
            if (matchPlayerOneLabel != null)
                matchPlayerOneLabel.Text = match?.PlayerOne ?? "—";
            if (matchPlayerTwoLabel != null)
                matchPlayerTwoLabel.Text = match?.PlayerTwo ?? "—";
            if (matchStatus != null)
            {
                matchStatus.Text = match == null
                    ? "Chọn một trận ở danh sách bên phải"
                    : $"Đang đấu · {match.SpectatorCount} người xem";
                matchStatus.Invalidate();
            }
            if (watchMatchButton != null)
                watchMatchButton.Enabled = match != null && isOnline && !socketManager.IsInRoom;
        }

        #endregion

        private static readonly Color White = ColorTranslator.FromHtml("#F8FAFC");
        private static readonly Color Muted = ColorTranslator.FromHtml("#94A3B8");
        private static readonly Color Subtle = ColorTranslator.FromHtml("#64748B");
        private static readonly Color Cyan = ColorTranslator.FromHtml("#38BDF8");
        private static readonly Color Coral = ColorTranslator.FromHtml("#F87171");
        private static readonly Color Green = ColorTranslator.FromHtml("#34D399");

        private static Label MakeLobbyLabel(string text, Font font, Color color, Point location, Size size,
            ContentAlignment alignment, Color? backColor = null) => new()
            {
                AutoSize = false,
                BackColor = backColor ?? Color.Transparent,
                Text = text,
                Font = font,
                ForeColor = color,
                Location = location,
                Size = size,
                TextAlign = alignment
            };

        private static RoundedPanel MakeLobbyCard(Point location, Size size, Color background, Color border, int radius) => new()
        {
            Location = location,
            Size = size,
            BackColor = background,
            BorderColor = border,
            BorderWidth = 1.2F,
            Radius = radius
        };

        private static LobbyButton MakeLobbyButton(string text, Point location, Size size, Color background,
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

        private static void StyleSidebarButton(Button button, Color backColor, Color foreColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(197, 211, 225);
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.12f);
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
        }

        private void LayoutRoomView()
        {
            if (roomViewTitle == null || information == null || pnlChessBoard == null)
                return;

            const int sideGap = 20;
            const int outerMargin = 20;
            int sideWidth = Math.Clamp(ClientSize.Width / 4, 250, 300);
            int titleTop = menuStrip1.Height + 4;
            int boardTop = titleTop + 36;
            int boardAvailableWidth = ClientSize.Width - outerMargin * 2 - sideGap - sideWidth;
            int boardAvailableHeight = ClientSize.Height - boardTop - 18;
            int cellSize = Math.Max(20, Math.Min(boardAvailableWidth / Cons.CHESS_BOARD_WIDTH,
                boardAvailableHeight / Cons.CHESS_BOARD_HEIGHT));
            int boardWidth = cellSize * Cons.CHESS_BOARD_WIDTH;
            int boardHeight = cellSize * Cons.CHESS_BOARD_HEIGHT;
            int sidebarLeft = outerMargin + boardWidth + sideGap;

            Cons.CHESS_WIDTH = cellSize;
            Cons.CHESS_HEIGHT = cellSize;
            ChessBoard.SetCellSize(cellSize);
            roomViewTitle.Bounds = new Rectangle(outerMargin, titleTop, boardWidth, 30);
            roomViewTitle.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            pnlChessBoard.Location = new Point(outerMargin, boardTop);
            pnlChessBoard.Size = new Size(boardWidth, boardHeight);
            pnlChessBoard.BackColor = Color.FromArgb(250, 248, 245);

            int avatarHeight = Math.Min(180, sideWidth - 24);
            pctbAvatar.Location = new Point(sidebarLeft, boardTop);
            pctbAvatar.Size = new Size(sideWidth, avatarHeight);
            information.Location = new Point(sidebarLeft, boardTop + avatarHeight + 12);
            information.Size = new Size(sideWidth, Math.Max(280,
                ClientSize.Height - information.Top - 16));

            float scale = Math.Clamp(information.Height / 460f, 0.68f, 1f);
            int Scale(int value) => Math.Max(1, (int)Math.Round(value * scale));
            int contentLeft = 16;
            int contentWidth = Math.Max(120, sideWidth - contentLeft * 2 - 2);

            foreach (Label label in information.Controls.OfType<Label>())
            {
                label.Font = new Font("Segoe UI", Scale(9), FontStyle.Bold);
                label.Size = new Size(contentWidth, Scale(18));
                label.Location = label.Text switch
                {
                    "Thời gian lượt" => new Point(contentLeft, Scale(84)),
                    "Người chơi hiện tại" => new Point(contentLeft, Scale(135)),
                    "Phòng" => new Point(contentLeft, Scale(190)),
                    _ => new Point(contentLeft, Scale(405))
                };
            }

            pctbMark.Size = new Size(64, 64);
            pctbMark.Location = new Point((sideWidth - pctbMark.Width) / 2, Scale(8));
            prcbCoolDown.Bounds = new Rectangle(contentLeft, Scale(105), contentWidth, Scale(20));
            txbPlayerName.Bounds = new Rectangle(contentLeft, Scale(154), contentWidth, Scale(27));
            txbIP.Bounds = new Rectangle(contentLeft, Scale(209), contentWidth, Scale(27));
            txbPlayerName.Font = new Font("Segoe UI", Scale(9));
            txbIP.Font = new Font("Segoe UI", Scale(9));

            Lan.Bounds = new Rectangle(contentLeft, Scale(254), contentWidth, Scale(40));
            Lan.Font = new Font("Segoe UI", Scale(9), FontStyle.Bold);
            if (quickNewGameButton != null)
            {
                quickNewGameButton.Bounds = new Rectangle(contentLeft, Scale(301), contentWidth, Scale(40));
                quickNewGameButton.Font = new Font("Segoe UI", Scale(9), FontStyle.Bold);
            }
            if (quickUndoButton != null)
            {
                quickUndoButton.Bounds = new Rectangle(contentLeft, Scale(348), contentWidth, Scale(40));
                quickUndoButton.Font = new Font("Segoe UI", Scale(9), FontStyle.Bold);
            }
            rule.Bounds = new Rectangle(contentLeft, Scale(397), contentWidth, Scale(32));
            rule.Font = new Font("Segoe UI", Scale(8));
        }

        private void AddSidebarLabel(string text, Point location)
        {
            information.Controls.Add(new Label
            {
                AutoSize = true,
                Text = text,
                Location = location,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(74, 88, 104)
            });
        }

        private void ShowRoomLobbyView()
        {
            if (lobbyViewPanel == null)
                return;

            lobbyViewPanel.Visible = true;
            lobbyViewPanel.BringToFront();
            menuStrip1.Visible = false;
            pnlChessBoard.Visible = false;
            information.Visible = false;
            pctbAvatar.Visible = false;
            if (roomViewTitle != null)
                roomViewTitle.Visible = false;
            Text = "CaroOnline - Sảnh chọn phòng";
        }

        private void LeaveRoomToLobbyUI()
        {
            if (selectedRoom != null && joinedRoomAsPlayer && selectedRoom.Occupancy > 0)
                selectedRoom.Occupancy--;

            selectedRoom = null;
            joinedRoomAsPlayer = false;
            currentRoomId = string.Empty;
            tmCoolDown.Stop();
            prcbCoolDown.Value = 0;
            UpdateUndoAvailability();
            ShowRoomLobbyView();
        }

        private void StartNewGameUI()
        {
            if (selectedRoom != null && !joinedRoomAsPlayer)
                return;

            isGameEnded = false;
            ChessBoard.IsOnlineMode = false;
            ChessBoard.CanMakeOnlineMove = true;
            ChessBoard.NewGame();
            pnlChessBoard.Enabled = selectedRoom == null || joinedRoomAsPlayer;
            undoToolStripMenuItem.Enabled = false;
            if (quickNewGameButton != null)
                quickNewGameButton.Enabled = selectedRoom != null && joinedRoomAsPlayer;
            prcbCoolDown.Value = 0;
            tmCoolDown.Stop();
            UpdateUndoAvailability();
        }

        private void UndoLastMoveUI()
        {
            if (selectedRoom == null || !joinedRoomAsPlayer || isGameEnded || !ChessBoard.Undo())
                return;

            tmCoolDown.Stop();
            prcbCoolDown.Value = 0;
            UpdateUndoAvailability();
        }

        private void UpdateUndoAvailability()
        {
            bool canUndo = selectedRoom != null && joinedRoomAsPlayer && !isGameEnded && ChessBoard.MoveCount > 0;
            undoToolStripMenuItem.Enabled = canUndo;
            if (quickUndoButton != null)
                quickUndoButton.Enabled = canUndo;
        }

        private void AddLobbyMenuItems()
        {
            menuToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            menuToolStripMenuItem.DropDownItems.Add("Sảnh 10 phòng", null, (_, _) => LeaveRoomToLobbyUI());
        }

        private async void ChessBoard_OnlineMoveRequested(Point point, int playerIndex)
        {
            try
            {
                // Server dùng phòng và vị trí người chơi của chính phiên này
                await socketManager.SendMoveAsync(point.X, point.Y);
            }
            catch (Exception ex)
            {
                SetConnectionStatus("Không gửi được nước đi: " + ex.Message, Coral);
            }
        }

        private sealed class LocalRoomState
        {
            public int Number { get; }
            public int Occupancy { get; set; }

            public LocalRoomState(int number, int occupancy)
            {
                Number = number;
                Occupancy = occupancy;
            }
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

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            }

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

        private sealed class LobbyButton : Button
        {
            public Color BorderColor = Color.Transparent;

            public LobbyButton()
            {
                DoubleBuffered = true;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                Invalidate();
            }

            protected override void OnEnabledChanged(EventArgs e)
            {
                base.OnEnabledChanged(e);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
                using GraphicsPath path = CreateRoundedPath(bounds, 10F);
                Color fill = !Enabled ? Color.FromArgb(54, 66, 78)
                    : Capture ? ControlPaint.Dark(BackColor, 0.08F)
                    : ClientRectangle.Contains(PointToClient(MousePosition)) ? ControlPaint.Light(BackColor, 0.08F)
                    : BackColor;
                e.Graphics.Clear(fill);
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

        private sealed class HistoryAvatarButton : Control
        {
            public HistoryAvatarButton()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
                Resize += (_, _) => UpdateRegion();
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                Invalidate();
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool hovered = ClientRectangle.Contains(PointToClient(MousePosition));
                Color background = hovered ? ControlPaint.Light(BackColor, 0.08F) : BackColor;
                using var backgroundBrush = new SolidBrush(background);
                e.Graphics.FillEllipse(backgroundBrush, 2, 2, Width - 4, Height - 4);

                using var iconBrush = new SolidBrush(ColorTranslator.FromHtml("#E2E8F0"));
                float centerX = Width / 2F;
                e.Graphics.FillEllipse(iconBrush, centerX - 5, 8, 10, 10);
                e.Graphics.FillEllipse(iconBrush, centerX - 10, 22, 20, 13);

                using var borderPen = new Pen(ColorTranslator.FromHtml("#2CE6FF"), 2F);
                e.Graphics.DrawEllipse(borderPen, 1, 1, Width - 3, Height - 3);
            }

            private void UpdateRegion()
            {
                if (Width <= 0 || Height <= 0)
                    return;
                using var path = new GraphicsPath();
                path.AddEllipse(0, 0, Width, Height);
                Region? previousRegion = Region;
                Region = new Region(path);
                previousRegion?.Dispose();
            }
        }

        private sealed class LobbySubtitleControl : Control
        {
            public LobbySubtitleControl()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var font = new Font("Segoe UI", 10.5F);
                using var bright = new SolidBrush(White);
                using var muted = new SolidBrush(Muted);
                float x = 0;
                float y = (Height - font.GetHeight(e.Graphics)) / 2F;
                string[] segments = { "Chọn", " đối thủ. ", "Sẵn sàng", " lên bàn." };
                for (int i = 0; i < segments.Length; i++)
                {
                    Brush brush = i is 0 or 2 ? bright : muted;
                    e.Graphics.DrawString(segments[i], font, brush, x, y);
                    x += e.Graphics.MeasureString(segments[i], font).Width;
                }
            }
        }

        private sealed class PlayerEntryControl : Control
        {
            private bool _hovered;

            public PlayerEntryControl(string playerName)
            {
                DoubleBuffered = true;
                Text = playerName;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }
            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (_hovered)
                {
                    using GraphicsPath hoverPath = CreateRoundedPath(new RectangleF(0, 0, Width - 1, Height - 1), 7F);
                    using var hoverBrush = new SolidBrush(ColorTranslator.FromHtml("#172638"));
                    e.Graphics.FillPath(hoverBrush, hoverPath);
                }
                using var dotBrush = new SolidBrush(Green);
                e.Graphics.FillEllipse(dotBrush, 9, (Height - 8) / 2, 8, 8);
                using var font = new Font("Segoe UI", 9.5F);
                TextRenderer.DrawText(e.Graphics, Text, font,
                    new Rectangle(27, 0, Width - 32, Height), ColorTranslator.FromHtml("#E2E8F0"),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        private sealed class MatchStatusControl : Control
        {
            public MatchStatusControl()
            {
                DoubleBuffered = true;
                Text = "Đang đấu · Nước đi 08";
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                using var dotBrush = new SolidBrush(Green);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(dotBrush, 5, (Height - 8) / 2, 8, 8);
                using var font = new Font("Segoe UI", 9.5F);
                TextRenderer.DrawText(e.Graphics, Text, font,
                    new Rectangle(20, 0, Width - 20, Height), Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        private sealed class BoardPreviewControl : Control
        {
            // Server chỉ gửi tóm tắt trận (không kèm bàn cờ) nên bản xem trước để trống.
            private static readonly (int Row, int Column, string Mark)[] Moves =
                Array.Empty<(int Row, int Column, string Mark)>();

            public BoardPreviewControl()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                const int cells = 15;
                const int gap = 3;
                int cell = Math.Min((Width - (cells - 1) * gap) / cells,
                    (Height - (cells - 1) * gap) / cells);
                int boardSize = cells * cell + (cells - 1) * gap;
                int left = (Width - boardSize) / 2;
                int top = (Height - boardSize) / 2;
                using var markFont = new Font("Segoe UI", Math.Max(9, cell - 5), FontStyle.Bold);
                var markMap = new Dictionary<(int Row, int Column), string>();
                foreach (var move in Moves) markMap[(move.Row, move.Column)] = move.Mark;

                for (int row = 0; row < cells; row++)
                {
                    for (int column = 0; column < cells; column++)
                    {
                        int x = left + column * (cell + gap);
                        int y = top + row * (cell + gap);
                        var rect = new RectangleF(x, y, cell, cell);
                        using GraphicsPath path = CreateRoundedPath(rect, 3F);
                        using var brush = new SolidBrush(ColorTranslator.FromHtml("#131D2B"));
                        using var pen = new Pen(ColorTranslator.FromHtml("#1E2F45"), 1F);
                        e.Graphics.FillPath(brush, path);
                        e.Graphics.DrawPath(pen, path);

                        if (markMap.TryGetValue((row, column), out string? mark))
                        {
                            Color color = mark == "X" ? Cyan : Coral;
                            TextRenderer.DrawText(e.Graphics, mark, markFont,
                                Rectangle.Round(rect), color,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                        }
                    }
                }
            }
        }

        /// <summary>Danh sách tự vẽ, có cuộn bằng con lăn / thanh trượt, dùng chung cho người chơi và trận đấu.</summary>
        private abstract class LobbyListControl<T> : Control
        {
            private readonly List<T> _items = new();
            private int _scrollOffset;
            private int _hoveredIndex = -1;
            private bool _draggingThumb;
            private int _dragStartY;
            private int _dragStartOffset;
            private Rectangle _thumbBounds;

            public string EmptyText = "";

            protected abstract int RowHeight { get; }
            protected virtual int RowSpacing => 5;

            protected LobbyListControl()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            public void SetItems(IEnumerable<T> items)
            {
                _items.Clear();
                _items.AddRange(items);
                _hoveredIndex = -1;
                _scrollOffset = Math.Clamp(_scrollOffset, 0, GetMaxScroll());
                Invalidate();
            }

            protected abstract void PaintRow(Graphics g, T item, Rectangle bounds, bool hovered);

            protected virtual void OnRowClicked(T item)
            {
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int pitch = RowHeight + RowSpacing;
                int rowWidth = Width - 21;
                for (int i = 0; i < _items.Count; i++)
                {
                    int y = 5 + i * pitch - _scrollOffset;
                    if (y + RowHeight < 0 || y > Height) continue;
                    PaintRow(e.Graphics, _items[i], new Rectangle(4, y, rowWidth, RowHeight), i == _hoveredIndex);
                }

                if (_items.Count == 0 && EmptyText.Length > 0)
                {
                    TextRenderer.DrawText(e.Graphics, EmptyText, Font, new Rectangle(8, 0, Width - 16, Height), Subtle,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                }

                if (GetMaxScroll() <= 0)
                {
                    _thumbBounds = Rectangle.Empty;
                    return;
                }

                int trackY = 5;
                int trackHeight = Math.Max(1, Height - 10);
                using var trackBrush = new SolidBrush(ColorTranslator.FromHtml("#1B2A3D"));
                using GraphicsPath trackPath = CreateRoundedPath(new RectangleF(Width - 8, trackY, 3, trackHeight), 2F);
                e.Graphics.FillPath(trackBrush, trackPath);
                int contentHeight = GetContentHeight();
                int maxScroll = GetMaxScroll();
                int thumbHeight = Math.Max(30, (int)((long)trackHeight * trackHeight / Math.Max(trackHeight, contentHeight)));
                int thumbY = trackY + (trackHeight - thumbHeight) * _scrollOffset / maxScroll;
                _thumbBounds = new Rectangle(Width - 9, thumbY, 5, thumbHeight);
                using var thumbBrush = new SolidBrush(Cyan);
                using GraphicsPath thumbPath = CreateRoundedPath(_thumbBounds, 3F);
                e.Graphics.FillPath(thumbBrush, thumbPath);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_draggingThumb)
                {
                    int trackHeight = Math.Max(1, Height - 10 - _thumbBounds.Height);
                    SetScrollOffset(_dragStartOffset + (e.Y - _dragStartY) * GetMaxScroll() / trackHeight);
                    return;
                }

                int index = IndexAt(e.Y);
                if (index != _hoveredIndex)
                {
                    _hoveredIndex = index;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hoveredIndex = -1;
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                if (GetMaxScroll() > 0 && e.X >= Width - 12)
                {
                    _draggingThumb = true;
                    _dragStartY = e.Y;
                    _dragStartOffset = _scrollOffset;
                    return;
                }

                int index = IndexAt(e.Y);
                if (index >= 0)
                {
                    OnRowClicked(_items[index]);
                    Invalidate();
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _draggingThumb = false;
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                SetScrollOffset(_scrollOffset - Math.Sign(e.Delta) * (RowHeight + RowSpacing));
            }

            private int IndexAt(int y)
            {
                int pitch = RowHeight + RowSpacing;
                int offset = y + _scrollOffset - 5;
                if (offset < 0 || offset % pitch >= RowHeight) return -1;
                int index = offset / pitch;
                return index < _items.Count ? index : -1;
            }

            private int GetContentHeight() =>
                _items.Count == 0 ? 0 : _items.Count * (RowHeight + RowSpacing) - RowSpacing + 10;

            private int GetMaxScroll() => Math.Max(0, GetContentHeight() - Height);

            private void SetScrollOffset(int value)
            {
                _scrollOffset = Math.Clamp(value, 0, GetMaxScroll());
                Invalidate();
            }
        }

        private sealed class PlayerListControl : LobbyListControl<PlayerSummary>
        {
            public string SelfName = "";

            // Bấm vào một người chơi trong danh sách
            public event Action<PlayerSummary>? PlayerSelected;

            protected override int RowHeight => 33;
            protected override int RowSpacing => 6;

            protected override void PaintRow(Graphics g, PlayerSummary player, Rectangle bounds, bool hovered)
            {
                if (hovered)
                {
                    using GraphicsPath hoverPath = CreateRoundedPath(bounds, 7F);
                    using var hoverBrush = new SolidBrush(ColorTranslator.FromHtml("#172638"));
                    g.FillPath(hoverBrush, hoverPath);
                }

                bool isSelf = string.Equals(player.Name, SelfName, StringComparison.OrdinalIgnoreCase);
                using var dotBrush = new SolidBrush(player.InRoom ? Coral : Green);
                g.FillEllipse(dotBrush, bounds.X + 9, bounds.Y + (bounds.Height - 8) / 2, 8, 8);

                using var nameFont = new Font("Segoe UI", 9.5F, isSelf ? FontStyle.Bold : FontStyle.Regular);
                string text = isSelf ? $"{player.Name} (bạn)" : player.Name;
                TextRenderer.DrawText(g, text, nameFont,
                    new Rectangle(bounds.X + 27, bounds.Y, bounds.Width - 31, bounds.Height),
                    isSelf ? Cyan : ColorTranslator.FromHtml("#E2E8F0"),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis);
            }

            protected override void OnRowClicked(PlayerSummary player)
            {
                PlayerSelected?.Invoke(player);
            }
        }

        private sealed class MatchListControl : LobbyListControl<RoomSummary>
        {
            public string? SelectedRoomId;
            public event Action<RoomSummary>? MatchSelected;

            protected override int RowHeight => 46;

            protected override void PaintRow(Graphics g, RoomSummary match, Rectangle bounds, bool hovered)
            {
                bool selected = match.RoomId == SelectedRoomId;
                Color fill = selected ? ColorTranslator.FromHtml("#13283C")
                    : hovered ? ColorTranslator.FromHtml("#17283A")
                    : ColorTranslator.FromHtml("#131D2B");
                Color border = selected ? Cyan : ColorTranslator.FromHtml("#1E2F45");
                using GraphicsPath path = CreateRoundedPath(bounds, 8F);
                using var brush = new SolidBrush(fill);
                using var pen = new Pen(border, selected ? 1.2F : 1F);
                g.FillPath(brush, path);
                g.DrawPath(pen, path);

                using var dotBrush = new SolidBrush(Coral);
                g.FillEllipse(dotBrush, bounds.Right - 16, bounds.Y + 12, 6, 6);

                const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                using var titleFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                using var detailFont = new Font("Segoe UI", 8.5F);
                TextRenderer.DrawText(g, $"{match.PlayerOne}  vs  {match.PlayerTwo}", titleFont,
                    new Rectangle(bounds.X + 12, bounds.Y + 4, bounds.Width - 34, 22), White, flags);
                TextRenderer.DrawText(g, $"Phòng {match.RoomId} · {match.SpectatorCount} người xem", detailFont,
                    new Rectangle(bounds.X + 12, bounds.Y + 24, bounds.Width - 24, 18), Muted, flags);
            }

            protected override void OnRowClicked(RoomSummary match)
            {
                SelectedRoomId = match.RoomId;
                MatchSelected?.Invoke(match);
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

        #region Events

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            LeaveRoomToLobbyUI();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void chessboard_Paint(object sender, PaintEventArgs e)
        {

        }

        private void ChessBoard_EndeGame(object? sender, EventArgs e)
        {
            EndGame();
        }

        private void ChessBoard_PlayerMarked(object? sender, EventArgs e)
        {
            tmCoolDown.Start();

            prcbCoolDown.Value = 0;
            UpdateUndoAvailability();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            prcbCoolDown.PerformStep();

            if (prcbCoolDown.Value >= prcbCoolDown.Maximum)
            {
                EndGame();
            }
        }

        private void newGameToolStripMenuItem_Click(
            object sender, EventArgs e)
        {
            StartNewGameUI();
        }

        private void undoToolStripMenuItem_Click(
            object sender, EventArgs e)
        {
            UndoLastMoveUI();
        }

        private void quitToolStripMenuItem_Click(
            object sender, EventArgs e)
        {
            Quit();
        }

        private void Form1_FormClosing(
            object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show(
                "Nhấn OK để thoát",
                "Thông báo",
                MessageBoxButtons.OKCancel
            ) != DialogResult.OK)
            {
                e.Cancel = true;
            }
            else
            {
                reconnectTimer?.Stop();
                socketManager.Dispose();
            }
        }

        #endregion

        private async void Form1_Load(object sender, EventArgs e)
        {
            await ConnectLobbyAsync();
        }
    }
}