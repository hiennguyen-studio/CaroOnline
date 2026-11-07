
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GameCaro;
using System.Threading.Tasks;

namespace CaroOnline.Winforms
{
    public partial class Form1 : Form
    {
        #region Properties

        ChessBoardManager ChessBoard;

        SocketManager socketManager;

        bool isOnline = false;

        bool isGameEnded;

        int onlinePlayerIndex = -1;

        bool isSpectator;

        string resumeToken = string.Empty;

        string currentRoomId = string.Empty;

        string localPlayerName = string.Empty;

        private readonly List<LocalRoomState> roomStates = new();
        private Panel? lobbyViewPanel;
        private FlowLayoutPanel? roomCardsPanel;
        private Label? roomViewTitle;
        private LocalRoomState? selectedRoom;
        private bool joinedRoomAsPlayer;
        private Button? quickNewGameButton;
        private Button? quickUndoButton;

        public Form1()
        {
            InitializeComponent();
            WindowState = FormWindowState.Maximized;

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
            for (int roomNumber = 1; roomNumber <= 10; roomNumber++)
            {
                int occupancy = roomNumber % 4 == 0 ? 2 : roomNumber % 3 == 0 ? 1 : 0;
                roomStates.Add(new LocalRoomState(roomNumber, occupancy));
            }

            lobbyViewPanel = new Panel
            {
                Name = "lobbyViewPanel",
                BackColor = Color.FromArgb(242, 246, 250),
                Location = new Point(0, menuStrip1.Height),
                Size = new Size(ClientSize.Width, ClientSize.Height - menuStrip1.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            Label lobbyTitle = new()
            {
                AutoSize = false,
                Text = "SẢNH CHỌN PHÒNG",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 52, 74),
                Location = new Point(28, 18),
                Size = new Size(600, 42)
            };
            Label lobbyHint = new()
            {
                AutoSize = false,
                Text = "Chọn một phòng để bắt đầu ván Caro.",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(91, 107, 124),
                Location = new Point(30, 60),
                Size = new Size(600, 25)
            };
            roomCardsPanel = new FlowLayoutPanel
            {
                Name = "roomCardsPanel",
                Location = new Point(20, 98),
                Size = new Size(ClientSize.Width - 40, ClientSize.Height - menuStrip1.Height - 112),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(6)
            };
            lobbyViewPanel.Controls.Add(lobbyTitle);
            lobbyViewPanel.Controls.Add(lobbyHint);
            lobbyViewPanel.Controls.Add(roomCardsPanel);
            Controls.Add(lobbyViewPanel);
            Controls.SetChildIndex(lobbyViewPanel, 0);

            roomViewTitle = new Label
            {
                Name = "roomViewTitle",
                AutoSize = false,
                Location = new Point(9, 30),
                Size = new Size(860, 28),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 52, 74),
                Visible = false
            };
            Controls.Add(roomViewTitle);
            Controls.SetChildIndex(roomViewTitle, 0);

            pnlChessBoard.Padding = Padding.Empty;
            pnlChessBoard.BackColor = Color.FromArgb(250, 248, 245);
            information.BorderStyle = BorderStyle.FixedSingle;
            information.BackColor = Color.White;
            pctbAvatar.SizeMode = PictureBoxSizeMode.Zoom;
            information.Controls.Clear();
            information.Controls.Add(rule);
            information.Controls.Add(Lan);
            information.Controls.Add(pctbMark);
            information.Controls.Add(txbIP);
            information.Controls.Add(txbPlayerName);
            information.Controls.Add(prcbCoolDown);

            AddSidebarLabel("Thời gian lượt", new Point(15, 88));
            AddSidebarLabel("Người chơi hiện tại", new Point(15, 142));
            AddSidebarLabel("Phòng", new Point(15, 194));
            txbPlayerName.ReadOnly = true;
            txbPlayerName.Location = new Point(15, 165);
            txbIP.ReadOnly = true;
            txbIP.Location = new Point(15, 217);
            prcbCoolDown.Location = new Point(15, 111);
            Lan.Text = "Rời phòng / Chọn phòng";
            Lan.Location = new Point(15, 263);
            Lan.Size = new Size(193, 36);
            StyleSidebarButton(Lan, Color.FromArgb(232, 238, 245), Color.FromArgb(47, 69, 92));
            rule.Text = "Nối 5 quân liên tiếp để thắng";
            rule.Location = new Point(15, 420);

            quickNewGameButton = new Button
            {
                Name = "quickNewGameButton",
                Text = "Ván mới",
                Location = new Point(15, 309),
                Size = new Size(193, 36),
                Enabled = false
            };
            StyleSidebarButton(quickNewGameButton, Color.FromArgb(40, 109, 176), Color.White);
            quickNewGameButton.Click += (_, _) => StartNewGameUI();
            quickUndoButton = new Button
            {
                Name = "quickUndoButton",
                Text = "Undo nước đi",
                Location = new Point(15, 355),
                Size = new Size(193, 36),
                Enabled = false
            };
            StyleSidebarButton(quickUndoButton, Color.FromArgb(237, 243, 249), Color.FromArgb(47, 69, 92));
            quickUndoButton.Click += (_, _) => UndoLastMoveUI();
            information.Controls.Add(quickNewGameButton);
            information.Controls.Add(quickUndoButton);

            SizeChanged += (_, _) =>
            {
                if (lobbyViewPanel != null)
                    lobbyViewPanel.Size = new Size(ClientSize.Width, Math.Max(0, ClientSize.Height - menuStrip1.Height));
                if (roomCardsPanel != null && lobbyViewPanel != null)
                    roomCardsPanel.Size = new Size(Math.Max(0, lobbyViewPanel.Width - 40), Math.Max(0, lobbyViewPanel.Height - 112));
                LayoutRoomView();
            };

            RefreshRoomCards();
            LayoutRoomView();
        }

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

