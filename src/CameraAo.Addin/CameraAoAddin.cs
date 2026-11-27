// Add-in "Camera ao" cho RobotStudio 2026 - v0.4
// Kien truc 4 phan:
//   CameraAo.Model     : mo hinh camera (cau hinh, pinhole, tu the, metadata)  - khong phu thuoc RobotStudio
//   CameraAo.Transport : ghi thu muc + file .done, gui TCP co ACK              - khong phu thuoc RobotStudio
//   BoChup.cs          : CHUP (GraphicControl rieng, khoa chuot, ScreenShot W x H)
//   CameraAoAddin.cs   : GIAO DIEN + dieu phoi (ribbon, nut, log)
// Don vi: API RobotStudio dung met + radian (rieng GraphicControl.FieldOfView dung DO); mo hinh dung mm + do.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Environment;
using ABB.Robotics.RobotStudio.Stations;
using CameraAo.Model;
using CameraAo.Transport;

namespace CameraAo.Addin
{
    public static class CameraAoAddin
    {
        static CauHinhCamera _ch;
        static MoHinhPinhole _mh;
        static List<string> _canhBao = new List<string>();
        static readonly BoChup _bo = new BoChup();
        static readonly VeVungNhin _ve = new VeVungNhin();

        static int _chiSo;              // so thu tu anh trong phien
        static string _thuMucPhien;     // thu muc anh cua phien hien tai

        // Diem vao: RobotStudio goi khi nap add-in. Khong lam viec lau o day.
        public static void AddinMain()
        {
            try
            {
                _bo.LayTuTheCamera = LayTuTheCamera;
                _bo.LayMoHinh = () => _mh;
                _bo.Log = Log;
                _bo.DaCapNhat = cam => { try { _ve.Ve(Station.ActiveStation, _bo.Control, cam, _mh, _ch.Ten); } catch { } };
                NapCauHinh();
                TaoRibbon();
                Log("Da nap add-in Camera ao v0.4. " + MoTaCauHinh());
            }
            catch (Exception ex)
            {
                Logger.AddMessage(new LogMessage("[Camera ao] LOI khi nap add-in: " + ex));
            }
        }

        // =====================================================================
        // GIAO DIEN
        // =====================================================================
        static void TaoRibbon()
        {
            var tab = new RibbonTab("CameraAo.Tab", "Camera ảo");

            var nhomCam = new RibbonGroup("CameraAo.NhomCamera", "Camera");
            ThemNut(nhomCam, "CameraAo.MoCamera", "Mở camera", MoCamera);
            ThemNut(nhomCam, "CameraAo.Chup", "Chụp ảnh", () => ChupAnh("nut"));
            ThemNut(nhomCam, "CameraAo.PhienMoi", "Phiên mới", PhienMoi);

            var nhomCh = new RibbonGroup("CameraAo.NhomCauHinh", "Cấu hình");
            ThemNut(nhomCh, "CameraAo.NapLai", "Nạp lại cấu hình", () => { NapCauHinh(); Log("Da nap lai. " + MoTaCauHinh()); _ve.Xoa(); if (_bo.DaMo) MoCamera(); });
            ThemNut(nhomCh, "CameraAo.LayTuFrame", "Lấy vị trí từ frame", LayViTriTuFrame);
            ThemNut(nhomCh, "CameraAo.ThongTin", "Thông tin", ThongTin);

            tab.Groups.Add(nhomCam);
            tab.Groups.Add(nhomCh);
            UIEnvironment.RibbonTabs.Add(tab);
        }

        static void ThemNut(RibbonGroup nhom, string id, string chu, Action khiBam)
        {
            var nut = new CommandBarButton(id, chu);
            nut.DefaultEnabled = true;
            nut.ExecuteCommand += (s, e) =>
            {
                try { khiBam(); }
                catch (Exception ex) { Log("LOI (" + chu + "): " + ex.Message + "\n" + ex); }
            };
            nhom.Controls.Add(nut);
            nhom.SetControlLayout(nut, RibbonControlLayout.Large);
        }

        static void MoCamera()
        {
            var st = Station.ActiveStation;
            if (st == null) { Log("Hay mo station truoc."); return; }
            _bo.MoCuaSo(st, TieuDe());
            var cam = LayTuTheCamera();
            Log(cam.HasValue ? "Da mo cua so camera. Camera tai " + cam.Value.T + " mm, nhin theo " + cam.Value.TrucZ
                             : "Da mo cua so camera nhung chua lay duoc tu the (task dang chon khong co robot?)");
        }

        static string TieuDe() => FormattableString.Invariant(
            $"Camera ảo – {_ch.Ten} | {_mh.Rong}x{_mh.Cao} | FOV {_mh.FovNgangDo:F1}° x {_mh.FovDocDo:F1}°");

