# CaroOnline — Function Inventory

> Danh sách dưới đây được lập từ source hiện tại trên nhánh `client-server`. Mỗi mục ghi công dụng và vị trí file/dòng để tra cứu nhanh. Trong phiên này chưa thêm hàm mới và chưa chạy build; đây là inventory các hàm đang có, không phải xác nhận commit nào vừa được build thành công. Các file `*.Designer.cs`, mã generated, resource và thành viên chỉ là property/field được lược khỏi danh sách.

## Client

### `Form1.cs` — `Code/CaroOnline.Winforms/CaroOnline.Winforms/Form1.cs`

> Lưu ý: Các helper lobby/phòng trong form thao tác với trạng thái UI/local. `ProcessServerMessage()` hiện chỉ xử lý các loại `CONNECT`, `GAME_OVER`, `NEW_GAME`, `INFO`, không xử lý các message room/lobby như `ROOM_STATE` hoặc danh sách phòng.

- `Form1()` — Khởi tạo giao diện, bàn cờ, socket, event và lobby. (42–87)
- `EndGame()` — Dừng timer và khóa thao tác sau khi kết thúc ván. (93–107)
- `NewGame()` — Bắt đầu lại ván qua lớp UI. (109–112)
- `UndoMove()` — Hoàn tác nước đi qua lớp UI. (114–117)
- `Quit()` — Đóng socket và thoát ứng dụng. (119–124)
- `ConnectToServer()` — Nạp/kiểm tra cấu hình endpoint và kết nối Client tới Server. (130–161)
- `SocketManager_MessageReceived()` — Nhận message từ socket và chuyển xử lý về UI thread. (163–179)
- `ProcessServerMessage()` — Deserialize và xử lý các loại `CONNECT`, `GAME_OVER`, `NEW_GAME`, `INFO`; chưa xử lý message room/lobby. (181–216)
- `SendMessage()` — Serialize và gửi message đến Server. (218–232)
- `InitializeRoomLobbyUI()` — Dựng giao diện lobby/sidebar và bố cục ban đầu. (236–365)
- `StyleSidebarButton()` — Áp dụng style cho nút sidebar. (367–377)
- `LayoutRoomView()` — Tính và áp dụng bố cục bàn cờ, panel thông tin theo cửa sổ. (379–453)
- `AddSidebarLabel()` — Tạo và thêm label vào sidebar. (455–465)
- `RefreshRoomCards()` — Tạo lại các thẻ phòng trong lobby. (467–497)
- `GetRoomStatusText()` — Tạo nội dung trạng thái phòng theo số người/trạng thái. (499–507)
- `ShowRoomLobbyView()` — Hiển thị lobby và ẩn giao diện phòng chơi. (509–523)
- `EnterSelectedRoomUI()` — Chọn phòng local, tự cập nhật occupancy/vai trò và mở ván local; không gửi yêu cầu join phòng lên Server. (525–550)
- `ShowRoomGameView()` — Hiển thị giao diện phòng chơi. (552–563)
- `LeaveRoomToLobbyUI()` — Cập nhật occupancy local, rời giao diện phòng và quay lại lobby. (565–580)
- `StartNewGameUI()` — Reset trạng thái UI/bàn cờ local cho ván mới. (582–598)
- `UndoLastMoveUI()` — Kiểm tra quyền theo trạng thái UI rồi Undo cục bộ; không gửi yêu cầu Undo lên Server. (600–608)
- `UpdateUndoAvailability()` — Cập nhật trạng thái khả dụng của nút Undo. (610–616)
- `AddLobbyMenuItems()` — Thêm mục điều hướng lobby vào menu. (618–622)
- `TryParseServerEndpoint()` — Phân tích endpoint `host:port` và kiểm tra port. (624–636)
- `SocketManager_Disconnected()` — Xử lý mất kết nối và cập nhật trạng thái UI. (638–643)
- `ChessBoard_OnlineMoveRequested()` — Gửi nước đi online kèm phòng và tọa độ. (645–655)
- `LocalRoomState()` — Khởi tạo trạng thái phòng local. (662–666)
- `progressBar1_Click()` — Handler click progress bar; hiện không có xử lý. (671–674)
- `button1_Click()` — Xử lý nút rời phòng. (676–679)
- `label1_Click()` — Handler click label; hiện không có xử lý. (681–684)
- `chessboard_Paint()` — Handler Paint bàn cờ; hiện không có xử lý. (686–689)
- `ChessBoard_EndeGame()` — Nhận event kết thúc từ bàn cờ và gọi xử lý kết thúc ván. (691–694)
- `ChessBoard_PlayerMarked()` — Khởi động đồng hồ lượt và cập nhật khả năng Undo sau khi đánh. (696–702)
- `timer1_Tick()` — Cập nhật thời gian lượt và kết thúc ván khi hết giờ. (704–712)
- `newGameToolStripMenuItem_Click()` — Xử lý lệnh New Game trên menu. (714–718)
- `undoToolStripMenuItem_Click()` — Xử lý lệnh Undo trên menu. (720–724)
- `quitToolStripMenuItem_Click()` — Xử lý lệnh thoát trên menu. (726–730)
- `Form1_FormClosing()` — Xác nhận đóng cửa sổ và đóng socket. (732–747)
- `Form1_Load()` — Handler Load; hiện không có xử lý. (751–754)

