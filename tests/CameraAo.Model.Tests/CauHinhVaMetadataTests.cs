using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CameraAo.Model;

namespace CameraAo.Model.Tests
{
    public class CauHinhVaMetadataTests
    {
        const string JsonMau = @"{
          ""ten"": ""CAMX"",
          ""anh"": { ""rong"": 1920, ""cao"": 1200 },
          ""intrinsic"": { ""fx"": 2000, ""fy"": 2000, ""cx"": 960, ""cy"": 600, ""dist"": [0,0,0,0,0] },
          ""flange_to_camera"": { ""x_mm"": 10, ""y_mm"": 20, ""z_mm"": 30, ""rx_deg"": 0, ""ry_deg"": 0, ""rz_deg"": 90 },
          ""thu_muc_anh"": ""anh_thu"",
          // cho phep chu thich trong file cau hinh
        }";

        [Fact]
        public void DocCauHinh_DungGiaTri()
        {
            var ch = CauHinhCamera.DocTuChuoi(JsonMau);
            Assert.Equal("CAMX", ch.Ten);
            Assert.Equal(1920, ch.Anh.Rong);
            Assert.Equal(600, ch.Intrinsic.Cy);
            Assert.Equal(30, ch.FlangeToCamera.ZMm);
            Assert.Empty(ch.KiemTra(out var cb));
            Assert.Empty(cb);
            var t = ch.TuTheFlangeToCamera();
            Assert.Equal(30, t.T.Z, 9);
            Assert.Equal(1, t.TrucX.Y, 9);   // xoay 90 do quanh Z
        }

        [Fact]
        public void KiemTra_BaoLoiVaCanhBao()
        {
            var ch = CauHinhCamera.DocTuChuoi(JsonMau);
            ch.Intrinsic.Fx = 0;
            Assert.NotEmpty(ch.KiemTra(out _));

            ch = CauHinhCamera.DocTuChuoi(JsonMau);
            ch.Intrinsic.Cx = 970;              // tam lech -> canh bao (khong phai loi)
            ch.Intrinsic.Dist = new double[] { -0.1, 0, 0, 0, 0 };
            Assert.Empty(ch.KiemTra(out var cb));
            Assert.Equal(2, cb.Count);
        }

        [Fact]
        public void ThuMucAnhTuongDoi_TinhTuThuMucFileCauHinh()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "cameraao_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                string f = Path.Combine(tmp, "CAMX.json");
                File.WriteAllText(f, JsonMau);
                var ch = CauHinhCamera.Doc(f);
                Assert.Equal(Path.Combine(tmp, "anh_thu"), ch.ThuMucAnhTuyetDoi());
            }
            finally { Directory.Delete(tmp, true); }
        }

        [Fact]
        public void FileCauHinhTrongRepo_HopLe()
        {
            // Tim config/CAM1.json tu thu muc chay test di nguoc len
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "config", "CAM1.json"))) d = d.Parent;
            Assert.NotNull(d);
            var ch = CauHinhCamera.Doc(Path.Combine(d.FullName, "config", "CAM1.json"));
            Assert.Empty(ch.KiemTra(out var cb));
            Assert.Empty(cb);
            Assert.Equal(32.81, ch.TaoMoHinh().FovDocDo, 2);
            Assert.Equal("DO_Chup", ch.Trigger.DoRobot);
            Assert.Equal("DI_DaChup", ch.Trigger.DiRobot);
        }

        [Fact]
        public void Trigger_ThieuTruongMoi_DungMacDinh_CoTruong_DocDung()
        {
            // File cau hinh cu (chua co controller/do_robot/di_robot) van doc duoc, dung mac dinh
            var cu = CauHinhCamera.DocTuChuoi(JsonMau);
            Assert.Equal("CameraAo_CAM1", cu.Trigger.SmartComponent);
            Assert.Equal("", cu.Trigger.Controller);
            Assert.Equal("DO_Chup", cu.Trigger.DoRobot);
            Assert.Equal("DI_DaChup", cu.Trigger.DiRobot);

            var moi = CauHinhCamera.DocTuChuoi(@"{ ""trigger"": { ""smart_component"": ""SC"", ""tin_hieu_chup"": ""A"", ""tin_hieu_xong"": ""B"",
                ""controller"": ""Controller1"", ""do_robot"": ""doCam"", ""di_robot"": ""diCam"" } }");
            Assert.Equal("SC", moi.Trigger.SmartComponent);
            Assert.Equal("A", moi.Trigger.TinHieuChup);
            Assert.Equal("B", moi.Trigger.TinHieuXong);
            Assert.Equal("Controller1", moi.Trigger.Controller);
            Assert.Equal("doCam", moi.Trigger.DoRobot);
            Assert.Equal("diCam", moi.Trigger.DiRobot);
        }

        [Fact]
        public void Metadata_DuTruongBatBuoc()
        {
            var md = new MetadataAnh
            {
                ChiSo = 7, TenAnh = "CAM1_000007.png", Camera = "CAM1", ThoiGianMoPhongS = 12.5,
                NguonTrigger = "DO", Rong = 2448, Cao = 2048,
                MoHinh = new MoHinhPinhole(2448, 2048, 3478.3, 3478.3, 1224, 1024),
                CameraTrongWorld = TuThe.TuXyzRpy(500, 0, 800, 180, 0, 0),
                GocKhopDo = new[] { 0.0, 10, 20, 0, 60, 0 },
            };
            string json = md.TaoJson();
            Assert.Empty(MetadataAnh.TruongThieu(json));
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(7, doc.RootElement.GetProperty("chi_so").GetInt32());
            Assert.Equal(12.5, doc.RootElement.GetProperty("thoi_gian_mo_phong_s").GetDouble());
            Assert.Equal(3478.3, doc.RootElement.GetProperty("intrinsic").GetProperty("fx").GetDouble());
            var vt = doc.RootElement.GetProperty("camera_trong_world").GetProperty("vi_tri_mm").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            Assert.Equal(new[] { 500.0, 0, 800 }, vt);
        }

        [Fact]
        public void Metadata_KhongMoPhong_ThoiGianMoPhongLaNull()
        {
            string json = new MetadataAnh { ChiSo = 1, MoHinh = new MoHinhPinhole(10, 10, 5, 5, 5, 5) }.TaoJson();
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("thoi_gian_mo_phong_s").ValueKind);
            Assert.Empty(MetadataAnh.TruongThieu(json));
        }
    }
}
