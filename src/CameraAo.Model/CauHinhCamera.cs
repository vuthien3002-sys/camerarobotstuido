using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CameraAo.Model
{
    /// <summary>
    /// Cau hinh mot camera, doc tu file JSON (vd config/CAM1.json).
    /// Ten truong JSON dung tieng Viet khong dau + snake_case.
    /// </summary>
    public sealed class CauHinhCamera
    {
        [JsonPropertyName("ten")] public string Ten { get; set; } = "CAM1";
        [JsonPropertyName("mo_ta")] public string MoTa { get; set; } = "";
        [JsonPropertyName("anh")] public KichThuocAnh Anh { get; set; } = new KichThuocAnh();
        [JsonPropertyName("intrinsic")] public ThongSoNoi Intrinsic { get; set; } = new ThongSoNoi();
        [JsonPropertyName("flange_to_camera")] public ViTriGan FlangeToCamera { get; set; } = new ViTriGan();
        [JsonPropertyName("thu_muc_anh")] public string ThuMucAnh { get; set; } = "anh";
        [JsonPropertyName("gui_ra")] public CauHinhGuiRa GuiRa { get; set; } = new CauHinhGuiRa();
        [JsonPropertyName("trigger")] public CauHinhTrigger Trigger { get; set; } = new CauHinhTrigger();

        /// <summary>Duong dan file cau hinh da doc (de tinh duong dan tuong doi).</summary>
        [JsonIgnore] public string DuongDanFile { get; private set; } = "";

        public sealed class KichThuocAnh
        {
            [JsonPropertyName("rong")] public int Rong { get; set; } = 2448;
            [JsonPropertyName("cao")] public int Cao { get; set; } = 2048;
        }

        public sealed class ThongSoNoi
        {
            [JsonPropertyName("fx")] public double Fx { get; set; } = 3478.3;
            [JsonPropertyName("fy")] public double Fy { get; set; } = 3478.3;
            [JsonPropertyName("cx")] public double Cx { get; set; } = 1224.0;
            [JsonPropertyName("cy")] public double Cy { get; set; } = 1024.0;
            /// <summary>He so meo [k1, k2, p1, p2, k3]. Mo hinh toi thieu: phai bang 0.</summary>
            [JsonPropertyName("dist")] public double[] Dist { get; set; } = new double[5];
        }

        /// <summary>Tu the camera so voi mat bich robot (eye-in-hand). mm va do, R = Rz*Ry*Rx.</summary>
        public sealed class ViTriGan
        {
            [JsonPropertyName("x_mm")] public double XMm { get; set; }
            [JsonPropertyName("y_mm")] public double YMm { get; set; }
            [JsonPropertyName("z_mm")] public double ZMm { get; set; }
            [JsonPropertyName("rx_deg")] public double RxDo { get; set; }
            [JsonPropertyName("ry_deg")] public double RyDo { get; set; }
            [JsonPropertyName("rz_deg")] public double RzDo { get; set; }
        }

        public sealed class CauHinhGuiRa
        {
            /// <summary>Tao file "&lt;ten&gt;.done" sau khi ghi xong PNG + JSON.</summary>
            [JsonPropertyName("file_ghi_xong")] public bool FileGhiXong { get; set; } = true;
            /// <summary>Gui metadata qua TCP (localhost) va cho xac nhan ACK.</summary>
            [JsonPropertyName("tcp_bat")] public bool TcpBat { get; set; } = true;
            [JsonPropertyName("tcp_may")] public string TcpMay { get; set; } = "127.0.0.1";
            [JsonPropertyName("tcp_cong")] public int TcpCong { get; set; } = 50555;
            [JsonPropertyName("tcp_cho_ack_ms")] public int TcpChoAckMs { get; set; } = 2000;
        }

        public sealed class CauHinhTrigger
        {
            [JsonPropertyName("smart_component")] public string SmartComponent { get; set; } = "CameraAo_CAM1";
            [JsonPropertyName("tin_hieu_chup")] public string TinHieuChup { get; set; } = "Chup";
            [JsonPropertyName("tin_hieu_xong")] public string TinHieuXong { get; set; } = "DaChup";
        }

        static readonly JsonSerializerOptions TuyChonJson = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        public static CauHinhCamera Doc(string duongDan)
        {
            string json = File.ReadAllText(duongDan);
            var ch = DocTuChuoi(json);
            ch.DuongDanFile = Path.GetFullPath(duongDan);
            return ch;
        }

        public static CauHinhCamera DocTuChuoi(string json)
        {
            var ch = JsonSerializer.Deserialize<CauHinhCamera>(json, TuyChonJson);
            if (ch == null) throw new InvalidDataException("File cau hinh rong");
            return ch;
        }

        public string GhiRaChuoi() => JsonSerializer.Serialize(this, TuyChonJson);

        /// <summary>Thu muc anh tuyet doi (neu cau hinh ghi duong dan tuong doi thi tinh tu thu muc cua file cau hinh).</summary>
        public string ThuMucAnhTuyetDoi()
        {
            if (Path.IsPathRooted(ThuMucAnh)) return ThuMucAnh;
            string goc = string.IsNullOrEmpty(DuongDanFile) ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(DuongDanFile);
            return Path.GetFullPath(Path.Combine(goc, ThuMucAnh));
        }

        public MoHinhPinhole TaoMoHinh() =>
            new MoHinhPinhole(Anh.Rong, Anh.Cao, Intrinsic.Fx, Intrinsic.Fy, Intrinsic.Cx, Intrinsic.Cy);

        public TuThe TuTheFlangeToCamera() =>
            TuThe.TuXyzRpy(FlangeToCamera.XMm, FlangeToCamera.YMm, FlangeToCamera.ZMm,
                           FlangeToCamera.RxDo, FlangeToCamera.RyDo, FlangeToCamera.RzDo);

        /// <summary>Kiem tra cau hinh. Tra ve danh sach LOI (rong = hop le); canhBao: dieu mo hinh khong ho tro.</summary>
        public List<string> KiemTra(out List<string> canhBao)
        {
            var loi = new List<string>();
            canhBao = new List<string>();
            if (string.IsNullOrWhiteSpace(Ten)) loi.Add("Thieu 'ten'");
            if (Anh == null || Anh.Rong <= 0 || Anh.Cao <= 0) loi.Add("'anh.rong' va 'anh.cao' phai > 0");
            if (Intrinsic == null || Intrinsic.Fx <= 0 || Intrinsic.Fy <= 0) loi.Add("'intrinsic.fx' va 'intrinsic.fy' phai > 0");
            if (FlangeToCamera == null) loi.Add("Thieu 'flange_to_camera'");
            if (string.IsNullOrWhiteSpace(ThuMucAnh)) loi.Add("Thieu 'thu_muc_anh'");
            if (loi.Count > 0) return loi;

            if (Intrinsic.Cx < 0 || Intrinsic.Cx > Anh.Rong || Intrinsic.Cy < 0 || Intrinsic.Cy > Anh.Cao)
                loi.Add("cx, cy phai nam trong anh");
            if (!TaoMoHinh().TamAnhOGiua())
                canhBao.Add(FormattableString.Invariant(
                    $"Tam anh lech (cx={Intrinsic.Cx}, cy={Intrinsic.Cy}; giua anh = {Anh.Rong / 2.0}, {Anh.Cao / 2.0}). Camera RobotStudio khong ho tro tam lech: anh render coi nhu tam o giua."));
            if (Intrinsic.Dist != null)
                foreach (var k in Intrinsic.Dist)
                    if (Math.Abs(k) > 1e-12) { canhBao.Add("He so meo khac 0: mo hinh toi thieu bo qua meo ong kinh."); break; }
            if (Math.Abs(Intrinsic.Fx - Intrinsic.Fy) > 1e-6)
                canhBao.Add("fx khac fy (pixel khong vuong): RobotStudio chi dat duoc mot goc nhin -> dung fy cho chieu doc.");
            return loi;
        }
    }
}
