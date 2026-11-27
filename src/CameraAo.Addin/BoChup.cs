using System;
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
        Timer _timer;

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
                CapNhatGocNhin();
                return;
            }

            _gc = new GraphicControl();
            _gc.Dock = DockStyle.Fill;
            _gc.RootObject = st;
            try { _gc.ShowUCSGrid = false; _gc.ShowButtons = false; _gc.ShowKeyTips = false; } catch { }
            try { _gc.PickingEnabled = false; _gc.Enabled = false; } catch { }   // khoa chuot

            _cuaSo = new ToolWindow("CameraAo.CuaSo", _gc, tieuDe);
            _cuaSo.PreferredSize = new Size(640, 540);
            UIEnvironment.Windows.AddDocked(_cuaSo, DockStyle.Right);
            AnVatKhoiCamera(st);

            // Cap nhat xem truoc 10 lan/giay de bam theo robot
            _timer = new Timer { Interval = 100 };
            _timer.Tick += (s, e) => { try { if (_cuaSo != null && _cuaSo.Visible) CapNhatGocNhin(); } catch { } };
            _timer.Start();
            CapNhatGocNhin();
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
        }

        /// <summary>Dat goc nhin cua GraphicControl theo tu the camera hien tai. Tra ve tu the da dung.</summary>
        public TuThe? CapNhatGocNhin()
        {
            if (!DaMo || LayTuTheCamera == null || LayMoHinh == null) return null;
            TuThe? tt = LayTuTheCamera();
            if (!tt.HasValue) return null;
            var cam = tt.Value;
            var mh = LayMoHinh();

            // He camera giong OpenCV: Z nhin ve truoc, Y huong XUONG trong anh -> huong "len" = -Y
            Vec3 tu = cam.T;
            Vec3 vao = cam.T + cam.TrucZ * KhoangNhinMm;
            Vec3 len = -cam.TrucY;

            var c = new Camera();
            c.LookFrom = ChuyenDoiRs.DiemMet(tu);
            c.LookAt = ChuyenDoiRs.DiemMet(vao);
            c.UpDirection = ChuyenDoiRs.Huong(len);
            c.FieldOfView = mh.FovDocDo;                 // RobotStudio dung DO (mac dinh doc duoc 30.0)
            _gc.SyncCamera(c, false, 0f);
            _gc.FieldOfView = mh.FovDocDo;
            _gc.LookFrom = c.LookFrom;
            _gc.LookAt = c.LookAt;
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
            var tt = CapNhatGocNhin();
            if (!tt.HasValue) throw new InvalidOperationException("Chua lay duoc tu the camera (station chua co robot?)");
            camLucChup = tt.Value;
            try { Station.UpdateGraphics(true); } catch { }
            try { _gc.Update(); } catch { }

            var tuyChon = ScreenshotOptions.HideTemporaryGraphics | ScreenshotOptions.HideCoordinateSystem | ScreenshotOptions.HideButtons;
            using (var bmp = _gc.ScreenShot(rong, cao, tuyChon))
            {
                kichThuocThat = bmp.Size;
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }
    }
}
