namespace CaroOnline.Winforms
{
    partial class FormLobby
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            pnlChessBoard = new Panel();
            pctbAvatar = new PictureBox();
            information = new Panel();
            rule = new Label();
            Lan = new Button();
            pctbMark = new PictureBox();
            txbIP = new TextBox();
            txbPlayerName = new TextBox();
            prcbCoolDown = new ProgressBar();
            tmCoolDown = new System.Windows.Forms.Timer(components);
            menuStrip1 = new MenuStrip();
            menuToolStripMenuItem = new ToolStripMenuItem();
            newGameToolStripMenuItem = new ToolStripMenuItem();
            undoToolStripMenuItem = new ToolStripMenuItem();
            quitToolStripMenuItem = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)pctbAvatar).BeginInit();
            information.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pctbMark).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();

            pnlChessBoard.BackColor = ColorTranslator.FromHtml("#0E1622");
            pnlChessBoard.Location = new Point(0, 0);
            pnlChessBoard.Name = "pnlChessBoard";
            pnlChessBoard.Size = new Size(870, 700);
            pnlChessBoard.Visible = false;
            pnlChessBoard.Paint += chessboard_Paint;

            pctbAvatar.Image = Properties.Resources.logo;
            pctbAvatar.Location = new Point(0, 0);
            pctbAvatar.Name = "pctbAvatar";
            pctbAvatar.Size = new Size(180, 180);
            pctbAvatar.SizeMode = PictureBoxSizeMode.Zoom;
            pctbAvatar.TabStop = false;
            pctbAvatar.Visible = false;

            information.BackColor = ColorTranslator.FromHtml("#101926");
            information.Controls.Add(rule);
            information.Controls.Add(Lan);
            information.Controls.Add(pctbMark);
            information.Controls.Add(txbIP);
            information.Controls.Add(txbPlayerName);
            information.Controls.Add(prcbCoolDown);
            information.Location = new Point(0, 0);
            information.Name = "information";
            information.Size = new Size(223, 471);
            information.Visible = false;

            rule.AutoSize = true;
            rule.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            rule.Location = new Point(15, 344);
            rule.Name = "rule";
            rule.Size = new Size(160, 23);
            rule.TabIndex = 5;
            rule.Text = "Nối 5 quân liên tiếp để thắng";
            rule.Click += label1_Click;

            Lan.Location = new Point(15, 263);
            Lan.Name = "Lan";
            Lan.Size = new Size(193, 37);
            Lan.TabIndex = 4;
            Lan.Text = "Rời phòng / Chọn phòng";
            Lan.UseVisualStyleBackColor = true;
            Lan.Click += button1_Click;

            pctbMark.BackColor = Color.Black;
            pctbMark.Location = new Point(73, 15);
            pctbMark.Name = "pctbMark";
            pctbMark.Size = new Size(80, 80);
            pctbMark.TabStop = false;

            txbIP.Location = new Point(15, 217);
            txbIP.Name = "txbIP";
            txbIP.Size = new Size(191, 27);
            txbIP.TabIndex = 2;

            txbPlayerName.Location = new Point(15, 165);
            txbPlayerName.Name = "txbPlayerName";
            txbPlayerName.Size = new Size(191, 27);
            txbPlayerName.TabIndex = 1;

            prcbCoolDown.Location = new Point(15, 111);
            prcbCoolDown.Name = "prcbCoolDown";
            prcbCoolDown.Size = new Size(191, 27);
            prcbCoolDown.TabIndex = 0;
            prcbCoolDown.Click += progressBar1_Click;

            tmCoolDown.Tick += timer1_Tick;

            menuStrip1.Items.AddRange(new ToolStripItem[] { menuToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1120, 28);
            menuStrip1.TabIndex = 3;
            menuStrip1.Visible = false;

            menuToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                newGameToolStripMenuItem,
                undoToolStripMenuItem,
                quitToolStripMenuItem
            });
            menuToolStripMenuItem.Name = "menuToolStripMenuItem";
            menuToolStripMenuItem.Size = new Size(60, 24);
            menuToolStripMenuItem.Text = "Menu";

            newGameToolStripMenuItem.Name = "newGameToolStripMenuItem";
            newGameToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newGameToolStripMenuItem.Size = new Size(217, 26);
            newGameToolStripMenuItem.Text = "New game";
            newGameToolStripMenuItem.Click += newGameToolStripMenuItem_Click;

            undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            undoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
            undoToolStripMenuItem.Size = new Size(217, 26);
            undoToolStripMenuItem.Text = "Undo";
            undoToolStripMenuItem.Click += undoToolStripMenuItem_Click;

            quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            quitToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Q;
            quitToolStripMenuItem.Size = new Size(217, 26);
            quitToolStripMenuItem.Text = "Quit";
            quitToolStripMenuItem.Click += quitToolStripMenuItem_Click;

            AutoScaleMode = AutoScaleMode.None;
            BackColor = ColorTranslator.FromHtml("#080D14");
            ClientSize = new Size(1120, 760);
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormLobby";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Caro Online - Sảnh chờ";
            MainMenuStrip = menuStrip1;
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;

            Controls.Add(information);
            Controls.Add(pctbAvatar);
            Controls.Add(pnlChessBoard);
            Controls.Add(menuStrip1);

            ((System.ComponentModel.ISupportInitialize)pctbAvatar).EndInit();
            information.ResumeLayout(false);
            information.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pctbMark).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private Panel pnlChessBoard;
        private PictureBox pctbAvatar;
        private Panel information;
        private ProgressBar prcbCoolDown;
        private TextBox txbIP;
        private TextBox txbPlayerName;
        private PictureBox pctbMark;
        private Button Lan;
        private Label rule;
        private System.Windows.Forms.Timer tmCoolDown;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem menuToolStripMenuItem;
        private ToolStripMenuItem undoToolStripMenuItem;
        private ToolStripMenuItem quitToolStripMenuItem;
        private ToolStripMenuItem newGameToolStripMenuItem;
    }
}
