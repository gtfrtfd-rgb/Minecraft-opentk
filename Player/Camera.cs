using OpenTK.Mathematics;

namespace MinecraftClone.Player;

public class Camera
{
    public Vector3 Position { get; set; }
    public float Yaw { get; set; } = -90f;
    public float Pitch { get; set; } = 0f;
    public float Fov { get; set; } = 75f; // Новое свойство
    public float Speed { get; set; } = 8f;

    public Camera(Vector3 position) => Position = position;

    public Matrix4 GetViewMatrix()
        => Matrix4.LookAt(Position, Position + GetFrontVector(), Vector3.UnitY);

    public Matrix4 GetProjectionMatrix(float aspect)
        => Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Fov), aspect, 0.1f, 500f);

    public Vector3 GetFrontVector()
    {
        float yawRad = MathHelper.DegreesToRadians(Yaw);
        float pitchRad = MathHelper.DegreesToRadians(Pitch);
        return new Vector3(
            MathF.Cos(yawRad) * MathF.Cos(pitchRad),
            MathF.Sin(pitchRad),
            MathF.Sin(yawRad) * MathF.Cos(pitchRad)).Normalized();
    }

    public void MoveForward(float amount) => Position += GetFrontVector() * Speed * amount;
    public void MoveRight(float amount)
    {
        var right = Vector3.Cross(GetFrontVector(), Vector3.UnitY).Normalized();
        Position += right * Speed * amount;
    }
    public void MoveUp(float amount) => Position += Vector3.UnitY * Speed * amount;
}