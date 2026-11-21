using System;
using System.Threading.Tasks;

namespace CaroOnline.Server
{
    // Cổng vào của Game Server dành cho FormServer.
    // Toàn bộ logic nhiều phòng nằm trong RoomServerManager.
    public class ServerSocketManager
    {
        private readonly RoomServerManager roomManager;

        // ==========================================
        // CÁC EVENT GỬI THÔNG TIN VỀ FORMSERVER
        // ==========================================

        public event Action<string>? LogReceived;

        // Số kết nối đang mở
        public event Action<int>? ClientCountChanged;

        // Danh sách Client kèm vị trí (sảnh / phòng nào)
        public event Action<string[]>? ClientListChanged;

        // Số phòng đang chạy
        public event Action<int>? RoomCountChanged;

        // ==========================================
        // THÔNG TIN SERVER
        // ==========================================

        public bool IsRunning => roomManager.IsRunning;

        public int Port => roomManager.Port;

        public int MaxConnections => roomManager.MaxConnections;

        public int MaxRooms => roomManager.MaxRooms;
        public Database? Database
        {
            get => roomManager.Database;
            set => roomManager.Database = value;
        }

        public int RoomCount => roomManager.RoomCount;

        public int ConnectedClientCount => roomManager.ConnectedClientCount;

        // ==========================================
        // KHỞI TẠO
        // ==========================================

        public ServerSocketManager(ServerSettings? settings = null)
        {
            roomManager = new RoomServerManager(settings ?? ServerSettings.Load());

            roomManager.LogReceived += message => LogReceived?.Invoke(message);

            roomManager.ClientCountChanged += count => ClientCountChanged?.Invoke(count);

            roomManager.ClientListChanged += clients => ClientListChanged?.Invoke(clients);

            roomManager.RoomCountChanged += count => RoomCountChanged?.Invoke(count);
        }

        // ==========================================
        // START / STOP
        // ==========================================

        // Chạy cho đến khi Server dừng (hoặc không mở được cổng)
        public Task StartServer()
        {
            return roomManager.StartServer();
        }

        public void StopServer()
        {
            roomManager.StopServer();
        }
    }
}