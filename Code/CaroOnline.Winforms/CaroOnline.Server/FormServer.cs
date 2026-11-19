
using System;
using System.Windows.Forms;

namespace CaroOnline.Server
{
    public partial class FormServer : Form
    {
        private ServerSocketManager server;

        public FormServer()
        {
            InitializeComponent();

            server = new ServerSocketManager();

            // Nhận thông báo từ Server
            server.LogReceived += Server_LogReceived;

            // Nhận số lượng Client
            server.ClientCountChanged += Server_ClientCountChanged;

            // Nhận danh sách Client thực tế
            server.ClientListChanged += Server_ClientListChanged;

            lblStatus.Text = "Server đang dừng";

            label1.Text = "IP Address: 0.0.0.0";

            listBox1.Items.Clear();

            button2.Enabled = false;
        }

        // =================================
        // START SERVER
        // =================================

        private void button1_Click(object sender, EventArgs e)
        {
            if (server.IsRunning)
                return;

            _ = server.StartServer();

            lblStatus.Text = "Server đang chạy";

            button1.Enabled = false;

            button2.Enabled = true;

            richTextBox1.AppendText(
                "Đang khởi động Server...\n");
        }

        // =================================
        // STOP SERVER
        // =================================

        private void button2_Click(object sender, EventArgs e)
        {
            server.StopServer();

            lblStatus.Text = "Server đang dừng";

            button1.Enabled = true;

            button2.Enabled = false;
        }

        // =================================
        // SERVER LOG
        // =================================

        private void Server_LogReceived(string message)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed)
                    {
                        richTextBox1.AppendText(
                            message + Environment.NewLine);

                        richTextBox1.ScrollToCaret();
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // Form đang đóng.
            }
        }

        // =================================
        // CLIENT COUNT
        // =================================

        private void Server_ClientCountChanged(int count)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed)
                    {
                        label2.Text =
                            $"Danh sách Client ({count}/2)";
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // Form đang đóng.
            }
        }

        // =================================
        // CLIENT LIST
        // =================================

        private void Server_ClientListChanged(string[] clients)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed)
                        return;

                    listBox1.Items.Clear();

                    foreach (string client in clients)
                    {
                        listBox1.Items.Add(client);
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // Form đang đóng.
            }
        }

        // =================================
        // FORM CLOSING
        // =================================

        private void FormServer_FormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            server?.StopServer();
        }
    }
}
