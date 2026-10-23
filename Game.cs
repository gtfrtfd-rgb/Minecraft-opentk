using System.Diagnostics;
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

    // --- ПЕРЕМЕННЫЕ ДЛЯ СЧЕТЧИКА FPS ---
    private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
    private int _frameCount = 0;
    private int _currentFps = 0;

    public Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

    protected override void OnLoad()
    {
        base.OnLoad();
        MakeCurrent();

        // ВКЛЮЧАЕМ V-SYNC
        VSync = VSyncMode.On;

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

        // --- ОБНОВЛЕНИЕ СЧЕТЧИКА FPS ---
        _frameCount++;
        if (_fpsTimer.ElapsedMilliseconds >= 1000)
        {
            _currentFps = _frameCount;
            _frameCount = 0;
            _fpsTimer.Restart();
        }

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

        // Проход 1: Непрозрачные
        GL.DepthMask(true);
        foreach (var chunk in visibleChunks) chunk.OpaqueMesh?.Draw(_shader);

        // Проход 2: Прозрачные
        GL.DepthMask(false); 
        foreach (var chunk in visibleChunks) chunk.TransparentMesh?.Draw(_shader);
        GL.DepthMask(true);

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

        // --- СЧЕТЧИК FPS (ИСПОЛЬЗУЕМ _currentFps) ---
        DrawFPS(_currentFps, 20, 20, 4f, screenW, screenH);

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

    // --- СИСТЕМА ОТРИСОВКИ ЦИФР (МИНИ-ШРИФТ 3x5) ---
    private void DrawFPS(int fps, float x, float y, float scale, int sw, int sh)
    {
        string text = $"FPS: {fps}";
        float cursorX = x;

        foreach (char c in text)
        {
            if (c == 'F') { DrawChar(F, cursorX, y, scale, sw, sh); cursorX += scale * 4; }
            else if (c == 'P') { DrawChar(P, cursorX, y, scale, sw, sh); cursorX += scale * 4; }
            else if (c == 'S') { DrawChar(S, cursorX, y, scale, sw, sh); cursorX += scale * 4; }
            else if (c == ':') { DrawChar(Colon, cursorX, y, scale, sw, sh); cursorX += scale * 2; }
            else if (c == ' ') { cursorX += scale * 3; }
            else if (char.IsDigit(c)) 
            { 
                DrawChar(Digits[c - '0'], cursorX, y, scale, sw, sh); 
                cursorX += scale * 4; 
            }
        }
    }

    private void DrawChar(int[] mask, float x, float y, float scale, int sw, int sh)
    {
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                if ((mask[row] & (1 << (2 - col))) != 0)
                {
                    _ui.DrawSolidQuad(x + col * scale - 1, y + row * scale - 1, scale + 2, scale + 2, new Vector4(0, 0, 0, 0.8f), sw, sh);
                }
            }
        }
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                if ((mask[row] & (1 << (2 - col))) != 0)
                {
                    _ui.DrawSolidQuad(x + col * scale, y + row * scale, scale, scale, new Vector4(1, 1, 1, 1f), sw, sh);
                }
            }
        }
    }

    private static readonly int[] F = { 0b111, 0b100, 0b111, 0b100, 0b100 };
    private static readonly int[] P = { 0b111, 0b101, 0b111, 0b100, 0b100 };
    private static readonly int[] S = { 0b011, 0b100, 0b010, 0b001, 0b110 };
    private static readonly int[] Colon = { 0b000, 0b010, 0b000, 0b010, 0b000 };
    private static readonly int[][] Digits = {
        new int[] { 0b111, 0b101, 0b101, 0b101, 0b111 }, // 0
        new int[] { 0b010, 0b110, 0b010, 0b010, 0b111 }, // 1
        new int[] { 0b111, 0b001, 0b111, 0b100, 0b111 }, // 2
        new int[] { 0b111, 0b001, 0b111, 0b001, 0b111 }, // 3
        new int[] { 0b101, 0b101, 0b111, 0b001, 0b001 }, // 4
        new int[] { 0b111, 0b100, 0b111, 0b001, 0b111 }, // 5
        new int[] { 0b111, 0b100, 0b111, 0b101, 0b111 }, // 6
        new int[] { 0b111, 0b001, 0b001, 0b001, 0b001 }, // 7
        new int[] { 0b111, 0b101, 0b111, 0b101, 0b111 }, // 8
        new int[] { 0b111, 0b101, 0b111, 0b001, 0b111 }  // 9
    };

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
    }
}