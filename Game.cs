using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MinecraftClone;

public class Game : GameWindow
{
    private World.World _world = null!;
    private Renderer.Shader _shader = null!;
    private Renderer.TextureAtlas _atlas = null!;
    private Player.Camera _camera = null!;
    private Player.PlayerController _controller = null!;
    private Vector2 _lastMousePos;
    private bool _firstMove = true;
    private bool _isWorldGenerated = false;

    public Game(GameWindowSettings gws, NativeWindowSettings nws)
        : base(gws, nws) { }

    protected override void OnLoad()
    {
        base.OnLoad();
        MakeCurrent();

        GL.ClearColor(0.5f, 0.7f, 1.0f, 1.0f);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);

        _shader = new Renderer.Shader("Shaders/vertex.glsl", "Shaders/fragment.glsl");
        _atlas = new Renderer.TextureAtlas(16, 16);
        _world = new World.World(_atlas);

        _camera = new Player.Camera(new Vector3(16, 30, 16));
        _controller = new Player.PlayerController(_camera, _world);

        CursorState = CursorState.Grabbed;
        _lastMousePos = new Vector2(MousePosition.X, MousePosition.Y);
        Console.WriteLine("Загрузка ресурсов завершена. Мир будет сгенерирован при первом кадре.");
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
            Close();

        if (KeyboardState.IsKeyDown(Keys.W)) _camera.MoveForward((float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.S)) _camera.MoveForward(-(float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.A)) _camera.MoveRight(-(float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.D)) _camera.MoveRight((float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.Space)) _camera.MoveUp((float)args.Time);
        if (KeyboardState.IsKeyDown(Keys.LeftShift)) _camera.MoveUp(-(float)args.Time);

        var mouse = new Vector2(MousePosition.X, MousePosition.Y);
        if (_firstMove) { _lastMousePos = mouse; _firstMove = false; }
        var delta = mouse - _lastMousePos;
        _lastMousePos = mouse;
        _camera.Yaw += delta.X * 0.15f;
        _camera.Pitch -= delta.Y * 0.15f;
        _camera.Pitch = Math.Clamp(_camera.Pitch, -89f, 89f);

        _controller.Update((float)args.Time, KeyboardState, MouseState);

        // Используем IsButtonPressed, чтобы избежать многократного срабатывания за кадр
        if (MouseState.IsButtonPressed(MouseButton.Left))
            _controller.BreakBlock();
        if (MouseState.IsButtonPressed(MouseButton.Right))
            _controller.PlaceBlock();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        // Генерируем мир только один раз, при первом кадре
        if (!_isWorldGenerated)
        {
            Console.WriteLine("Начинаем генерацию мира (это может занять время)...");
            _world.Generate(4, 4); // Уменьшим размер для начала
            _isWorldGenerated = true;
            Console.WriteLine("Генерация мира завершена!");
        }

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