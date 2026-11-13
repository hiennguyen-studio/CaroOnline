
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

        private readonly List<LocalRoomState> roomStates = new();
        private Panel? lobbyViewPanel;
        private RoomListControl? roomCardsPanel;
        private Label? roomViewTitle;
        private LocalRoomState? selectedRoom;
        private int selectedPreviewRoomNumber = 2;
        private bool joinedRoomAsPlayer;
        private LobbyButton? joinSelectedRoomButton;
        private Button? quickNewGameButton;
        private Button? quickUndoButton;

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
            localPlayerName = Environment.UserName;
            txbPlayerName.ReadOnly = false;
            txbPlayerName.Text = localPlayerName;
            Lan.Text = "Rời phòng / Chọn phòng";

            AddLobbyMenuItems();

            InitializeRoomLobbyUI();

            NewGame();
            ShowRoomLobbyView();
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
            roomStates.Clear();
            int[] initialOccupancy = { 2, 1, 0, 0, 1, 0, 0, 1, 0, 0 };
            for (int roomNumber = 1; roomNumber <= initialOccupancy.Length; roomNumber++)
            {
                roomStates.Add(new LocalRoomState(roomNumber, initialOccupancy[roomNumber - 1]));
            }

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
            playersCard.Controls.Add(MakeLobbyLabel("128 trực tuyến", new Font("Segoe UI", 9F), Muted,
                new Point(14, 58), new Size(182, 20), ContentAlignment.MiddleLeft));
            string[] players = { "Player 1", "Player 2", "Minh Anh", "Hoàng Nam", "Player 5", "Hải Đăng", "Player 7", "Player 8" };
            for (int i = 0; i < players.Length; i++)
            {
                playersCard.Controls.Add(new PlayerEntryControl(players[i])
                {
                    Location = new Point(10, 87 + i * 39),
                    Size = new Size(188, 33),
                    BackColor = playersCard.BackColor
                });
            }
            playersCard.Controls.Add(MakeLobbyLabel("+120 người khác", new Font("Segoe UI", 9F), Muted,
                new Point(14, 451), new Size(180, 22), ContentAlignment.MiddleLeft));

            var matchCard = MakeLobbyCard(new Point(246, 110), new Size(450, 598),
                ColorTranslator.FromHtml("#0E1622"), ColorTranslator.FromHtml("#1E2D3D"), 16);
            surface.Controls.Add(matchCard);
            matchCard.Controls.Add(MakeLobbyLabel("Player 1", new Font("Segoe UI", 13F, FontStyle.Bold), Cyan,
                new Point(36, 16), new Size(130, 26), ContentAlignment.MiddleRight));
            matchCard.Controls.Add(MakeLobbyLabel("vs", new Font("Segoe UI", 10F, FontStyle.Bold), Muted,
                new Point(184, 18), new Size(82, 24), ContentAlignment.MiddleCenter));
            matchCard.Controls.Add(MakeLobbyLabel("Player 2", new Font("Segoe UI", 13F, FontStyle.Bold), Coral,
                new Point(284, 16), new Size(130, 26), ContentAlignment.MiddleLeft));
            matchCard.Controls.Add(MakeLobbyLabel("BÀN CỜ · 15 × 15", new Font("Segoe UI", 8.5F, FontStyle.Bold), Muted,
                new Point(80, 52), new Size(290, 20), ContentAlignment.MiddleCenter));
            matchCard.Controls.Add(new BoardPreviewControl
            {
                Location = new Point(60, 82),
                Size = new Size(330, 330),
                BackColor = matchCard.BackColor
            });
            var matchStatus = new MatchStatusControl
            {
                Location = new Point(90, 426),
                Size = new Size(270, 24),
                BackColor = matchCard.BackColor
            };
            matchCard.Controls.Add(matchStatus);
            var watchMatchButton = MakeLobbyButton("VÀO XEM TRẬN ĐẤU", new Point(30, 480), new Size(390, 44),
                ColorTranslator.FromHtml("#112233"), Cyan, ColorTranslator.FromHtml("#1D3B58"));
            watchMatchButton.Click += (_, _) =>
            {
                matchStatus.Text = "Đang xem Player 1 vs Player 2";
                matchStatus.Invalidate();
                OpenInMatch();
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
            roomsCard.Controls.Add(MakeLobbyLabel("Phòng chờ", new Font("Segoe UI", 12F, FontStyle.Bold), White,
                new Point(14, 10), new Size(210, 24), ContentAlignment.MiddleLeft));
            roomsCard.Controls.Add(MakeLobbyLabel("10 phòng", new Font("Segoe UI", 9.5F), Muted,
                new Point(254, 11), new Size(84, 22), ContentAlignment.MiddleRight));
            roomCardsPanel = new RoomListControl(roomStates)
            {
                Name = "roomCardsPanel",
                Location = new Point(8, 42),
                Size = new Size(340, 348),
                BackColor = roomsCard.BackColor,
                SelectedRoomNumber = selectedPreviewRoomNumber
            };
            roomCardsPanel.RoomSelected += roomNumber =>
            {
                selectedPreviewRoomNumber = roomNumber;
                joinSelectedRoomButton!.Visible = true;
            };
            roomsCard.Controls.Add(roomCardsPanel);
            joinSelectedRoomButton = MakeLobbyButton("VÀO PHÒNG", new Point(8, 398), new Size(340, 36),
                ColorTranslator.FromHtml("#123447"), Cyan, ColorTranslator.FromHtml("#1B4D65"));
            joinSelectedRoomButton.Visible = false;
            joinSelectedRoomButton.Click += (_, _) => EnterSelectedRoomUI(selectedPreviewRoomNumber);
            roomsCard.Controls.Add(joinSelectedRoomButton);

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
            RefreshRoomCards();
        }

        private void OpenInMatch()
        {
            using var match = new FormInMatch();
            match.ShowDialog(this);
        }

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

        private void RefreshRoomCards()
        {
            if (roomCardsPanel == null)
                return;

            roomCardsPanel.SelectedRoomNumber = selectedPreviewRoomNumber;
            roomCardsPanel.Invalidate();
        }

        private string GetRoomStatusText(LocalRoomState room)
        {
            return room.Occupancy switch
            {
                0 => "Trống",
                1 => "Đang chờ 1/2",
                _ => "Đang đấu 2/2"
            };
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

        private void EnterSelectedRoomUI(int roomNumber)
        {
            LocalRoomState? room = roomStates.Find(item => item.Number == roomNumber);
            if (room == null)
                return;

            bool enterAsSpectator = room.Occupancy >= 2;
            if (!enterAsSpectator)
                room.Occupancy++;
            RefreshRoomCards();
            using (var match = new FormInMatch(enterAsSpectator))
                match.ShowDialog(this);

            if (!enterAsSpectator && room.Occupancy > 0)
                room.Occupancy--;
            RefreshRoomCards();
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
            RefreshRoomCards();
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
            BeginInvoke(new Action(() => isOnline = false));
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
            private static readonly (int Row, int Column, string Mark)[] Moves =
            {
                (6, 7, "X"), (6, 8, "O"), (7, 8, "X"), (7, 9, "O"),
                (8, 7, "X"), (8, 9, "O"), (9, 6, "X"), (9, 10, "O")
            };

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

        private sealed class RoomListControl : Control
        {
            private const int RowHeight = 34;
            private const int RowSpacing = 5;
            private readonly List<LocalRoomState> _rooms;
            private int _scrollOffset;
            private int _hoveredIndex = -1;
            private bool _draggingThumb;
            private int _dragStartY;
            private int _dragStartOffset;
            private Rectangle _thumbBounds;

            public int SelectedRoomNumber;
            public event Action<int>? RoomSelected;

            public RoomListControl(List<LocalRoomState> rooms)
            {
                DoubleBuffered = true;
                _rooms = rooms;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int rowWidth = Width - 21;
                using var roomFont = new Font("Segoe UI", 9F, FontStyle.Bold);
                using var countFont = new Font("Segoe UI", 9F);
                using var statusFont = new Font("Segoe UI", 8.5F);
                for (int i = 0; i < _rooms.Count; i++)
                {
                    int y = 5 + i * (RowHeight + RowSpacing) - _scrollOffset;
                    var rect = new RectangleF(4, y, rowWidth, RowHeight);
                    if (rect.Bottom < 0 || rect.Top > Height) continue;

                    bool selected = _rooms[i].Number == SelectedRoomNumber;
                    bool hovered = i == _hoveredIndex;
                    Color fill = selected ? ColorTranslator.FromHtml("#13283C")
                        : hovered ? ColorTranslator.FromHtml("#17283A")
                        : ColorTranslator.FromHtml("#131D2B");
                    Color border = selected ? Cyan : ColorTranslator.FromHtml("#1E2F45");
                    using GraphicsPath path = CreateRoundedPath(rect, 8F);
                    using var brush = new SolidBrush(fill);
                    using var pen = new Pen(border, selected ? 1.2F : 1F);
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);

                    LocalRoomState room = _rooms[i];
                    Color statusColor = room.Occupancy == 2 ? Coral : room.Occupancy == 1 ? Cyan : Subtle;
                    string status = room.Occupancy == 2 ? "Đang đấu" : room.Occupancy == 1 ? "Đang chờ" : "Trống";
                    TextRenderer.DrawText(e.Graphics, $"Phòng {room.Number}", roomFont,
                        new Rectangle(14, y, 91, RowHeight), White,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    TextRenderer.DrawText(e.Graphics, $"{room.Occupancy}/2", countFont,
                        new Rectangle(102, y, 42, RowHeight), Muted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    TextRenderer.DrawText(e.Graphics, status, statusFont,
                        new Rectangle(151, y, 81, RowHeight), statusColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    using var dotBrush = new SolidBrush(statusColor);
                    e.Graphics.FillEllipse(dotBrush, rowWidth - 16, y + (RowHeight - 5) / 2, 5, 5);
                }

                int trackY = 5;
                int trackHeight = Math.Max(1, Height - 10);
                using var trackBrush = new SolidBrush(ColorTranslator.FromHtml("#1B2A3D"));
                using GraphicsPath trackPath = CreateRoundedPath(new RectangleF(Width - 8, trackY, 3, trackHeight), 2F);
                e.Graphics.FillPath(trackBrush, trackPath);
                int contentHeight = _rooms.Count * (RowHeight + RowSpacing) - RowSpacing + 10;
                int thumbHeight = Math.Max(30, (int)((long)trackHeight * trackHeight / Math.Max(trackHeight, contentHeight)));
                int maxScroll = Math.Max(0, contentHeight - Height);
                int thumbY = trackY + (maxScroll == 0 ? 0 : (trackHeight - thumbHeight) * _scrollOffset / maxScroll);
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
                    int maxScroll = GetMaxScroll();
                    int offset = _dragStartOffset + (e.Y - _dragStartY) * maxScroll / trackHeight;
                    SetScrollOffset(offset);
                    return;
                }

                int index = (e.Y + _scrollOffset - 5) / (RowHeight + RowSpacing);
                if (index != _hoveredIndex)
                {
                    _hoveredIndex = index >= 0 && index < _rooms.Count ? index : -1;
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
                if (e.X >= Width - 12)
                {
                    _draggingThumb = true;
                    _dragStartY = e.Y;
                    _dragStartOffset = _scrollOffset;
                    return;
                }

                int index = (e.Y + _scrollOffset - 5) / (RowHeight + RowSpacing);
                if (index >= 0 && index < _rooms.Count)
                {
                    SelectedRoomNumber = _rooms[index].Number;
                    RoomSelected?.Invoke(SelectedRoomNumber);
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

            private int GetMaxScroll()
            {
                int contentHeight = _rooms.Count * (RowHeight + RowSpacing) - RowSpacing + 10;
                return Math.Max(0, contentHeight - Height);
            }

            private void SetScrollOffset(int value)
            {
                _scrollOffset = Math.Clamp(value, 0, GetMaxScroll());
                Invalidate();
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
                socketManager.Close();
            }
        }

        #endregion

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