### `ChessBoardManager.cs` — `Code/CaroOnline.Winforms/CaroOnline.Winforms/ChessBoardManager.cs`

- `ChessBoardManager()` — Khởi tạo bàn cờ, người chơi, ảnh quân và lượt hiện tại. (91–109)
- `DrawChessBoard()` — Tạo lưới nút bàn cờ và đăng ký sự kiện click. (113–163)
- `SetCellSize()` — Điều chỉnh kích thước ô/bố cục bàn cờ. (165–185)
- `btn_Click()` — Xử lý click ô; tạo nước đi local hoặc phát yêu cầu đánh online. (187–219)
- `EndGame()` — Phát sự kiện kết thúc ván. (221–225)
- `NewGame()` — Xóa lịch sử, đặt lại lượt và vẽ lại bàn cờ. (227–238)
- `Undo()` — Hoàn tác nước đi cuối và khôi phục lượt. (240–260)
- `ApplyRemoteMove()` — Áp dụng nước đi nhận từ Server và kiểm tra kết thúc ván. (262–292)
- `ApplyRemoteSnapshot()` — Nạp snapshot bàn cờ và lượt hiện tại từ Server. (294–318)
- `isEndGame()` — Kiểm tra thắng theo bốn hướng quanh một ô. (320–327)
- `GetChessPoint()` — Chuyển Button thành tọa độ trên bàn cờ. (329–347)
- `isEndHorizontal()` — Kiểm tra năm quân liên tiếp theo chiều ngang. (349–371)
- `isEndVertical()` — Kiểm tra năm quân liên tiếp theo chiều dọc. (373–395)
- `isEndPrimary()` — Kiểm tra năm quân trên đường chéo chính. (398–424)
- `isEndSub()` — Kiểm tra năm quân trên đường chéo phụ. (426–452)
- `Mark()` — Đặt ảnh quân của người chơi lên ô. (454–459)
- `HighlightRecentMove()` — Đánh dấu nước đi mới nhất trên giao diện. (461–475)
- `ChangePlayer()` — Cập nhật tên và biểu tượng người chơi hiện tại. (477–481)

### `SocketManager.cs` — `Code/CaroOnline.Winforms/CaroOnline.Winforms/SocketManager.cs`

- `ConnectServer()` — Kết nối tới endpoint mặc định với tên người dùng hệ thống. (59–62)
- `ConnectServer(...)` — Mở TCP connection, khởi chạy receive loop và gửi HELLO. (64–98)
- `ReceiveClientLoop()` — Đọc liên tục frame từ Server và phát event nhận message. (100–127)
- `CreateServer()` — Khởi động listener TCP và nhận kết nối Client. (133–170)
- `ReceiveServerLoop()` — Nhận message từ Client, phát event và chuyển tiếp. (172–201)
- `SendToAll()` — Gửi message tới toàn bộ Client đang kết nối. (203–216)
- `StopServer()` — Dừng listener, đóng Client và xóa danh sách kết nối. (218–230)
- `Send()` — Gửi message theo chế độ Client hoặc Server. (236–246)
- `SerializeData()` — Serialize dữ liệu thành JSON. (248–251)
- `DeserializeData()` — Deserialize JSON về kiểu dữ liệu yêu cầu. (253–256)
- `SendData()` — Gửi frame TCP gồm header độ dài và payload, đồng bộ thao tác gửi. (258–282)
- `ReceiveData()` — Đọc header/payload TCP và kiểm tra kích thước frame. (284–312)
- `ReadExact()` — Đọc đủ số byte yêu cầu từ stream. (314–332)
- `GetLocalIPv4()` — Tìm IPv4 của network interface đang hoạt động. (334–355)
- `Close()` — Đóng kết nối và hủy tác vụ/listener liên quan. (357–366)

### Cấu hình, khởi động và model Client

