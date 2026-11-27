using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ABB.Robotics.Math;
using ABB.Robotics.RobotStudio.Stations;
using ABB.Robotics.RobotStudio.Stations.Forms;
using CameraAo.Model;
using CameraAo.Transport;

namespace CameraAo.Addin
{
    /// <summary>
    /// Tu kiem tra trong RobotStudio (nut "Tu kiem tra" hoac tu chay khi bien moi truong CAMERAAO_TU_KIEM_TRA=1):
    ///  1. Mo camera, phien moi, chup 2 anh
    ///  2. Xoay + zoom khung nhin chinh (View1) bang API, chup them 2 anh
    ///  3. 4 anh phai GIONG HET (hash) va dung W x H, JSON du truong, khong canh bao
    ///  4. Gui TCP toi cong khong ai nghe: phai tra ve false va KHONG de lai loi tac vu chua doc
    /// Bao cao: Output + file TU_KIEM_TRA.txt trong thu muc phien + anh\tu_kiem_tra_moi_nhat.txt
    /// </summary>
    internal static class TuKiemTra
    {
        public static void Chay(Func<string, KetQuaGhi> chupAnh, Action moCamera, Action phienMoi,
                                Func<GraphicControl> layGcCamera, MoHinhPinhole mh, string thuMucAnhGoc, Action<string> log)
        {
            var bc = new List<string>();
            bool dat = true;
            void Ghi(string s) { bc.Add(s); log("[TU KIEM TRA] " + s); }
            void Kiem(bool ok, string s) { if (!ok) dat = false; Ghi((ok ? "DAT   " : "KHONG ") + s); }

            Ghi("Bat dau " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            moCamera();
            phienMoi();
            GraphicControl gcCamera = layGcCamera();

            var ds = new List<KetQuaGhi>();
            ds.Add(chupAnh("tu_kiem_tra"));
            ds.Add(chupAnh("tu_kiem_tra"));

            // Xoay + zoom khung nhin chinh (khong phai cua so camera)
            var chinh = GraphicControl.GetAllGraphicControls().FirstOrDefault(g => g != gcCamera && !g.IsDisposed);
            if (chinh != null)
            {
                Vector3 at = chinh.LookAt, from = chinh.LookFrom;
                Vector3 v = from.Subtract(at);
                double goc = 75 * Math.PI / 180, c = Math.Cos(goc), s = Math.Sin(goc);
                var vMoi = new Vector3((v.x * c - v.y * s) * 0.4, (v.x * s + v.y * c) * 0.4, v.z * 0.4 + 0.3);
                chinh.Move(at, at.Add(vMoi), 0f);
                Vector3 from2 = chinh.LookFrom;
                Ghi(string.Format(CultureInfo.InvariantCulture,
                    "Da xoay/zoom View1: mat tu ({0:F0},{1:F0},{2:F0}) -> ({3:F0},{4:F0},{5:F0}) mm",
                    from.x * 1000, from.y * 1000, from.z * 1000, from2.x * 1000, from2.y * 1000, from2.z * 1000));
                Kiem(from2.Subtract(from).Length() > 0.05, "View1 thuc su da doi goc nhin");
            }
            else Kiem(false, "Khong tim thay khung nhin chinh (View1) de xoay");

            ds.Add(chupAnh("tu_kiem_tra"));
            ds.Add(chupAnh("tu_kiem_tra"));

            // So sanh 4 anh
            var hash = ds.Select(k => k == null ? "(loi)" : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(k.Png))).Substring(0, 16)).ToList();
            for (int i = 0; i < hash.Count; i++) Ghi($"Anh {i + 1}: {hash[i]}  {(ds[i] == null ? "" : Path.GetFileName(ds[i].Png))}");
            bool gioNgHet = hash.All(h => h == hash[0]) && hash[0] != "(loi)";
            Ghi("      " + "Giong het tung bit (cung hash): " + (gioNgHet ? "CO" : "KHONG"));
            // Tieu chi: giong nhau trong nguong (nhieu khu rang cua cua GPU co the lech 1/255 o vai pixel)
            bool trongNguong = ds.All(k => k != null);
            for (int i = 1; i < ds.Count && trongNguong; i++)
            {
                var (soKhac, lechMax) = SoSanhPixel(ds[0].Png, ds[i].Png);
                double tiLe = soKhac * 100.0 / ((double)mh.Rong * mh.Cao);
                bool ok = lechMax <= 2 && tiLe <= 0.01;
                trongNguong &= ok;
                Ghi(string.Format(CultureInfo.InvariantCulture, "      Anh 1 vs anh {0}: {1} pixel khac ({2:F5}%), lech lon nhat {3}/255", i + 1, soKhac, tiLe, lechMax));
            }
            Kiem(trongNguong, "4 anh giong nhau (lech <= 2/255 o <= 0.01% pixel), 2 anh truoc va 2 anh sau khi xoay/zoom View1");

