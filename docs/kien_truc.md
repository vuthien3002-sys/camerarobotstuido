# Kiến trúc add-in "Camera ảo" (RobotStudio 2026)

```mermaid
flowchart LR
    subgraph RS["RobotStudio 2026 (luồng giao diện)"]
        UI["4. GIAO DIỆN<br/>CameraAoAddin.cs<br/>ribbon, nút, log"]
        CAP["2. CHỤP<br/>BoChup.cs + VeVungNhin.cs<br/>GraphicControl riêng, khóa chuột<br/>ScreenShot(W,H)"]
        SC["Smart Component<br/>CameraAo_CAM1<br/>Chup / DaChup"]
        VC["Controller ảo<br/>RAPID: DO_Chup / DI_DaChup"]
    end
    CFG[("config/CAM1.json<br/>W×H, fx, fy, cx, cy<br/>flange→camera, thư mục, TCP")]
    MOD["1. MÔ HÌNH CAMERA<br/>CameraAo.Model<br/>CauHinhCamera, MoHinhPinhole,<br/>TuThe, MetadataAnh"]
    TR["3. TRUYỀN DỮ LIỆU<br/>CameraAo.Transport<br/>GhiThuMuc (.png .json .done)<br/>GuiSocket (TCP + ACK)"]
    EXT["Bên ngoài<br/>tools/BoNhanAnh"]
    TEST["Unit test<br/>tests/CameraAo.Model.Tests<br/>(không cần RobotStudio)"]

    CFG --> MOD
    UI -- "nút Chụp" --> CAP
    VC -- "Station Logic" --> SC -- "sườn lên Chup" --> CAP
    CAP -- "DaChup" --> SC --> VC
    MOD -- "tư thế camera, FOV" --> CAP
    CAP -- "PNG + tư thế lúc chụp" --> MOD
    MOD -- "JSON metadata" --> TR
    TR -- "thư mục chung + .done" --> EXT
    TR -- "TCP: thông báo → ACK" --> EXT
    TEST -.-> MOD
    TEST -.-> TR
```

| Phần | Project / file | Phụ thuộc RobotStudio? | Trách nhiệm |
|---|---|---|---|
| 1. Mô hình camera | `src/CameraAo.Model` | **Không** | Đọc/kiểm tra cấu hình; pinhole u = fx·X/Z + cx; FOV dọc = 2·atan(H/2fy); tư thế camera = mặt bích × flange→camera; tạo JSON metadata |
| 2. Chụp | `src/CameraAo.Addin/BoChup.cs`, `VeVungNhin.cs` | Có | Đặt góc nhìn từ mô hình (FieldOfView của RobotStudio tính bằng **độ**), ép vẽ lại rồi `ScreenShot(W,H)`; vẽ hình chóp vùng nhìn trong View1 |
| 3. Truyền dữ liệu | `src/CameraAo.Transport` | **Không** | Ghi PNG → JSON → `.done` (ghi file tạm rồi đổi tên); gửi thông báo TCP, chờ `ACK n` |
| 4. Giao diện | `src/CameraAo.Addin/CameraAoAddin.cs` | Có | Ribbon "Camera ảo", nút, trigger, điều phối, log ra Output |
| Bên ngoài | `tools/BoNhanAnh` | Không | Nhận ảnh qua TCP hoặc quét thư mục; kiểm tra file + trường JSON; tổng kết N/N |

**Luồng một lần chụp:** trigger (nút hoặc sườn lên `Chup`) → tính tư thế camera từ mặt bích robot + cấu hình → đặt góc nhìn, ép vẽ lại → `ScreenShot(W,H)` → metadata (chỉ số, thời gian mô phỏng, tư thế camera trong World, intrinsic) → ghi thư mục + `.done` → (nền) gửi TCP, chờ ACK → bật `DaChup`.

**Quy ước:** hệ camera giống OpenCV (Z nhìn về trước, Y hướng xuống ảnh); mô hình dùng mm + độ; API RobotStudio dùng mét + radian (riêng `GraphicControl.FieldOfView` dùng độ).

**Giới hạn đã biết:** camera RobotStudio luôn có tâm ảnh ở giữa (cx = W/2, cy = H/2) và không có méo ống kính → cấu hình khác sẽ bị cảnh báo và ghi vào JSON (`canh_bao`); không mô phỏng độ sâu trường ảnh, nhiễu cảm biến.