- `ClientNetworkSettings.Load()` — Nạp `clientsettings.json`, áp dụng mặc định và kiểm tra cấu hình. `ClientNetworkSettings.cs` (12–27)
- `Player()` — Khởi tạo tên và ảnh quân cho người chơi. `Player.cs` (12–16)
- `PlayInfo()` — Constructor mặc định cho bản ghi nước đi. `PlayInfo.cs` (14–16)
- `PlayInfo(point, player)` — Tạo bản ghi lịch sử từ tọa độ và người chơi. `PlayInfo.cs` (18–22)
- `Program.Main()` — Khởi tạo WinForms và mở `Form1`. `Program.cs` (9–15)

## Server

### `RoomServerManager.cs` — `Code/CaroOnline.Winforms/CaroOnline.Server/RoomServerManager.cs`

- `RoomServerManager()` — Nạp và kiểm tra cấu hình Server. (35–39)
- `StartServer()` — Khởi động listener, nhận kết nối và tạo handler session. (41–91)
- `HandleConnectionAsync()` — Thực hiện handshake, message loop và dọn kết nối. (93–185)
- `RegisterSessionAsync()` — Đăng ký session mới hoặc khôi phục session theo token. (187–230)
- `ProcessMessageAsync()` — Điều phối message tới nghiệp vụ tương ứng. (232–273)
- `SendChallengeAsync()` — Kiểm tra và gửi lời mời đấu tới người chơi khác. (275–309)
- `AcceptChallengeAsync()` — Chấp nhận lời mời và tạo phòng đấu. (311–365)
- `RejectChallengeAsync()` — Từ chối lời mời và thông báo người mời. (367–373)
- `AddSpectatorToRoomAsync()` — Thêm spectator vào phòng hợp lệ. (375–415)
- `LeaveRoomAsync()` — Xử lý người chơi/spectator rời phòng và trường hợp forfeiture. (417–460)
- `ValidateAndApplyMoveAsync()` — Kiểm tra và áp dụng nước đi, phát cập nhật trạng thái/kết quả và lưu lịch sử. (462–546)
- `UndoLastMoveAsync()` — Kiểm tra quyền và hoàn tác nước đi cuối. (548–576)
- `StartNewRoomGameAsync()` — Reset game trong phòng và phát trạng thái mới. (578–598)
- `StartTurnTimer()` — Tạo hoặc thay thế timer của lượt hiện tại. (600–612)
- `RunTurnTimerAsync()` — Chạy timer, xử lý timeout và kết thúc ván. (614–682)
- `FinishRoom()` — Kết thúc phòng, giải phóng trạng thái và tạo lịch sử trận. (684–714)
- `HandleSessionDisconnectedAsync()` — Xử lý mất kết nối, tạm dừng phòng và bắt đầu thời hạn reconnect. (716–771)
- `HandlePlayerReconnectAsync()` — Khôi phục người chơi reconnect và tiếp tục trạng thái/timer. (773–795)
- `ExpireDisconnectedSessionAsync()` — Xử lý hết hạn reconnect và xác định kết quả phòng. (797–854)
- `SendRoomSnapshotAsync()` — Gửi snapshot trạng thái phòng tới một session. (856–869)
- `CreateRoomStateMessage()` — Tạo message `ROOM_STATE` từ dữ liệu phòng. (871–885)
- `SendPlayerListAsync()` — Tạo và gửi danh sách người chơi. (887–899)
- `BroadcastPlayerListAsync()` — Phát danh sách người chơi tới các Client. (901–904)
- `SendRoomListAsync()` — Tạo và gửi danh sách phòng. (906–924)
- `BroadcastRoomAsync()` — Phát message tới các session trong phòng. (926–929)
- `GetRoomSessions()` — Lấy các player/spectator còn kết nối của phòng. (931–938)
- `BroadcastToSessionsAsync()` — Gửi message tới tập session; bổ sung player index nếu cần. (940–951)
- `SendErrorAsync()` — Tạo và gửi message lỗi. (953–956)
- `CreateError()` — Tạo object lỗi theo định dạng chuẩn. (958–961)
- `SendToSessionAsync()` — Gửi frame tới session có đồng bộ thao tác và xử lý lỗi socket. (963–985)
- `ReadMessageAsync()` — Đọc frame TCP và deserialize thành `Message`. (987–1002)
- `WriteMessageAsync()` — Serialize và ghi frame TCP. (1004–1013)
- `ReadExactlyAsync()` — Đọc đủ dữ liệu được yêu cầu từ stream. (1015–1026)
- `ValidateMessageEnvelope()` — Kiểm tra loại message và giới hạn độ dài các trường. (1028–1047)
- `IsValidPlayerName()` — Kiểm tra tên người chơi. (1049–1053)
- `SaveMatchHistoryAsync()` — Lưu lịch sử trận dạng JSON thông qua file tạm. (1055–1080)
- `StopServer()` — Dừng listener, đóng Client và hủy timer/tác vụ liên quan. (1082–1096)
- `Log()` — Phát log kèm timestamp. (1098–1101)

