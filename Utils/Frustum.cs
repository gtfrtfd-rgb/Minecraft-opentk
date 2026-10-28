using OpenTK.Mathematics;

namespace MinecraftClone.Utils;

public class Frustum
{
    private readonly Vector4[] _planes = new Vector4[6];

    public Frustum() { }

    public Frustum(Matrix4 viewProjectionMatrix) => Update(viewProjectionMatrix);

    public void Update(Matrix4 m)
    {
        // В Game.cs передается view * proj, поэтому матрица транспонирована
        // относительно стандартной proj * view. Извлекаем плоскости из столбцов.
        
        // Left (Column 4 + Column 1)
        _planes[0] = Vector4.Normalize(new Vector4(
            m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41));

        // Right (Column 4 - Column 1)
        _planes[1] = Vector4.Normalize(new Vector4(
            m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41));

        // Bottom (Column 4 + Column 2)
        _planes[2] = Vector4.Normalize(new Vector4(
            m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42));

        // Top (Column 4 - Column 2)
        _planes[3] = Vector4.Normalize(new Vector4(
            m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42));

        // Near (Column 4 + Column 3)
        _planes[4] = Vector4.Normalize(new Vector4(
            m.M14 + m.M13, m.M24 + m.M23, m.M34 + m.M33, m.M44 + m.M43));

        // Far (Column 4 - Column 3)
        _planes[5] = Vector4.Normalize(new Vector4(
            m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43));
    }

    public bool IsBoxVisible(Vector3 min, Vector3 max)
    {
        foreach (var plane in _planes)
        {
            // Находим "положительную" вершину AABB (ближайшую к плоскости по направлению нормали)
            var p = new Vector3(
                plane.X >= 0 ? max.X : min.X,
                plane.Y >= 0 ? max.Y : min.Y,
                plane.Z >= 0 ? max.Z : min.Z);

            // Если самая дальняя точка находится за плоскостью, значит весь бокс за ней
            if (Vector4.Dot(plane, new Vector4(p, 1.0f)) < 0)
                return false;
        }
        return true;
    }
}