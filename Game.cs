using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using MinecraftClone.Utils;
using MinecraftClone.World;
using MinecraftClone.Settings;
using MinecraftClone.UI;
using MinecraftClone.Renderer;
using MinecraftClone.Player;

namespace MinecraftClone
{
    public enum GameState { Playing, Paused, Settings }

    public class Game : GameWindow
    {
        private World.World _world = null!;
        private Shader _shader = null!;
        private TextureAtlas _atlas = null!;
        private UIRenderer _ui = null!;
        private OutlineRenderer _outline = null!;
        private Camera _camera = null!;
        private PlayerController _controller = null!;
        private MenuManager _menu = null!;

        private GameSettings _settings = null!;
        private GameState _state = GameState.Playing;

        private int _appliedRenderDistance = 5;
        private float _appliedFov = 75f;
        private int _appliedFpsLimit = 60;
        private bool _appliedTransparency = true;

        private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
        private readonly Stopwatch _gameTimer = Stopwatch.StartNew();
        private readonly Stopwatch _frameTimer = new Stopwatch();
        private int _frameCount = 0;
        private int _currentFps = 0;

        private readonly Frustum _frustum = new();
        private readonly List<Chunk> _visibleChunks = new();
        private readonly List<Chunk> _waterSortScratch = new();

        private Vector3 _lastCameraPos;
        private float _lastCameraYaw;
        private float _lastCameraPitch;
        private int _lastDrawnRenderDistance = -1;
        private bool _visibleCacheValid = false;

        private Vector2 _lastMousePos;
        private bool _firstMove = true;

        private string _fpsText = "FPS: 0";
        private int _lastDrawnFps = -1;

        private static readonly Vector3 FogColor = new Vector3(0.5f, 0.7f, 1.0f);

        private sealed class ChunkDistanceToCameraComparer : IComparer<Chunk>
        {
            public Vector3 CameraPos;
            public int Compare(Chunk? a, Chunk? b)
            {
                if (a == null) return 1;
                if (b == null) return -1;
                Vector3 aCenter = new Vector3(a.Position.X * Chunk.SizeX + Chunk.SizeX * 0.5f, 64f, a.Position.Y * Chunk.SizeZ + Chunk.SizeZ * 0.5f);
                Vector3 bCenter = new Vector3(b.Position.X * Chunk.SizeX + Chunk.SizeX * 0.5f, 64f, b.Position.Y * Chunk.SizeZ + Chunk.SizeZ * 0.5f);
                float dA = (aCenter - CameraPos).LengthSquared;
                float dB = (bCenter - CameraPos).LengthSquared;
                return dB.CompareTo(dA);
            }
        }

        private readonly ChunkDistanceToCameraComparer _waterComparer = new();

        public Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

