using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CameraAo.Model
{
    /// <summary>Thong tin di kem moi anh chup, ghi ra file JSON cung ten voi anh.</summary>
    public sealed class MetadataAnh
    {
        /// <summary>Cac truong bat buoc co trong JSON (dung de kiem tra o phia nhan).</summary>
        public static readonly string[] TruongBatBuoc =
        {
            "chi_so", "ten_anh", "camera", "thoi_gian", "thoi_gian_mo_phong_s", "nguon_trigger",
            "anh", "intrinsic", "camera_trong_world",
        };

        public int ChiSo { get; set; }
        public string TenAnh { get; set; } = "";
        public string Camera { get; set; } = "";
        public DateTime ThoiGian { get; set; } = DateTime.Now;
        /// <summary>Thoi gian mo phong (s); null neu luc chup khong mo phong.</summary>
        public double? ThoiGianMoPhongS { get; set; }
        public string NguonTrigger { get; set; } = "nut";
        public int Rong { get; set; }
        public int Cao { get; set; }
        public MoHinhPinhole MoHinh { get; set; }
        public double[] Dist { get; set; } = new double[5];
        public TuThe CameraTrongWorld { get; set; } = TuThe.DonVi;
        public TuThe? MatBichTrongWorld { get; set; }
        public TuThe? FlangeToCamera { get; set; }
        public string Robot { get; set; }
        public double[] GocKhopDo { get; set; }
        public List<string> CanhBao { get; set; } = new List<string>();

        public string TaoJson()
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteNumber("chi_so", ChiSo);
                w.WriteString("ten_anh", TenAnh);
                w.WriteString("camera", Camera);
                w.WriteString("thoi_gian", ThoiGian.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
                if (ThoiGianMoPhongS.HasValue) w.WriteNumber("thoi_gian_mo_phong_s", Math.Round(ThoiGianMoPhongS.Value, 4));
                else w.WriteNull("thoi_gian_mo_phong_s");
                w.WriteString("nguon_trigger", NguonTrigger);

                w.WriteStartObject("anh");
                w.WriteNumber("rong", Rong); w.WriteNumber("cao", Cao);
                w.WriteEndObject();

                w.WriteStartObject("intrinsic");
                if (MoHinh != null)
                {
                    w.WriteNumber("fx", MoHinh.Fx); w.WriteNumber("fy", MoHinh.Fy);
                    w.WriteNumber("cx", MoHinh.Cx); w.WriteNumber("cy", MoHinh.Cy);
                    w.WriteNumber("fov_doc_do", Math.Round(MoHinh.FovDocDo, 4));
                    w.WriteNumber("fov_ngang_do", Math.Round(MoHinh.FovNgangDo, 4));
                }
                w.WriteStartArray("dist"); foreach (var k in Dist ?? new double[5]) w.WriteNumberValue(k); w.WriteEndArray();
                w.WriteEndObject();

                GhiTuThe(w, "camera_trong_world", CameraTrongWorld);
                if (MatBichTrongWorld.HasValue) GhiTuThe(w, "mat_bich_trong_world", MatBichTrongWorld.Value);
                if (FlangeToCamera.HasValue) GhiTuThe(w, "flange_to_camera", FlangeToCamera.Value);

                if (Robot != null) w.WriteString("robot", Robot);
                if (GocKhopDo != null)
                {
                    w.WriteStartArray("goc_khop_do");
                    foreach (var q in GocKhopDo) w.WriteNumberValue(Math.Round(q, 4));
                    w.WriteEndArray();
                }
                w.WriteStartArray("canh_bao"); foreach (var s in CanhBao) w.WriteStringValue(s); w.WriteEndArray();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        static void GhiTuThe(Utf8JsonWriter w, string ten, TuThe t)
        {
            w.WriteStartObject(ten);
            w.WriteStartArray("vi_tri_mm");
            w.WriteNumberValue(Math.Round(t.T.X, 4)); w.WriteNumberValue(Math.Round(t.T.Y, 4)); w.WriteNumberValue(Math.Round(t.T.Z, 4));
            w.WriteEndArray();
            w.WriteStartArray("quaternion_wxyz");
            foreach (var q in t.Quaternion()) w.WriteNumberValue(Math.Round(q, 8));
            w.WriteEndArray();
            w.WriteStartArray("ma_tran_xoay");
            foreach (var hang in t.MaTranXoay())
            {
                w.WriteStartArray();
                foreach (var g in hang) w.WriteNumberValue(Math.Round(g, 8));
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        /// <summary>Kiem tra mot chuoi JSON co du truong bat buoc khong. Tra ve danh sach truong thieu.</summary>
        public static List<string> TruongThieu(string json)
        {
            var thieu = new List<string>();
            using var doc = JsonDocument.Parse(json);
            foreach (var t in TruongBatBuoc)
                if (!doc.RootElement.TryGetProperty(t, out _)) thieu.Add(t);
            return thieu;
        }
    }
}
