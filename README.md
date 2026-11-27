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

## Nhận ảnh từ bên ngoài
```powershell
dotnet run --project tools\BoNhanAnh -- --cong 50555 --so-anh 20
```

## Cấu trúc
Xem [docs/kien_truc.md](docs/kien_truc.md).
