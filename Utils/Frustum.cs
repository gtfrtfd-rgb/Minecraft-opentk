using OpenTK.Mathematics;

namespace MinecraftClone.Utils;

public class Frustum
{
    private readonly Vector4[] _planes = new Vector4[6];

    public Frustum() { }

    public Frustum(Matrix4 viewProjectionMatrix) => Update(viewProjectionMatrix);

    public void Update(Matrix4 m)
    {
        // Левая и правая плоскости
        _planes[0] = Vector4.Normalize(new Vector4(
            m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41));

        _planes[1] = Vector4.Normalize(new Vector4(
            m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41));

        // Нижняя и верхняя плоскости
        _planes[2] = Vector4.Normalize(new Vector4(
            m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42));

        _planes[3] = Vector4.Normalize(new Vector4(
            m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42));

        // Ближняя и дальняя плоскости
        _planes[4] = Vector4.Normalize(new Vector4(
            m.M14 + m.M13, m.M24 + m.M23, m.M34 + m.M33, m.M44 + m.M43));

        _planes[5] = Vector4.Normalize(new Vector4(
            m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43));
    }

    public bool IsBoxVisible(Vector3 min, Vector3 max)
    {
        foreach (var plane in _planes)
        {
            // Находим "положительную" вершину AABB (ближайшую к плоскости)
            var p = new Vector3(
                plane.X >= 0 ? max.X : min.X,
                plane.Y >= 0 ? max.Y : min.Y,
                plane.Z >= 0 ? max.Z : min.Z);

            if (Vector3.Dot(plane.Xyz, p) + plane.W < 0)
                return false;
        }
        return true;
    }
}