using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Player = CaroOnline.Winforms.Player;

namespace GameCaro
{
    public class ChessBoardManager
    {

        #region Properties
        private Panel chessBoard;

        public Panel ChessBoard
        {
            get { return chessBoard; }
            set { chessBoard = value; }
        }

        private List<Player> player;

        public List<Player> Player
        {
            get { return player; }
            set { player = value; }
        }

        private int currentPlayer;

        public int CurrentPlayer
        {
            get { return currentPlayer; }
            set { currentPlayer = value; }
        }

        private TextBox playerName;

        public TextBox PlayerName
        {
            get { return playerName; }
            set { playerName = value; }
        }

        private PictureBox markPlayer;

        public PictureBox MarkPlayer
        {
            get { return markPlayer; }
            set { markPlayer = value; }
        }

        #endregion

        #region Initialize
        public ChessBoardManager(Panel chessBoard, TextBox playerName, PictureBox markPlayer)
        {
            this.ChessBoard = chessBoard;
            this.PlayerName = playerName;
            this.MarkPlayer = markPlayer;
            this.Player = new List<Player>()
            {
                new Player("Player 1", Image.FromFile("012012301303-NET_262701303_10-UDM_16\\Code\\CaroOnline.Winforms\\CaroOnline.Winforms\\Resources\\iconX.png")),
                new Player("Player 2", Image.FromFile("012012301303-NET_262701303_10-UDM_16\\Code\\CaroOnline.Winforms\\CaroOnline.Winforms\\Resources\\iconO.png"))
            };
            currentPlayer=0;

            ChangePlayer();
        }
        #endregion

        #region Methods
        public void DrawChessBoard()
        {
            Button oldButton = new Button() { Width = 0, Location = new Point(0, 0) };
            for (int i = 0; i < Cons.CHESS_BOARD_HEIGHT; i++)
            {
                for (int j = 0; j < Cons.CHESS_BOARD_WIDTH; j++)
                {
                    Button btn = new Button()
                    {
                        Width = Cons.CHESS_WIDTH,
                        Height = Cons.CHESS_HEIGHT,
                        Location = new Point(oldButton.Location.X + oldButton.Width, oldButton.Location.Y),
                        BackgroundImageLayout = ImageLayout.Stretch
                    };

                    btn.Click += btn_Click;

                    chessBoard.Controls.Add(btn);

                    oldButton = btn;
                }
                oldButton.Location = new Point(0, oldButton.Location.Y + Cons.CHESS_HEIGHT);
                oldButton.Width = 0;
                oldButton.Height = 0;
            }
        }

        private void btn_Click(object? sender, EventArgs e)
        {
            Button btn = sender as Button;

            if(btn.BackgroundImage != null)
                return;
            
            Mark(btn);
            ChangePlayer();

        }

        private void Mark(Button btn)
        {
            btn.BackgroundImage = Player[currentPlayer].Mark;

            currentPlayer = (currentPlayer == 0) ? 1 : 0;
        }

        private void ChangePlayer()
        {
            PlayerName.Text = Player[currentPlayer].Name;
            MarkPlayer.Image = Player[currentPlayer].Mark;
        }

        #endregion
    }
}
