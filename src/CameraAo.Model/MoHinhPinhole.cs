using System;

namespace CameraAo.Model
{
    /// <summary>
    /// Mo hinh camera pinhole KHONG meo:
    ///   u = fx * X/Z + cx ,  v = fy * Y/Z + cy   (X, Y, Z trong he camera: Z nhin ve truoc, Y huong xuong anh)
    /// Pixel (0,0) la goc tren-trai cua anh; tam pixel dau tien nam o (0.5, 0.5).
    /// </summary>
    public sealed class MoHinhPinhole
    {
        public int Rong { get; }
        public int Cao { get; }
        public double Fx { get; }
        public double Fy { get; }
        public double Cx { get; }
        public double Cy { get; }

        public MoHinhPinhole(int rong, int cao, double fx, double fy, double cx, double cy)
        {
            if (rong <= 0 || cao <= 0) throw new ArgumentException("Kich thuoc anh phai > 0");
            if (fx <= 0 || fy <= 0) throw new ArgumentException("fx, fy phai > 0");
            Rong = rong; Cao = cao; Fx = fx; Fy = fy; Cx = cx; Cy = cy;
        }

        /// <summary>Goc nhin doc (do): 2*atan(H / (2*fy)) - dung khi tam anh o giua.</summary>
        public double FovDocDo => 2.0 * Math.Atan(Cao / (2.0 * Fy)) * 180.0 / Math.PI;

        /// <summary>Goc nhin ngang (do): 2*atan(W / (2*fx)).</summary>
        public double FovNgangDo => 2.0 * Math.Atan(Rong / (2.0 * Fx)) * 180.0 / Math.PI;

        /// <summary>Tam anh co nam o giua (cx = W/2, cy = H/2) trong sai so cho phep (pixel) khong.</summary>
        public bool TamAnhOGiua(double saiSoPx = 0.5) =>
            Math.Abs(Cx - Rong / 2.0) <= saiSoPx && Math.Abs(Cy - Cao / 2.0) <= saiSoPx;

        /// <summary>Kich thuoc vung nhin (mm) tren mat phang vuong goc truc quang, cach camera d mm.</summary>
        public (double rong, double cao) VungNhinMm(double khoangCachMm) =>
            (khoangCachMm * Rong / Fx, khoangCachMm * Cao / Fy);

        /// <summary>Chieu diem trong HE CAMERA (mm) ra pixel. null neu diem nam sau hoac tren mat phang camera.</summary>
        public (double u, double v)? Chieu(Vec3 pCam)
        {
            if (pCam.Z <= 1e-9) return null;
            return (Fx * pCam.X / pCam.Z + Cx, Fy * pCam.Y / pCam.Z + Cy);
        }

        /// <summary>Chieu diem trong HE WORLD (mm) ra pixel, biet tu the camera trong world.</summary>
        public (double u, double v)? ChieuTuWorld(Vec3 pWorld, TuThe cameraTrongWorld) =>
            Chieu(cameraTrongWorld.NghichDao().BienDoiDiem(pWorld));

        public bool TrongAnh(double u, double v) => u >= 0 && v >= 0 && u < Rong && v < Cao;
    }
}
