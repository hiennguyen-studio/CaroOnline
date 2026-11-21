using System.Drawing;
using System.Windows.Forms;

namespace CaroOnline.Winforms
{
    public sealed class FormOverlay : Form
    {
        public FormOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Opacity = 0.65;
            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Normal;
            ShowIcon = false;
        }
    }
}
