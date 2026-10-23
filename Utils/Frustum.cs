using OpenTK.Mathematics;

namespace MinecraftClone.Utils;

public class Frustum
{
    private readonly Vector4[] _planes = new Vector4[6];

    public Frustum(Matrix4 viewProjectionMatrix)
    {
        // Левая и правая плоскости
        _planes[0] = Vector4.Normalize(new Vector4(
            viewProjectionMatrix.M14 + viewProjectionMatrix.M11,
            viewProjectionMatrix.M24 + viewProjectionMatrix.M21,
            viewProjectionMatrix.M34 + viewProjectionMatrix.M31,
            viewProjectionMatrix.M44 + viewProjectionMatrix.M41));

        _planes[1] = Vector4.Normalize(new Vector4(
            viewProjectionMatrix.M14 - viewProjectionMatrix.M11,
            viewProjectionMatrix.M24 - viewProjectionMatrix.M21,
            viewProjectionMatrix.M34 - viewProjectionMatrix.M31,
            viewProjectionMatrix.M44 - viewProjectionMatrix.M41));

        // Нижняя и верхняя плоскости
        _planes[2] = Vector4.Normalize(new Vector4(
            viewProjectionMatrix.M14 + viewProjectionMatrix.M12,
            viewProjectionMatrix.M24 + viewProjectionMatrix.M22,
            viewProjectionMatrix.M34 + viewProjectionMatrix.M32,
            viewProjectionMatrix.M44 + viewProjectionMatrix.M42));

        _planes[3] = Vector4.Normalize(new Vector4(
            viewProjectionMatrix.M14 - viewProjectionMatrix.M12,
            viewProjectionMatrix.M24 - viewProjectionMatrix.M22,
            viewProjectionMatrix.M34 - viewProjectionMatrix.M32,
            viewProjectionMatrix.M44 - viewProjectionMatrix.M42));

        // Ближняя и дальняя плоскости
        _planes[4] = Vector4.Normalize(new Vector4(
            viewProjectionMatrix.M14 + viewProjectionMatrix.M13,
            viewProjectionMatrix.M24 + viewProjectionMatrix.M23,
            viewProjectionMatrix.M34 + viewProjectionMatrix.M33,
            viewProjectionMatrix.M44 + viewProjectionMatrix.M43));

        _planes[5] = Vector4.Normalize(new Vector4(
            viewProjectionMatrix.M14 - viewProjectionMatrix.M13,
            viewProjectionMatrix.M24 - viewProjectionMatrix.M23,
            viewProjectionMatrix.M34 - viewProjectionMatrix.M33,
            viewProjectionMatrix.M44 - viewProjectionMatrix.M43));
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

            // Если вершина за плоскостью, весь куб невидим
            if (Vector3.Dot(plane.Xyz, p) + plane.W < 0)
                return false;
        }
        return true;
    }
}