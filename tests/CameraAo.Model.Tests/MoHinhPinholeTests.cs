using System;
using CameraAo.Model;

namespace CameraAo.Model.Tests
{
    public class MoHinhPinholeTests
    {
        // Thong so mac dinh: Basler acA2440 (3.45 um) + ong kinh 12 mm -> fx = fy = 12 / 0.00345 = 3478.3 px
        static MoHinhPinhole MacDinh() => new MoHinhPinhole(2448, 2048, 3478.3, 3478.3, 1224, 1024);

        [Fact]
        public void FovDoc_DungCongThuc_2atan_H_chia_2fy()
        {
            var mh = MacDinh();
            double kyVong = 2 * Math.Atan(2048 / (2 * 3478.3)) * 180 / Math.PI;
            Assert.Equal(kyVong, mh.FovDocDo, 9);
            Assert.Equal(32.81, mh.FovDocDo, 2);
        }

        [Fact]
        public void FovNgang_VaCheo_KhopThongSoOngKinh()
        {
            var mh = MacDinh();
            Assert.Equal(38.77, mh.FovNgangDo, 2);
            // FOV cheo cua ong 12 mm tren cam bien 2/3" ~ 49.3 do (B&H cong bo 49.2 do)
            double cheo = 2 * Math.Atan(Math.Sqrt(2448.0 * 2448 + 2048.0 * 2048) / (2 * 3478.3)) * 180 / Math.PI;
            Assert.InRange(cheo, 49.0, 49.6);
        }

        [Fact]
        public void Chieu_DiemTrenTrucQuang_RoiVaoTamAnh()
        {
            var p = MacDinh().Chieu(new Vec3(0, 0, 500));
            Assert.NotNull(p);
            Assert.Equal(1224, p.Value.u, 9);
            Assert.Equal(1024, p.Value.v, 9);
        }

        [Fact]
        public void Chieu_DiemLech_TinhTay()
        {
            // u = fx * X/Z + cx = 3478.3 * 0.1 + 1224 ; v = 3478.3 * 0.05 + 1024
            var p = MacDinh().Chieu(new Vec3(100, 50, 1000)).Value;
            Assert.Equal(1571.83, p.u, 6);
            Assert.Equal(1197.915, p.v, 6);
        }

        [Fact]
        public void Chieu_DiemSauCamera_TraVeNull()
        {
            Assert.Null(MacDinh().Chieu(new Vec3(10, 10, -5)));
            Assert.Null(MacDinh().Chieu(new Vec3(10, 10, 0)));
        }

        [Fact]
        public void Chieu_TamAnhLech_DichDungCxCy()
        {
            var mh = new MoHinhPinhole(2448, 2048, 3478.3, 3478.3, 1230.5, 1019.0);
            var p = mh.Chieu(new Vec3(0, 0, 300)).Value;
            Assert.Equal(1230.5, p.u, 9);
            Assert.Equal(1019.0, p.v, 9);
            Assert.False(mh.TamAnhOGiua());
            Assert.True(MacDinh().TamAnhOGiua());
        }

        [Fact]
        public void VungNhin_O300mm()
        {
            var (r, c) = MacDinh().VungNhinMm(300);
            Assert.Equal(211.14, r, 2);
            Assert.Equal(176.64, c, 2);
        }

        [Fact]
        public void TrongAnh_KiemTraBien()
        {
            var mh = MacDinh();
            Assert.True(mh.TrongAnh(0, 0));
            Assert.True(mh.TrongAnh(2447.9, 2047.9));
            Assert.False(mh.TrongAnh(2448, 10));
            Assert.False(mh.TrongAnh(-0.1, 10));
        }

        [Fact]
        public void ThongSoSai_NemLoi()
        {
            Assert.Throws<ArgumentException>(() => new MoHinhPinhole(0, 100, 10, 10, 0, 0));
            Assert.Throws<ArgumentException>(() => new MoHinhPinhole(100, 100, -1, 10, 0, 0));
        }

        [Fact]
        public void ChieuTuWorld_CameraNhinThangXuong()
        {
            // Camera o (0,0,1000) mm nhin thang xuong: Z_cam = -Z_world, X_cam = X_world, Y_cam = -Y_world
            var cam = new TuThe(new Vec3(1, 0, 0), new Vec3(0, -1, 0), new Vec3(0, 0, -1), new Vec3(0, 0, 1000));
            var mh = MacDinh();
            var p = mh.ChieuTuWorld(new Vec3(100, 0, 0), cam).Value;   // cach camera 1000 mm, lech 100 mm theo X
            Assert.Equal(1224 + 347.83, p.u, 6);
            Assert.Equal(1024, p.v, 6);
            var q = mh.ChieuTuWorld(new Vec3(0, 50, 0), cam).Value;    // +Y world -> len tren anh (v nho hon)
            Assert.Equal(1024 - 173.915, q.v, 6);
        }
    }
}