        protected override void OnLoad()
        {
            base.OnLoad();
            MakeCurrent();

            _settings = GameSettings.Load();

            _shader = new Shader();
            _atlas = new TextureAtlas(16, 16);
            _ui = new UIRenderer();
            _outline = new OutlineRenderer();

            // Инициализация менеджера UI
            _menu = new MenuManager(_ui, _atlas);
            _menu.CurrentResolutionIndex = Array.FindIndex(MenuManager.AvailableResolutions, r => r.Width == _settings.ResolutionWidth && r.Height == _settings.ResolutionHeight);
            if (_menu.CurrentResolutionIndex == -1)
            {
                _settings.ResolutionWidth = 1280;
                _settings.ResolutionHeight = 720;
                _menu.CurrentResolutionIndex = 2;
            }

            ClientSize = new Vector2i(_settings.ResolutionWidth, _settings.ResolutionHeight);
            WindowState = _settings.IsFullscreen ? WindowState.Fullscreen : WindowState.Normal;

            _appliedRenderDistance = _settings.RenderDistance;
            _appliedFov = _settings.Fov;
            _appliedFpsLimit = _settings.FpsLimit;
            _appliedTransparency = _settings.Transparency;
            VSync = _settings.VSync ? VSyncMode.On : VSyncMode.Off;

            GL.ClearColor(FogColor.X, FogColor.Y, FogColor.Z, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _world = new World.World();
            _camera = new Camera(Vector3.Zero);
            _camera.Fov = _appliedFov;
            _controller = new PlayerController(_camera, _world, new Vector3(0.5f, 0f, 0.5f));

            CursorState = CursorState.Grabbed;
            _lastMousePos = new Vector2(MousePosition.X, MousePosition.Y);
        }

        protected override void OnUnload()
        {
            _world?.Dispose();
            _shader?.Dispose();
            _atlas?.Dispose();
            _ui?.Dispose();
            _outline?.Dispose();
            base.OnUnload();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            if (KeyboardState.IsKeyPressed(Keys.Escape))
            {
                if (_state == GameState.Playing) { _state = GameState.Paused; CursorState = CursorState.Normal; _settings.Save(); }
                else if (_state == GameState.Paused) { ResumeGame(); }
                else if (_state == GameState.Settings) { _state = GameState.Paused; _settings.Save(); }
            }

            if (_state == GameState.Playing)
            {
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

                _camera.Yaw += delta.X * _settings.MouseSensitivity;
                _camera.Pitch -= delta.Y * _settings.MouseSensitivity;
                _camera.Pitch = Math.Clamp(_camera.Pitch, -89f, 89f);

                float dt = Math.Min((float)args.Time, 0.05f);
                _controller.Update(dt, KeyboardState, MouseState);
                _world.UpdateChunks(_controller.Position, _appliedRenderDistance);

                if (MouseState.IsButtonPressed(MouseButton.Left)) _controller.BreakBlock();
                if (MouseState.IsButtonPressed(MouseButton.Right)) _controller.PlaceBlock();
            }
            else
            {
                HandleMenuInput();
            }
        }

        private void HandleMenuInput()
        {
            var mousePos = new Vector2(MousePosition.X, MousePosition.Y);
            bool clicked = MouseState.IsButtonPressed(MouseButton.Left);
            int screenW = ClientSize.X;
            int screenH = ClientSize.Y;
            float cx = screenW / 2f;
            float cy = screenH / 2f;

            if (_state == GameState.Paused && clicked)
            {
                if (_menu.IsHovered(cx - 100, cy - 20, 200, 40, mousePos)) ResumeGame();
                if (_menu.IsHovered(cx - 100, cy + 30, 200, 40, mousePos)) _state = GameState.Settings;
                if (_menu.IsHovered(cx - 100, cy + 80, 200, 40, mousePos)) { _settings.Save(); Close(); }
            }
            else if (_state == GameState.Settings)
            {
                _menu.HandleSettingsInput(mousePos.X, mousePos.Y, screenW, screenH, MouseState, _settings,
                    () => VSync = _settings.VSync ? VSyncMode.On : VSyncMode.Off,
                    () => { /* Логика смены разрешения, если нужна мгновенная */ }
                );

                if (clicked && _menu.IsHovered(cx - 100, cy + 265, 200, 40, mousePos))
                {
                    _state = GameState.Paused;
                    _settings.Save();
                }
            }
        }

        private void ResumeGame()
        {
            _state = GameState.Playing;
            CursorState = CursorState.Grabbed;
            _firstMove = true;

            _appliedRenderDistance = _settings.RenderDistance;
            _appliedFov = _settings.Fov;
            _appliedFpsLimit = _settings.FpsLimit;
            _appliedTransparency = _settings.Transparency;
            _camera.Fov = _appliedFov;

            _frameTimer.Restart();
            _world.NeedsVisibilityUpdate = true;

            if (_settings.IsFullscreen)
            {
                if (WindowState != WindowState.Fullscreen) WindowState = WindowState.Fullscreen;
            }
            else
            {
                if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;

                var monitor = Monitors.GetMonitorFromWindow(this);
                int maxW = monitor.HorizontalResolution;
                int maxH = monitor.VerticalResolution - 120;
                int targetW = Math.Min(_settings.ResolutionWidth, maxW);
                int targetH = Math.Min(_settings.ResolutionHeight, maxH);
                var targetSize = new Vector2i(targetW, targetH);
                
                if (ClientSize != targetSize) ClientSize = targetSize;
                Location = new Vector2i((monitor.HorizontalResolution - targetW) / 2, (monitor.VerticalResolution - targetH) / 2);
            }
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            _frameTimer.Restart();

            _frameCount++;
            if (_fpsTimer.ElapsedMilliseconds >= 1000)
            {
                _currentFps = _frameCount;
                _frameCount = 0;
                _fpsTimer.Restart();
            }

            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            if (_appliedTransparency) { GL.Enable(EnableCap.Blend); GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); }
            else { GL.Disable(EnableCap.Blend); }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            var view = _camera.GetViewMatrix();
            var proj = _camera.GetProjectionMatrix((float)ClientSize.X / ClientSize.Y);

            _shader.Use();
            _shader.SetMatrix4("view", view);
            _shader.SetMatrix4("projection", proj);
            _shader.SetInt("textureAtlas", 0);
            _shader.SetInt("useTransparency", _appliedTransparency ? 1 : 0);
            _shader.SetVector3("cameraPos", _camera.Position);
            _shader.SetVector3("fogColor", FogColor);
            _shader.SetFloat("time", (float)_gameTimer.Elapsed.TotalSeconds);
            
            float maxViewDist = _appliedRenderDistance * Chunk.SizeX;
            _shader.SetFloat("fogStart", maxViewDist * 0.5f);
            _shader.SetFloat("fogEnd", maxViewDist * 0.95f);

            _atlas.Bind(TextureUnit.Texture0);

            bool cameraChanged =
                _camera.Position != _lastCameraPos ||
                _camera.Yaw != _lastCameraYaw ||
                _camera.Pitch != _lastCameraPitch ||
                _appliedRenderDistance != _lastDrawnRenderDistance ||
                !_visibleCacheValid ||
                _world.NeedsVisibilityUpdate;

            if (cameraChanged)
            {
                _frustum.Update(view * proj);
                _world.GetVisibleChunks(_camera.Position, _frustum, _visibleChunks, _appliedRenderDistance);
                _lastCameraPos = _camera.Position;
                _lastCameraYaw = _camera.Yaw;
                _lastCameraPitch = _camera.Pitch;
                _lastDrawnRenderDistance = _appliedRenderDistance;
                _visibleCacheValid = true;
                _world.NeedsVisibilityUpdate = false;
            }

            // 1. Отрисовка непрозрачных блоков
            GL.DepthMask(true);
            foreach (var chunk in _visibleChunks) chunk.OpaqueMesh?.Draw();

            // 2. Отрисовка выделения блока
            if (_state == GameState.Playing && _controller.Raycast(out var targetPos, out _))
            {
                _outline.Draw(new Vector3(targetPos.X, targetPos.Y, targetPos.Z), view, proj);
                _shader.Use(); // Возвращаем основной шейдер после OutlineRenderer
            }

                        // 3. Отрисовка прозрачных блоков (листья и вода)
            if (_appliedTransparency)
            {
                GL.Disable(EnableCap.CullFace);
                GL.DepthMask(true);
                foreach (var chunk in _visibleChunks) chunk.LeafMesh?.Draw();

                _waterComparer.CameraPos = _camera.Position;
                _waterSortScratch.Clear();
                foreach (var chunk in _visibleChunks)
                {
                    if (chunk.WaterMesh != null)
                        _waterSortScratch.Add(chunk);
                }
                _waterSortScratch.Sort(_waterComparer);

                // ВКЛЮЧАЕМ CullFace, чтобы не рисовать задние стенки водоёмов.
                // Это убирает "просвечивание" воды сквозь землю.
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(CullFaceMode.Back);
                GL.DepthMask(false);
                
                foreach (var chunk in _waterSortScratch) chunk.WaterMesh?.Draw();
                
                GL.DepthMask(true);
            }
            else
            {
                GL.DepthMask(true);
                foreach (var chunk in _visibleChunks) chunk.LeafMesh?.Draw();
                foreach (var chunk in _visibleChunks) chunk.WaterMesh?.Draw();
            }

            // 4. Отрисовка UI
            GL.Disable(EnableCap.DepthTest);
            _atlas.Bind(TextureUnit.Texture0);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            if (_state == GameState.Playing)
                _menu.DrawHUD(ClientSize.X, ClientSize.Y, _controller, _currentFps, ref _fpsText, ref _lastDrawnFps);
            else if (_state == GameState.Paused)
                _menu.DrawPauseMenu(ClientSize.X, ClientSize.Y, new Vector2(MousePosition.X, MousePosition.Y));
            else if (_state == GameState.Settings)
                _menu.DrawSettingsMenu(ClientSize.X, ClientSize.Y, new Vector2(MousePosition.X, MousePosition.Y), _settings);

            GL.Enable(EnableCap.DepthTest);
            SwapBuffers();

            // 5. Ограничение FPS
            if (!VSync.Equals(VSyncMode.On) && _appliedFpsLimit > 0)
            {
                double targetMs = 1000.0 / _appliedFpsLimit;
                double elapsedMs = _frameTimer.Elapsed.TotalMilliseconds;
                double sleepMs = targetMs - elapsedMs;
                if (sleepMs > 1.0) Thread.Sleep((int)sleepMs);
            }
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);
        }
    }
}