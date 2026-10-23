using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using MinecraftClone.World;

namespace MinecraftClone;

public class Game : GameWindow
{
    private World.World _world = null!;
    private Renderer.Shader _shader = null!;
    private Renderer.TextureAtlas _atlas = null!;
    private Renderer.UIRenderer _ui = null!;
    private Player.Camera _camera = null!;
    private Player.PlayerController _controller = null!;
    private Vector2 _lastMousePos;
    private bool _firstMove = true;
    private bool _isWorldGenerated = false;

    public Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

    protected override void OnLoad()
    {
        base.OnLoad();
        MakeCurrent();

        GL.ClearColor(0.5f, 0.7f, 1.0f, 1.0f);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _shader = new Renderer.Shader();
        _atlas = new Renderer.TextureAtlas(16, 16);
        _ui = new Renderer.UIRenderer();
        _world = new World.World(_atlas);

        _camera = new Player.Camera(Vector3.Zero);
        _controller = new Player.PlayerController(_camera, _world, new Vector3(16, 40, 16));

        CursorState = CursorState.Grabbed;
        _lastMousePos = new Vector2(MousePosition.X, MousePosition.Y);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape)) Close();

        if (KeyboardState.IsKeyPressed(Keys.D1)) _controller.SelectedBlock = BlockType.Grass;
        if (KeyboardState.IsKeyPressed(Keys.D2)) _controller.SelectedBlock = BlockType.Dirt;
        if (KeyboardState.IsKeyPressed(Keys.D3)) _controller.SelectedBlock = BlockType.Stone;
        if (KeyboardState.IsKeyPressed(Keys.D4)) _controller.SelectedBlock = BlockType.Wood;
        if (KeyboardState.IsKeyPressed(Keys.D5)) _controller.SelectedBlock = BlockType.Leaves;
        if (KeyboardState.IsKeyPressed(Keys.D6)) _controller.SelectedBlock = BlockType.Sand;
        if (KeyboardState.IsKeyPressed(Keys.D7)) _controller.SelectedBlock = BlockType.Water;

        var mouse = new Vector2(MousePosition.X, MousePosition.Y);
        if (_firstMove) { _lastMousePos = mouse; _firstMove = false; }
        var delta = mouse - _lastMousePos;
        _lastMousePos = mouse;
        
        _camera.Yaw += delta.X * 0.15f;
        _camera.Pitch -= delta.Y * 0.15f;
        _camera.Pitch = Math.Clamp(_camera.Pitch, -89f, 89f);

        float dt = Math.Min((float)args.Time, 0.05f);
        _controller.Update(dt, KeyboardState, MouseState);

        if (MouseState.IsButtonPressed(MouseButton.Left)) _controller.BreakBlock();
        if (MouseState.IsButtonPressed(MouseButton.Right)) _controller.PlaceBlock();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        if (!_isWorldGenerated)
        {
            Console.WriteLine("Генерация мира...");
            _world.Generate(8, 8); 
            _isWorldGenerated = true;
            Console.WriteLine("Мир готов!");
        }

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _shader.Use();
        _shader.SetMatrix4("view", _camera.GetViewMatrix());
        _shader.SetMatrix4("projection", _camera.GetProjectionMatrix((float)Size.X / Size.Y));
        _shader.SetInt("textureAtlas", 0);
        _atlas.Bind(TextureUnit.Texture0);

        var visibleChunks = _world.GetVisibleChunks(_camera.Position).ToList();

        // --- ПРОХОД 1: Отрисовка НЕПРОЗРАЧНЫХ блоков (Камень, земля) ---
        GL.DepthMask(true);
        foreach (var chunk in visibleChunks)
        {
            chunk.OpaqueMesh?.Draw(_shader);
        }

        // --- ПРОХОД 2: Отрисовка ПРОЗРАЧНЫХ блоков (Вода, листва) ---
        // Отключаем запись в буфер глубины, чтобы вода не перекрывала саму себя
        GL.DepthMask(false); 
        foreach (var chunk in visibleChunks)
        {
            chunk.TransparentMesh?.Draw(_shader);
        }
        GL.DepthMask(true); // Возвращаем обратно

        // 3. Рендер UI
        GL.Disable(EnableCap.DepthTest);
        _atlas.Bind(TextureUnit.Texture0);

        int screenW = Size.X;
        int screenH = Size.Y;
        float cx = screenW / 2f;
        float cy = screenH / 2f;

        // Прицел
        _ui.DrawSolidQuad(cx - 11, cy - 2, 22, 4, new Vector4(0, 0, 0, 0.8f), screenW, screenH);
        _ui.DrawSolidQuad(cx - 2, cy - 11, 4, 22, new Vector4(0, 0, 0, 0.8f), screenW, screenH);
        _ui.DrawSolidQuad(cx - 10, cy - 1, 20, 2, new Vector4(1, 1, 1, 1f), screenW, screenH);
        _ui.DrawSolidQuad(cx - 1, cy - 10, 2, 20, new Vector4(1, 1, 1, 1f), screenW, screenH);

        // Хотбар
        float slotSize = 56f;
        float padding = 4f;
        float totalWidth = 9 * slotSize + 8 * padding;
        float startX = cx - totalWidth / 2f;
        float startY = screenH - slotSize - 20f;

        _ui.DrawSolidQuad(startX - 6, startY - 6, totalWidth + 12, slotSize + 12, new Vector4(0.1f, 0.1f, 0.1f, 0.6f), screenW, screenH);

        for (int i = 0; i < 9; i++)
        {
            float sx = startX + i * (slotSize + padding);
            bool isSelected = (i + 1) == (int)_controller.SelectedBlock;

            if (isSelected)
            {
                _ui.DrawSolidQuad(sx - 4, startY - 4, slotSize + 8, slotSize + 8, new Vector4(0.9f, 0.9f, 0.9f, 1f), screenW, screenH);
                _ui.DrawSolidQuad(sx, startY, slotSize, slotSize, new Vector4(0.3f, 0.3f, 0.3f, 0.9f), screenW, screenH);
            }
            else
            {
                _ui.DrawSolidQuad(sx, startY, slotSize, slotSize, new Vector4(0.4f, 0.4f, 0.4f, 0.6f), screenW, screenH);
            }

            if (i + 1 <= (int)BlockType.Water)
            {
                BlockType blockToDraw = (BlockType)(i + 1);
                var uv = BlockData.GetUV(blockToDraw, 0);
                float itemSize = isSelected ? slotSize - 8 : slotSize - 16;
                float itemOffset = (slotSize - itemSize) / 2f;
                _ui.DrawQuad(sx + itemOffset, startY + itemOffset, itemSize, itemSize, uv, Vector4.One, screenW, screenH);
            }
        }

        GL.Enable(EnableCap.DepthTest);
        SwapBuffers();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
    }
}