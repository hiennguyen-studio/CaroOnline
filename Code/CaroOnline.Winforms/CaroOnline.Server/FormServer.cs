
using System;
using System.Windows.Forms;

namespace CaroOnline.Server
{
    public partial class FormServer : Form
    {
        private ServerSocketManager server;
        private Database database;
        private bool isDatabaseConnected = false;

        public FormServer()
        {
            InitializeComponent();

            server = new ServerSocketManager();

            server.LogReceived += Server_LogReceived;

            server.ClientCountChanged += Server_ClientCountChanged;

            lblStatus.Text = "Server đang dừng";

            listBox1.Items.Clear();

            button2.Enabled = false;
            CheckDatabaseConnection();
        }
        private void CheckDatabaseConnection()
        {
            database = new Database();

            if (database.TestConnection(out string error))
            {
                isDatabaseConnected = true;

                lblStatus.Text = "Database đã kết nối";

                richTextBox1.AppendText(
                    "Database kết nối thành công!" +
                    Environment.NewLine
                );

                button1.Enabled = true;
            }
            else
            {
                isDatabaseConnected = false;

                lblStatus.Text = "Database mất kết nối";

                richTextBox1.AppendText(
                    "Database kết nối thất bại!" +
                    Environment.NewLine +
                    "Lỗi: " + error +
                    Environment.NewLine
                );

                button1.Enabled = false;

                MessageBox.Show(
                    "Không thể kết nối Database!\n" + error,
                    "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (server.IsRunning)
                return;

            if (!isDatabaseConnected)
            {
                MessageBox.Show(
                    "Database chưa kết nối!",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return;
            }

            _ = server.StartServer();

            lblStatus.Text = "Server đang chạy";

            button1.Enabled = false;
            button2.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            server.StopServer();

            lblStatus.Text = "Server đang dừng";

            button1.Enabled = true;
            button2.Enabled = false;
        }

        private void Server_LogReceived(string message)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed)
                {
                    richTextBox1.AppendText(message + Environment.NewLine);
                }
            }));
        }

        private void Server_ClientCountChanged(int count)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                    return;

                listBox1.Items.Clear();

                for (int i = 0; i < count; i++)
                {
                    listBox1.Items.Add("Player " + (i + 1));
                }
            }));
        }

        private void FormServer_FormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            server.StopServer();
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }
}
