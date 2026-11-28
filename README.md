# Camera ảo – add-in RobotStudio 2026

Camera mô phỏng eye-in-hand: đọc thông số từ `config/CAM1.json` (W×H, fx, fy, cx, cy, vị trí so với mặt bích),
chụp ảnh đúng W×H từ góc nhìn camera, lưu PNG + JSON, gửi ra ngoài (thư mục + file `.done`, TCP có ACK).

## Build + cài (một lệnh)
Yêu cầu: .NET 10 SDK, RobotStudio 2026. **Đóng RobotStudio** rồi chạy trong PowerShell:
```powershell
.\build.ps1
```
Script tự tìm RobotStudio, build solution, chạy unit test, kiểm tra `.rsaddin` theo schema và chép add-in vào
`<RobotStudio>\Bin\Addins\CameraAo`. Mở RobotStudio → mở station → tab **Camera ảo**.

Chỉ build/test: `.\build.ps1 -KhongCai` · Chỉ unit test: `dotnet test tests\CameraAo.Model.Tests`

## Trigger bằng DO từ RAPID
1. Controller ảo cần 2 tín hiệu `DO_Chup` (DO) và `DI_DaChup` (DI) — *Controller → Configuration → Load Parameters* (hoặc *Add Signals*), rồi warm restart.
2. Tab **Camera ảo → Kết nối trigger**: tự tạo Smart Component `CameraAo_CAM1` (vào `Chup`, ra `DaChup`) và Station Logic
   `Controller1.DO_Chup → Chup`, `DaChup → Controller1.DI_DaChup` (tên đổi được trong mục `trigger` của cấu hình).
3. RAPID bắt tay 2 chiều ở mỗi tư thế:
   ```
   MoveL p, v200, fine, tool0;
   SetDO DO_Chup, 1;  WaitDI DI_DaChup, 1;   ! camera chụp xong
   SetDO DO_Chup, 0;  WaitDI DI_DaChup, 0;
   ```
4. Chạy bằng **Simulation → Play** (Station Logic chỉ truyền tín hiệu khi đang mô phỏng). Ảnh có `"nguon_trigger": "DO"`.
   Nút **Trạng thái trigger** in số xung / số ảnh.

## Nhận ảnh từ bên ngoài
```powershell
dotnet run --project tools\BoNhanAnh -- --cong 50555 --so-anh 20
```

## Cấu trúc
Xem [docs/kien_truc.md](docs/kien_truc.md).
