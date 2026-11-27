// BoNhanAnh - chuong trinh ben ngoai nhan anh tu add-in "Camera ao".
//
// Che do TCP (mac dinh):
//   dotnet run --project tools/BoNhanAnh -- --cong 50555 --so-anh 20
//   -> nghe 127.0.0.1:<cong>, moi thong bao: kiem tra file PNG/JSON/.done, tra "ACK n" hoac "NACK n ly_do".
// Che do thu muc:
//   dotnet run --project tools/BoNhanAnh -- --thu-muc <thu muc phien anh> --so-anh 20
//   -> kiem tra moi cap anh da co file .done.
// Ket thuc: in tong ket (nhan duoc / thieu / trung / loi) va tra ma thoat 0 neu du va khong loi.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CameraAo.Model;

static class Program
{
    static readonly SortedDictionary<int, string> DaNhan = new SortedDictionary<int, string>();
    static readonly List<string> Loi = new List<string>();
    static int SoTrung;

    static int Main(string[] args)
    {
        int cong = 50555, soAnh = 20;
        string thuMuc = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--cong") cong = int.Parse(args[++i]);
            else if (args[i] == "--so-anh") soAnh = int.Parse(args[++i]);
            else if (args[i] == "--thu-muc") thuMuc = args[++i];
        }
        Console.OutputEncoding = Encoding.UTF8;
        return thuMuc != null ? CheDoThuMuc(thuMuc, soAnh) : CheDoTcp(cong, soAnh);
    }

    static int CheDoTcp(int cong, int soAnh)
    {
        var nghe = new TcpListener(IPAddress.Loopback, cong);
        nghe.Start();
        Console.WriteLine($"[BoNhanAnh] Dang nghe 127.0.0.1:{cong}, cho {soAnh} anh... (Ctrl+C de dung)");
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; TongKet(soAnh); Environment.Exit(DaNhan.Count == soAnh && Loi.Count == 0 ? 0 : 1); };
        while (DaNhan.Count < soAnh)
        {
            using var c = nghe.AcceptTcpClient();
            using var luong = c.GetStream();
            var doc = new StreamReader(luong, Encoding.UTF8);
            string dong = doc.ReadLine();
            if (dong == null) continue;
            string traLoi = XuLyThongBao(dong);
            var b = Encoding.UTF8.GetBytes(traLoi + "\n");
            luong.Write(b, 0, b.Length);
        }
        nghe.Stop();
        return TongKet(soAnh);
    }

    static string XuLyThongBao(string dong)
    {
        int chiSo = -1;
        try
        {
            using var tb = JsonDocument.Parse(dong);
            chiSo = tb.RootElement.GetProperty("chi_so").GetInt32();
            string png = tb.RootElement.GetProperty("png").GetString();
            string json = tb.RootElement.GetProperty("json").GetString();
            string done = tb.RootElement.TryGetProperty("done", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            string loi = KiemTraCapAnh(png, json, done, out string moTa);
            if (loi != null) { Loi.Add($"#{chiSo}: {loi}"); Console.WriteLine($"  NACK #{chiSo}: {loi}"); return $"NACK {chiSo} {loi}"; }
            if (DaNhan.ContainsKey(chiSo)) { SoTrung++; Console.WriteLine($"  TRUNG #{chiSo}"); }
            DaNhan[chiSo] = png;
            Console.WriteLine($"  ACK  #{chiSo}: {moTa}");
            return $"ACK {chiSo}";
        }
        catch (Exception ex)
        {
            Loi.Add($"#{chiSo}: thong bao loi - {ex.Message}");
            return $"NACK {chiSo} {ex.Message}";
        }
    }

    static int CheDoThuMuc(string thuMuc, int soAnh)
    {
        Console.WriteLine($"[BoNhanAnh] Kiem tra thu muc {thuMuc}");
        foreach (var done in Directory.GetFiles(thuMuc, "*.done").OrderBy(f => f))
        {
            string goc = Path.Combine(thuMuc, Path.GetFileNameWithoutExtension(done));
            string loi = KiemTraCapAnh(goc + ".png", goc + ".json", done, out string moTa);
            int chiSo = -1;
            try { using var j = JsonDocument.Parse(File.ReadAllText(goc + ".json")); chiSo = j.RootElement.GetProperty("chi_so").GetInt32(); } catch { }
            if (loi != null) { Loi.Add($"{Path.GetFileName(goc)}: {loi}"); continue; }
            if (DaNhan.ContainsKey(chiSo)) SoTrung++;
            DaNhan[chiSo] = goc + ".png";
            Console.WriteLine($"  OK #{chiSo}: {moTa}");
        }
        return TongKet(soAnh);
    }

    /// <summary>Tra ve null neu hop le, nguoc lai la ly do loi.</summary>
    static string KiemTraCapAnh(string png, string json, string done, out string moTa)
    {
        moTa = "";
        if (done != null && !File.Exists(done)) return "chua co file .done";
        if (!File.Exists(png)) return "khong thay PNG";
        if (!File.Exists(json)) return "khong thay JSON";
        if (!DocKichThuocPng(png, out int w, out int h)) return "PNG hong";
        string noiDung = File.ReadAllText(json);
        var thieu = MetadataAnh.TruongThieu(noiDung);
        if (thieu.Count > 0) return "JSON thieu truong: " + string.Join(",", thieu);
        using var j = JsonDocument.Parse(noiDung);
        int jw = j.RootElement.GetProperty("anh").GetProperty("rong").GetInt32();
        int jh = j.RootElement.GetProperty("anh").GetProperty("cao").GetInt32();
        if (jw != w || jh != h) return $"kich thuoc PNG {w}x{h} khac JSON {jw}x{jh}";
        var vt = j.RootElement.GetProperty("camera_trong_world").GetProperty("vi_tri_mm");
        moTa = $"{Path.GetFileName(png)} {w}x{h}, camera tai ({vt[0].GetDouble():F1}, {vt[1].GetDouble():F1}, {vt[2].GetDouble():F1}) mm";
        return null;
    }

    /// <summary>Doc rong/cao tu chunk IHDR cua file PNG.</summary>
    static bool DocKichThuocPng(string f, out int w, out int h)
    {
        w = h = 0;
        var b = new byte[24];
        using (var s = File.OpenRead(f)) if (s.Read(b, 0, 24) < 24) return false;
        byte[] sig = { 137, 80, 78, 71, 13, 10, 26, 10 };
        for (int i = 0; i < 8; i++) if (b[i] != sig[i]) return false;
        w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
        h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
        return true;
    }

    static int TongKet(int soAnh)
    {
        var thieu = Enumerable.Range(1, soAnh).Where(i => !DaNhan.ContainsKey(i)).ToList();
        Console.WriteLine();
        Console.WriteLine($"===== TONG KET: nhan {DaNhan.Count}/{soAnh} anh, trung {SoTrung}, loi {Loi.Count} =====");
        if (thieu.Count > 0) Console.WriteLine("Thieu chi so: " + string.Join(", ", thieu));
        foreach (var l in Loi) Console.WriteLine("LOI " + l);
        bool dat = DaNhan.Count == soAnh && thieu.Count == 0 && SoTrung == 0 && Loi.Count == 0;
        Console.WriteLine(dat ? "KET QUA: DAT" : "KET QUA: CHUA DAT");
        return dat ? 0 : 1;
    }
}
