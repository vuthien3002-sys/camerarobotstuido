using System;
using System.Threading;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;
using CameraAo.Model;
using CameraAo.Transport;

namespace CameraAo.Addin
{
    /// <summary>
    /// TRIGGER BANG DO (yeu cau 4): Smart Component co tin hieu vao "Chup" va ra "DaChup",
    /// noi bang Station Logic voi controller ao:  DO_Chup -> Chup ;  DaChup -> DI_DaChup.
    /// Bat tay 2 chieu (RAPID):  SetDO DO_Chup,1;  WaitDI DI_DaChup,1;  SetDO DO_Chup,0;  WaitDI DI_DaChup,0;
    ///   - Chup len 1 (suon len) -> chup 1 anh -> DaChup = 1   (chup loi: KHONG bat DaChup -> RAPID het gio, lo ra loi)
    ///   - Chup ve 0             -> DaChup = 0
    /// Tin hieu Station Logic chi truyen khi DANG MO PHONG. Smart Component luu cung station; handler thi
    /// phai gan lai moi lan nap add-in / mo station (GanLai).
    /// </summary>
    internal static class TriggerDo
    {
        public static Func<string, KetQuaGhi> ChupAnh { get; set; }
        public static Action<string> Log { get; set; }
        public static Func<CauHinhCamera.CauHinhTrigger> LayCauHinh { get; set; }

        static SmartComponent _sc;
        static SynchronizationContext _ui;
        static int _chupTruoc;
        public static int SoXung { get; private set; }
        public static int SoAnh { get; private set; }

        /// <summary>Tao (neu chua co) Smart Component + tin hieu + ket noi Station Logic, roi gan handler.</summary>
        public static void KetNoi(Station st)
        {
            if (st == null) { Log("Hay mo station truoc."); return; }
            _ui = SynchronizationContext.Current ?? _ui;
            var ch = LayCauHinh();
            var vc = TimController(st, ch.Controller);
            if (vc == null) { Log("Station khong co controller ao -> khong noi duoc trigger DO."); return; }

            SmartComponent sc = TimSmartComponent(st, ch.SmartComponent);
            int themKetNoi = 0;
            Project.UndoContext.ExecuteUndoStep("Camera ao: trigger DO", () =>
            {
                if (sc == null)
                {
                    sc = new SmartComponent { Name = ch.SmartComponent };
                    st.GraphicComponents.Add(sc);
                }
                if (!sc.IOSignals.Contains(ch.TinHieuChup)) sc.IOSignals.Add(new IOSignal(ch.TinHieuChup, IOSignalType.DigitalInput));
                if (!sc.IOSignals.Contains(ch.TinHieuXong)) sc.IOSignals.Add(new IOSignal(ch.TinHieuXong, IOSignalType.DigitalOutput));
                if (!CoKetNoi(st, vc, ch.DoRobot, sc, ch.TinHieuChup)) { st.Connections.Add(new IOConnection(vc, ch.DoRobot, sc, ch.TinHieuChup)); themKetNoi++; }
                if (!CoKetNoi(st, sc, ch.TinHieuXong, vc, ch.DiRobot)) { st.Connections.Add(new IOConnection(sc, ch.TinHieuXong, vc, ch.DiRobot)); themKetNoi++; }
            });
            Gan(sc);
            Log($"Trigger DO san sang: Station Logic {vc.Name}.{ch.DoRobot} -> {sc.Name}.{ch.TinHieuChup} ; {sc.Name}.{ch.TinHieuXong} -> {vc.Name}.{ch.DiRobot}"
                + (themKetNoi > 0 ? $" (vua them {themKetNoi} ket noi)" : " (ket noi da co)")
                + ". Controller phai co san 2 tin hieu nay (nap file cfg + warm restart). Tin hieu chi truyen khi DANG MO PHONG.");
        }

