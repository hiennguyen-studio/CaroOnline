namespace CaroOnline.Winforms
{
    partial class FormInMatch
    {
        private System.ComponentModel.IContainer components = null;
        private Panel pnlChessBoard;
        private TextBox txbPlayerName;
        private PictureBox pctbMark;
        private ProgressBar prcbCoolDown;
        private System.Windows.Forms.Timer tmCoolDown;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem menuToolStripMenuItem;
        private ToolStripMenuItem newGameToolStripMenuItem;
        private ToolStripMenuItem undoToolStripMenuItem;
        private ToolStripMenuItem quitToolStripMenuItem;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            pnlChessBoard = new Panel();
            txbPlayerName = new TextBox();
            pctbMark = new PictureBox();
            prcbCoolDown = new ProgressBar();
            tmCoolDown = new System.Windows.Forms.Timer(components);
            menuStrip1 = new MenuStrip();
            menuToolStripMenuItem = new ToolStripMenuItem();
            newGameToolStripMenuItem = new ToolStripMenuItem();
            undoToolStripMenuItem = new ToolStripMenuItem();
            quitToolStripMenuItem = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)pctbMark).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();

            pnlChessBoard.BackColor = ColorTranslator.FromHtml("#131D2B");
            pnlChessBoard.Location = new Point(242, 240);
            pnlChessBoard.Name = "pnlChessBoard";
            pnlChessBoard.Size = new Size(450, 450);
            pnlChessBoard.TabIndex = 0;

            txbPlayerName.Location = new Point(-200, -200);
            txbPlayerName.Name = "txbPlayerName";
            txbPlayerName.ReadOnly = true;
            txbPlayerName.Size = new Size(1, 1);
            txbPlayerName.TabIndex = 1;
            txbPlayerName.Visible = false;

            pctbMark.Location = new Point(-200, -200);
            pctbMark.Name = "pctbMark";
            pctbMark.Size = new Size(1, 1);
            pctbMark.SizeMode = PictureBoxSizeMode.StretchImage;
            pctbMark.TabStop = false;
            pctbMark.Visible = false;

            prcbCoolDown.Location = new Point(-200, -200);
            prcbCoolDown.Name = "prcbCoolDown";
            prcbCoolDown.Size = new Size(1, 1);
            prcbCoolDown.TabIndex = 2;
            prcbCoolDown.Visible = false;

            tmCoolDown.Tick += timer1_Tick;

            menuStrip1.Items.AddRange(new ToolStripItem[] { menuToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1100, 24);
            menuStrip1.Visible = false;

            menuToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                newGameToolStripMenuItem,
                undoToolStripMenuItem,
                quitToolStripMenuItem
            });
            menuToolStripMenuItem.Name = "menuToolStripMenuItem";
            menuToolStripMenuItem.Text = "Menu";

            newGameToolStripMenuItem.Name = "newGameToolStripMenuItem";
            newGameToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newGameToolStripMenuItem.Text = "New game";
            newGameToolStripMenuItem.Click += newGameToolStripMenuItem_Click;

            undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            undoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
            undoToolStripMenuItem.Text = "Undo";
            undoToolStripMenuItem.Click += undoToolStripMenuItem_Click;

            quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            quitToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Q;
            quitToolStripMenuItem.Text = "Quit";
            quitToolStripMenuItem.Click += quitToolStripMenuItem_Click;

            AutoScaleMode = AutoScaleMode.None;
            BackColor = ColorTranslator.FromHtml("#080D14");
            ClientSize = new Size(1100, 860);
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormInMatch";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Caro Online - Phòng 01";
            MainMenuStrip = menuStrip1;
            FormClosing += Form1_FormClosing;

            Controls.Add(pnlChessBoard);
            Controls.Add(txbPlayerName);
            Controls.Add(pctbMark);
            Controls.Add(prcbCoolDown);
            Controls.Add(menuStrip1);

            ((System.ComponentModel.ISupportInitialize)pctbMark).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