        // =====================================================================
        // CAU HINH
        // =====================================================================
        static string ThuMucAddin() => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        /// <summary>
        /// Tim file cau hinh: duong dan ghi trong "duong_dan_cau_hinh.txt" (build.ps1 tao) canh DLL,
        /// neu khong co thi "CAM1.json" canh DLL.
        /// </summary>
        static string TimFileCauHinh()
        {
            string troi = Path.Combine(ThuMucAddin(), "duong_dan_cau_hinh.txt");
            if (File.Exists(troi))
            {
                string p = File.ReadAllText(troi).Trim();
                if (File.Exists(p)) return p;
            }
            return Path.Combine(ThuMucAddin(), "CAM1.json");
        }

        static void NapCauHinh()
        {
            string f = TimFileCauHinh();
            if (!File.Exists(f))
            {
                _ch = new CauHinhCamera();
                Log("Khong thay file cau hinh (" + f + ") -> dung gia tri mac dinh.");
            }
            else _ch = CauHinhCamera.Doc(f);

            var loi = _ch.KiemTra(out _canhBao);
            if (loi.Count > 0)
            {
                Log("Cau hinh LOI: " + string.Join("; ", loi) + " -> dung gia tri mac dinh.");
                _ch = new CauHinhCamera();
                _ch.KiemTra(out _canhBao);
            }
            foreach (var c in _canhBao) Log("CANH BAO cau hinh: " + c);
            _mh = _ch.TaoMoHinh();
            _thuMucPhien = null;    // cau hinh moi -> phien moi
        }

        static string MoTaCauHinh()
        {
            var (vr, vc) = _mh.VungNhinMm(300);
            var f = _ch.FlangeToCamera;
            return string.Create(CultureInfo.InvariantCulture,
                $"Camera '{_ch.Ten}' ({_ch.MoTa}): {_mh.Rong}x{_mh.Cao} px, fx={_mh.Fx} fy={_mh.Fy} cx={_mh.Cx} cy={_mh.Cy}, FOV {_mh.FovNgangDo:F2} x {_mh.FovDocDo:F2} do, vung nhin o 300 mm = {vr:F0} x {vc:F0} mm. Gan so voi mat bich: ({f.XMm}, {f.YMm}, {f.ZMm}) mm, ({f.RxDo}, {f.RyDo}, {f.RzDo}) do. File: {_ch.DuongDanFile}");
        }

        static void ThongTin()
        {
            Log(MoTaCauHinh());
            Log("Thu muc anh: " + _ch.ThuMucAnhTuyetDoi() + (_thuMucPhien != null ? " | phien hien tai: " + _thuMucPhien + ", da chup " + _chiSo + " anh" : ""));
            var cam = LayTuTheCamera();
            if (cam.HasValue) Log("Camera hien tai: vi tri " + cam.Value.T + " mm, huong nhin " + cam.Value.TrucZ);
        }

