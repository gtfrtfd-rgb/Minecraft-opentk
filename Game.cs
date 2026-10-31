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

namespace MinecraftClone
{
    public enum GameState
    {
        Playing,
        Paused,
        Settings
    }

    public class Game : GameWindow
    {
        private World.World _world = null!;
        private Renderer.Shader _shader = null!;
        private Renderer.TextureAtlas _atlas = null!;
        private Renderer.UIRenderer _ui = null!;
        private Renderer.OutlineRenderer _outline = null!;
        private Player.Camera _camera = null!;
        private Player.PlayerController _controller = null!;

        private GameSettings _settings = null!;
        private GameState _state = GameState.Playing;
        private int _activeSlider = 0;
        private int _currentResolutionIndex = 2;

        private static readonly (int Width, int Height)[] AvailableResolutions = {
            (800, 600), (1024, 768), (1280, 720), (1366, 768), (1600, 900), (1920, 1080)
        };

        private int _appliedRenderDistance = 5;
        private float _appliedFov = 75f;
        private int _appliedFpsLimit = 60;
        private bool _appliedTransparency = true;

        private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
        private readonly Stopwatch _frameTimer = new Stopwatch();
        private int _frameCount = 0;
        private int _currentFps = 0;

        private readonly Frustum _frustum = new();
        private readonly List<Chunk> _visibleChunks = new();

        private Vector3 _lastCameraPos;
        private float _lastCameraYaw;
        private float _lastCameraPitch;
        private int _lastDrawnRenderDistance = -1;
        private bool _visibleCacheValid = false;

        private Vector2 _lastMousePos;
        private bool _firstMove = true;

        private string _fpsText = "FPS: 0";
        private int _lastDrawnFps = -1;

        // Цвет тумана (должен совпадать с ClearColor)
        private static readonly Vector3 FogColor = new Vector3(0.5f, 0.7f, 1.0f);

        public Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

        protected override void OnLoad()
        {
            base.OnLoad();
            MakeCurrent();

            _settings = GameSettings.Load();

            _currentResolutionIndex = Array.FindIndex(AvailableResolutions, r => r.Width == _settings.ResolutionWidth && r.Height == _settings.ResolutionHeight);
            if (_currentResolutionIndex == -1)
            {
                _settings.ResolutionWidth = 1280;
                _settings.ResolutionHeight = 720;
                _currentResolutionIndex = 2;
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

            _shader = new Renderer.Shader();
            _atlas = new Renderer.TextureAtlas(16, 16);
            _ui = new Renderer.UIRenderer();
            _outline = new Renderer.OutlineRenderer();
            _world = new World.World();

            _camera = new Player.Camera(Vector3.Zero);
            _camera.Fov = _appliedFov;
            _controller = new Player.PlayerController(_camera, _world, new Vector3(0.5f, 0f, 0.5f));

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
                if (_state == GameState.Playing)
                {
                    _state = GameState.Paused;
                    CursorState = CursorState.Normal;
                    _settings.Save();
                }
                else if (_state == GameState.Paused)
                {
                    ResumeGame();
                }
                else if (_state == GameState.Settings)
                {
                    _state = GameState.Paused;
                    _settings.Save();
                }
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
                if (WindowState != WindowState.Fullscreen)
                    WindowState = WindowState.Fullscreen;
            }
            else
            {
                if (WindowState != WindowState.Normal)
                    WindowState = WindowState.Normal;

                var monitor = Monitors.GetMonitorFromWindow(this);
                
                int maxW = monitor.HorizontalResolution;
                int maxH = monitor.VerticalResolution - 120;
                
                int targetW = Math.Min(_settings.ResolutionWidth, maxW);
                int targetH = Math.Min(_settings.ResolutionHeight, maxH);
                
                var targetSize = new Vector2i(targetW, targetH);
                if (ClientSize != targetSize)
                {
                    ClientSize = targetSize;
                }

                Location = new Vector2i(
                    (monitor.HorizontalResolution - targetW) / 2,
                    (monitor.VerticalResolution - targetH) / 2
                );
            }
        }

