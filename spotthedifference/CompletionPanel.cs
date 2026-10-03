using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;

namespace spotthedifference
{
    /// <summary>Centered results panel, 75% of the logical screen in each dimension.</summary>
    public sealed class CompletionPanel : IDisposable
    {
        private readonly Rectangle bounds = new Rectangle(240, 135, 1440, 810);
        private readonly Rectangle previewArea = new Rectangle(320, 455, 620, 410);
        private readonly SpriteFont resultsFont, titleFont;
        private readonly Texture2D image, timeIcon;
        private readonly LevelData level;
        private readonly LevelProgress progress;
        private readonly PauseButton home;
        public event Action HomeRequested;
        public Func<Vector2, Vector2> ScreenToLocal { set => home.ScreenToLocal = value; }

        public CompletionPanel(Game game, SpriteFont font, LevelData level, LevelProgress progress, Texture2D image)
        {
            this.level = level; this.progress = progress; this.image = image;
            resultsFont = Globals.Content.Load<SpriteFont>("ResultsFont");
            titleFont = Globals.Content.Load<SpriteFont>("TitleFont");
            timeIcon = Globals.Content.Load<Texture2D>("UI/ongoing");
            home = new PauseButton(game, new Vector2(bounds.Right - 105, bounds.Bottom - 105), 100)
            {
                Icon = MenuIcon.Home, IconTexture = Globals.Content.Load<Texture2D>("UI/home"),
                IdleColor = Color.Yellow, HoverColor = new Color(255, 255, 170)
            };
            home.ResetInteraction();
            home.Clicked += () => HomeRequested?.Invoke();
        }

        public void Update(GameTime gameTime) => home.Update(gameTime);
        public void PrepareDraw() => home.PrepareDraw();

        public void Draw()
        {
            var batch = Globals.spriteBatch;
            batch.Draw(Globals.Pixel, new Rectangle(0, 0, 1920, 1080), Color.Black * 0.5f);
            batch.Draw(Globals.Pixel, bounds, Color.Black);
            batch.Draw(Globals.Pixel, new Rectangle(bounds.X + 5, bounds.Y + 5, bounds.Width - 10, bounds.Height - 10), Color.White);
            DrawText(titleFont, "Level", new Vector2(960, 225), 0.68f);
            DrawText(titleFont, "Complete!", new Vector2(960, 335), 0.68f);

            batch.Draw(Globals.Pixel, new Rectangle(previewArea.X - 2, previewArea.Y - 2, previewArea.Width + 4, previewArea.Height + 4), Color.Black);
            batch.Draw(Globals.Pixel, previewArea, Color.White);
            Rectangle fitted = CreateMenu.FitImage(image.Width, image.Height, previewArea);
            batch.Draw(image, fitted, Color.White);

            Rectangle savedImage = CreateMenu.FitImage(image.Width, image.Height,
                new Rectangle(level.Width / 2 + 24, 220, level.Width / 2 - 48, level.Height - 244));
            float scale = (float)fitted.Width / savedImage.Width;
            foreach (int index in progress.Found)
            {
                LevelCircle circle = level.Circles[index];
                Vector2 center = new Vector2(fitted.X + (circle.X - savedImage.X) * scale,
                    fitted.Y + (circle.Y - savedImage.Y) * (float)fitted.Height / savedImage.Height);
                Vector2 radius = new Vector2(circle.Radius * scale,
                    circle.Radius * (float)fitted.Height / savedImage.Height);
                for (int segment = 0; segment < 96; segment++)
                {
                    float a = segment * MathHelper.TwoPi / 96;
                    float b = (segment + 1) * MathHelper.TwoPi / 96;
                    Vector2 start = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
                    Vector2 end = center + new Vector2(MathF.Cos(b), MathF.Sin(b)) * radius;
                    // Keep annotations within the preview panel.
                    if (start.X < previewArea.Left + 3 || start.X > previewArea.Right - 3
                        || start.Y < previewArea.Top + 3 || start.Y > previewArea.Bottom - 3
                        || end.X < previewArea.Left + 3 || end.X > previewArea.Right - 3
                        || end.Y < previewArea.Top + 3 || end.Y > previewArea.Bottom - 3) continue;
                    DrawLine(start, end, 3, Color.LimeGreen);
                }
            }

            Vector2 timePosition = new Vector2(1080, 545);
            batch.Draw(timeIcon, timePosition, null, Color.White, 0,
                new Vector2(timeIcon.Width / 2f, timeIcon.Height / 2f),
                100f / Math.Max(timeIcon.Width, timeIcon.Height), SpriteEffects.None, 0);
            string time = "--:--";
            if (progress.HasStatistics)
            {
                long seconds = (long)Math.Floor(progress.ElapsedSeconds);
                time = $"{seconds / 60:00}:{seconds % 60:00}";
            }
            DrawText(resultsFont, time, new Vector2(1300, 545), 0.5f);
            DrawText(resultsFont, "Time", new Vector2(1300, 600), 16f / 92f);

            Vector2 cross = new Vector2(1080, 725);
            DrawLine(cross - new Vector2(36), cross + new Vector2(36), 24, Color.Black);
            DrawLine(cross + new Vector2(-36, 36), cross + new Vector2(36, -36), 24, Color.Black);
            DrawLine(cross - new Vector2(30), cross + new Vector2(30), 12, Color.White);
            DrawLine(cross + new Vector2(-30, 30), cross + new Vector2(30, -30), 12, Color.White);
            DrawText(resultsFont, progress.HasStatistics ? progress.WrongClicks.ToString() : "--", new Vector2(1300, 725), 0.5f);
            DrawText(resultsFont, "Wrong clicks", new Vector2(1300, 780), 16f / 92f);
            home.Draw();
        }

        private static void DrawText(SpriteFont font, string text, Vector2 position, float scale)
        {
            Vector2 topLeft = position - font.MeasureString(text) * scale / 2;
            topLeft = new Vector2(MathF.Round(topLeft.X), MathF.Round(topLeft.Y));
            Globals.spriteBatch.DrawString(font, text, topLeft, Color.Black, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        private static void DrawLine(Vector2 start, Vector2 end, float thickness, Color color)
        {
            Vector2 direction = end - start;
            Globals.spriteBatch.Draw(Globals.Pixel, (start + end) / 2, null, color,
                MathF.Atan2(direction.Y, direction.X), new Vector2(0.5f),
                new Vector2(direction.Length(), thickness), SpriteEffects.None, 0);
        }

        public void Dispose() => home.Dispose();
    }
}

