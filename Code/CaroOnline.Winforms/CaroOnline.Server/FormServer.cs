using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CaroOnline.Server
{
    public partial class FormServer : Form
    {
        private readonly ServerSocketManager server;
        private readonly AuthServer authServer;
        private readonly Database database;

        public FormServer()
        {
            InitializeComponent();

            // Khởi tạo Database
            database = new Database();

            // Khởi tạo Auth Server
            authServer = new AuthServer(database);

            // Khởi tạo Game Server (đa phòng)
            server = new ServerSocketManager();
            server.Database = database;

            // Nhận log từ Auth Server
            authServer.Log += message =>
            {
                Server_LogReceived("[AUTH] " + message);
            };

            // Nhận log từ Game Server
            server.LogReceived += Server_LogReceived;

            // Nhận số lượng kết nối
            server.ClientCountChanged += Server_ClientCountChanged;

            // Nhận danh sách Client (kèm phòng đang ở)
            server.ClientListChanged += Server_ClientListChanged;

            // Nhận số lượng phòng đang chạy
            server.RoomCountChanged += Server_RoomCountChanged;

            ResetServerLabels();

            listBox1.Items.Clear();

            button2.Enabled = false;
        }

        // =================================
        // GIAO DIỆN
        // =================================

        private void ResetServerLabels()
        {
            lblStatus.Text = "Server đang dừng";
            UpdateInfoLabel(0);
            label2.Text = $"Danh sách Client (0/{server.MaxConnections})";
        }

        private void UpdateInfoLabel(int roomCount)
        {
            label1.Text =
                $"Auth: {AuthServer.Port} | Game: {server.Port} | " +
                $"Phòng: {roomCount}/{server.MaxRooms}";
        }

        // Chạy một hành động trên luồng giao diện, bỏ qua nếu Form đang đóng
        private void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed)
                        return;

                    action();
                }));
            }
            catch (InvalidOperationException)
            {
                // Form đang đóng hoặc đã đóng.
                // (ObjectDisposedException kế thừa InvalidOperationException
                // nên đã được bắt ở đây.)
            }
        }

        // =================================
        // START SERVER
        // =================================

        private async void button1_Click(object sender, EventArgs e)
        {
            if (server.IsRunning)
                return;

            button1.Enabled = false;

            try
            {
                // Kiểm tra kết nối SQL Server
                var result = await Task.Run(() =>
                {
                    bool ok = database.TestConnection(out string error);
                    return (ok, error);
                });

                if (!result.ok)
                {
                    MessageBox.Show(
                        "Không kết nối được SQL Server:\n" +
                        result.error,
                        "Lỗi Database",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    button1.Enabled = true;
                    return;
                }

                Server_LogReceived("Kết nối SQL Server thành công.");

                // Khởi động Auth Server
                authServer.Start();

                Server_LogReceived(
                    $"Auth Server đang chạy tại cổng {AuthServer.Port}."
                );

                // Khởi động Game Server (đa phòng)
                _ = RunGameServerAsync();

                Server_LogReceived(
                    $"Đang khởi động Game Server tại cổng {server.Port}..."
                );

                lblStatus.Text = "Server đang chạy";

                button2.Enabled = true;
            }
            catch (Exception ex)
            {
                authServer.Stop();
                server.StopServer();

                lblStatus.Text = "Server đang dừng";

                button1.Enabled = true;
                button2.Enabled = false;

                MessageBox.Show(
                    "Không thể khởi động Server:\n" + ex.Message,
                    "Lỗi Server",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        // Chạy Game Server và cập nhật giao diện khi Server tự dừng
        // (ví dụ: cổng đang bị chiếm)
        private async Task RunGameServerAsync()
        {
            await server.StartServer();

            RunOnUi(() =>
            {
                // Nếu người dùng chưa bấm Stop mà Server đã dừng
                if (button2.Enabled)
                {
                    authServer.Stop();

                    ResetServerLabels();
                    listBox1.Items.Clear();

                    button1.Enabled = true;
                    button2.Enabled = false;
                }
            });
        }

        // =================================
        // STOP SERVER
        // =================================

        private void button2_Click(object sender, EventArgs e)
        {
            StopAllServers();
        }

        private void StopAllServers()
        {
            try
            {
                // Dừng Auth Server
                authServer?.Stop();

                // Dừng Game Server (đóng toàn bộ phòng)
                server?.StopServer();

                button1.Enabled = true;
                button2.Enabled = false;

                listBox1.Items.Clear();
                ResetServerLabels();

                Server_LogReceived("Đã dừng toàn bộ Server.");
            }
            catch (Exception ex)
            {
                Server_LogReceived("Lỗi khi dừng Server: " + ex.Message);
            }
        }

        // =================================
        // SERVER LOG
        // =================================

        private void Server_LogReceived(string message)
        {
            RunOnUi(() =>
            {
                richTextBox1.AppendText(
                    message + Environment.NewLine
                );

                richTextBox1.ScrollToCaret();
            });
        }

        // =================================
        // CLIENT COUNT
        // =================================

        private void Server_ClientCountChanged(int count)
        {
            RunOnUi(() =>
            {
                label2.Text =
                    $"Danh sách Client ({count}/{server.MaxConnections})";
            });
        }

        // =================================
        // ROOM COUNT
        // =================================

        private void Server_RoomCountChanged(int roomCount)
        {
            RunOnUi(() => UpdateInfoLabel(roomCount));
        }

        // =================================
        // CLIENT LIST
        // =================================

        private void Server_ClientListChanged(string[] clients)
        {
            RunOnUi(() =>
            {
                listBox1.BeginUpdate();

                try
                {
                    listBox1.Items.Clear();

                    foreach (string client in clients)
                    {
                        listBox1.Items.Add(client);
                    }
                }
                finally
                {
                    listBox1.EndUpdate();
                }
            });
        }

        // =================================
        // FORM CLOSING
        // =================================

        private void FormServer_FormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            StopAllServers();
        }
    }
}