        private void HandleMenuInput()
        {
            float mouseX = MousePosition.X;
            float mouseY = MousePosition.Y;
            bool clicked = MouseState.IsButtonPressed(MouseButton.Left);
            bool released = MouseState.IsButtonReleased(MouseButton.Left);

            if (released) _activeSlider = 0;

            int screenW = ClientSize.X;
            int screenH = ClientSize.Y;
            float cx = screenW / 2f;
            float cy = screenH / 2f;

            if (_state == GameState.Paused)
            {
                if (clicked && IsHovered(cx - 100, cy - 20, 200, 40, mouseX, mouseY)) ResumeGame();
                if (clicked && IsHovered(cx - 100, cy + 30, 200, 40, mouseX, mouseY)) _state = GameState.Settings;
                if (clicked && IsHovered(cx - 100, cy + 80, 200, 40, mouseX, mouseY))
                {
                    _settings.Save();
                    Close();
                }
            }
            else if (_state == GameState.Settings)
            {
                if (clicked && IsHovered(cx - 150, cy - 120, 300, 20, mouseX, mouseY)) _activeSlider = 1;
                if (_activeSlider == 1)
                {
                    float pct = Math.Clamp((mouseX - (cx - 150)) / 300f, 0f, 1f);
                    _settings.RenderDistance = (int)MathF.Round(MathHelper.Lerp(2, 20, pct));
                }

                if (clicked && IsHovered(cx - 150, cy - 75, 300, 20, mouseX, mouseY)) _activeSlider = 2;
                if (_activeSlider == 2)
                {
                    float pct = Math.Clamp((mouseX - (cx - 150)) / 300f, 0f, 1f);
                    _settings.Fov = MathHelper.Lerp(50f, 110f, pct);
                }

                if (clicked && IsHovered(cx - 150, cy - 30, 300, 20, mouseX, mouseY)) _activeSlider = 3;
                if (_activeSlider == 3)
                {
                    float pct = Math.Clamp((mouseX - (cx - 150)) / 300f, 0f, 1f);
                    _settings.MouseSensitivity = MathHelper.Lerp(0.05f, 0.5f, pct);
                }

                if (clicked && IsHovered(cx - 150, cy + 5, 30, 30, mouseX, mouseY))
                {
                    _settings.VSync = !_settings.VSync;
                    VSync = _settings.VSync ? VSyncMode.On : VSyncMode.Off;
                }

                if (clicked && IsHovered(cx - 150, cy + 40, 30, 30, mouseX, mouseY))
                {
                    _settings.Transparency = !_settings.Transparency;
                }

                if (clicked && IsHovered(cx - 100, cy + 75, 200, 40, mouseX, mouseY))
                {
                    _currentResolutionIndex = (_currentResolutionIndex + 1) % AvailableResolutions.Length;
                    _settings.ResolutionWidth = AvailableResolutions[_currentResolutionIndex].Width;
                    _settings.ResolutionHeight = AvailableResolutions[_currentResolutionIndex].Height;
                }

                if (clicked && IsHovered(cx - 150, cy + 120, 30, 30, mouseX, mouseY))
                {
                    _settings.IsFullscreen = !_settings.IsFullscreen;
                }

                if (!_settings.VSync)
                {
                    if (clicked && IsHovered(cx - 150, cy + 175, 300, 20, mouseX, mouseY)) _activeSlider = 4;
                    if (_activeSlider == 4)
                    {
                        float pct = Math.Clamp((mouseX - (cx - 150)) / 300f, 0f, 1f);
                        if (pct < 0.05f) _settings.FpsLimit = 0;
                        else _settings.FpsLimit = (int)MathF.Round(MathHelper.Lerp(30, 500, pct));
                    }
                }

                if (clicked && IsHovered(cx - 100, cy + 215, 200, 40, mouseX, mouseY))
                {
                    _settings.Reset();
                    VSync = _settings.VSync ? VSyncMode.On : VSyncMode.Off;
                    _currentResolutionIndex = 2;
                    _settings.Save();
                }

                if (clicked && IsHovered(cx - 100, cy + 265, 200, 40, mouseX, mouseY))
                {
                    _state = GameState.Paused;
                    _settings.Save();
                }
            }
        }

