using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Environment;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using CameraAo.Model;

namespace CameraAo.Addin
{
    /// <summary>
    /// PHAN CHUP: mot GraphicControl rieng (cua so "Camera ao") dat dung goc nhin camera.
    /// - Khoa chuot: nguoi dung khong xoay/zoom duoc -> anh khong phu thuoc goc nhin View1.
    /// - Moi lenh o day phai chay tren LUONG GIAO DIEN cua RobotStudio.
    /// </summary>
    internal sealed class BoChup
    {
        const double KhoangNhinMm = 300;        // diem "nhin vao" dat truoc camera, chi de dinh huong

        ToolWindow _cuaSo;
        GraphicControl _gc;
        Camera _cam;
        Timer _timer;

        /// <summary>Goc nhin THUC TE cua GraphicControl (de kiem tra da dat dung chua): mat, diem nhin (mm), FOV (do).</summary>
        public (Vec3 tu, Vec3 vao, double fov) GocNhinThucTe()
        {
            var a = _gc.LookFrom; var b = _gc.LookAt;
            return (new Vec3(a.x * 1000, a.y * 1000, a.z * 1000), new Vec3(b.x * 1000, b.y * 1000, b.z * 1000), _gc.FieldOfView);
        }

        public GraphicControl Control => _gc;
        public bool DaMo => _gc != null && !_gc.IsDisposed;

        /// <summary>Ham tinh tu the camera trong world (mm). Tra ve null neu chua co robot.</summary>
        public Func<TuThe?> LayTuTheCamera { get; set; }
        public Func<MoHinhPinhole> LayMoHinh { get; set; }
        public Action<string> Log { get; set; }
        /// <summary>Goi sau moi lan dat goc nhin (vd de ve lai hinh chop trong View1).</summary>
        public Action<TuThe> DaCapNhat { get; set; }

        public void MoCuaSo(Station st, string tieuDe)
        {
            if (DaMo)
            {
                _cuaSo.Caption = tieuDe;
                _cuaSo.Visible = true;
                AnVatKhoiCamera(st);
                CapNhatGocNhin(batBuoc: true);
                return;
            }

            _gc = new GraphicControl();
            _gc.Dock = DockStyle.Fill;
            _gc.RootObject = st;
            try { _gc.ShowUCSGrid = false; _gc.ShowButtons = false; _gc.ShowKeyTips = false; } catch { }
            try { _gc.ShowUCSFrame = false; _gc.ShowCoordinateSystem = false; } catch { }
            // Xem truoc dung muc chi tiet mac dinh (nhe). Muc Fine + khong bo vat nho chi bat LUC CHUP (xem Chup()).
            try { _gc.PickingEnabled = false; _gc.Enabled = false; } catch { }   // khoa chuot

            _cuaSo = new ToolWindow("CameraAo.CuaSo", _gc, tieuDe);
            _cuaSo.PreferredSize = new Size(640, 540);
            UIEnvironment.Windows.AddDocked(_cuaSo, DockStyle.Right);
            AnVatKhoiCamera(st);

            // Kiem tra 10 lan/giay de bam theo robot, nhung CHI dat lai goc nhin (va ve lai) khi tu the camera / FOV
            // thay doi. Truoc day lan nao cung SyncCamera -> ve lai ca station 10 lan/giay tren luong giao dien
            // -> zoom/xoay/keo View1 bi cham.
            _timer = new Timer { Interval = 100 };
            _timer.Tick += (s, e) => { try { if (_cuaSo != null && _cuaSo.Visible) CapNhatGocNhin(); } catch { } };
            _timer.Start();
            CapNhatGocNhin(batBuoc: true);
        }

        /// <summary>An trong RIENG cua so camera: dung cu (co cau loai Tool) va vat co ten chua "camera".</summary>
        public void AnVatKhoiCamera(Station st)
        {
            if (!DaMo || st == null) return;
            foreach (GraphicComponent g in st.GraphicComponents)
            {
                string ten = g.Name ?? "";
                bool an = (g is Mechanism m && m.MechanismType == MechanismType.Tool)
                       || ten.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0;
                if (an) { try { _gc.SetObjectVisible(g, false); } catch { } }
            }
            // Camera that khong thay frame, target, workobject, tool frame, duong path -> an trong cua so camera
            foreach (var o in st.GetAllObjects())
            {
                if (o is Frame || o is RsTarget || o is RsWorkObject || o is RsToolData || o is RsPathProcedure)
                {
                    try { _gc.SetObjectVisible(o, false); } catch { }
                }
            }
        }

