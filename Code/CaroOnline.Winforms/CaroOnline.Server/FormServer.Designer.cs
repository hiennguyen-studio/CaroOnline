using System;
using System.Drawing;
using System.Windows.Forms;

namespace CaroOnline.Server
{
    partial class FormServer
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            button1 = new Button();
            button2 = new Button();
            label1 = new Label();
            label2 = new Label();
            lblStatus = new Label();
            listBox1 = new ListBox();
            richTextBox1 = new RichTextBox();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(20, 78);
            button1.Margin = new Padding(2);
            button1.Name = "button1";
            button1.Size = new Size(130, 32);
            button1.TabIndex = 1;
            button1.Text = "START SERVER";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Location = new Point(170, 78);
            button2.Margin = new Padding(2);
            button2.Name = "button2";
            button2.Size = new Size(130, 32);
            button2.TabIndex = 2;
            button2.Text = "STOP SERVER";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(20, 12);
            label1.Margin = new Padding(2, 0, 2, 0);
            label1.Name = "label1";
            label1.Size = new Size(81, 20);
            label1.TabIndex = 3;
            label1.Text = "IP Address:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(20, 125);
            label2.Margin = new Padding(2, 0, 2, 0);
            label2.Name = "label2";
            label2.Size = new Size(119, 20);
            label2.TabIndex = 4;
            label2.Text = "Danh sách Client";
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(20, 42);
            lblStatus.Margin = new Padding(2, 0, 2, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(127, 20);
            lblStatus.TabIndex = 5;
            lblStatus.Text = "Server đang dừng";
            // 
            // listBox1
            // 
            listBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            listBox1.FormattingEnabled = true;
            listBox1.HorizontalScrollbar = true;
            listBox1.Location = new Point(20, 150);
            listBox1.Margin = new Padding(2);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(480, 304);
            listBox1.TabIndex = 6;
            // 
            // richTextBox1
            // 
            richTextBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            richTextBox1.Location = new Point(520, 150);
            richTextBox1.Margin = new Padding(2);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(496, 304);
            richTextBox1.TabIndex = 7;
            richTextBox1.Text = "";
            // 
            // FormServer
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1036, 473);
            Controls.Add(richTextBox1);
            Controls.Add(listBox1);
            Controls.Add(lblStatus);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(button2);
            Controls.Add(button1);
            Margin = new Padding(2);
            Name = "FormServer";
            Text = "Caro Online Server - 127.0.0.1";
            FormClosing += FormServer_FormClosing;
            ResumeLayout(false);
            PerformLayout();
        }

        private Button button1;
        private Button button2;
        private Label label1;
        private Label label2;
        private Label lblStatus;
        private ListBox listBox1;
        private RichTextBox richTextBox1;
    }
}