        private bool IsHovered(float x, float y, float w, float h, float mouseX, float mouseY)
        {
            return mouseX >= x && mouseX <= x + w &&
                   mouseY >= y && mouseY <= y + h;
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

            if (_appliedTransparency)
            {
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            }
            else
            {
                GL.Disable(EnableCap.Blend);
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            var view = _camera.GetViewMatrix();
            var proj = _camera.GetProjectionMatrix((float)ClientSize.X / ClientSize.Y);

            _shader.Use();
            _shader.SetMatrix4("view", view);
            _shader.SetMatrix4("projection", proj);
            _shader.SetInt("textureAtlas", 0);
            _shader.SetInt("useTransparency", _appliedTransparency ? 1 : 0);

            // Настройки тумана
            _shader.SetVector3("cameraPos", _camera.Position);
            _shader.SetVector3("fogColor", FogColor);
            
            // Туман начинается на 50% дистанции и заканчивается чуть раньше края чанков
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

            GL.DepthMask(true);
            foreach (var chunk in _visibleChunks) chunk.OpaqueMesh?.Draw();

            if (_state == GameState.Playing && _controller.Raycast(out var targetPos, out _))
            {
                _outline.Draw(new Vector3(targetPos.X, targetPos.Y, targetPos.Z), view, proj);
                _shader.Use();
            }

            if (_appliedTransparency)
            {
                GL.Disable(EnableCap.CullFace);
                GL.DepthMask(true);
                foreach (var chunk in _visibleChunks) chunk.LeafMesh?.Draw();

                GL.DepthMask(false);
                foreach (var chunk in _visibleChunks) chunk.WaterMesh?.Draw();
                GL.DepthMask(true);
                GL.Enable(EnableCap.CullFace);
            }
            else
            {
                GL.DepthMask(true);
                foreach (var chunk in _visibleChunks) chunk.LeafMesh?.Draw();
                foreach (var chunk in _visibleChunks) chunk.WaterMesh?.Draw();
            }

            GL.Disable(EnableCap.DepthTest);
            _atlas.Bind(TextureUnit.Texture0);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            int screenW = ClientSize.X;
            int screenH = ClientSize.Y;
            _ui.SetScreenSize(screenW, screenH);
            _ui.Begin();

            float cx = screenW / 2f;
            float cy = screenH / 2f;

            if (_state == GameState.Playing)
            {
                DrawHUD(cx, cy, screenH);
            }
            else if (_state == GameState.Paused)
            {
                DrawPauseMenu(cx, cy);
            }
            else if (_state == GameState.Settings)
            {
                DrawSettingsMenu(cx, cy);
            }

            _ui.End();
            GL.Enable(EnableCap.DepthTest);
            SwapBuffers();

            if (!VSync.Equals(VSyncMode.On) && _appliedFpsLimit > 0)
            {
                double targetMs = 1000.0 / _appliedFpsLimit;
                double elapsedMs = _frameTimer.Elapsed.TotalMilliseconds;
                double sleepMs = targetMs - elapsedMs;

                if (sleepMs > 1.0)
                {
                    Thread.Sleep((int)sleepMs);
                }
            }
        }

        private void DrawHUD(float cx, float cy, float screenH)
        {
            _ui.DrawSolidQuad(cx - 11, cy - 2, 22, 4, new Vector4(0, 0, 0, 0.8f));
            _ui.DrawSolidQuad(cx - 2, cy - 11, 4, 22, new Vector4(0, 0, 0, 0.8f));
            _ui.DrawSolidQuad(cx - 10, cy - 1, 20, 2, new Vector4(1, 1, 1, 1f));
            _ui.DrawSolidQuad(cx - 1, cy - 10, 2, 20, new Vector4(1, 1, 1, 1f));

            if (_currentFps != _lastDrawnFps)
            {
                _fpsText = $"FPS: {_currentFps}";
                _lastDrawnFps = _currentFps;
            }
            _ui.DrawText(_fpsText, 20, 20, 4f, new Vector4(0, 0, 0, 0.8f));
            _ui.DrawText(_fpsText, 19, 19, 4f, Vector4.One);

            float slotSize = 56f;
            float padding = 4f;
            float totalWidth = 9 * slotSize + 8 * padding;
            float startX = cx - totalWidth / 2f;
            float startY = screenH - slotSize - 20f;

            _ui.DrawSolidQuad(startX - 6, startY - 6, totalWidth + 12, slotSize + 12, new Vector4(0.1f, 0.1f, 0.1f, 0.6f));

            for (int i = 0; i < 9; i++)
            {
                float sx = startX + i * (slotSize + padding);
                bool isSelected = (i + 1) == (int)_controller.SelectedBlock;

                if (isSelected)
                {
                    _ui.DrawSolidQuad(sx - 4, startY - 4, slotSize + 8, slotSize + 8, new Vector4(0.9f, 0.9f, 0.9f, 1f));
                    _ui.DrawSolidQuad(sx, startY, slotSize, slotSize, new Vector4(0.3f, 0.3f, 0.3f, 0.9f));
                }
                else
                {
                    _ui.DrawSolidQuad(sx, startY, slotSize, slotSize, new Vector4(0.4f, 0.4f, 0.4f, 0.6f));
                }

                if (i + 1 <= (int)BlockType.Water)
                {
                    BlockType blockToDraw = (BlockType)(i + 1);
                    Vector2 tileCoords = BlockData.GetTileCoords(blockToDraw, 1, 0);
                    var uv = new Vector4(tileCoords.X / 16f, tileCoords.Y / 16f, (tileCoords.X + 1) / 16f, (tileCoords.Y + 1) / 16f);

                    float itemSize = isSelected ? slotSize - 8 : slotSize - 16;
                    float itemOffset = (slotSize - itemSize) / 2f;
                    _ui.DrawQuad(sx + itemOffset, startY + itemOffset, itemSize, itemSize, uv, Vector4.One);
                }
            }
        }

        private void DrawPauseMenu(float cx, float cy)
        {
            _ui.DrawSolidQuad(0, 0, ClientSize.X, ClientSize.Y, new Vector4(0, 0, 0, 0.5f));
            _ui.DrawText("GAME PAUSED", cx - 110, cy - 100, 5f, Vector4.One);

            float mx = MousePosition.X;
            float my = MousePosition.Y;

            _ui.DrawButton(cx - 100, cy - 20, 200, 40, "RESUME", IsHovered(cx - 100, cy - 20, 200, 40, mx, my));
            _ui.DrawButton(cx - 100, cy + 30, 200, 40, "SETTINGS", IsHovered(cx - 100, cy + 30, 200, 40, mx, my));
            _ui.DrawButton(cx - 100, cy + 80, 200, 40, "QUIT GAME", IsHovered(cx - 100, cy + 80, 200, 40, mx, my));
        }

        private void DrawSettingsMenu(float cx, float cy)
        {
            _ui.DrawSolidQuad(0, 0, ClientSize.X, ClientSize.Y, new Vector4(0, 0, 0, 0.7f));
            _ui.DrawText("SETTINGS", cx - 80, cy - 170, 5f, Vector4.One);

            float mx = MousePosition.X;
            float my = MousePosition.Y;

            _ui.DrawText($"RENDER DISTANCE: {_settings.RenderDistance}", cx - 150, cy - 140, 3f, Vector4.One);
            float distPct = (_settings.RenderDistance - 2) / 18f;
            _ui.DrawSlider(cx - 150, cy - 120, 300, 10, distPct, 0, 1, IsHovered(cx - 150, cy - 120, 300, 20, mx, my));

            _ui.DrawText($"FOV: {(int)_settings.Fov}", cx - 150, cy - 95, 3f, Vector4.One);
            float fovPct = (_settings.Fov - 50f) / 60f;
            _ui.DrawSlider(cx - 150, cy - 75, 300, 10, fovPct, 0, 1, IsHovered(cx - 150, cy - 75, 300, 20, mx, my));

            _ui.DrawText($"SENSITIVITY: {_settings.MouseSensitivity:F2}", cx - 150, cy - 50, 3f, Vector4.One);
            float sensPct = (_settings.MouseSensitivity - 0.05f) / 0.45f;
            _ui.DrawSlider(cx - 150, cy - 30, 300, 10, sensPct, 0, 1, IsHovered(cx - 150, cy - 30, 300, 20, mx, my));

            _ui.DrawCheckbox(cx - 150, cy + 5, 30, "VSYNC", _settings.VSync, IsHovered(cx - 150, cy + 5, 30, 30, mx, my));
            _ui.DrawCheckbox(cx - 150, cy + 40, 30, "TRANSPARENCY", _settings.Transparency, IsHovered(cx - 150, cy + 40, 30, 30, mx, my));

            string resText = $"{_settings.ResolutionWidth}x{_settings.ResolutionHeight}";
            _ui.DrawButton(cx - 100, cy + 75, 200, 40, resText, IsHovered(cx - 100, cy + 75, 200, 40, mx, my));
            _ui.DrawCheckbox(cx - 150, cy + 120, 30, "FULLSCREEN", _settings.IsFullscreen, IsHovered(cx - 150, cy + 120, 30, 30, mx, my));

            string fpsText = _settings.FpsLimit == 0 ? "UNLIMITED" : _settings.FpsLimit.ToString();
            Vector4 fpsColor = _settings.VSync ? new Vector4(0.5f, 0.5f, 0.5f, 1f) : Vector4.One;
            _ui.DrawText($"FPS LIMIT: {fpsText}", cx - 150, cy + 155, 3f, fpsColor);

            float fpsPct = _settings.FpsLimit == 0 ? 0f : MathHelper.Clamp((_settings.FpsLimit - 30) / 470f, 0f, 1f);
            _ui.DrawSlider(cx - 150, cy + 175, 300, 10, fpsPct, 0, 1, !_settings.VSync && IsHovered(cx - 150, cy + 175, 300, 20, mx, my));

            _ui.DrawButton(cx - 100, cy + 215, 200, 40, "RESET", IsHovered(cx - 100, cy + 215, 200, 40, mx, my));
            _ui.DrawButton(cx - 100, cy + 265, 200, 40, "BACK", IsHovered(cx - 100, cy + 265, 200, 40, mx, my));
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);
        }
    }
}