        /// <summary>So sanh 2 anh tung pixel.</summary>
        static bool GiongHet(Bitmap a, Bitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            var r = new Rectangle(0, 0, a.Width, a.Height);
            var da = a.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var db = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int n = da.Stride * da.Height;
                var ba = new byte[n]; var bb = new byte[n];
                System.Runtime.InteropServices.Marshal.Copy(da.Scan0, ba, 0, n);
                System.Runtime.InteropServices.Marshal.Copy(db.Scan0, bb, 0, n);
                return ba.AsSpan().SequenceEqual(bb);
            }
            finally { a.UnlockBits(da); b.UnlockBits(db); }
        }

        TuThe? _daDat;          // tu the camera lan dat goc nhin gan nhat
        double _fovDaDat = double.NaN;

        static bool GanBang(TuThe a, TuThe b) =>
            (a.T - b.T).Length() < 0.01 && (a.TrucZ - b.TrucZ).Length() < 1e-5 && (a.TrucY - b.TrucY).Length() < 1e-5;

        /// <summary>
        /// Dat goc nhin cua GraphicControl theo tu the camera hien tai. Tra ve tu the da dung.
        /// batBuoc = false: bo qua neu tu the + FOV khong doi so voi lan truoc (tranh ve lai thua).
        /// </summary>
        public TuThe? CapNhatGocNhin(bool batBuoc = false)
        {
            if (!DaMo || LayTuTheCamera == null || LayMoHinh == null) return null;
            TuThe? tt = LayTuTheCamera();
            if (!tt.HasValue) return null;
            var cam = tt.Value;
            var mh = LayMoHinh();
            // GraphicControl/Camera.FieldOfView cua RobotStudio la goc nhin NGANG (do). Kiem chung bang 5 qua cau
            // (truoc day dat goc DOC -> anh bi phong ~W/H = 1.195 lan). Goc doc tu suy ra = 2*atan(H/2fy).
            double fov = mh.FovNgangDo;
            if (!batBuoc && _daDat.HasValue && GanBang(_daDat.Value, cam) && Math.Abs(fov - _fovDaDat) < 1e-9) return cam;

            // He camera giong OpenCV: Z nhin ve truoc, Y huong XUONG trong anh -> huong "len" = -Y
            Vec3 tu = cam.T;
            Vec3 vao = cam.T + cam.TrucZ * KhoangNhinMm;
            Vec3 len = -cam.TrucY;

            // Mot doi tuong Camera duy nhat, GAN vao GraphicControl (gc.Camera = cam) -> khung nhin do camera dieu khien.
            // SyncCamera(cam, TRUE, 0): khung nhin di chuyen TOI camera (false = nguoc lai, chep khung nhin vao camera).
            // Camera.SetView(lookAt, lookFrom, up) - chu y thu tu tham so. FieldOfView tinh bang DO.
            if (_cam == null)
            {
                _cam = new Camera();
                try { _cam.LockRotate = 0; _cam.LockTranslate = 0; _cam.LockZoom = 0; } catch { }   // khoa thao tac nguoi dung
            }
            _cam.SetView(ChuyenDoiRs.DiemMet(vao), ChuyenDoiRs.DiemMet(tu), ChuyenDoiRs.Huong(len));
            _cam.FieldOfView = fov;
            _gc.SyncCamera(_cam, true, 0f);
            if (!ReferenceEquals(_gc.Camera, _cam)) _gc.Camera = _cam;
            _daDat = cam; _fovDaDat = fov;
            try { DaCapNhat?.Invoke(cam); } catch { }
            return cam;
        }

