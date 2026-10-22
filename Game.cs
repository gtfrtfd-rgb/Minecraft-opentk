using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MinecraftClone;

public class Game : OpenTK.Windowing.Desktop.GameWindow
{
    private World.World _world = null!;
    private Renderer.Shader _shader = null!;
    private Renderer.TextureAtlas _atlas = null!;
    private Player.Camera _camera = null!;
    private Player.PlayerController _controller = null!;
    private Vector2 _lastMousePos;
    private bool _firstMove = true;

    public Game(OpenTK.Windowing.Common.GameWindowSettings gws, NativeWindowSettings nws)
        : base(gws, nws) { }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(0.5f, 0.7f, 1.0f, 1.0f); // небо
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);

        _shader = new Renderer.Shader("Shaders/vertex.glsl", "Shaders/fragment.glsl");
        _atlas = new Renderer.TextureAtlas(16, 16);
        _world = new World.World(_atlas);
        _world.Generate(8, 8); // чанки 8×8

        _camera = new Player.Camera(new Vector3(16, 30, 16));
        _controller = new Player.PlayerController(_camera, _world);

        CursorState = CursorState.Grabbed;
        _lastMousePos = new Vector2(MousePosition.X, MousePosition.Y);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
            Close();

        // Камера
        if (KeyboardState.IsKeyDown(Keys.W)) _camera.MoveForward((float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.S)) _camera.MoveForward(-(float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.A)) _camera.MoveRight(-(float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.D)) _camera.MoveRight((float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.Space)) _camera.MoveUp((float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.LeftShift)) _camera.MoveUp(-(float)args.Time);

        // Мышь
        var mouse = new Vector2(MousePosition.X, MousePosition.Y);
        if (_firstMove) { _lastMousePos = mouse; _firstMove = false; }
        var delta = mouse - _lastMousePos;
        _lastMousePos = mouse;
        _camera.Yaw += delta.X * 0.15f;
        _camera.Pitch -= delta.Y * 0.15f;
        _camera.Pitch = Math.Clamp(_camera.Pitch, -89f, 89f);

        // Физика игрока
        _controller.Update((float)args.Time, KeyboardState, MouseState);

        // Разрушение / установка блоков
        if (MouseState.IsButtonDown(MouseButton.Left))
            _controller.BreakBlock();
        if (MouseState.IsButtonDown(MouseButton.Right))
            _controller.PlaceBlock();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _shader.Use();
        _shader.SetMatrix4("view", _camera.GetViewMatrix());
        _shader.SetMatrix4("projection", _camera.GetProjectionMatrix((float)Size.X / Size.Y));
        _shader.SetInt("textureAtlas", 0);

        _atlas.Bind(TextureUnit.Texture0);

        foreach (var chunk in _world.GetVisibleChunks(_camera.Position))
        {
            chunk.Mesh?.Draw(_shader);
        }

        SwapBuffers();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
    }
}