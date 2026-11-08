using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

namespace CaroOnline.Winforms
{
    public class FormRegister : Form
    {
        private const int FormW = 460;
        private const int FieldW = 340;
        private const int SideMargin = (FormW - FieldW) / 2;

        private readonly TextBox txtUsername = MakeInput();
        private readonly TextBox txtPassword = MakeInput(true);
        private readonly TextBox txtConfirm = MakeInput(true);
        private readonly Label lblError = MakeCenterLabel("", Color.Firebrick, 32, 8);
        private readonly Button btnRegister = new()
        {
            Text = "Đăng ký",
            Size = new Size(FieldW, 44),
            Margin = new Padding(SideMargin, 6, SideMargin, 0)
        };
        private readonly string _endpoint;

        public string RegisteredUsername { get; private set; } = "";

        public FormRegister(string endpoint)
        {
            _endpoint = endpoint;

            Text = "Đăng ký - Caro Online";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 20, 0, 28)
            };
            panel.Controls.Add(MakeCaption("Tên đăng nhập (tối thiểu 3 ký tự)"));
            panel.Controls.Add(txtUsername);
            panel.Controls.Add(MakeCaption("Mật khẩu (tối thiểu 6 ký tự)"));
            panel.Controls.Add(txtPassword);
            panel.Controls.Add(MakeCaption("Nhập lại mật khẩu"));
            panel.Controls.Add(txtConfirm);
            panel.Controls.Add(lblError);
            panel.Controls.Add(btnRegister);
            Controls.Add(panel);

            AcceptButton = btnRegister;
            ActiveControl = txtUsername;
            btnRegister.Click += BtnRegister_Click;
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

        private void InitializeComponent()
        {

        }

        private async void BtnRegister_Click(object? sender, EventArgs e)
        {
            lblError.Text = "";
            string user = txtUsername.Text.Trim();

            if (user.Length < 3) { lblError.Text = "Tên đăng nhập tối thiểu 3 ký tự"; return; }
            if (txtPassword.Text.Length < 6) { lblError.Text = "Mật khẩu tối thiểu 6 ký tự"; return; }
            if (txtPassword.Text != txtConfirm.Text) { lblError.Text = "Mật khẩu nhập lại không khớp"; return; }

            btnRegister.Enabled = false;
            try
            {
                var (ok, error) = await AuthService.RegisterAsync(_endpoint, user, txtPassword.Text);
                if (ok)
                {
                    RegisteredUsername = user;
                    DialogResult = DialogResult.OK;
                }
                else lblError.Text = error;
            }
            catch (Exception ex) { lblError.Text = "Lỗi: " + ex.Message; }
            finally { btnRegister.Enabled = true; }
        }
    }
}