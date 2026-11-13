using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CaroOnline.Winforms
{
    public class FormRegister : Form
    {
        private const int FormW = 544;
        private const int FormH = 400;
        private const int FieldW = 392;
        private static readonly Color FormColor = ColorTranslator.FromHtml("#0B1118");
        private static readonly Color CardColor = ColorTranslator.FromHtml("#0F1722");
        private static readonly Color TextColor = ColorTranslator.FromHtml("#F8FAFC");
        private static readonly Color MutedColor = ColorTranslator.FromHtml("#94A3B8");
        private static readonly Color CyanColor = ColorTranslator.FromHtml("#38BDF8");
        private static readonly Color ButtonCyanColor = ColorTranslator.FromHtml("#4CE0F6");
        private static readonly Color InputColor = ColorTranslator.FromHtml("#131C28");

        private readonly TextBox txtUsername = MakeInput("Tối thiểu 3 ký tự");
        private readonly TextBox txtPassword = MakeInput("Tối thiểu 6 ký tự", true);
        private readonly TextBox txtConfirm = MakeInput("Nhập lại mật khẩu", true);
        private readonly Label lblError = new()
        {
            AutoEllipsis = true,
            ForeColor = Color.Firebrick,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        private readonly RoundedButton btnRegister = new()
        {
            Text = "ĐĂNG KÝ",
            BackColor = ButtonCyanColor,
            ForeColor = ColorTranslator.FromHtml("#081018"),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        private readonly RoundedButton btnBack = new()
        {
            Text = "QUAY LẠI",
            BackColor = ColorTranslator.FromHtml("#111C28"),
            ForeColor = CyanColor,
            BorderColor = ColorTranslator.FromHtml("#26465B"),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        private readonly string _endpoint;

        public string RegisteredUsername { get; private set; } = "";

        public FormRegister(string endpoint)
        {
            _endpoint = endpoint;

            Text = "Đăng ký - Caro Online";
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9.5F);
            BackColor = FormColor;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(FormW, FormH);
            ApplyRoundedWindowRegion();

            var card = new RoundedPanel
            {
                Location = new Point(16, 5),
                Size = new Size(512, 390),
                BackColor = CardColor,
                BorderColor = ColorTranslator.FromHtml("#243447"),
                BorderWidth = 1.5F,
                Radius = 24
            };
            Controls.Add(card);
            card.MouseDown += DragWindow;

            var step = MakeLabel("02", new Font("Segoe UI", 11F, FontStyle.Bold), CyanColor,
                new Point(24, 14), new Size(32, 24), ContentAlignment.MiddleLeft);
            var title = MakeLabel("ĐĂNG KÝ", new Font("Segoe UI", 12F, FontStyle.Bold), MutedColor,
                new Point(55, 13), new Size(160, 26), ContentAlignment.MiddleLeft);
            step.MouseDown += DragWindow;
            title.MouseDown += DragWindow;
            card.Controls.Add(step);
            card.Controls.Add(title);

            var close = MakeLabel("×", new Font("Segoe UI", 16F), MutedColor,
                new Point(474, 8), new Size(26, 28), ContentAlignment.MiddleCenter);
            close.Cursor = Cursors.Hand;
            close.Click += (_, _) => Close();
            card.Controls.Add(close);

            var logo = MakeLabel("Caro", new Font("Segoe UI", 32F, FontStyle.Bold | FontStyle.Italic), CyanColor,
                new Point(76, 67), new Size(360, 48), ContentAlignment.MiddleCenter);
            var slogan = MakeLabel("Kết nối. Thách đấu. Bứt phá.", new Font("Segoe UI", 10F), MutedColor,
                new Point(56, 117), new Size(400, 24), ContentAlignment.MiddleCenter);
            logo.MouseDown += DragWindow;
            slogan.MouseDown += DragWindow;
            card.Controls.Add(logo);
            card.Controls.Add(slogan);

            card.Controls.Add(MakeInputRow("Tên người chơi", txtUsername, 145));
            card.Controls.Add(MakeInputRow("Mật khẩu", txtPassword, 197));
            card.Controls.Add(MakeInputRow("Nhập lại MK", txtConfirm, 249));

            btnBack.Location = new Point(61, 305);
            btnBack.Size = new Size(188, 44);
            btnRegister.Location = new Point(265, 305);
            btnRegister.Size = new Size(188, 44);
            card.Controls.Add(btnBack);
            card.Controls.Add(btnRegister);

            lblError.Location = new Point(44, 352);
            lblError.Size = new Size(424, 26);
            card.Controls.Add(lblError);

            AcceptButton = btnRegister;
            ActiveControl = txtUsername;
            btnBack.Click += (_, _) => Close();
            btnRegister.Click += BtnRegister_Click;
        }

        private static TextBox MakeInput(string placeholder, bool password = false) => new()
        {
            BorderStyle = BorderStyle.None,
            BackColor = InputColor,
            ForeColor = TextColor,
            Font = new Font("Segoe UI", 10F),
            PlaceholderText = placeholder,
            UseSystemPasswordChar = password,
            ShortcutsEnabled = true
        };

        private static Label MakeLabel(string text, Font font, Color color, Point location, Size size,
            ContentAlignment alignment) => new()
        {
            Text = text,
            Font = font,
            ForeColor = color,
            BackColor = Color.Transparent,
            Location = location,
            Size = size,
            TextAlign = alignment
        };

        private static RoundedPanel MakeInputRow(string caption, TextBox input, int top)
        {
            var row = new RoundedPanel
            {
                Location = new Point(61, top),
                Size = new Size(FieldW, 44),
                BackColor = InputColor,
                BorderColor = ColorTranslator.FromHtml("#263547"),
                BorderWidth = 1.5F,
                Radius = 18
            };
            var label = MakeLabel(caption, new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ColorTranslator.FromHtml("#E2E8F0"), new Point(14, 0), new Size(130, 44),
                ContentAlignment.MiddleLeft);
            input.Location = new Point(151, 11);
            input.Size = new Size(222, 23);
            input.Enter += (_, _) => { row.IsFocused = true; row.Invalidate(); };
            input.Leave += (_, _) => { row.IsFocused = false; row.Invalidate(); };
            row.Controls.Add(label);
            row.Controls.Add(input);
            return row;
        }

        private async void BtnRegister_Click(object? sender, EventArgs e)
        {
            lblError.Text = "";
            lblError.Visible = false;
            string user = txtUsername.Text.Trim();

            if (user.Length < 3) { ShowError("Tên đăng nhập tối thiểu 3 ký tự"); return; }
            if (txtPassword.Text.Length < 6) { ShowError("Mật khẩu tối thiểu 6 ký tự"); return; }
            if (txtPassword.Text != txtConfirm.Text) { ShowError("Mật khẩu nhập lại không khớp"); return; }

            btnRegister.Enabled = false;
            try
            {
                var (ok, error) = await AuthService.RegisterAsync(_endpoint, user, txtPassword.Text);
                if (ok)
                {
                    RegisteredUsername = user;
                    DialogResult = DialogResult.OK;
                }
                else ShowError(error);
            }
            catch (Exception ex) { ShowError("Lỗi: " + ex.Message); }
            finally { btnRegister.Enabled = true; }
        }

        private void ShowError(string text)
        {
            lblError.Text = text;
            lblError.Visible = true;
        }

        private void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        private void ApplyRoundedWindowRegion()
        {
            using var path = CreatePath(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 28);
            Region = new Region(path);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private sealed class RoundedPanel : Panel
        {
            public Color BorderColor = Color.Transparent;
            public float BorderWidth = 1F;
            public int Radius = 12;
            public bool IsFocused;

            public RoundedPanel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent?.BackColor ?? FormColor);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new RectangleF(BorderWidth / 2, BorderWidth / 2,
                    Width - BorderWidth, Height - BorderWidth);
                using var path = CreatePath(rect, Radius);
                using var brush = new SolidBrush(BackColor);
                using var pen = new Pen(IsFocused ? CyanColor : BorderColor, BorderWidth);
                e.Graphics.FillPath(brush, path);
                if (BorderColor != Color.Transparent || IsFocused) e.Graphics.DrawPath(pen, path);
                base.OnPaint(e);
            }
        }

        private sealed class RoundedButton : Button
        {
            public Color BorderColor = Color.Transparent;

            public RoundedButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
                using var path = CreatePath(rect, 14);
                var fillColor = Enabled ? (ClientRectangle.Contains(PointToClient(MousePosition))
                    ? ControlPaint.Light(BackColor, 0.08F) : BackColor) : Color.DimGray;
                using var brush = new SolidBrush(fillColor);
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

        private static GraphicsPath CreatePath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