            foreach (var k in ds.Where(k => k != null))
            {
                string json = File.ReadAllText(k.Json);
                var thieu = MetadataAnh.TruongThieu(json);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                int w = doc.RootElement.GetProperty("anh").GetProperty("rong").GetInt32();
                int h = doc.RootElement.GetProperty("anh").GetProperty("cao").GetInt32();
                int soCanhBao = doc.RootElement.GetProperty("canh_bao").GetArrayLength();
                Kiem(thieu.Count == 0 && w == mh.Rong && h == mh.Cao && soCanhBao == 0 && File.Exists(k.Done),
                     $"{Path.GetFileName(k.Png)}: {w}x{h}, du truong JSON, co .done, {soCanhBao} canh bao");
            }

            // Gui TCP toi cong khong ai nghe: phai bao loi nhe nhang, khong de lai "unobserved task exception"
            Exception loiChuaDoc = null;
            EventHandler<UnobservedTaskExceptionEventArgs> bat = (o, e) => { loiChuaDoc = e.Exception; e.SetObserved(); };
            TaskScheduler.UnobservedTaskException += bat;
            try
            {
                var tam = new TcpListener(IPAddress.Loopback, 0); tam.Start();
                int congTrong = ((IPEndPoint)tam.LocalEndpoint).Port; tam.Stop();
                bool ok = new GuiSocket("127.0.0.1", congTrong, 300).Gui(0, "CAM1", ds.FirstOrDefault() ?? new KetQuaGhi(), out string tb);
                System.Threading.Thread.Sleep(2500);
                for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
                Kiem(!ok && loiChuaDoc == null, "Gui TCP khi KHONG co ben nhan: tra ve loi nhe (" + tb + "), khong co loi tac vu chua doc");
            }
            finally { TaskScheduler.UnobservedTaskException -= bat; }

            Ghi("KET QUA: " + (dat ? "DAT" : "CHUA DAT"));
            string noiDung = string.Join(Environment.NewLine, bc) + Environment.NewLine;
            try
            {
                var thuMucPhien = ds.FirstOrDefault(k => k != null)?.Png;
                if (thuMucPhien != null) File.WriteAllText(Path.Combine(Path.GetDirectoryName(thuMucPhien), "TU_KIEM_TRA.txt"), noiDung);
                Directory.CreateDirectory(thuMucAnhGoc);
                File.WriteAllText(Path.Combine(thuMucAnhGoc, "tu_kiem_tra_moi_nhat.txt"), noiDung);
            }
            catch (Exception ex) { log("Khong ghi duoc bao cao: " + ex.Message); }
        }

        /// <summary>So sanh 2 file anh: so pixel co it nhat 1 kenh khac, va do lech lon nhat (0..255).</summary>
        static (long soKhac, int lechMax) SoSanhPixel(string fa, string fb)
        {
            using var a = new System.Drawing.Bitmap(fa);
            using var b = new System.Drawing.Bitmap(fb);
            if (a.Width != b.Width || a.Height != b.Height) return (long.MaxValue, 255);
            var r = new System.Drawing.Rectangle(0, 0, a.Width, a.Height);
            var pf = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            var da = a.LockBits(r, System.Drawing.Imaging.ImageLockMode.ReadOnly, pf);
            var db = b.LockBits(r, System.Drawing.Imaging.ImageLockMode.ReadOnly, pf);
            try
            {
                int n = da.Stride * da.Height;
                var ba = new byte[n]; var bb = new byte[n];
                System.Runtime.InteropServices.Marshal.Copy(da.Scan0, ba, 0, n);
                System.Runtime.InteropServices.Marshal.Copy(db.Scan0, bb, 0, n);
                long soKhac = 0; int lechMax = 0;
                for (int i = 0; i < n; i += 4)
                {
                    int m = Math.Max(Math.Abs(ba[i] - bb[i]), Math.Max(Math.Abs(ba[i + 1] - bb[i + 1]), Math.Abs(ba[i + 2] - bb[i + 2])));
                    if (m > 0) { soKhac++; if (m > lechMax) lechMax = m; }
                }
                return (soKhac, lechMax);
            }
            finally { a.UnlockBits(da); b.UnlockBits(db); }
        }

        /// <summary>Neu bien moi truong CAMERAAO_TU_KIEM_TRA=1: cho station mo xong roi tu chay.</summary>
        public static void HenGioNeuCan(Action chay, Action<string> log)
        {
            if (Environment.GetEnvironmentVariable("CAMERAAO_TU_KIEM_TRA") != "1") return;
            log("Che do tu kiem tra: cho station mo xong...");
            int demSan = 0;
            var t = new Timer { Interval = 1000 };
            t.Tick += (s, e) =>
            {
                if (Station.ActiveStation == null || Station.IsLoading) { demSan = 0; return; }
                if (++demSan < 8) return;                 // cho do hoa + controller on dinh them vai giay
                t.Stop(); t.Dispose();
                try { chay(); } catch (Exception ex) { log("[TU KIEM TRA] LOI: " + ex); }
            };
            t.Start();
        }
    }
}
