using System;
using CameraAo.Model;

namespace CameraAo.Model.Tests
{
    public class TuTheTests
    {
        static void GanBang(Vec3 a, Vec3 b, int soLe = 9)
        {
            Assert.Equal(a.X, b.X, soLe); Assert.Equal(a.Y, b.Y, soLe); Assert.Equal(a.Z, b.Z, soLe);
        }

        [Fact]
        public void Xoay90QuanhZ_TrucXThanhTrucY()
        {
            var t = TuThe.TuXyzRpy(0, 0, 0, 0, 0, 90);
            GanBang(new Vec3(0, 1, 0), t.TrucX);
            GanBang(new Vec3(-1, 0, 0), t.TrucY);
            GanBang(new Vec3(0, 0, 1), t.TrucZ);
        }

        [Fact]
        public void NhanVoiNghichDao_RaDonVi()
        {
            var t = TuThe.TuXyzRpy(123, -45, 678, 10, -20, 35);
            var p = new Vec3(5, 6, 7);
            GanBang(p, t.Nhan(t.NghichDao()).BienDoiDiem(p));
            GanBang(p, t.NghichDao().Nhan(t).BienDoiDiem(p));
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(10, -20, 35)]
        [InlineData(-170, 45, 90)]
        [InlineData(90, 0, 0)]
        public void GocDo_KhuHoi(double rx, double ry, double rz)
        {
            var t = TuThe.TuXyzRpy(1, 2, 3, rx, ry, rz);
            var (a, b, c) = t.GocDo();
            var t2 = TuThe.TuXyzRpy(1, 2, 3, a, b, c);
            GanBang(t.TrucX, t2.TrucX); GanBang(t.TrucY, t2.TrucY); GanBang(t.TrucZ, t2.TrucZ);
        }

        [Fact]
        public void Quaternion_DonViVaXoay90Z()
        {
            Assert.Equal(new[] { 1.0, 0, 0, 0 }, TuThe.DonVi.Quaternion());
            var q = TuThe.TuXyzRpy(0, 0, 0, 0, 0, 90).Quaternion();
            Assert.Equal(Math.Sqrt(0.5), q[0], 9);
            Assert.Equal(0, q[1], 9);
            Assert.Equal(0, q[2], 9);
            Assert.Equal(Math.Sqrt(0.5), q[3], 9);
        }

        [Fact]
        public void CameraBangMatBichNhanOffset_EyeInHand()
        {
            // Mat bich o (500, 0, 800) mm, chi xuong (Z mat bich = -Z world); camera lech 100 mm theo Z mat bich, 50 mm theo X
            var matBich = TuThe.TuXyzRpy(500, 0, 800, 180, 0, 0);
            var flangeToCam = TuThe.TuXyzRpy(50, 0, 100, 0, 0, 0);
            var cam = matBich.Nhan(flangeToCam);
            GanBang(new Vec3(550, 0, 700), cam.T);
            GanBang(new Vec3(0, 0, -1), cam.TrucZ);
        }
    }
}
