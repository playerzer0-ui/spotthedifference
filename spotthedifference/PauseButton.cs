using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace spotthedifference
{
    public enum MenuIcon { Pause, AddCircle, Reset, Save, Home }
    /// <summary>A diamond pause button that morphs to a square and toggles to a red X.</summary>
    public sealed class PauseButton : IDisposable
    {
        private readonly Game game;
        private readonly float size;
        private MouseState previousMouse;
        private bool armed;
        private float rotation = MathHelper.PiOver4;
        private float iconProgress;
        private Vector4 currentColor;
        private readonly RenderTarget2D artwork;
        private readonly SpriteBatch artworkBatch;
        private readonly int artworkSize;
        private const int Supersampling = 4;

        // The collider encloses the diamond throughout the rotation.
        public CollisionRect Collider { get; }
        public Func<Vector2, Vector2> ScreenToLocal { get; set; }
        public bool IsHovered { get; private set; }
        public bool IsPaused { get; set; }
        public bool Enabled { get; set; } = true;
        public float SquareAmount { get; set; }
        public MenuIcon Icon { get; set; } = MenuIcon.Pause;
        /// <summary>Optional content-managed icon, drawn upright independently of the background.</summary>
        public Texture2D IconTexture { get; set; }
        public event Action Clicked;
        public event Action<bool> PauseChanged;
        public Color IdleColor { get; set; } = new Color(208, 108, 202);
        public Color HoverColor { get; set; } = new Color(240, 207, 237);
        public Color PausedColor { get; set; } = Color.Red;
        public float AnimationSpeed { get; set; } = 12f;
        public float IconTransitionSeconds { get; set; } = 0.35f;

        /// <param name="size">Side length of the square; the idle diamond extends farther.</param>
        public PauseButton(Game game, Vector2 center, int size = 100)
        {
            if (size < 24) throw new ArgumentOutOfRangeException(nameof(size), "Use at least 24 pixels.");
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.size = size;
            int boundsSize = (int)MathF.Ceiling(size * MathF.Sqrt(2));
            Collider = new CollisionRect((int)MathF.Round(center.X), (int)MathF.Round(center.Y),
                boundsSize, boundsSize);
            currentColor = IdleColor.ToVector4();
            artworkSize = boundsSize + 8;
            artwork = new RenderTarget2D(game.GraphicsDevice,
                artworkSize * Supersampling, artworkSize * Supersampling);
            artworkBatch = new SpriteBatch(game.GraphicsDevice);
        }

        public void Update(GameTime gameTime)
        {
            MouseState mouse = Mouse.GetState();
            Vector2 screen = new Vector2(mouse.X, mouse.Y);
            Vector2 position = ScreenToLocal?.Invoke(screen) ?? screen;
            IsHovered = Enabled && game.IsActive && HitTest(position);

            if (!game.IsActive || !Enabled) armed = false;
            else if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
                armed = IsHovered;
            else if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
            {
                if (armed && IsHovered)
                {
                    if (Icon == MenuIcon.Pause)
                    {
                        IsPaused = !IsPaused;
                        PauseChanged?.Invoke(IsPaused);
                    }
                    Clicked?.Invoke();
                }
                armed = false;
            }

            float delta = Math.Max(0, (float)gameTime.ElapsedGameTime.TotalSeconds);
            float blend = 1f - MathF.Exp(-Math.Max(0, AnimationSpeed) * delta);
            float targetRotation = IsPaused || IsHovered ? 0f
                : MathHelper.PiOver4 * (1f - MathHelper.Clamp(SquareAmount, 0f, 1f));
            rotation = MathHelper.Lerp(rotation, targetRotation, blend);
            float iconStep = delta / Math.Max(0.01f, IconTransitionSeconds);
            iconProgress = IsPaused ? Math.Min(1f, iconProgress + iconStep)
                : Math.Max(0f, iconProgress - iconStep);
            currentColor = Vector4.Lerp(currentColor,
                (IsPaused ? PausedColor : IsHovered ? HoverColor : IdleColor).ToVector4(), blend);
            previousMouse = mouse;
        }

        public bool HitTest(Vector2 position)
        {
            if (!Collider.Contains(new Point((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y))))
                return false;
            // Refine the rectangular hit area so the empty diamond corners don't activate it.
            Vector2 local = Vector2.Transform(position - Collider.Center, Matrix.CreateRotationZ(-rotation));
            return MathF.Abs(local.X) <= size / 2f && MathF.Abs(local.Y) <= size / 2f;
        }

        /// <summary>
        /// Call before beginning the scene or activating its canvas. Renders at four times
        /// the button resolution so rotated edges can be smoothly downsampled.
        /// </summary>
        public void PrepareDraw()
        {
            GraphicsDevice device = game.GraphicsDevice;
            RenderTargetBinding[] previousTargets = device.GetRenderTargets();
            device.SetRenderTarget(artwork);
            device.Clear(Color.Transparent);
            artworkBatch.Begin(samplerState: SamplerState.LinearClamp,
                transformMatrix: Matrix.CreateScale(Supersampling));
            Vector2 center = new Vector2(artworkSize / 2f);
            float border = Math.Max(2f, size * 0.04f);
            DrawRectangle(center, new Vector2(size), rotation, Color.Black);
            DrawRectangle(center, new Vector2(size - border * 2f), rotation, new Color(currentColor));

            if (Icon != MenuIcon.Pause)
            {
                if (IconTexture != null)
                {
                    float scale = size * 0.65f / Math.Max(IconTexture.Width, IconTexture.Height);
                    artworkBatch.Draw(IconTexture, center, null, Color.White, 0,
                        new Vector2(IconTexture.Width / 2f, IconTexture.Height / 2f), scale, SpriteEffects.None, 0);
                }
                else
                    DrawMenuIcon(center, border);
                artworkBatch.End();
                device.SetRenderTargets(previousTargets);
                return;
            }

            // First merge the pause bars, then tilt them. Reversing straightens them before separating.
            float merge = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(iconProgress / 0.45f, 0f, 1f));
            float tilt = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp((iconProgress - 0.25f) / 0.75f, 0f, 1f));
            float separation = size * 0.12f * (1f - merge);
            Vector2 barSize = new Vector2(size * 0.19f, size * 0.57f);
            DrawBar(center + new Vector2(-separation, 0), barSize,
                tilt * MathHelper.PiOver4, border);
            DrawBar(center + new Vector2(separation, 0), barSize,
                -tilt * MathHelper.PiOver4, border);
            artworkBatch.End();
            device.SetRenderTargets(previousTargets);
        }

        /// <summary>Call inside a SpriteBatch using LinearClamp after PrepareDraw.</summary>
        public void Draw()
        {
            Globals.spriteBatch.Draw(artwork, Collider.Center, null, Color.White, 0f,
                new Vector2(artwork.Width / 2f, artwork.Height / 2f),
                1f / Supersampling, SpriteEffects.None, 0f);
        }

        public void Dispose()
        {
            artwork.Dispose();
            artworkBatch.Dispose();
        }

        private void DrawBar(Vector2 center, Vector2 dimensions, float angle, float border)
        {
            DrawRectangle(center, dimensions, angle, Color.Black);
            DrawRectangle(center, dimensions - new Vector2(border * 2f), angle, Color.White);
        }

        private void DrawMenuIcon(Vector2 center, float border)
        {
            if (Icon == MenuIcon.AddCircle)
            {
                float radius = size * 0.28f;
                for (int i = 0; i < 80; i++)
                {
                    float a = i * MathHelper.TwoPi / 80f;
                    float b = (i + 1) * MathHelper.TwoPi / 80f;
                    DrawLine(center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius,
                        center + new Vector2(MathF.Cos(b), MathF.Sin(b)) * radius, border, Color.Red);
                }
                Vector2 plus = center + new Vector2(size * 0.21f, -size * 0.16f);
                DrawRectangle(plus, new Vector2(size * 0.32f, size * 0.12f), 0, Color.Black);
                DrawRectangle(plus, new Vector2(size * 0.12f, size * 0.32f), 0, Color.Black);
                DrawRectangle(plus, new Vector2(size * 0.32f - border * 2, size * 0.12f - border), 0, Color.LightGreen);
                DrawRectangle(plus, new Vector2(size * 0.12f - border, size * 0.32f - border * 2), 0, Color.LightGreen);
            }
            else if (Icon == MenuIcon.Home)
            {
                DrawRectangle(center + new Vector2(0, size * 0.12f), new Vector2(size * 0.45f, size * 0.4f), 0, Color.Black);
                DrawRectangle(center + new Vector2(0, size * 0.12f), new Vector2(size * 0.45f - border * 2, size * 0.4f - border * 2), 0, Color.White);
                Vector2 peak = center - new Vector2(0, size * 0.3f);
                Vector2 left = center + new Vector2(-size * 0.29f, -size * 0.06f);
                Vector2 right = center + new Vector2(size * 0.29f, -size * 0.06f);
                DrawTriangle(peak, left, right, Color.White);
                DrawLine(peak, left, border, Color.Black);
                DrawLine(peak, right, border, Color.Black);
                DrawLine(left, right, border, Color.Black);
            }
            else
            {
                // Angular return arrow, matching the reset symbol in the reference.
                Vector2 a = center + new Vector2(-size * 0.22f, size * 0.28f);
                Vector2 b = center + new Vector2(-size * 0.22f, -size * 0.2f);
                Vector2 c = center + new Vector2(size * 0.2f, -size * 0.2f);
                Vector2 d = center + new Vector2(size * 0.2f, size * 0.1f);
                float width = size * 0.16f;
                DrawLine(a, b, width, Color.Black);
                DrawLine(b, c, width, Color.Black);
                DrawLine(c, d, width, Color.Black);
                DrawLine(a, b, width - border * 2, Color.White);
                DrawLine(b, c, width - border * 2, Color.White);
                DrawLine(c, d, width - border * 2, Color.White);
                DrawTriangle(d + new Vector2(-size * 0.2f, -size * 0.04f),
                    d + new Vector2(size * 0.2f, -size * 0.04f), d + new Vector2(0, size * 0.18f), Color.Black);
                DrawTriangle(d + new Vector2(-size * 0.11f, 0),
                    d + new Vector2(size * 0.11f, 0), d + new Vector2(0, size * 0.12f), Color.White);
            }
        }

        private void DrawLine(Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 delta = end - start;
            DrawRectangle((start + end) / 2, new Vector2(delta.Length() + width * 0.25f, width),
                MathF.Atan2(delta.Y, delta.X), color);
        }

        private void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            // These icon triangles have a horizontal base; fill with supersampled scanlines.
            if (MathF.Abs(a.Y - b.Y) < 0.01f)
            {
                Vector2 temporary = a; a = c; c = b; b = temporary;
            }
            float height = b.Y - a.Y;
            int rows = (int)MathF.Ceiling(MathF.Abs(height) * Supersampling);
            for (int i = 0; i <= rows; i++)
            {
                float t = rows == 0 ? 0 : (float)i / rows;
                Vector2 left = Vector2.Lerp(a, b, t);
                Vector2 right = Vector2.Lerp(a, c, t);
                DrawRectangle((left + right) / 2, new Vector2(MathF.Abs(right.X - left.X), 1f / Supersampling), 0, color);
            }
        }

        private void DrawRectangle(Vector2 center, Vector2 dimensions, float angle, Color color)
        {
            artworkBatch.Draw(Globals.Pixel, center, null, color, angle,
                new Vector2(0.5f), dimensions, SpriteEffects.None, 0f);
        }
    }
}