        /// <summary>Gan lai handler neu station da co Smart Component trigger (khong tao gi moi).</summary>
        public static void GanLai(Station st)
        {
            if (st == null) return;
            var sc = TimSmartComponent(st, LayCauHinh().SmartComponent);
            if (sc == null || ReferenceEquals(sc, _sc)) return;
            _ui = SynchronizationContext.Current ?? _ui;
            Gan(sc);
            Log("Da gan lai trigger DO vao Smart Component '" + sc.Name + "'.");
        }

        public static string TrangThai()
        {
            if (_sc == null) return "Trigger DO chua ket noi (bam 'Kết nối trigger').";
            var ch = LayCauHinh();
            return $"Trigger DO: {_sc.Name}, {ch.TinHieuChup}={GiaTri(_sc, ch.TinHieuChup)}, {ch.TinHieuXong}={GiaTri(_sc, ch.TinHieuXong)}; "
                 + $"so xung nhan = {SoXung}, so anh chup do DO = {SoAnh}.";
        }

        public static void DatLaiDem() { SoXung = 0; SoAnh = 0; }

        // ------------------------------------------------------------------
        static void Gan(SmartComponent sc)
        {
            if (_sc != null) _sc.IOSignalValueChanged -= KhiDoiTinHieu;
            _sc = sc;
            _sc.IOSignalValueChanged += KhiDoiTinHieu;
            _chupTruoc = GiaTri(sc, LayCauHinh().TinHieuChup);
        }

        static void KhiDoiTinHieu(object sender, IOSignalChangedEventArgs e)
        {
            // Chup anh phai chay tren luong giao dien cua RobotStudio
            if (_ui != null && SynchronizationContext.Current != _ui) _ui.Post(_ => XuLy(e.Signal), null);
            else XuLy(e.Signal);
        }

        static void XuLy(IOSignal sig)
        {
            var ch = LayCauHinh();
            if (_sc == null || sig == null || sig.Name != ch.TinHieuChup) return;
            int v = ToInt(sig.Value);
            int truoc = _chupTruoc;
            _chupTruoc = v;
            if (v == 1 && truoc == 0)
            {
                SoXung++;
                KetQuaGhi kq = null;
                try { kq = ChupAnh("DO"); }
                catch (Exception ex) { Log($"LOI chup theo DO (xung #{SoXung}): {ex.Message}"); }
                if (kq != null) { SoAnh++; Dat(ch.TinHieuXong, 1); }
                else Log($"Xung #{SoXung}: KHONG chup duoc -> khong bat {ch.TinHieuXong} (RAPID se het gio cho).");
            }
            else if (v == 0 && truoc == 1)
            {
                Dat(ch.TinHieuXong, 0);
            }
        }

        static void Dat(string ten, int giaTri)
        {
            try { _sc.IOSignals[ten].Value = giaTri; }
            catch (Exception ex) { Log($"LOI dat {ten}={giaTri}: {ex.Message}"); }
        }

        static int GiaTri(SmartComponent sc, string ten)
        {
            try { return sc.IOSignals.Contains(ten) ? ToInt(sc.IOSignals[ten].Value) : 0; } catch { return 0; }
        }

        static int ToInt(object o)
        {
            try { return Convert.ToInt32(o, System.Globalization.CultureInfo.InvariantCulture) != 0 ? 1 : 0; } catch { return 0; }
        }

        static VirtualController TimController(Station st, string ten)
        {
            if (!string.IsNullOrEmpty(ten)) return st.VirtualControllers.TryGetVirtualController(ten, out var vc) ? vc : null;
            foreach (VirtualController v in st.VirtualControllers) return v;
            return null;
        }

        static SmartComponent TimSmartComponent(Station st, string ten)
        {
            foreach (GraphicComponent g in st.GraphicComponents)
                if (g is SmartComponent sc && sc.Name == ten) return sc;
            return null;
        }

        static bool CoKetNoi(Station st, ProjectObject nguon, string tinHieuNguon, ProjectObject dich, string tinHieuDich)
        {
            foreach (IOConnection c in st.Connections)
                if (ReferenceEquals(c.SourceObject, nguon) && c.SourceSignal == tinHieuNguon
                    && ReferenceEquals(c.TargetObject, dich) && c.TargetSignal == tinHieuDich) return true;
            return false;
        }
    }
}
