using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CameraAo.Transport;

namespace CameraAo.Model.Tests
{
    public class TransportTests
    {
        [Fact]
        public void GhiThuMuc_TaoDu3File_KhongConFileTam()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "cameraao_ghi_" + Guid.NewGuid().ToString("N"));
            try
            {
                var kq = new GhiThuMuc(tmp).Ghi("CAM1_000001", new byte[] { 1, 2, 3 }, "{\"chi_so\":1}");
                Assert.True(File.Exists(kq.Png));
                Assert.True(File.Exists(kq.Json));
                Assert.True(File.Exists(kq.Done));
                Assert.Empty(Directory.GetFiles(tmp, "*.tmp"));
                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(kq.Png));
            }
            finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
        }

        [Fact]
        public async Task GuiSocket_NhanDuocAck()
        {
            var nghe = new TcpListener(IPAddress.Loopback, 0);
            nghe.Start();
            int cong = ((IPEndPoint)nghe.LocalEndpoint).Port;
            string nhanDuoc = null;
            var ben = Task.Run(() =>
            {
                using var c = nghe.AcceptTcpClient();
                using var s = c.GetStream();
                var r = new StreamReader(s, Encoding.UTF8);
                nhanDuoc = r.ReadLine();
                var ack = Encoding.UTF8.GetBytes("ACK 5\n");
                s.Write(ack, 0, ack.Length);
            });
            bool ok = new GuiSocket("127.0.0.1", cong).Gui(5, "CAM1", new KetQuaGhi { Png = "a.png", Json = "a.json", Done = "a.done" }, out string tb);
            await Task.WhenAny(ben, Task.Delay(3000));
            nghe.Stop();
            Assert.True(ok, tb);
            Assert.Contains("\"chi_so\":5", nhanDuoc);
        }

        [Fact]
        public void GuiSocket_KhongCoBenNhan_TraVeFalseNhanh()
        {
            // Lay mot cong trong roi dong lai -> chac chan khong co ai nghe
            var tam = new TcpListener(IPAddress.Loopback, 0); tam.Start();
            int cong = ((IPEndPoint)tam.LocalEndpoint).Port; tam.Stop();
            var dh = System.Diagnostics.Stopwatch.StartNew();
            bool ok = new GuiSocket("127.0.0.1", cong, 2000).Gui(1, "CAM1", new KetQuaGhi(), out _);
            Assert.False(ok);
            Assert.True(dh.ElapsedMilliseconds < 3000);
        }
    }
}
