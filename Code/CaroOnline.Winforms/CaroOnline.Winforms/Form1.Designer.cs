namespace CaroOnline.Winforms
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
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
            // 
            // pnlChessBoard
            // 
            pnlChessBoard.BackColor = SystemColors.Control;
            pnlChessBoard.Location = new Point(9, 27);
            pnlChessBoard.Name = "pnlChessBoard";
            pnlChessBoard.Size = new Size(870, 700);
            pnlChessBoard.TabIndex = 0;
            pnlChessBoard.Paint += chessboard_Paint;
            // 
            // pctbAvatar
            // 
            pctbAvatar.Image = Properties.Resources.logo;
            pctbAvatar.Location = new Point(885, 26);
            pctbAvatar.Name = "pctbAvatar";
            pctbAvatar.Size = new Size(223, 223);
            pctbAvatar.SizeMode = PictureBoxSizeMode.StretchImage;
            pctbAvatar.TabIndex = 1;
            pctbAvatar.TabStop = false;
            // 
            // information
            // 
            information.BackColor = SystemColors.Control;
            information.Controls.Add(rule);
            information.Controls.Add(Lan);
            information.Controls.Add(pctbMark);
            information.Controls.Add(txbIP);
            information.Controls.Add(txbPlayerName);
            information.Controls.Add(prcbCoolDown);
            information.Location = new Point(885, 255);
            information.Name = "information";
            information.Size = new Size(223, 471);
            information.TabIndex = 2;
            // 
            // rule
            // 
            rule.AutoSize = true;
            rule.Font = new Font("Times New Roman", 13.8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            rule.Location = new Point(38, 344);
            rule.Name = "rule";
            rule.Size = new Size(141, 26);
            rule.TabIndex = 5;
            rule.Text = "5 point in line";
            rule.Click += label1_Click;
            // 
            // Lan
            // 
            Lan.Location = new Point(53, 263);
            Lan.Name = "Lan";
            Lan.Size = new Size(112, 37);
            Lan.TabIndex = 4;
            Lan.Text = "Rời phòng / Chọn phòng";
            Lan.UseVisualStyleBackColor = true;
            Lan.Click += button1_Click;
            // 
            // pctbMark
            // 
            pctbMark.BackColor = SystemColors.ActiveCaptionText;
            pctbMark.Location = new Point(73, 15);
            pctbMark.Name = "pctbMark";
            pctbMark.Size = new Size(80, 80);
            pctbMark.TabIndex = 3;
            pctbMark.TabStop = false;
            // 
            // txbIP
            // 
            txbIP.Location = new Point(15, 217);
            txbIP.Name = "txbIP";
            txbIP.Size = new Size(191, 27);
            txbIP.TabIndex = 2;
            // 
            // txbPlayerName
            // 
            txbPlayerName.Location = new Point(15, 165);
            txbPlayerName.Name = "txbPlayerName";
            txbPlayerName.ReadOnly = true;
            txbPlayerName.Size = new Size(191, 27);
            txbPlayerName.TabIndex = 1;
            // 
            // prcbCoolDown
            // 
            prcbCoolDown.Location = new Point(15, 111);
            prcbCoolDown.Name = "prcbCoolDown";
            prcbCoolDown.Size = new Size(191, 27);
            prcbCoolDown.TabIndex = 0;
            prcbCoolDown.Click += progressBar1_Click;
            // 
            // tmCoolDown
            // 
            tmCoolDown.Tick += timer1_Tick;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { menuToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1122, 28);
            menuStrip1.TabIndex = 3;
            menuStrip1.Text = "menuStrip1";
            // 
            // menuToolStripMenuItem
            // 
            menuToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newGameToolStripMenuItem, undoToolStripMenuItem, quitToolStripMenuItem });
            menuToolStripMenuItem.Name = "menuToolStripMenuItem";
            menuToolStripMenuItem.Size = new Size(60, 24);
            menuToolStripMenuItem.Text = "Menu";
            // 
            // newGameToolStripMenuItem
            // 
            newGameToolStripMenuItem.Name = "newGameToolStripMenuItem";
            newGameToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newGameToolStripMenuItem.Size = new Size(217, 26);
            newGameToolStripMenuItem.Text = "New game";
            newGameToolStripMenuItem.Click += newGameToolStripMenuItem_Click;
            // 
            // undoToolStripMenuItem
            // 
            undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            undoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
            undoToolStripMenuItem.Size = new Size(217, 26);
            undoToolStripMenuItem.Text = "Undo";
            undoToolStripMenuItem.Click += undoToolStripMenuItem_Click;
            // 
            // quitToolStripMenuItem
            // 
            quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            quitToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Q;
            quitToolStripMenuItem.Size = new Size(217, 26);
            quitToolStripMenuItem.Text = "Quit";
            quitToolStripMenuItem.Click += quitToolStripMenuItem_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1280, 900);
            MinimumSize = new Size(960, 680);
            StartPosition = FormStartPosition.CenterScreen;
            Controls.Add(information);
            Controls.Add(pctbAvatar);
            Controls.Add(pnlChessBoard);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "CaroOnline";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)pctbAvatar).EndInit();
            information.ResumeLayout(false);
            information.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pctbMark).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

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
