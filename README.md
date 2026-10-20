# LTE Device Automated Test Station (MTE-01)
> **Dự án Trạm Kiểm thử Tự động Thiết bị Mạng & LTE dành cho Kỹ sư MTE (Manufacturing Test Engineer)**

---

## 🎯 1. Tổng quan Dự án

Dự án này là giải pháp phần mềm chuyên dụng trên dây chuyền sản xuất bo mạch / thiết bị mạng không dây (4G/LTE Router, CPE Modem) tại các nhà máy điện tử viễn thông.

Phần mềm được thiết kế chuẩn theo mô hình công nghiệp, giải quyết trọn vẹn các yêu cầu trong JD của Kỹ sư MTE:
1. **Thiết lập cấu trúc thử nghiệm:** Quản lý kịch bản test tuần tự 8 bước tiêu chuẩn (AT Command, Serial Port, TCP/IP Socket, RF Signal RSSI).
2. **Cải tiến & tối ưu hóa:** Đo đạc chính xác thời gian chu kỳ (Cycle Time ms), cơ chế dừng ngay khi phát hiện lỗi (*Stop on First Failure*).
3. **Phân tích dữ liệu & Báo cáo tuần (Weekly Report Review):** Tích hợp Microsoft SQL Server (MSSQL), tự động tính toán Tỷ lệ Đạt (Yield Rate %), biểu đồ phân tích lỗi Pareto, xuất báo cáo CSV.
4. **Tích hợp Virtual Simulator:** Cho phép chạy thử nghiệm và demo đầy đủ tính năng ngay trên máy tính mà không cần kết nối phần cứng thật.

---

## 🏗️ 2. Kiến trúc & Công nghệ

* **Ngôn ngữ:** C# (.NET WinForms)
* **Giao tiếp phần cứng:**
  * `System.IO.Ports.SerialPort`: Giao tiếp cổng nối tiếp RS232 / UART / COM Port (Baud 115200).
  * `System.Net.Sockets`: Giao tiếp TCP/IP Socket, Ping kiểm tra độ trễ mạng.
  * `MockLteDeviceSimulator`: Trình giả lập modem LTE ảo (EC25-V2.1) hỗ trợ mô phỏng lỗi (rút SIM, sóng yếu).
* **Cơ sở dữ liệu:** Microsoft SQL Server (`Microsoft.Data.SqlClient`) + file log lưu vết cục bộ (`./Logs/`).

---

## 📋 3. Quy trình 8 Bước Kiểm thử Tự động (Test Sequence)

| Bước | Tên bước | Lệnh gửi | Mẫu phản hồi chuẩn | Ý nghĩa trong sản xuất |
| :---: | :--- | :--- | :--- | :--- |
| **1** | `COMM_CHECK` | `AT` | `OK` | Kiểm tra giao tiếp giữa máy tính test và thiết bị |
| **2** | `READ_IMEI` | `AT+CGSN` | `\d{15}` | Đọc mã nhận dạng 15 số IMEI của modem |
| **3** | `CHECK_SIM` | `AT+CPIN?` | `READY` | Kiểm tra khe cắm SIM có tiếp xúc và sẵn sàng |
| **4** | `RF_SIGNAL_CSQ` | `AT+CSQ` | `\+CSQ: (\d+)` | Đo công suất sóng vô tuyến (yêu cầu RSSI $\ge 15$) |
| **5** | `LTE_ATTACH` | `AT+CGATT?` | `\+CGATT: 1` | Kiểm tra thiết bị đã đính kèm vào mạng gói LTE |
| **6** | `NETWORK_OPERATOR` | `AT+COPS?` | `\+COPS:` | Xác nhận nhà mạng đăng ký (Viettel, Vinaphone...) |
| **7** | `SOCKET_PING` | `PING 8.8.8.8` | `OK` | Kiểm tra độ trễ mạng Internet thực tế qua Socket |
| **8** | `DATA_THROUGHPUT` | `SPEED_TEST` | `OK` | Đo băng thông tải lên (UL) và tải xuống (DL) |

---

## 🚀 4. Hướng dẫn Chạy Ứng dụng

### Cách 1: Chạy bằng Visual Studio (Khuyên dùng)
1. Mở file giải pháp **`LteTestStation.sln`** bằng **Visual Studio**.
2. Nhấn phím **`F5`** (hoặc nút **`Start`**) để khởi chạy ứng dụng.
3. Nhập hoặc nhấn **`🎲 Random SN`** -> Nhấn **`▶ START TEST`** (hoặc gõ **Enter**) để xem quy trình test chạy mượt mà.

### Cách 2: Chạy bằng Dòng lệnh (Terminal)
```powershell
dotnet run
```

---

## 🗄️ 5. Hướng dẫn Thiết lập Cơ sở dữ liệu MSSQL

1. Mở **SQL Server Management Studio (SSMS)**.
2. Mở file [Database/Database_Setup.sql](file:///d:/$HOME/agy2-projects/q-a/Database/Database_Setup.sql) và nhấn **Execute** (`F5`).
   * Script sẽ tự động tạo Database `LteTestStationDb` và Bảng `TestRecords`.
   * Tạo sẵn dữ liệu mẫu cho tuần làm việc để demo tính năng **Weekly Review**.
3. Nếu SQL Server của bạn có cấu hình tài khoản/mật khẩu riêng, bạn có thể chỉnh Connection String ngay trên giao diện bằng nút **`⚙ DB Setup`**.
> *Lưu ý:* Nếu chưa bật MSSQL, phần mềm sẽ tự động chuyển sang chế độ **Local Fallback** và lưu toàn bộ log chi tiết vào thư mục `./Logs/` mà không bị treo hay gián đoạn.

---

## 📊 6. Tính năng Báo cáo Tuần (Weekly Report Review)

Nhấn vào nút **`📊 Weekly Review`** trên thanh tiêu đề để mở màn hình quản trị:
* **Dashboard chỉ số:** Tổng sản phẩm đã test, số lượng PASS, số lượng FAIL, Tỷ lệ Đạt (Yield Rate %), Thời gian test trung bình (Average Cycle Time).
* **Biểu đồ lỗi Pareto (Top Failure Pareto):** Thống kê bước nào trên dây chuyền hay bị lỗi nhất (ví dụ: `RF_SIGNAL_CSQ` lỗi sóng RF hay `CHECK_SIM` lỏng thẻ SIM).
* **Nút `📥 Export Weekly CSV`:** Xuất báo cáo ra định dạng CSV để gửi cho Quản lý xưởng hoặc đính kèm email báo cáo tuần.
