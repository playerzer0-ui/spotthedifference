using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace spotthedifference
{
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
            IsHovered = game.IsActive && HitTest(position);

            if (!game.IsActive) armed = false;
            else if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
                armed = IsHovered;
            else if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
            {
                if (armed && IsHovered)
                {
                    IsPaused = !IsPaused;
                    PauseChanged?.Invoke(IsPaused);
                }
                armed = false;
            }

            float delta = Math.Max(0, (float)gameTime.ElapsedGameTime.TotalSeconds);
            float blend = 1f - MathF.Exp(-Math.Max(0, AnimationSpeed) * delta);
            float targetRotation = IsPaused || IsHovered ? 0f : MathHelper.PiOver4;
            rotation = MathHelper.Lerp(rotation, targetRotation, blend);
            float iconStep = delta / Math.Max(0.01f, IconTransitionSeconds);
            iconProgress = IsPaused ? Math.Min(1f, iconProgress + iconStep)
                : Math.Max(0f, iconProgress - iconStep);
            currentColor = Vector4.Lerp(currentColor,
                (IsPaused ? PausedColor : IsHovered ? HoverColor : IdleColor).ToVector4(), blend);
            previousMouse = mouse;
        }

        private bool HitTest(Vector2 position)
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
            artworkBatch.Begin(samplerState: SamplerState.PointClamp,
                transformMatrix: Matrix.CreateScale(Supersampling));
            Vector2 center = new Vector2(artworkSize / 2f);
            float border = Math.Max(2f, size * 0.04f);
            DrawRectangle(center, new Vector2(size), rotation, Color.Black);
            DrawRectangle(center, new Vector2(size - border * 2f), rotation, new Color(currentColor));

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

        private void DrawRectangle(Vector2 center, Vector2 dimensions, float angle, Color color)
        {
            artworkBatch.Draw(Globals.Pixel, center, null, color, angle,
                new Vector2(0.5f), dimensions, SpriteEffects.None, 0f);
        }
    }
}
