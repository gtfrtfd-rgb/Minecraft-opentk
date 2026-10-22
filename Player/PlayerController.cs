using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using MinecraftClone.World;

namespace MinecraftClone.Player;

public class PlayerController
{
    private readonly Camera _camera;
    private readonly World.World _world;
    
    public Vector3 Position { get; private set; }
    public Vector3 Velocity;
    public bool IsGrounded;
    
    public BlockType SelectedBlock { get; set; } = BlockType.Stone;

    private const float PlayerWidth = 0.6f;
    private const float PlayerHeight = 1.8f;
    private const float EyeHeight = 1.62f;
    
    private const float Gravity = -24f; 
    private const float JumpForce = 8.5f;
    private const float MoveSpeed = 5.5f;
    private const float Reach = 5f;

    public PlayerController(Camera camera, World.World world, Vector3 startPosition)
    {
        _camera = camera;
        _world = world;
        Position = startPosition;
        _camera.Position = Position + new Vector3(0, EyeHeight, 0);
    }

    public void Update(float dt, KeyboardState keyboard, MouseState mouse)
    {
        Vector3 front = _camera.GetFrontVector();
        front.Y = 0;
        if (front.LengthSquared > 0) front.Normalize();
        
        Vector3 right = Vector3.Cross(front, Vector3.UnitY).Normalized();

        Vector3 moveDir = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) moveDir += front;
        if (keyboard.IsKeyDown(Keys.S)) moveDir -= front;
        if (keyboard.IsKeyDown(Keys.A)) moveDir -= right;
        if (keyboard.IsKeyDown(Keys.D)) moveDir += right;
        
        if (moveDir.LengthSquared > 0) moveDir.Normalize();

        Velocity.X = moveDir.X * MoveSpeed;
        Velocity.Z = moveDir.Z * MoveSpeed;

        if (keyboard.IsKeyDown(Keys.Space) && IsGrounded)
        {
            Velocity.Y = JumpForce;
            IsGrounded = false;
        }

        float remainingTime = dt;
        IsGrounded = false; 
        
        while (remainingTime > 0f)
        {
            float step = Math.Min(remainingTime, 0.016f);
            remainingTime -= step;

            Velocity.Y += Gravity * step;
            Velocity.Y = Math.Max(Velocity.Y, -40f);

            Vector3 playerSize = new Vector3(PlayerWidth, PlayerHeight, PlayerWidth);
            Position = Physics.Collision.Move(_world, Position, playerSize, ref Velocity, step, out bool onGround);
            
            if (onGround) IsGrounded = true;
        }

        _camera.Position = Position + new Vector3(0, EyeHeight, 0);
    }

    public void BreakBlock()
    {
        if (Raycast(out var pos, out _))
            _world.SetBlock(pos.X, pos.Y, pos.Z, BlockType.Air);
    }

    public void PlaceBlock()
    {
        if (Raycast(out var pos, out var normal))
        {
            var placePos = pos + normal;
            if (_world.GetBlock(placePos.X, placePos.Y, placePos.Z) == BlockType.Air)
                _world.SetBlock(placePos.X, placePos.Y, placePos.Z, SelectedBlock); // Ставим выбранный блок
        }
    }

    private bool Raycast(out Vector3i blockPos, out Vector3i normal)
    {
        blockPos = Vector3i.Zero;
        normal = Vector3i.Zero;

        Vector3 origin = _camera.Position;
        Vector3 dir = _camera.GetFrontVector();

        var pos = new Vector3i(
            (int)MathF.Floor(origin.X),
            (int)MathF.Floor(origin.Y),
            (int)MathF.Floor(origin.Z));

        var step = new Vector3i(dir.X > 0 ? 1 : -1, dir.Y > 0 ? 1 : -1, dir.Z > 0 ? 1 : -1);
        var tMax = new Vector3(
            ((dir.X > 0 ? pos.X + 1 - origin.X : origin.X - pos.X) / MathF.Abs(dir.X)),
            ((dir.Y > 0 ? pos.Y + 1 - origin.Y : origin.Y - pos.Y) / MathF.Abs(dir.Y)),
            ((dir.Z > 0 ? pos.Z + 1 - origin.Z : origin.Z - pos.Z) / MathF.Abs(dir.Z)));
        var tDelta = new Vector3(1f / MathF.Abs(dir.X), 1f / MathF.Abs(dir.Y), 1f / MathF.Abs(dir.Z));

        float t = 0f;
        while (t < Reach)
        {
            var block = _world.GetBlock(pos.X, pos.Y, pos.Z);
            if (block != BlockType.Air && BlockData.IsSolid(block))
            {
                blockPos = pos;
                return true;
            }

            if (tMax.X < tMax.Y && tMax.X < tMax.Z) { pos.X += step.X; t = tMax.X; tMax.X += tDelta.X; normal = new Vector3i(-step.X, 0, 0); }
            else if (tMax.Y < tMax.Z) { pos.Y += step.Y; t = tMax.Y; tMax.Y += tDelta.Y; normal = new Vector3i(0, -step.Y, 0); }
            else { pos.Z += step.Z; t = tMax.Z; tMax.Z += tDelta.Z; normal = new Vector3i(0, 0, -step.Z); }
        }
        return false;
    }
}