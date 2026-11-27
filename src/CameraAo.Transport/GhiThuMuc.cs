using System.IO;
using System.Text;

namespace CameraAo.Transport
{
    /// <summary>Ket qua ghi mot anh: duong dan tuyet doi cua 3 file.</summary>
    public sealed class KetQuaGhi
    {
        public string Png { get; set; }
        public string Json { get; set; }
        public string Done { get; set; }
    }

    /// <summary>
    /// Ghi anh + metadata vao thu muc chung.
    /// Thu tu: PNG (ghi file tam roi doi ten) -> JSON (tam roi doi ten) -> file "&lt;ten&gt;.done".
    /// Ben nhan chi doc khi da thay file .done => khong bao gio doc phai file ghi do.
    /// </summary>
    public sealed class GhiThuMuc
    {
        public string ThuMuc { get; }
        public bool TaoFileDone { get; }

        public GhiThuMuc(string thuMuc, bool taoFileDone = true)
        {
            ThuMuc = thuMuc;
            TaoFileDone = taoFileDone;
        }

        public KetQuaGhi Ghi(string tenGoc, byte[] png, string json)
        {
            Directory.CreateDirectory(ThuMuc);
            var kq = new KetQuaGhi
            {
                Png = Path.Combine(ThuMuc, tenGoc + ".png"),
                Json = Path.Combine(ThuMuc, tenGoc + ".json"),
                Done = Path.Combine(ThuMuc, tenGoc + ".done"),
            };
            GhiNguyenTu(kq.Png, png);
            GhiNguyenTu(kq.Json, new UTF8Encoding(false).GetBytes(json));
            if (TaoFileDone)
                GhiNguyenTu(kq.Done, Encoding.ASCII.GetBytes(Path.GetFileName(kq.Png) + "\n" + Path.GetFileName(kq.Json) + "\n"));
            else kq.Done = null;
            return kq;
        }

        static void GhiNguyenTu(string duongDan, byte[] duLieu)
        {
            string tam = duongDan + ".tmp";
            File.WriteAllBytes(tam, duLieu);
            if (File.Exists(duongDan)) File.Delete(duongDan);
            File.Move(tam, duongDan);
        }
    }
}
