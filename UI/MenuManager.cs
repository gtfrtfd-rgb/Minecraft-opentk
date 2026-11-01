using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using MinecraftClone.Renderer;
using MinecraftClone.World;
using MinecraftClone.Settings;
using MinecraftClone.Player; // <--- Добавлено для PlayerController

namespace MinecraftClone.UI
{
    public class MenuManager
    {
        private readonly UIRenderer _ui;
        private readonly TextureAtlas _atlas;

        private int _activeSlider = 0;

        public int CurrentResolutionIndex { get; set; } = 2;

        public static readonly (int Width, int Height)[] AvailableResolutions = {
            (800, 600), (1024, 768), (1280, 720), (1366, 768), (1600, 900), (1920, 1080)
        };

        public MenuManager(UIRenderer ui, TextureAtlas atlas)
        {
            _ui = ui;
            _atlas = atlas;
        }

        public void DrawHUD(int screenW, int screenH, PlayerController controller, int currentFps, ref string fpsText, ref int lastDrawnFps)
        {
            _ui.SetScreenSize(screenW, screenH);
            _ui.Begin();

            float cx = screenW / 2f;
            float cy = screenH / 2f;

            // Прицел
            _ui.DrawSolidQuad(cx - 11, cy - 2, 22, 4, new Vector4(0, 0, 0, 0.8f));
            _ui.DrawSolidQuad(cx - 2, cy - 11, 4, 22, new Vector4(0, 0, 0, 0.8f));
            _ui.DrawSolidQuad(cx - 10, cy - 1, 20, 2, new Vector4(1, 1, 1, 1f));
            _ui.DrawSolidQuad(cx - 1, cy - 10, 2, 20, new Vector4(1, 1, 1, 1f));

            // FPS
            if (currentFps != lastDrawnFps)
            {
                fpsText = $"FPS: {currentFps}";
                lastDrawnFps = currentFps;
            }
            _ui.DrawText(fpsText, 20, 20, 4f, new Vector4(0, 0, 0, 0.8f));
            _ui.DrawText(fpsText, 19, 19, 4f, Vector4.One);

            // Координаты
            var pos = controller.Position;
            float feetY = pos.Y - 1.62f;
            string coordsText = $"X: {(int)MathF.Floor(pos.X)} Y: {(int)MathF.Floor(feetY)} Z: {(int)MathF.Floor(pos.Z)}";
            _ui.DrawText(coordsText, 20, 45, 3f, new Vector4(0, 0, 0, 0.8f));
            _ui.DrawText(coordsText, 19, 44, 3f, Vector4.One);

            // Хотбар
            float slotSize = 56f;
            float padding = 4f;
            float totalWidth = 9 * slotSize + 8 * padding;
            float startX = cx - totalWidth / 2f;
            float startY = screenH - slotSize - 20f;

            _ui.DrawSolidQuad(startX - 6, startY - 6, totalWidth + 12, slotSize + 12, new Vector4(0.1f, 0.1f, 0.1f, 0.6f));

            for (int i = 0; i < 9; i++)
            {
                float sx = startX + i * (slotSize + padding);
                bool isSelected = (i + 1) == (int)controller.SelectedBlock;

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

            _ui.End();
        }

        public void DrawPauseMenu(int screenW, int screenH, Vector2 mousePos)
        {
            _ui.SetScreenSize(screenW, screenH);
            _ui.Begin();

            float cx = screenW / 2f;
            float cy = screenH / 2f;

            _ui.DrawSolidQuad(0, 0, screenW, screenH, new Vector4(0, 0, 0, 0.5f));
            _ui.DrawText("GAME PAUSED", cx - 110, cy - 100, 5f, Vector4.One);

            _ui.DrawButton(cx - 100, cy - 20, 200, 40, "RESUME", IsHovered(cx - 100, cy - 20, 200, 40, mousePos));
            _ui.DrawButton(cx - 100, cy + 30, 200, 40, "SETTINGS", IsHovered(cx - 100, cy + 30, 200, 40, mousePos));
            _ui.DrawButton(cx - 100, cy + 80, 200, 40, "QUIT GAME", IsHovered(cx - 100, cy + 80, 200, 40, mousePos));

            _ui.End();
        }

        public void DrawSettingsMenu(int screenW, int screenH, Vector2 mousePos, GameSettings settings)
        {
            _ui.SetScreenSize(screenW, screenH);
            _ui.Begin();

            float cx = screenW / 2f;
            float cy = screenH / 2f;

            _ui.DrawSolidQuad(0, 0, screenW, screenH, new Vector4(0, 0, 0, 0.7f));
            _ui.DrawText("SETTINGS", cx - 80, cy - 170, 5f, Vector4.One);

            _ui.DrawText($"RENDER DISTANCE: {settings.RenderDistance}", cx - 150, cy - 140, 3f, Vector4.One);
            float distPct = (settings.RenderDistance - 2) / 18f;
            _ui.DrawSlider(cx - 150, cy - 120, 300, 10, distPct, 0, 1, IsHovered(cx - 150, cy - 120, 300, 20, mousePos));

            _ui.DrawText($"FOV: {(int)settings.Fov}", cx - 150, cy - 95, 3f, Vector4.One);
            float fovPct = (settings.Fov - 50f) / 60f;
            _ui.DrawSlider(cx - 150, cy - 75, 300, 10, fovPct, 0, 1, IsHovered(cx - 150, cy - 75, 300, 20, mousePos));

            _ui.DrawText($"SENSITIVITY: {settings.MouseSensitivity:F2}", cx - 150, cy - 50, 3f, Vector4.One);
            float sensPct = (settings.MouseSensitivity - 0.05f) / 0.45f;
            _ui.DrawSlider(cx - 150, cy - 30, 300, 10, sensPct, 0, 1, IsHovered(cx - 150, cy - 30, 300, 20, mousePos));

            _ui.DrawCheckbox(cx - 150, cy + 5, 30, "VSYNC", settings.VSync, IsHovered(cx - 150, cy + 5, 30, 30, mousePos));
            _ui.DrawCheckbox(cx - 150, cy + 40, 30, "TRANSPARENCY", settings.Transparency, IsHovered(cx - 150, cy + 40, 30, 30, mousePos));

            string resText = $"{settings.ResolutionWidth}x{settings.ResolutionHeight}";
            _ui.DrawButton(cx - 100, cy + 75, 200, 40, resText, IsHovered(cx - 100, cy + 75, 200, 40, mousePos));
            _ui.DrawCheckbox(cx - 150, cy + 120, 30, "FULLSCREEN", settings.IsFullscreen, IsHovered(cx - 150, cy + 120, 30, 30, mousePos));

            string fpsText = settings.FpsLimit == 0 ? "UNLIMITED" : settings.FpsLimit.ToString();
            Vector4 fpsColor = settings.VSync ? new Vector4(0.5f, 0.5f, 0.5f, 1f) : Vector4.One;
            _ui.DrawText($"FPS LIMIT: {fpsText}", cx - 150, cy + 155, 3f, fpsColor);

            float fpsPct = settings.FpsLimit == 0 ? 0f : MathHelper.Clamp((settings.FpsLimit - 30) / 470f, 0f, 1f);
            _ui.DrawSlider(cx - 150, cy + 175, 300, 10, fpsPct, 0, 1, !settings.VSync && IsHovered(cx - 150, cy + 175, 300, 20, mousePos));

            _ui.DrawButton(cx - 100, cy + 215, 200, 40, "RESET", IsHovered(cx - 100, cy + 215, 200, 40, mousePos));
            _ui.DrawButton(cx - 100, cy + 265, 200, 40, "BACK", IsHovered(cx - 100, cy + 265, 200, 40, mousePos));

            _ui.End();
        }

        public void HandleSettingsInput(float mouseX, float mouseY, int screenW, int screenH, MouseState mouseState, GameSettings settings, Action onVSliderChange, Action onResolutionChange)
        {
            bool clicked = mouseState.IsButtonPressed(MouseButton.Left);
            bool released = mouseState.IsButtonReleased(MouseButton.Left);

            if (released) _activeSlider = 0;

            float cx = screenW / 2f;
            float cy = screenH / 2f;
            Vector2 mousePos = new Vector2(mouseX, mouseY);

            if (clicked && IsHovered(cx - 150, cy - 120, 300, 20, mousePos)) _activeSlider = 1;
            if (_activeSlider == 1)
            {
                float pct = Math.Clamp((mouseX - (cx - 150)) / 300f, 0f, 1f);
                settings.RenderDistance = (int)MathF.Round(MathHelper.Lerp(2, 20, pct));
            }

            if (clicked && IsHovered(cx - 150, cy - 75, 300, 20, mousePos)) _activeSlider = 2;
            if (_activeSlider == 2)
            {
                float pct = Math.Clamp((mouseX - (cx - 150)) / 300f, 0f, 1f);
                settings.Fov = MathHelper.Lerp(50f, 110f, pct);
            }

            if (clicked && IsHovered(cx - 150, cy - 30, 300, 20, mousePos)) _activeSlider = 3;
            if (_activeSlider == 3)
            {
                float pct = Math.Clamp((mouseX - (cx - 150)) / 300f, 0f, 1f);
                settings.MouseSensitivity = MathHelper.Lerp(0.05f, 0.5f, pct);
            }

            if (clicked && IsHovered(cx - 150, cy + 5, 30, 30, mousePos))
            {
                settings.VSync = !settings.VSync;
                onVSliderChange?.Invoke();
            }

            if (clicked && IsHovered(cx - 150, cy + 40, 30, 30, mousePos))
            {
                settings.Transparency = !settings.Transparency;
            }

            if (clicked && IsHovered(cx - 100, cy + 75, 200, 40, mousePos))
            {
                CurrentResolutionIndex = (CurrentResolutionIndex + 1) % AvailableResolutions.Length;
                settings.ResolutionWidth = AvailableResolutions[CurrentResolutionIndex].Width;
                settings.ResolutionHeight = AvailableResolutions[CurrentResolutionIndex].Height;
                onResolutionChange?.Invoke();
            }

            if (clicked && IsHovered(cx - 150, cy + 120, 30, 30, mousePos))
            {
                settings.IsFullscreen = !settings.IsFullscreen;
            }

            if (!settings.VSync)
            {
                if (clicked && IsHovered(cx - 150, cy + 175, 300, 20, mousePos)) _activeSlider = 4;
                if (_activeSlider == 4)
                {
                    float pct = Math.Clamp((mouseX - (cx - 150)) / 300f, 0f, 1f);
                    if (pct < 0.05f) settings.FpsLimit = 0;
                    else settings.FpsLimit = (int)MathF.Round(MathHelper.Lerp(30, 500, pct));
                }
            }

            if (clicked && IsHovered(cx - 100, cy + 215, 200, 40, mousePos))
            {
                settings.Reset();
                onVSliderChange?.Invoke();
                CurrentResolutionIndex = 2;
                settings.Save();
            }
        }

        public bool IsHovered(float x, float y, float w, float h, Vector2 mousePos)
        {
            return mousePos.X >= x && mousePos.X <= x + w &&
                   mousePos.Y >= y && mousePos.Y <= y + h;
        }
    }
}