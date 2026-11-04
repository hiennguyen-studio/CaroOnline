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
            ChessBoard = new ChessBoardManager(pnlChessBoard, txbPlayerName, pctbMark);
            ChessBoard.EndeGame += ChessBoard_EndeGame;
            ChessBoard.PlayerMarked += ChessBoard_PlayerMarked;

            prcbCoolDown.Step = Cons.COOL_DOWN_STEP;
            prcbCoolDown.Maximum = Cons.COOL_DOWN_TIME;
            prcbCoolDown.Value = 0;

            tmCoolDown.Interval = Cons.COOL_DOWN_INTERVAL;

            ChessBoard.DrawChessBoard();

        } 

        void EndGame()
        {
            tmCoolDown.Stop();

            pnlChessBoard.Enabled = false;
            MessageBox.Show("kết thúc");
        }
        private void progressBar1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            EndGame();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void chessboard_Paint(object sender, PaintEventArgs e)
        {

        }
        #endregion

        private void ChessBoard_EndeGame(object sender, EventArgs e)
        {
            EndGame();
        }

        private void ChessBoard_PlayerMarked (object sender, EventArgs e)
        {
            tmCoolDown.Start();
            prcbCoolDown.Value = 0;
        }
        private void timer1_Tick(object sender, EventArgs e)
        {

            prcbCoolDown.PerformStep(); 
            if(prcbCoolDown.Value>=prcbCoolDown.Maximum)
            {
                EndGame();
                
            }
        }
    }
}
