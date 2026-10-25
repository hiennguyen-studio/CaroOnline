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
            chessboard = new Panel();
            logo = new PictureBox();
            information = new Panel();
            CoolDown = new ProgressBar();
            PlayerName = new TextBox();
            IPaddres = new TextBox();
            icon = new PictureBox();
            button1 = new Button();
            rule = new Label();
            ((System.ComponentModel.ISupportInitialize)logo).BeginInit();
            information.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)icon).BeginInit();
            SuspendLayout();
            // 
            // chessboard
            // 
            chessboard.BackColor = SystemColors.ActiveCaption;
            chessboard.Location = new Point(12, 12);
            chessboard.Name = "chessboard";
            chessboard.Size = new Size(868, 692);
            chessboard.TabIndex = 0;
            // 
            // logo
            // 
            logo.Image = Properties.Resources.logo;
            logo.Location = new Point(886, 12);
            logo.Name = "logo";
            logo.Size = new Size(223, 223);
            logo.SizeMode = PictureBoxSizeMode.StretchImage;
            logo.TabIndex = 1;
            logo.TabStop = false;
            // 
            // information
            // 
            information.BackColor = SystemColors.ActiveCaption;
            information.Controls.Add(rule);
            information.Controls.Add(button1);
            information.Controls.Add(icon);
            information.Controls.Add(IPaddres);
            information.Controls.Add(PlayerName);
            information.Controls.Add(CoolDown);
            information.Location = new Point(886, 241);
            information.Name = "information";
            information.Size = new Size(223, 463);
            information.TabIndex = 2;
            // 
            // CoolDown
            // 
            CoolDown.Location = new Point(15, 111);
            CoolDown.Name = "CoolDown";
            CoolDown.Size = new Size(191, 27);
            CoolDown.TabIndex = 0;
            CoolDown.Click += progressBar1_Click;
            // 
            // PlayerName
            // 
            PlayerName.Location = new Point(15, 165);
            PlayerName.Name = "PlayerName";
            PlayerName.ReadOnly = true;
            PlayerName.Size = new Size(191, 27);
            PlayerName.TabIndex = 1;
            // 
            // IPaddres
            // 
            IPaddres.Location = new Point(15, 217);
            IPaddres.Name = "IPaddres";
            IPaddres.Size = new Size(191, 27);
            IPaddres.TabIndex = 2;
            // 
            // icon
            // 
            icon.BackColor = SystemColors.ActiveCaptionText;
            icon.Location = new Point(73, 15);
            icon.Name = "icon";
            icon.Size = new Size(80, 80);
            icon.TabIndex = 3;
            icon.TabStop = false;
            // 
            // button1
            // 
            button1.Location = new Point(53, 263);
            button1.Name = "button1";
            button1.Size = new Size(112, 37);
            button1.TabIndex = 4;
            button1.Text = "button1";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
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
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1121, 716);
            Controls.Add(information);
            Controls.Add(logo);
            Controls.Add(chessboard);
            Name = "Form1";
            Text = "CaroOnline";
            ((System.ComponentModel.ISupportInitialize)logo).EndInit();
            information.ResumeLayout(false);
            information.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)icon).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel chessboard;
        private PictureBox logo;
        private Panel information;
        private ProgressBar CoolDown;
        private TextBox IPaddres;
        private TextBox PlayerName;
        private PictureBox icon;
        private Button button1;
        private Label rule;
    }
}