            roomCardsPanel.Controls.Clear();
            foreach (LocalRoomState room in roomStates)
            {
                Button roomCard = new()
                {
                    Name = $"roomCard{room.Number:00}",
                    Text = $"Phòng {room.Number:00}\n{GetRoomStatusText(room)}\n{room.Occupancy}/2 người",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.FromArgb(37, 52, 67),
                    BackColor = room.Occupancy == 0
                        ? Color.FromArgb(218, 241, 228)
                        : room.Occupancy == 1
                            ? Color.FromArgb(255, 239, 199)
                            : Color.FromArgb(225, 232, 247),
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(202, 132),
                    Margin = new Padding(10),
                    Tag = room.Number
                };
                roomCard.FlatAppearance.BorderColor = Color.FromArgb(198, 211, 224);
                roomCard.FlatAppearance.BorderSize = 1;
                roomCard.Click += (_, _) => EnterSelectedRoomUI(room.Number);
                roomCardsPanel.Controls.Add(roomCard);
            }
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
            menuStrip1.BringToFront();
            pnlChessBoard.Visible = false;
            information.Visible = false;
            pctbAvatar.Visible = false;
            if (roomViewTitle != null)
                roomViewTitle.Visible = false;
            Text = "CaroOnline - Sảnh chọn phòng";
        }

        private void EnterSelectedRoomUI(int roomNumber)
        {
            selectedRoom = roomStates.Find(room => room.Number == roomNumber);
            if (selectedRoom == null)
                return;

            int occupancyBeforeJoin = selectedRoom.Occupancy;
            joinedRoomAsPlayer = occupancyBeforeJoin < 2;
            isSpectator = !joinedRoomAsPlayer;
            onlinePlayerIndex = joinedRoomAsPlayer ? occupancyBeforeJoin : -1;
            if (joinedRoomAsPlayer)
                selectedRoom.Occupancy++;

            currentRoomId = $"room-{roomNumber:00}";
            txbIP.Text = $"Phòng {roomNumber:00}";
            ChessBoard.Player[0].Name = "Player 1";
            ChessBoard.Player[1].Name = "Player 2";
            roomViewTitle!.Text = isSpectator
                ? $"Phòng {roomNumber:00} - Khán giả"
                : $"Phòng {roomNumber:00} - Player {onlinePlayerIndex + 1}";

            RefreshRoomCards();
            StartNewGameUI();
            pnlChessBoard.Enabled = joinedRoomAsPlayer;
            ShowRoomGameView();
        }

        private void ShowRoomGameView()
        {
            if (lobbyViewPanel != null)
                lobbyViewPanel.Visible = false;
            pnlChessBoard.Visible = true;
            information.Visible = true;
            pctbAvatar.Visible = true;
            if (roomViewTitle != null)
                roomViewTitle.Visible = true;
            roomViewTitle?.BringToFront();
            Text = roomViewTitle?.Text ?? "CaroOnline";
        }

        private void LeaveRoomToLobbyUI()
        {
            if (selectedRoom != null && joinedRoomAsPlayer && selectedRoom.Occupancy > 0)
                selectedRoom.Occupancy--;

            selectedRoom = null;
            joinedRoomAsPlayer = false;
            isSpectator = false;
            onlinePlayerIndex = -1;
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
