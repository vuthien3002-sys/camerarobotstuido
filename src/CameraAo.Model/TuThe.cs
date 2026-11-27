using System;

namespace CameraAo.Model
{
    /// <summary>Vector 3 chieu (double). Don vi do dai dung trong mo hinh: mm.</summary>
    public readonly struct Vec3
    {
        public readonly double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double k) => new Vec3(a.X * k, a.Y * k, a.Z * k);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);

        public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
        public Vec3 Cross(Vec3 b) => new Vec3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
        public double Length() => Math.Sqrt(Dot(this));
        public Vec3 Normalized() { double l = Length(); return l > 0 ? this * (1.0 / l) : this; }
        public override string ToString() => FormattableString.Invariant($"({X:F3}, {Y:F3}, {Z:F3})");
    }

    /// <summary>
    /// Tu the (vi tri + huong) cua mot he toa do so voi he cha: p_cha = R * p_con + T.
    /// Cot cua R la 3 truc X, Y, Z cua he con bieu dien trong he cha. T tinh bang mm.
    /// </summary>
    public readonly struct TuThe
    {
        // R[hang, cot]
        private readonly double r00, r01, r02, r10, r11, r12, r20, r21, r22;
        public readonly Vec3 T;

        public TuThe(Vec3 trucX, Vec3 trucY, Vec3 trucZ, Vec3 goc)
        {
            r00 = trucX.X; r10 = trucX.Y; r20 = trucX.Z;
            r01 = trucY.X; r11 = trucY.Y; r21 = trucY.Z;
            r02 = trucZ.X; r12 = trucZ.Y; r22 = trucZ.Z;
            T = goc;
        }

        public static TuThe DonVi => new TuThe(new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1), new Vec3(0, 0, 0));

        public Vec3 TrucX => new Vec3(r00, r10, r20);
        public Vec3 TrucY => new Vec3(r01, r11, r21);
        public Vec3 TrucZ => new Vec3(r02, r12, r22);

        /// <summary>
        /// Tao tu the tu vi tri (mm) va 3 goc (do). Quy uoc: xoay quanh X co dinh truoc, roi Y, roi Z
        /// => R = Rz(rz) * Ry(ry) * Rx(rx) (giong o "Orientation" cua RobotStudio voi mot goc khac 0).
        /// </summary>
        public static TuThe TuXyzRpy(double xMm, double yMm, double zMm, double rxDo, double ryDo, double rzDo)
        {
            double a = rxDo * Math.PI / 180, b = ryDo * Math.PI / 180, c = rzDo * Math.PI / 180;
            double ca = Math.Cos(a), sa = Math.Sin(a), cb = Math.Cos(b), sb = Math.Sin(b), cc = Math.Cos(c), sc = Math.Sin(c);
            // R = Rz * Ry * Rx
            var x = new Vec3(cc * cb, sc * cb, -sb);
            var y = new Vec3(cc * sb * sa - sc * ca, sc * sb * sa + cc * ca, cb * sa);
            var z = new Vec3(cc * sb * ca + sc * sa, sc * sb * ca - cc * sa, cb * ca);
            return new TuThe(x, y, z, new Vec3(xMm, yMm, zMm));
        }

        /// <summary>Doi lai 3 goc (do) theo cung quy uoc R = Rz*Ry*Rx.</summary>
        public (double rx, double ry, double rz) GocDo()
        {
            double ry = Math.Asin(Math.Max(-1, Math.Min(1, -r20)));
            double rx, rz;
            if (Math.Abs(Math.Cos(ry)) > 1e-9) { rx = Math.Atan2(r21, r22); rz = Math.Atan2(r10, r00); }
            else { rx = Math.Atan2(-r12, r11); rz = 0; }
            const double k = 180 / Math.PI;
            return (rx * k, ry * k, rz * k);
        }

        public Vec3 BienDoiVecTo(Vec3 v) => new Vec3(
            r00 * v.X + r01 * v.Y + r02 * v.Z,
            r10 * v.X + r11 * v.Y + r12 * v.Z,
            r20 * v.X + r21 * v.Y + r22 * v.Z);

        public Vec3 BienDoiDiem(Vec3 p) => BienDoiVecTo(p) + T;

        /// <summary>Ghep: this * b (b bieu dien trong he cua this).</summary>
        public TuThe Nhan(TuThe b) => new TuThe(BienDoiVecTo(b.TrucX), BienDoiVecTo(b.TrucY), BienDoiVecTo(b.TrucZ), BienDoiDiem(b.T));

        /// <summary>Nghich dao cua phep bien doi cung (R^T, -R^T T).</summary>
        public TuThe NghichDao()
        {
            var x = new Vec3(r00, r01, r02);   // hang cua R = cot cua R^T
            var y = new Vec3(r10, r11, r12);
            var z = new Vec3(r20, r21, r22);
            var nd = new TuThe(x, y, z, new Vec3(0, 0, 0));
            return new TuThe(x, y, z, -nd.BienDoiVecTo(T));
        }

        /// <summary>Quaternion [w, x, y, z] (thu tu giong ABB/RAPID), w &gt;= 0.</summary>
        public double[] Quaternion()
        {
            double tr = r00 + r11 + r22, w, x, y, z;
            if (tr > 0)
            {
                double s = Math.Sqrt(tr + 1.0) * 2;
                w = 0.25 * s; x = (r21 - r12) / s; y = (r02 - r20) / s; z = (r10 - r01) / s;
            }
            else if (r00 > r11 && r00 > r22)
            {
                double s = Math.Sqrt(1.0 + r00 - r11 - r22) * 2;
                w = (r21 - r12) / s; x = 0.25 * s; y = (r01 + r10) / s; z = (r02 + r20) / s;
            }
            else if (r11 > r22)
            {
                double s = Math.Sqrt(1.0 + r11 - r00 - r22) * 2;
                w = (r02 - r20) / s; x = (r01 + r10) / s; y = 0.25 * s; z = (r12 + r21) / s;
            }
            else
            {
                double s = Math.Sqrt(1.0 + r22 - r00 - r11) * 2;
                w = (r10 - r01) / s; x = (r02 + r20) / s; y = (r12 + r21) / s; z = 0.25 * s;
            }
            if (w < 0) { w = -w; x = -x; y = -y; z = -z; }
            return new[] { w, x, y, z };
        }

        /// <summary>Ma tran xoay 3x3 theo hang.</summary>
        public double[][] MaTranXoay() => new[]
        {
            new[] { r00, r01, r02 }, new[] { r10, r11, r12 }, new[] { r20, r21, r22 }
        };
    }
}
