using GameCaro;

namespace CaroOnline.Winforms
{
    public partial class Form1 : Form
    {
        #region Properties
        ChessBoardManager ChessBoard;
        public Form1()
        {
            InitializeComponent();
            ChessBoard = new ChessBoardManager(pnlChessBoard);
            ChessBoard.DrawChessBoard();
        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void chessboard_Paint(object sender, PaintEventArgs e)
        {

        }
        #endregion
    }
}
