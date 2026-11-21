using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CaroOnline.Winforms
{
    public sealed class FormMatchHistory : Form
    {
        private static readonly Color Surface = Color.FromArgb(17, 28, 42);
        private static readonly Color Card = Color.FromArgb(22, 34, 50);
        private static readonly Color White = Color.FromArgb(238, 245, 252);
        private static readonly Color Muted = Color.FromArgb(138, 154, 169);
        private static readonly Color Cyan = ColorTranslator.FromHtml("#2CE6FF");
        private static readonly Color Coral = ColorTranslator.FromHtml("#FF4D4D");
        private static readonly Color Teal = Color.FromArgb(52, 211, 153);

        private readonly string playerName;
        private readonly FlowLayoutPanel historyList;
        private readonly Label statusLabel;
        private readonly Label resultCountLabel;
        private readonly Label totalValue;
        private readonly Label winsValue;
        private readonly Label lossesValue;
        private readonly Label rateValue;

        public FormMatchHistory(string playerName)
        {
            this.playerName = playerName ?? string.Empty;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Surface;
            ForeColor = White;
            ClientSize = new Size(680, 480);
            MinimumSize = Size;
            MaximumSize = Size;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "Lịch sử đấu";
            Resize += (_, _) => UpdateRoundedRegion();
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    Close();
                }
            };

            var title = MakeLabel("LỊCH SỬ ĐẤU", new Font("Segoe UI", 17F, FontStyle.Bold), White,
                new Point(24, 20), new Size(420, 32), ContentAlignment.MiddleLeft);
            var subtitle = MakeLabel("Nhìn lại từng nước đi. Sẵn sàng cho trận tiếp theo.",
                new Font("Segoe UI", 9F), Muted, new Point(26, 53), new Size(530, 22), ContentAlignment.MiddleLeft);
            var closeButton = new Button
            {
                Text = "✕",
                Location = new Point(622, 18),
                Size = new Size(34, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(25, 39, 56),
                ForeColor = Muted,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(117, 38, 52);
            closeButton.MouseEnter += (_, _) => closeButton.ForeColor = Color.White;
            closeButton.MouseLeave += (_, _) => closeButton.ForeColor = Muted;
            closeButton.Click += (_, _) => Close();
            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(closeButton);
            Controls.Add(new Panel
            {
                Location = new Point(24, 82),
                Size = new Size(632, 1),
                BackColor = Color.FromArgb(39, 54, 72)
            });

            totalValue = AddStat("Tổng trận: 0", new Point(24, 96), 140, Color.FromArgb(37, 52, 69), Muted);
            winsValue = AddStat("Thắng: 0", new Point(172, 96), 140, Color.FromArgb(14, 53, 65), Cyan);
            lossesValue = AddStat("Thua: 0", new Point(320, 96), 140, Color.FromArgb(64, 34, 45), Coral);
            rateValue = AddStat("Tỷ lệ thắng: 0%", new Point(468, 96), 188, Color.FromArgb(20, 54, 51), Teal);

            Controls.Add(MakeLabel("TRẬN GẦN ĐÂY", new Font("Segoe UI", 8.5F, FontStyle.Bold), Muted,
                new Point(25, 154), new Size(200, 22), ContentAlignment.MiddleLeft));
            resultCountLabel = MakeLabel("0 trận", new Font("Segoe UI", 8.5F), Muted,
                new Point(500, 154), new Size(155, 22), ContentAlignment.MiddleRight);
            Controls.Add(resultCountLabel);

            historyList = new FlowLayoutPanel
            {
                Location = new Point(24, 180),
                Size = new Size(632, 274),
                AutoScroll = true,
                BackColor = Surface,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Visible = false,
                Padding = new Padding(0)
            };
            Controls.Add(historyList);
            statusLabel = MakeLabel("Đang tải lịch sử trận đấu...", new Font("Segoe UI", 10F), Muted,
                new Point(24, 180), new Size(632, 274), ContentAlignment.MiddleCenter);
            Controls.Add(statusLabel);
            UpdateRoundedRegion();
        }

        public void SetLoading(string text = "Đang tải lịch sử trận đấu...")
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = Muted;
            statusLabel.Visible = true;
            historyList.Visible = false;
        }

        public void SetError(string text)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = Coral;
            statusLabel.Visible = true;
            historyList.Visible = false;
        }

        public void SetHistory(IEnumerable<HistoryItem> items)
        {
            List<HistoryItem> records = items.OrderByDescending(item => item.FinishedAtUtc).ToList();
            int wins = records.Count(item => GetOutcome(item) > 0);
            int losses = records.Count(item => GetOutcome(item) < 0);
            double winRate = records.Count == 0 ? 0 : wins * 100.0 / records.Count;

            totalValue.Text = $"Tổng trận: {records.Count}";
            winsValue.Text = $"Thắng: {wins}";
            lossesValue.Text = $"Thua: {losses}";
            rateValue.Text = $"Tỷ lệ thắng: {winRate:0.#}%";
            resultCountLabel.Text = $"{records.Count} trận";

            historyList.SuspendLayout();
            historyList.Controls.Clear();
            foreach (HistoryItem record in records)
                historyList.Controls.Add(CreateHistoryRow(record));
            historyList.ResumeLayout();

            historyList.Visible = records.Count > 0;
            statusLabel.Text = records.Count == 0 ? "Chưa có lịch sử trận đấu." : string.Empty;
            statusLabel.ForeColor = Muted;
            statusLabel.Visible = records.Count == 0;
        }

        private Label AddStat(string text, Point location, int width, Color backColor, Color accent)
        {
            var badge = new RoundedPanel
            {
                Location = location,
                Size = new Size(width, 46),
                Radius = 10,
                BackColor = backColor
            };
            var label = MakeLabel(text, new Font("Segoe UI", 9F, FontStyle.Bold), accent,
                new Point(8, 0), new Size(width - 16, 46), ContentAlignment.MiddleCenter, backColor);
            badge.Controls.Add(label);
            Controls.Add(badge);
            return label;
        }

        private Control CreateHistoryRow(HistoryItem record)
        {
            int outcome = GetOutcome(record);
            string result = outcome > 0 ? "THẮNG" : outcome < 0 ? "THUA" : "HÒA";
            Color accent = outcome > 0 ? Cyan : outcome < 0 ? Coral : Teal;
            bool isPlayerOne = string.Equals(record.PlayerOne, playerName, StringComparison.OrdinalIgnoreCase);
            string mark = isPlayerOne ? "X" : "O";
            string opponent = isPlayerOne ? record.PlayerTwo : record.PlayerOne;
            var row = new RoundedPanel
            {
                Size = new Size(608, 70),
                Radius = 10,
                BackColor = Card,
                Margin = new Padding(0, 0, 0, 8)
            };

            var resultBadge = MakeLabel(result, new Font("Segoe UI", 7.5F, FontStyle.Bold), accent,
                new Point(10, 22), new Size(60, 25), ContentAlignment.MiddleCenter, Color.FromArgb(31, 47, 63));
            var markLabel = MakeLabel(mark, new Font("Segoe UI", 12F, FontStyle.Bold), isPlayerOne ? Cyan : Coral,
                new Point(78, 17), new Size(24, 30), ContentAlignment.MiddleCenter, Card);
            var opponentLabel = MakeLabel($"vs. {opponent}", new Font("Segoe UI", 9F, FontStyle.Bold), White,
                new Point(108, 11), new Size(350, 22), ContentAlignment.MiddleLeft, Card);
            opponentLabel.AutoEllipsis = true;
            var metadata = MakeLabel(GetMetadata(record), new Font("Segoe UI", 7.5F), Muted,
                new Point(108, 34), new Size(398, 23), ContentAlignment.MiddleLeft, Card);
            metadata.AutoEllipsis = true;

            var detailButton = new Button
            {
                Text = "Chi tiết",
                Location = new Point(526, 19),
                Size = new Size(70, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(26, 42, 59),
                ForeColor = Color.FromArgb(203, 218, 231),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            detailButton.FlatAppearance.BorderColor = Color.FromArgb(55, 75, 96);
            detailButton.FlatAppearance.BorderSize = 1;
            detailButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(39, 66, 88);
            using (GraphicsPath path = CreateRoundedPath(new RectangleF(0, 0,
                detailButton.Width, detailButton.Height), 8F))
            {
                detailButton.Region = new Region(path);
            }
            detailButton.Click += (_, _) => ShowDetails(record, result, mark, opponent, outcome);

            row.Controls.Add(resultBadge);
            row.Controls.Add(markLabel);
            row.Controls.Add(opponentLabel);
            row.Controls.Add(metadata);
            row.Controls.Add(detailButton);
            return row;
        }

        private int GetOutcome(HistoryItem item)
        {
            if (string.Equals(item.Result, "DRAW", StringComparison.OrdinalIgnoreCase))
                return 0;

            bool playerOneWon = item.Result.StartsWith("PLAYER_ONE_WIN", StringComparison.OrdinalIgnoreCase);
            bool playerTwoWon = item.Result.StartsWith("PLAYER_TWO_WIN", StringComparison.OrdinalIgnoreCase);
            if (!playerOneWon && !playerTwoWon)
                return 0;

            bool isPlayerOne = string.Equals(item.PlayerOne, playerName, StringComparison.OrdinalIgnoreCase);
            return isPlayerOne == playerOneWon ? 1 : -1;
        }

        private static string GetMetadata(HistoryItem item)
        {
            TimeSpan duration = item.FinishedAtUtc - item.StartedAtUtc;
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;
            string elapsed = duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}"
                : $"{(int)duration.TotalMinutes}:{duration.Seconds:D2}";
            string date = item.FinishedAtUtc.ToLocalTime().ToString("dd/MM HH:mm");
            return $"Phòng {item.RoomId} · {item.TotalMoves} nước · {elapsed} · {date}";
        }

        private void ShowDetails(HistoryItem item, string result, string mark, string opponent, int outcome)
        {
            string outcomeText = outcome > 0 ? "Thắng" : outcome < 0 ? "Thua" : "Hòa";
            string duration = GetMetadata(item);
            MessageBox.Show(this,
                $"Kết quả: {outcomeText}\nĐối thủ: {opponent}\nQuân cờ: {mark}\n{duration}",
                $"Chi tiết trận {item.RoomId}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateRoundedRegion()
        {
            if (Width <= 0 || Height <= 0)
                return;
            using GraphicsPath path = CreateRoundedPath(new RectangleF(0, 0, Width, Height), 16F);
            Region = new Region(path);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = CreateRoundedPath(new RectangleF(1, 1, Width - 2, Height - 2), 16F);
            using var pen = new Pen(Color.FromArgb(48, 77, 99), 1F);
            e.Graphics.DrawPath(pen, path);
            using var accentPen = new Pen(Color.FromArgb(90, Cyan), 2F);
            e.Graphics.DrawLine(accentPen, 24, 82, 86, 82);
        }

        private static Label MakeLabel(string text, Font font, Color color, Point location, Size size,
            ContentAlignment alignment, Color? backColor = null) => new()
            {
                AutoSize = false,
                Text = text,
                Font = font,
                ForeColor = color,
                BackColor = backColor ?? Color.Transparent,
                Location = location,
                Size = size,
                TextAlign = alignment
            };

        private static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
        {
            float diameter = Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class RoundedPanel : Panel
        {
            public float Radius = 10F;

            public RoundedPanel()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Resize += (_, _) => UpdateRegion();
            }

            protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(BackColor);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using GraphicsPath path = CreateRoundedPath(new RectangleF(0, 0, Width - 1, Height - 1), Radius);
                using var brush = new SolidBrush(BackColor);
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(30, 48, 66), 1F);
                e.Graphics.DrawPath(pen, path);
                base.OnPaint(e);
            }

            private void UpdateRegion()
            {
                if (Width <= 0 || Height <= 0)
                    return;
                using GraphicsPath path = CreateRoundedPath(new RectangleF(0, 0, Width, Height), Radius);
                Region = new Region(path);
            }
        }
    }
}
