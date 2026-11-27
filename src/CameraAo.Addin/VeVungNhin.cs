using System.Collections.Generic;
using System.Drawing;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using CameraAo.Model;

namespace CameraAo.Addin
{
    /// <summary>
    /// Ve hinh chop vung nhin cua camera trong cac khung nhin KHAC cua so camera (View1...).
    /// Canh do dam = canh tren cua anh. Chi ve lai khi tu the thay doi.
    /// </summary>
    internal sealed class VeVungNhin
    {
        readonly List<TemporaryGraphic> _hinh = new List<TemporaryGraphic>();
        TuThe? _lanTruoc;

        public double KhoangCachMm { get; set; } = 300;

        public void Ve(Station st, GraphicControl gcCamera, TuThe cam, MoHinhPinhole mh, string ten)
        {
            if (_lanTruoc.HasValue && GanBang(_lanTruoc.Value, cam)) return;
            Xoa();
            var ds = new List<TemporaryGraphicCollection>();
            foreach (var g in GraphicControl.GetAllGraphicControls())
                if (g != gcCamera && g.TemporaryGraphics != null && !ds.Contains(g.TemporaryGraphics)) ds.Add(g.TemporaryGraphics);
            if (ds.Count == 0 && st != null) ds.Add(st.TemporaryGraphics);

            // 4 goc anh chieu nguoc ra khong gian tai khoang cach d (dung ca cx, cy)
            double d = KhoangCachMm;
            var gocPx = new[] { (0.0, 0.0), (mh.Rong, 0.0), (mh.Rong, (double)mh.Cao), (0.0, (double)mh.Cao) };
            var goc = new Vec3[4];
            for (int i = 0; i < 4; i++)
            {
                var (u, v) = gocPx[i];
                var pCam = new Vec3((u - mh.Cx) / mh.Fx * d, (v - mh.Cy) / mh.Fy * d, d);
                goc[i] = cam.BienDoiDiem(pCam);
            }
            var O = ChuyenDoiRs.DiemMet(cam.T);
            foreach (var tg in ds)
            {
                for (int i = 0; i < 4; i++)
                {
                    _hinh.Add(tg.DrawLine(O, ChuyenDoiRs.DiemMet(goc[i]), 2, Color.Orange));
                    _hinh.Add(tg.DrawLine(ChuyenDoiRs.DiemMet(goc[i]), ChuyenDoiRs.DiemMet(goc[(i + 1) % 4]), 2, Color.Orange));
                }
                _hinh.Add(tg.DrawLine(ChuyenDoiRs.DiemMet(goc[0]), ChuyenDoiRs.DiemMet(goc[1]), 4, Color.Red));
                _hinh.Add(tg.DrawText(O, ten));
            }
            foreach (var h in _hinh) { try { h.Pickable = false; } catch { } }
            _lanTruoc = cam;
        }

        public void Xoa()
        {
            foreach (var h in _hinh) { try { if (!h.IsDeleted) h.Delete(); } catch { } }
            _hinh.Clear();
            _lanTruoc = null;
        }

        static bool GanBang(TuThe a, TuThe b) =>
            (a.T - b.T).Length() < 0.01 && (a.TrucZ - b.TrucZ).Length() < 1e-5 && (a.TrucY - b.TrucY).Length() < 1e-5;
    }
}