        /// <summary>
        /// Chup anh W x H tu goc nhin camera. Truoc khi chup: dat lai goc nhin + ep ve lai
        /// de anh khong tre so voi tu the robot.
        /// </summary>
        public byte[] Chup(int rong, int cao, out TuThe camLucChup, out Size kichThuocThat)
        {
            camLucChup = TuThe.DonVi; kichThuocThat = Size.Empty;
            if (!DaMo) throw new InvalidOperationException("Cua so camera chua mo");
            var tt = CapNhatGocNhin(batBuoc: true);
            if (!tt.HasValue) throw new InvalidOperationException("Chua lay duoc tu the camera (station chua co robot?)");
            camLucChup = tt.Value;
            try { Station.UpdateGraphics(true); } catch { }
            try { _gc.Update(); } catch { }

            // NoShadows: RobotStudio chi tinh ban do bong SAU khung hinh dau -> anh dau tien thieu bong.
            // Bo bong de anh lap lai duoc va khong phu thuoc cai dat do hoa cua tung may.
            var tuyChon = ScreenshotOptions.HideTemporaryGraphics | ScreenshotOptions.HideCoordinateSystem
                        | ScreenshotOptions.HideButtons | ScreenshotOptions.NoHighlights | ScreenshotOptions.NoShadows;

            // Frame / tool frame / target gan tren mat bich lot vao anh (SetObjectVisible rieng cho cua so camera
            // khong an duoc) -> tam an tren toan station trong luc chup roi tra lai.
            var daAn = TamAnDoiTuongKhongPhaiVat();
            // Luc chup: muc chi tiet cao nhat, khong bo vat nho -> anh day du, lap lai duoc. Chup xong tra ve nhu cu.
            GraphicDetailLevel? chiTietCu = null; double? cullCu = null;
            try { chiTietCu = _gc.DetailLevel; _gc.DetailLevel = GraphicDetailLevel.Fine; } catch { }
            try { cullCu = _gc.ScreenSizeCulling; _gc.ScreenSizeCulling = 0; } catch { }
            try { return ChupOnDinh(rong, cao, tuyChon, out kichThuocThat); }
            finally
            {
                try { if (chiTietCu.HasValue) _gc.DetailLevel = chiTietCu.Value; } catch { }
                try { if (cullCu.HasValue) _gc.ScreenSizeCulling = cullCu.Value; } catch { }
                TraLaiHienThi(daAn);
            }
        }

        /// <summary>Tam an moi doi tuong KHONG phai vat the (frame, tooldata, target, workobject, task...) co thuoc tinh Visible.</summary>
        System.Collections.Generic.List<(object o, System.Reflection.PropertyInfo p)> TamAnDoiTuongKhongPhaiVat()
        {
            var ds = new System.Collections.Generic.List<(object, System.Reflection.PropertyInfo)>();
            var st = Station.ActiveStation;
            if (st == null) return ds;
            try { Project.UndoContext.SuspendUndoStep(); } catch { }
            foreach (var o in st.GetAllObjects())
            {
                if (o is GraphicComponent || o is Station) continue;     // vat the that (part, robot...) giu nguyen
                var p = o.GetType().GetProperty("Visible", typeof(bool));
                if (p == null || !p.CanRead || !p.CanWrite) continue;
                try { if ((bool)p.GetValue(o)) { p.SetValue(o, false); ds.Add((o, p)); } } catch { }
            }
            try { Station.UpdateGraphics(true); } catch { }
            return ds;
        }

        void TraLaiHienThi(System.Collections.Generic.List<(object o, System.Reflection.PropertyInfo p)> ds)
        {
            foreach (var (o, p) in ds) { try { p.SetValue(o, true); } catch { } }
            try { Project.UndoContext.ResumeUndoStep(); } catch { }
            try { Station.UpdateGraphics(true); } catch { }
            SoDoiTuongTamAn = ds.Count;
        }

        public int SoDoiTuongTamAn { get; private set; }

        byte[] ChupOnDinh(int rong, int cao, ScreenshotOptions tuyChon, out Size kichThuocThat)
        {
            // Render lien tiep cho toi khi 2 lan giong het tung pixel (toi da SoLanToiDa lan) -> anh on dinh, lap lai duoc
            Bitmap truoc = _gc.ScreenShot(rong, cao, tuyChon);
            Bitmap ketQua = null;
            SoLanRender = 1;
            try
            {
                for (int i = 1; i < SoLanToiDa; i++)
                {
                    var sau = _gc.ScreenShot(rong, cao, tuyChon);
                    SoLanRender++;
                    if (GiongHet(truoc, sau)) { truoc.Dispose(); ketQua = sau; break; }
                    truoc.Dispose();
                    truoc = sau;
                }
                OnDinh = ketQua != null;
                if (ketQua == null) { ketQua = truoc; truoc = null; }
                kichThuocThat = ketQua.Size;
                using (var ms = new MemoryStream())
                {
                    ketQua.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            finally { truoc?.Dispose(); if (!ReferenceEquals(ketQua, truoc)) ketQua?.Dispose(); }
        }

        const int SoLanToiDa = 4;
        /// <summary>So lan render cua lan chup gan nhat, va anh co on dinh (2 lan lien tiep giong het) khong.</summary>
        public int SoLanRender { get; private set; }
        public bool OnDinh { get; private set; }
    }
}