### `GameManager.cs` — `Code/CaroOnline.Winforms/CaroOnline.Server/GameManager.cs`

- `MakeMove()` — Kiểm tra lượt/tọa độ/ô trống rồi ghi nước đi và đổi lượt. (21–54)
- `Undo()` — Hoàn tác nước đi cuối và khôi phục lượt. (56–71)
- `NewGame()` — Reset bàn cờ, lịch sử và trạng thái game. (73–84)
- `CheckWinner()` — Kiểm tra năm quân liên tiếp theo bốn hướng. (86–121)
- `IsBoardFull()` — Kiểm tra bàn cờ đã đầy. (123–126)
- `GetBoardSnapshot()` — Trả về bản sao trạng thái bàn cờ. (128–138)
- `GetMoveHistory()` — Trả về bản sao lịch sử nước đi. (140–149)
- `FinishInDraw()` — Đánh dấu ván kết thúc hòa. (151–154)
- `FinishGame()` — Đánh dấu ván kết thúc. (156–159)
- `CountDirection()` — Đếm quân liên tiếp theo một hướng. (161–184)

### `ServerSocketManager.cs` — `Code/CaroOnline.Winforms/CaroOnline.Server/ServerSocketManager.cs`

`FormServer` khởi động `ServerSocketManager`; wrapper này khởi tạo và ủy quyền vận hành cho `RoomServerManager`. File cũng còn các hàm xử lý kiểu cũ, được ghi chú riêng để không nhầm với luồng server phòng hiện tại.

- `ServerSocketManager()` — Khởi tạo `RoomServerManager` và nối event. (40–45)
- `StartServer()` — Ủy quyền khởi động cho `RoomServerManager`. (47–50)
- `HandleClient()` — Luồng xử lý TCP kiểu legacy, dùng player slot cố định và dọn kết nối. (52–144)
- `GetFreePlayer()` — Tìm slot người chơi 0/1 kiểu legacy. (146–155)
- `ProcessMessage()` — Xử lý MOVE/UNDO/NEW_GAME kiểu legacy qua một `GameManager`. (157–258)
- `Broadcast()` — Phát message theo giao thức newline kiểu legacy. (260–276)
- `SendToClient()` — Gửi JSON newline tới một player legacy với send lock. (278–305)
- `StopServer()` — Ủy quyền dừng cho `RoomServerManager`. (307–310)
- `Log()` — Phát log có timestamp. (312–316)

### `FormServer.cs` — `Code/CaroOnline.Winforms/CaroOnline.Server/FormServer.cs`

- `FormServer()` — Khởi tạo server UI, đăng ký event log/count và đặt trạng thái nút. (11–26)
- `button1_Click()` — Bắt đầu Server từ giao diện. (28–39)
- `button2_Click()` — Dừng Server từ giao diện. (41–49)
- `Server_LogReceived()` — Đưa log lên UI thread. (51–63)
- `Server_ClientCountChanged()` — Xóa và tạo lại các mục `Player 1` đến `Player n` trong danh sách UI. (65–82)
- `FormServer_FormClosing()` — Dừng Server khi đóng cửa sổ. (84–89)

### Cấu hình, khởi động và model Server

- `ServerSettings.Load()` — Nạp cấu hình Server và kiểm tra giá trị. `ServerSettings.cs` (19–29)
- `ServerSettings.Validate()` — Kiểm tra IP, port, giới hạn frame, timeout/reconnect và cấu hình lịch sử. `ServerSettings.cs` (31–53)
- `Program.Main()` — Khởi tạo WinForms và mở `FormServer`. `Program.cs` (9–14)
- `Message()` — Constructor mặc định của DTO message. `Message.cs` (41–44)

`ServerSession.cs`, `Message.cs`, Client `Message.cs` và `Cons.cs` còn chứa model/property/hằng số phục vụ truyền dữ liệu hoặc trạng thái; không có hàm nghiệp vụ cần liệt kê ngoài constructor đã nêu.

## Ghi chú về line numbers

Số dòng là theo working tree hiện tại. Khi thêm/xóa dòng trong source, vị trí trong tài liệu này có thể thay đổi; nên cập nhật lại line ranges sau các thay đổi tiếp theo.
