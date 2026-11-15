
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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

        public FormLobby()
        {
            InitializeComponent();
            DoubleBuffered = true;
            WindowState = FormWindowState.Normal;

            ChessBoard = new ChessBoardManager(
                pnlChessBoard,
                txbPlayerName,
                pctbMark
            );

            socketManager = new SocketManager();

            socketManager.MessageReceived +=
                SocketManager_MessageReceived;

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
            socketManager.Close();

            Application.Exit();
        }

        #endregion

        #region Client Connection

        public async Task ConnectToServer()  // Ket noi den Server
        {
            ClientNetworkSettings networkSettings = ClientNetworkSettings.Load();
            string endpoint = $"{networkSettings.ServerAddress}:{networkSettings.ServerPort}";
            if (!TryParseServerEndpoint(endpoint, out string host, out int port))
            {
                MessageBox.Show("Enter a valid server address in host:port format.");
                return;
            }

            localPlayerName = txbPlayerName.Text.Trim();
            if (localPlayerName.Length is < 1 or > 24)
            {
                MessageBox.Show("Player name must contain 1 to 24 characters.");
                return;
            }

            bool result = await socketManager.ConnectServer(host, port, localPlayerName, resumeToken);

            if (result)
            {
                isOnline = true;
                ChessBoard.IsOnlineMode = true;
                ChessBoard.CanMakeOnlineMove = false;
                Lan.Enabled = false;
                Text = $"CaroOnline - Connecting to {host}:{port}";
            }
            else
            {
                MessageBox.Show("Không thể kết nối Server!");
            }
        }

        private void SocketManager_MessageReceived(string data) // Nhan du lieu tu Server
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    ProcessServerMessage(data);
                }));

                return;
            }

            ProcessServerMessage(data);
        }

        private void ProcessServerMessage(string data) // Xu ly thong diep Server
        {
            try
            {
                Message? message =
                    socketManager.DeserializeData<Message>(data);

                if (message == null)
                    return;

                switch (message.Type)
                {
                    case "CONNECT":
                        MessageBox.Show("Đã kết nối Server");
                        break;

                    case "GAME_OVER":
                        EndGame();
                        break;

                    case "NEW_GAME":
                        NewGame();
                        break;

                    case "WELCOME":
                        resumeToken = message.ResumeToken;
                        isOnline = true;
                        reconnectTimer?.Stop();
                        SetConnectionStatus($"● Đã kết nối · {message.Name}", Green);
                        RefreshLobbyLists();
                        break;

                    case "PLAYER_LIST":
                        onlinePlayers.Clear();
                        onlinePlayers.AddRange(message.Players);
                        RefreshLobbyLists();
                        break;

                    case "ROOM_LIST":
                        ongoingMatches.Clear();
                        ongoingMatches.AddRange(message.Rooms.Where(room => !room.IsGameOver));
                        RefreshLobbyLists();
                        break;

                    case "ERROR":
                        HandleServerError(message);
                        break;

                    case "INFO":
                        MessageBox.Show(message.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi xử lý dữ liệu: " + ex.Message
                );
            }
        }

        private async Task SendMessage(Message message) // Gui thong diep den Server
        {
            if (!isOnline)
                return;

            try
            {
                string data = socketManager.SerializeData(message);
                await socketManager.Send(data);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to send message: " + ex.Message);
            }
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
            var notificationText = MakeLobbyLabel("Bạn có 1 thông báo mới", new Font("Segoe UI", 8.5F), Muted,
                new Point(785, 29), new Size(190, 22), ContentAlignment.MiddleRight);
            notificationText.Visible = false;
            var notification = new BellButton
            {
                Location = new Point(1014, 17),
                Size = new Size(42, 42),
                BackColor = ColorTranslator.FromHtml("#131E2E")
            };
            notification.Click += (_, _) => notificationText.Visible = !notificationText.Visible;

            surface.Controls.Add(stepNumber);
            surface.Controls.Add(title);
            surface.Controls.Add(subtitle);
            surface.Controls.Add(notificationText);
            surface.Controls.Add(notification);
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
                new Point(14, 14), new Size(182, 42), ContentAlignment.MiddleLeft));
            onlineCountLabel = MakeLobbyLabel("Ngoại tuyến", new Font("Segoe UI", 9F), Muted,
                new Point(14, 58), new Size(182, 20), ContentAlignment.MiddleLeft);
            playersCard.Controls.Add(onlineCountLabel);
            playerListControl = new PlayerListControl
            {
                Name = "playerListControl",
                Location = new Point(6, 86),
                Size = new Size(198, 418),
                BackColor = playersCard.BackColor,
                EmptyText = "Chưa kết nối server",
                SelfName = localPlayerName
            };
            playersCard.Controls.Add(playerListControl);
            playersCard.Controls.Add(MakeLobbyLabel("● Rảnh", new Font("Segoe UI", 8.5F), Green,
                new Point(14, 512), new Size(70, 20), ContentAlignment.MiddleLeft));
            playersCard.Controls.Add(MakeLobbyLabel("● Đang đấu", new Font("Segoe UI", 8.5F), Coral,
                new Point(92, 512), new Size(100, 20), ContentAlignment.MiddleLeft));

            var matchCard = MakeLobbyCard(new Point(246, 110), new Size(450, 598),
                ColorTranslator.FromHtml("#0E1622"), ColorTranslator.FromHtml("#1E2D3D"), 16);
            surface.Controls.Add(matchCard);
            matchPlayerOneLabel = MakeLobbyLabel("—", new Font("Segoe UI", 13F, FontStyle.Bold), Cyan,
                new Point(36, 16), new Size(130, 26), ContentAlignment.MiddleRight);
            matchPlayerOneLabel.AutoEllipsis = true;
            matchCard.Controls.Add(matchPlayerOneLabel);
            matchCard.Controls.Add(MakeLobbyLabel("vs", new Font("Segoe UI", 10F, FontStyle.Bold), Muted,
                new Point(184, 18), new Size(82, 24), ContentAlignment.MiddleCenter));
            matchPlayerTwoLabel = MakeLobbyLabel("—", new Font("Segoe UI", 13F, FontStyle.Bold), Coral,
                new Point(284, 16), new Size(130, 26), ContentAlignment.MiddleLeft);
            matchPlayerTwoLabel.AutoEllipsis = true;
            matchCard.Controls.Add(matchPlayerTwoLabel);
            matchCard.Controls.Add(MakeLobbyLabel("BÀN CỜ · 15 × 15", new Font("Segoe UI", 8.5F, FontStyle.Bold), Muted,
                new Point(80, 52), new Size(290, 20), ContentAlignment.MiddleCenter));
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
            watchMatchButton.Click += (_, _) =>
            {
                if (selectedMatch != null)
                    OpenInMatch(asSpectator: true);
            };
            matchCard.Controls.Add(watchMatchButton);

            var requestCard = MakeLobbyCard(new Point(712, 110), new Size(356, 140),
                ColorTranslator.FromHtml("#102027"), ColorTranslator.FromHtml("#1E464A"), 14);
            surface.Controls.Add(requestCard);
            requestCard.Controls.Add(MakeLobbyLabel("Yêu cầu thách đấu", new Font("Segoe UI", 11F, FontStyle.Bold), White,
                new Point(14, 11), new Size(320, 24), ContentAlignment.MiddleLeft));
            var challengeText = MakeLobbyLabel("Player 9 muốn đấu cùng bạn", new Font("Segoe UI", 9.5F), Muted,
                new Point(14, 38), new Size(320, 22), ContentAlignment.MiddleLeft);
            requestCard.Controls.Add(challengeText);
            var acceptButton = MakeLobbyButton("✓ Chấp nhận", new Point(12, 82), new Size(154, 38),
                ColorTranslator.FromHtml("#133829"), ColorTranslator.FromHtml("#4ADE80"), ColorTranslator.FromHtml("#1F5E43"));
            var declineButton = MakeLobbyButton("✕ Từ chối", new Point(178, 82), new Size(164, 38),
                ColorTranslator.FromHtml("#361923"), Coral, ColorTranslator.FromHtml("#612637"));
            acceptButton.Click += (_, _) =>
            {
                challengeText.Text = "Đã chấp nhận lời thách đấu";
                challengeText.ForeColor = Green;
                OpenInMatch();
            };
            declineButton.Click += (_, _) => { challengeText.Text = "Đã từ chối lời thách đấu"; challengeText.ForeColor = Coral; };
            requestCard.Controls.Add(acceptButton);
            requestCard.Controls.Add(declineButton);

            var roomsCard = MakeLobbyCard(new Point(712, 266), new Size(356, 442),
                ColorTranslator.FromHtml("#0E1622"), ColorTranslator.FromHtml("#1E2D3D"), 16);
            surface.Controls.Add(roomsCard);
            roomsCard.Controls.Add(MakeLobbyLabel("Trận đang diễn ra", new Font("Segoe UI", 12F, FontStyle.Bold), White,
                new Point(14, 10), new Size(210, 24), ContentAlignment.MiddleLeft));
            matchCountLabel = MakeLobbyLabel("0 trận", new Font("Segoe UI", 9.5F), Muted,
                new Point(254, 11), new Size(84, 22), ContentAlignment.MiddleRight);
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

        private void OpenInMatch(bool asSpectator = false)
        {
            using var match = new FormInMatch(asSpectator);
            match.ShowDialog(this);
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

                bool connected = await socketManager.ConnectServer(host, networkSettings.ServerPort, localPlayerName, resumeToken);
                if (!connected)
                {
                    SetConnectionStatus("Không kết nối được server · thử lại sau 5 giây", Coral);
                    reconnectTimer?.Start();
                }
                // Khi kết nối được, server trả WELCOME rồi tự đẩy PLAYER_LIST và ROOM_LIST.
            }
            finally
            {
                isConnecting = false;
            }
        }

        private void HandleServerError(Message message)
        {
            if (message.ErrorCode == "INVALID_LOGIN")
            {
                // Server giữ tên trong thời gian chờ kết nối lại, nên vẫn tiếp tục thử.
                loginRejected = true;
                SetConnectionStatus("Tên đang được dùng hoặc không hợp lệ · thử lại sau 5 giây", Coral);
                return;
            }

            SetConnectionStatus(string.IsNullOrWhiteSpace(message.Data) ? "Lỗi từ server" : message.Data, Coral);
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
                watchMatchButton.Enabled = match != null;
        }

        #endregion

        private static readonly Color White = ColorTranslator.FromHtml("#F8FAFC");
        private static readonly Color Muted = ColorTranslator.FromHtml("#94A3B8");
        private static readonly Color Subtle = ColorTranslator.FromHtml("#64748B");
        private static readonly Color Cyan = ColorTranslator.FromHtml("#38BDF8");
        private static readonly Color Coral = ColorTranslator.FromHtml("#F87171");
        private static readonly Color Green = ColorTranslator.FromHtml("#34D399");

        private static Label MakeLobbyLabel(string text, Font font, Color color, Point location, Size size,
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

        private bool TryParseServerEndpoint(string endpoint, out string host, out int port)
        {
            host = endpoint.Trim();
            port = ClientNetworkSettings.Load().ServerPort;
            int separator = host.LastIndexOf(':');
            if (separator <= 0)
                return !string.IsNullOrWhiteSpace(host);
            if (!int.TryParse(host[(separator + 1)..], out int parsedPort) || parsedPort is < 1 or > 65535)
                return false;
            port = parsedPort;
            host = host[..separator].Trim('[', ']');
            return !string.IsNullOrWhiteSpace(host);
        }

        private void SocketManager_Disconnected()
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            BeginInvoke(new Action(() =>
            {
                isOnline = false;
                ClearLobbyLists();
                if (!loginRejected)
                    SetConnectionStatus("Mất kết nối server · đang thử lại...", Coral);
                reconnectTimer?.Start();
            }));
        }

        private async void ChessBoard_OnlineMoveRequested(Point point, int playerIndex)
        {
            await SendMessage(new Message
            {
                Type = "MOVE",
                RoomId = currentRoomId,
                X = point.X,
                Y = point.Y,
                PlayerIndex = playerIndex
            });
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

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
                using GraphicsPath path = CreateRoundedPath(bounds, 10F);
                Color fill = !Enabled ? Color.FromArgb(45, 54, 66, 78)
                    : Capture ? ControlPaint.Dark(BackColor, 0.08F)
                    : ClientRectangle.Contains(PointToClient(MousePosition)) ? ControlPaint.Light(BackColor, 0.08F)
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

        private sealed class BellButton : Control
        {
            public BellButton()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
                using GraphicsPath buttonPath = CreateRoundedPath(bounds, 12F);
                using var buttonBrush = new SolidBrush(BackColor);
                using var borderPen = new Pen(ColorTranslator.FromHtml("#223249"), 1F);
                e.Graphics.FillPath(buttonBrush, buttonPath);
                e.Graphics.DrawPath(borderPen, buttonPath);

                using var bellPen = new Pen(White, 1.8F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var bellPath = new GraphicsPath();
                bellPath.AddArc(14, 11, 14, 16, 180, 180);
                bellPath.AddLine(14, 19, 12, 27);
                bellPath.AddLine(12, 27, 30, 27);
                bellPath.AddLine(30, 27, 28, 19);
                e.Graphics.DrawPath(bellPen, bellPath);
                e.Graphics.DrawLine(bellPen, 17, 30, 27, 30);
                using var badgeBrush = new SolidBrush(ColorTranslator.FromHtml("#EF4444"));
                using var badgeFont = new Font("Segoe UI", 7F, FontStyle.Bold);
                e.Graphics.FillEllipse(badgeBrush, 28, 2, 12, 12);
                TextRenderer.DrawText(e.Graphics, "1", badgeFont,
                    new Rectangle(28, 1, 12, 13), White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
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
                socketManager.Close();
            }
        }

        #endregion

        private async void Form1_Load(object sender, EventArgs e)
        {
            await ConnectLobbyAsync();
        }
    }
}
