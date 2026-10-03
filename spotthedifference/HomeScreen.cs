using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;

namespace spotthedifference
{
    /// <summary>Minimal title screen with animated Play and Create buttons.</summary>
    public sealed class HomeScreen : IDisposable
    {
        private readonly SpriteFont titleFont;
        private readonly Vector2 resolution;
        private readonly PauseButton play;
        private readonly PauseButton create;
        public event Action PlayRequested;
        public event Action CreateRequested;
        public Color BackgroundColor { get; set; } = new Color(0, 174, 232);
        public Func<Vector2, Vector2> ScreenToLocal
        {
            get => play.ScreenToLocal;
            set { play.ScreenToLocal = value; create.ScreenToLocal = value; }
        }

        public HomeScreen(Game game, SpriteFont titleFont, Vector2 resolution)
        {
            this.titleFont = titleFont;
            this.resolution = resolution;
            int buttonSize = (int)(Math.Min(resolution.X, resolution.Y) * 0.23f);
            play = MakeButton(game, new Vector2(resolution.X * 0.38f, resolution.Y * 0.71f), buttonSize, MenuIcon.Play, "UI/play");
            // A triangle's visual center differs from the center of its image bounds.
            play.IconOffset = new Vector2(buttonSize * 0.06f, 0);
            create = MakeButton(game, new Vector2(resolution.X * 0.64f, resolution.Y * 0.71f), buttonSize, MenuIcon.Create, "UI/create");
            play.Clicked += () => PlayRequested?.Invoke();
            create.Clicked += () => CreateRequested?.Invoke();
        }

        private static PauseButton MakeButton(Game game, Vector2 position, int size, MenuIcon icon, string texture)
        {
            var button = new PauseButton(game, position, size)
            {
                Icon = icon,
                IconTexture = Globals.Content.Load<Texture2D>(texture),
                IdleColor = Color.Black,
                HoverColor = Color.Gray
            };
            button.ResetInteraction();
            return button;
        }

        public void Enter()
        {
            play.ResetInteraction();
            create.ResetInteraction();
        }

        public void Update(GameTime gameTime)
        {
            play.Update(gameTime);
            create.Update(gameTime);
        }

        public void PrepareDraw()
        {
            play.PrepareDraw();
            create.PrepareDraw();
        }

        public void Draw()
        {
            string[] lines = { "Spot", "The", "Difference" };
            float scale = Math.Min(1f, resolution.X * 0.44f / titleFont.MeasureString("Difference").X);
            for (int i = 0; i < lines.Length; i++)
            {
                Vector2 measured = titleFont.MeasureString(lines[i]);
                Globals.spriteBatch.DrawString(titleFont, lines[i],
                    new Vector2(resolution.X / 2f, resolution.Y * (0.09f + i * 0.12f)),
                    Color.White, 0, measured / 2f, scale, SpriteEffects.None, 0);
            }
            play.Draw();
            create.Draw();
        }

        public void Dispose()
        {
            play.Dispose();
            create.Dispose();
        }
    }
}