        /// <summary>
        /// Doc vi tri frame cung ten camera (vd "CAM1") so voi mat bich robot va GHI vao file cau hinh.
        /// Dung de chon vi tri gan bang cong cu RobotStudio (Set Position / Move / Rotate) roi luu lai thanh so.
        /// </summary>
        static void LayViTriTuFrame()
        {
            var st = Station.ActiveStation;
            var robot = st?.ActiveTask?.Mechanism;
            if (robot == null) { Log("Chua co station/robot."); return; }
            Frame fr = null;
            foreach (var o in st.GetAllObjects())
                if (o is Frame f && string.Equals(f.Name, _ch.Ten, StringComparison.OrdinalIgnoreCase)) fr = f;
            if (fr == null) { Log("Khong co frame ten '" + _ch.Ten + "'. Tao frame (Home > Frame), dat ten '" + _ch.Ten + "', dat vao vi tri camera roi bam lai."); return; }
            if (string.IsNullOrEmpty(_ch.DuongDanFile) || !File.Exists(_ch.DuongDanFile)) { Log("Khong co file cau hinh de ghi."); return; }

            TuThe matBich = ChuyenDoiRs.TuMatrix4(robot.GetFlange(0).GlobalMatrix);
            TuThe frame = ChuyenDoiRs.TuMatrix4(fr.Transform.GlobalMatrix);
            TuThe rel = matBich.NghichDao().Nhan(frame);
            var (rx, ry, rz) = rel.GocDo();

            var goc = JsonNode.Parse(File.ReadAllText(_ch.DuongDanFile)).AsObject();
            var o2 = new JsonObject
            {
                ["x_mm"] = Math.Round(rel.T.X, 3), ["y_mm"] = Math.Round(rel.T.Y, 3), ["z_mm"] = Math.Round(rel.T.Z, 3),
                ["rx_deg"] = Math.Round(rx, 4), ["ry_deg"] = Math.Round(ry, 4), ["rz_deg"] = Math.Round(rz, 4),
            };
            goc["flange_to_camera"] = o2;
            File.Copy(_ch.DuongDanFile, _ch.DuongDanFile + ".bak", true);
            File.WriteAllText(_ch.DuongDanFile, goc.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            NapCauHinh();
            _ve.Xoa();
            Log("Da ghi vi tri frame '" + fr.Name + "' vao cau hinh (ban cu: .bak). " + MoTaCauHinh());
        }

        // =====================================================================
        // DIEU PHOI: tinh tu the -> chup -> metadata -> ghi -> gui
        // =====================================================================

        /// <summary>Tu the camera trong world (mm) = mat bich * flange_to_camera (tu cau hinh).</summary>
        static TuThe? LayTuTheCamera()
        {
            var robot = Station.ActiveStation?.ActiveTask?.Mechanism;
            if (robot == null) return null;
            TuThe matBich = ChuyenDoiRs.TuMatrix4(robot.GetFlange(0).GlobalMatrix);
            return matBich.Nhan(_ch.TuTheFlangeToCamera());
        }

        static void PhienMoi()
        {
            _thuMucPhien = null;
            _chiSo = 0;
            Log("Phien moi: anh tiep theo se vao thu muc moi, so thu tu bat dau tu 1.");
        }

        /// <summary>Chup mot anh. nguon = "nut" hoac "DO". Tra ve true neu ghi thanh cong.</summary>
        public static bool ChupAnh(string nguon)
        {
            var st = Station.ActiveStation;
            if (st == null) { Log("Hay mo station truoc."); return false; }
            if (!_bo.DaMo) _bo.MoCuaSo(st, TieuDe());

            if (_thuMucPhien == null)
            {
                _thuMucPhien = Path.Combine(_ch.ThuMucAnhTuyetDoi(), _ch.Ten + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                _chiSo = 0;
            }

            byte[] png = _bo.Chup(_mh.Rong, _mh.Cao, out TuThe cam, out var kt);
            int chiSo = ++_chiSo;
            string tenGoc = _ch.Ten + "_" + chiSo.ToString("D6", CultureInfo.InvariantCulture);

            var robot = st.ActiveTask?.Mechanism;
            var md = new MetadataAnh
            {
                ChiSo = chiSo,
                TenAnh = tenGoc + ".png",
                Camera = _ch.Ten,
                ThoiGian = DateTime.Now,
                ThoiGianMoPhongS = DangMoPhong() ? Simulator.CurrentTime : (double?)null,
                NguonTrigger = nguon,
                Rong = kt.Width,
                Cao = kt.Height,
                MoHinh = _mh,
                Dist = _ch.Intrinsic.Dist,
                CameraTrongWorld = cam,
                FlangeToCamera = _ch.TuTheFlangeToCamera(),
                CanhBao = new List<string>(_canhBao),
            };
            if (robot != null)
            {
                md.MatBichTrongWorld = ChuyenDoiRs.TuMatrix4(robot.GetFlange(0).GlobalMatrix);
                md.Robot = robot.Name;
                var q = robot.GetJointValues();
                md.GocKhopDo = Array.ConvertAll(q, r => r * 180 / Math.PI);
            }
            if (kt.Width != _mh.Rong || kt.Height != _mh.Cao)
                md.CanhBao.Add($"Anh render {kt.Width}x{kt.Height} khac cau hinh {_mh.Rong}x{_mh.Cao}");

            var ghi = new GhiThuMuc(_thuMucPhien, _ch.GuiRa.FileGhiXong);
            KetQuaGhi kq = ghi.Ghi(tenGoc, png, md.TaoJson());
            string tMoPhong = md.ThoiGianMoPhongS.HasValue
                ? string.Create(CultureInfo.InvariantCulture, $", t mo phong = {md.ThoiGianMoPhongS:F3} s") : "";
            Log(string.Create(CultureInfo.InvariantCulture,
                $"Anh #{chiSo} ({nguon}) {kt.Width}x{kt.Height} -> {kq.Png}. Camera tai {cam.T} mm{tMoPhong}"));

            if (_ch.GuiRa.TcpBat) GuiNen(chiSo, kq);
            return true;
        }

        static bool DangMoPhong()
        {
            try { return Simulator.State.ToString() != "Stopped"; } catch { return false; }
        }

        /// <summary>Gui TCP o luong nen (khong chan giao dien), bao ket qua ve luong giao dien.</summary>
        static void GuiNen(int chiSo, KetQuaGhi kq)
        {
            var gui = new GuiSocket(_ch.GuiRa.TcpMay, _ch.GuiRa.TcpCong, _ch.GuiRa.TcpChoAckMs);
            string ten = _ch.Ten;
            var gc = _bo.Control;
            Task.Run(() =>
            {
                bool ok = gui.Gui(chiSo, ten, kq, out string tb);
                string s = ok ? $"Anh #{chiSo}: ben nhan da xac nhan (ACK)." : $"Anh #{chiSo}: gui TCP khong thanh cong - {tb}";
                try { if (gc != null && !gc.IsDisposed) gc.BeginInvoke(new Action(() => Log(s))); } catch { }
            });
        }

        static void Log(string s) => Logger.AddMessage(new LogMessage("[Camera ao] " + s));
    }
}
