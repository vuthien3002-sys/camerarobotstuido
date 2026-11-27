using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace CameraAo.Transport
{
    /// <summary>
    /// Gui thong bao "co anh moi" toi chuong trinh ben ngoai qua TCP va cho xac nhan.
    /// Giao thuc (1 dong UTF-8 moi chieu):
    ///   gui :  {"chi_so":7,"camera":"CAM1","png":"...","json":"...","done":"..."}\n
    ///   nhan:  "ACK 7\n"   (hoac "NACK 7 &lt;ly do&gt;\n")
    /// Moi anh mot ket noi moi -> don gian, ben nhan khoi dong lai luc nao cung duoc.
    /// </summary>
    public sealed class GuiSocket
    {
        public string May { get; }
        public int Cong { get; }
        public int ChoAckMs { get; }

        public GuiSocket(string may, int cong, int choAckMs = 2000)
        {
            May = may; Cong = cong; ChoAckMs = choAckMs;
        }

        /// <summary>Gui va cho ACK. Tra ve true neu ben nhan xac nhan dung chi so.</summary>
        public bool Gui(int chiSo, string camera, KetQuaGhi f, out string thongBao)
        {
            try
            {
                using var tcp = new TcpClient();
                // KET NOI CO HUY THEO THOI GIAN va LUON doc ket qua cua tac vu ket noi.
                // (Loi cu: Wait(500) roi bo -> tac vu ket noi loi sau do ma khong ai doc ->
                //  "unobserved task exception" -> RobotStudio bao Fatal Error.)
                using (var huy = new CancellationTokenSource(Math.Min(500, ChoAckMs)))
                {
                    try
                    {
                        tcp.ConnectAsync(May, Cong, huy.Token).AsTask().GetAwaiter().GetResult();
                    }
                    catch (Exception ex) when (ex is OperationCanceledException || ex is SocketException)
                    {
                        thongBao = $"khong ket noi duoc {May}:{Cong} (chua chay chuong trinh nhan?) - {ex.GetType().Name}";
                        return false;
                    }
                }
                if (!tcp.Connected)
                {
                    thongBao = $"khong ket noi duoc {May}:{Cong}";
                    return false;
                }
                tcp.ReceiveTimeout = ChoAckMs;
                tcp.SendTimeout = ChoAckMs;
                using var luong = tcp.GetStream();
                string dong = JsonSerializer.Serialize(new
                {
                    chi_so = chiSo, camera, png = f.Png, json = f.Json, done = f.Done,
                }) + "\n";
                var byteGui = Encoding.UTF8.GetBytes(dong);
                luong.Write(byteGui, 0, byteGui.Length);
                luong.Flush();

                using var doc = new StreamReader(luong, Encoding.UTF8);
                string traLoi = doc.ReadLine() ?? "";
                if (traLoi.Trim() == "ACK " + chiSo) { thongBao = "ACK"; return true; }
                thongBao = "tra loi khong hop le: '" + traLoi + "'";
                return false;
            }
            catch (Exception ex)
            {
                thongBao = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }
    }
}
