using ABB.Robotics.Math;
using CameraAo.Model;

namespace CameraAo.Addin
{
    /// <summary>Chuyen doi giua kieu cua RobotStudio (met) va kieu cua mo hinh (mm).</summary>
    internal static class ChuyenDoiRs
    {
        public static TuThe TuMatrix4(Matrix4 m)
        {
            Vector3 x = m.GetAxisVector(Axis.X), y = m.GetAxisVector(Axis.Y), z = m.GetAxisVector(Axis.Z), t = m.Translation;
            return new TuThe(new Vec3(x.x, x.y, x.z), new Vec3(y.x, y.y, y.z), new Vec3(z.x, z.y, z.z),
                             new Vec3(t.x * 1000, t.y * 1000, t.z * 1000));
        }

        /// <summary>Diem (mm) -> Vector3 (m)</summary>
        public static Vector3 DiemMet(Vec3 pMm) => new Vector3(pMm.X / 1000, pMm.Y / 1000, pMm.Z / 1000);

        /// <summary>Vec to huong (khong doi don vi)</summary>
        public static Vector3 Huong(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
