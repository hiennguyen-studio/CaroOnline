using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

namespace CaroOnline.Winforms
{
    public class FormLogin : Form
    {
        private const int FormW = 460;                   
        private const int FieldW = 340;                   
        private const int SideMargin = (FormW - FieldW) / 2;

        private readonly TextBox txtServer = MakeInput();
        private readonly TextBox txtUsername = MakeInput();
        private readonly TextBox txtPassword = MakeInput(true);
        private readonly Label lblMessage = MakeCenterLabel("", Color.Firebrick, 32, 8);
        private readonly Button btnLogin = new()
        {
            Text = "Đăng nhập",
            Size = new Size(FieldW, 44),
            Margin = new Padding(SideMargin, 6, SideMargin, 0)
        };
        private readonly LinkLabel lnkRegister = new()
        {
            Text = "Chưa có tài khoản? Đăng ký",
            AutoSize = false,
            Size = new Size(FormW, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 20, 0, 0)
        };

        public string ServerEndpoint => txtServer.Text.Trim();
        public string Username => txtUsername.Text.Trim();
        public string Password => txtPassword.Text;

        public FormLogin()
        {
            Text = "Đăng nhập - Caro Online";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            txtServer.Text = $"{SocketManager.IP}:{SocketManager.PORT}";

            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 20, 0, 28)
            };
            panel.Controls.Add(MakeCaption("Server (host:port)"));
            panel.Controls.Add(txtServer);
            panel.Controls.Add(MakeCaption("Tên đăng nhập"));
            panel.Controls.Add(txtUsername);
            panel.Controls.Add(MakeCaption("Mật khẩu"));
            panel.Controls.Add(txtPassword);
            panel.Controls.Add(lblMessage);
            panel.Controls.Add(btnLogin);
            panel.Controls.Add(lnkRegister);
            Controls.Add(panel);

            AcceptButton = btnLogin;
            ActiveControl = txtUsername;
            btnLogin.Click += BtnLogin_Click;
            lnkRegister.LinkClicked += LnkRegister_LinkClicked;
        }

        private static TextBox MakeInput(bool password = false) => new()
        {
            Width = FieldW,
            UseSystemPasswordChar = password,
            Margin = new Padding(SideMargin, 0, SideMargin, 0)
        };

        private static Label MakeCenterLabel(string text, Color color, int height, int topMargin) => new()
        {
            Text = text,
            ForeColor = color,
            AutoSize = false,
            AutoEllipsis = true,                     
            Size = new Size(FormW, height),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, topMargin, 0, 0)
        };

        private static Label MakeCaption(string text) =>
            MakeCenterLabel(text, SystemColors.ControlText, 28, 10);

        private void ShowMessage(string text, bool isError)
        {
            lblMessage.ForeColor = isError ? Color.Firebrick : Color.SeaGreen;
            lblMessage.Text = text;
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            ShowMessage("", true);
            if (Username.Length == 0 || Password.Length == 0)
            {
                ShowMessage("Vui lòng nhập tên đăng nhập và mật khẩu", true);
                return;
            }

            btnLogin.Enabled = false;
            try
            {
                var (ok, error) = await AuthService.LoginAsync(ServerEndpoint, Username, Password);
                if (ok) DialogResult = DialogResult.OK;
                else ShowMessage(error, true);
            }
            catch (Exception ex) { ShowMessage("Lỗi: " + ex.Message, true); }
            finally { btnLogin.Enabled = true; }
        }

        private void InitializeComponent()
        {

        }

        private void LnkRegister_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            using var f = new FormRegister(ServerEndpoint);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                txtUsername.Text = f.RegisteredUsername;
                txtPassword.Clear();
                txtPassword.Focus();
                ShowMessage("Đăng ký thành công, hãy đăng nhập", false);
            }
        }
    }
}