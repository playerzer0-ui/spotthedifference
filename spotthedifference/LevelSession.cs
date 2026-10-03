using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace spotthedifference
{
    /// <summary>Loaded puzzle with GUID-keyed progress; completed levels cannot restart.</summary>
    public sealed class LevelSession : IDisposable
    {
        private readonly Game game;
        private readonly LevelData level;
        private readonly LevelProgress progress;
        private readonly ProgressStore store;
        private readonly SpriteFont font;
        private readonly Texture2D original, modified;
        private MouseState previousMouse;
        private readonly CompletionPanel completion;
        private double checkpointSeconds;
        public event Action HomeRequested;
        public Func<Vector2, Vector2> ScreenToLocal { get; set; }
        public string ErrorMessage { get; private set; }
        public bool IsCompleted => progress.Status == LevelStatus.Completed;

        public LevelSession(Game game, SpriteFont font, LevelData level, LevelProgress progress, ProgressStore store)
        {
            this.game = game; this.font = font; this.level = level; this.progress = progress; this.store = store;
            original = LoadTexture(game.GraphicsDevice, level.OriginalPng);
            try { modified = LoadTexture(game.GraphicsDevice, level.NewPng); }
            catch { original.Dispose(); throw; }
            previousMouse = Mouse.GetState();
            completion = new CompletionPanel(game, font, level, progress, modified);
            completion.HomeRequested += () => HomeRequested?.Invoke();
            if (progress.Status == LevelStatus.New)
            {
                progress.Status = level.Circles.Length == 0 ? LevelStatus.Completed : LevelStatus.Ongoing;
                Persist();
            }
        }

        public static Texture2D LoadTexture(GraphicsDevice device, byte[] png)
        {
            using var stream = new MemoryStream(png);
            Texture2D texture = Texture2D.FromStream(device, stream);
            try
            {
                Color[] pixels = new Color[texture.Width * texture.Height];
                texture.GetData(pixels);
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = Color.FromNonPremultiplied(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
                texture.SetData(pixels);
                return texture;
            }
            catch { texture.Dispose(); throw; }
        }

        private void Persist()
        {
            try { store.Save(level.Id, progress); ErrorMessage = null; }
            catch (Exception exception)
            {
                ErrorMessage = "Progress could not be saved. Check disk space and permissions.";
                System.Diagnostics.Debug.WriteLine(exception);
            }
        }

        public void Update(GameTime gameTime, bool allowInteraction, bool timerRunning = true)
        {
            if (IsCompleted)
            {
                completion.ScreenToLocal = ScreenToLocal;
                completion.Update(gameTime);
                return;
            }
            if (game.IsActive && timerRunning)
            {
                double elapsed = Math.Max(0, gameTime.ElapsedGameTime.TotalSeconds);
                progress.ElapsedSeconds += elapsed;
                checkpointSeconds += elapsed;
                if (checkpointSeconds >= 5) { Persist(); checkpointSeconds = 0; }
            }
            MouseState mouse = Mouse.GetState();
            if (game.IsActive && allowInteraction && !IsCompleted
                && mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            {
                Vector2 screen = new Vector2(mouse.X, mouse.Y);
                Vector2 position = ScreenToLocal?.Invoke(screen) ?? screen;
                // Stored markers use the level's logical canvas coordinates.
                Vector2 savedPosition = new Vector2(position.X * level.Width / 1920f, position.Y * level.Height / 1080f);
                bool hit = false;
                for (int i = 0; i < level.Circles.Length; i++)
                {
                    LevelCircle circle = level.Circles[i];
                    if (Vector2.DistanceSquared(savedPosition, new Vector2(circle.X, circle.Y)) > circle.Radius * circle.Radius)
                        continue;
                    hit = true;
                    if (progress.Found.Contains(i)) break;
                    progress.Found.Add(i);
                    if (progress.Found.Count == level.Circles.Length) progress.Status = LevelStatus.Completed;
                    Persist();
                    break;
                }
                Rectangle imageArea = CreateMenu.FitImage(modified.Width, modified.Height,
                    new Rectangle(level.Width / 2 + 24, 220, level.Width / 2 - 48, level.Height - 244));
                if (!hit && imageArea.Contains(new Point((int)savedPosition.X, (int)savedPosition.Y)))
                {
                    progress.WrongClicks++;
                    Persist();
                }
            }
            previousMouse = mouse;
        }

        public void PrepareDraw()
        {
            if (IsCompleted) completion.PrepareDraw();
        }

        public void Draw()
        {
            DrawImage(original, 0);
            DrawImage(modified, 1);
            if (!IsCompleted)
            {
                Globals.spriteBatch.Draw(Globals.Pixel, new Rectangle(955, 0, 10, 1080), Color.Black);
                Centered($"{level.Name} - {progress.Found.Count}/{level.Circles.Length}", new Vector2(700, 90), 1f);
                foreach (int index in progress.Found)
                {
                    LevelCircle circle = level.Circles[index];
                    Vector2 center = new Vector2(circle.X * 1920f / level.Width, circle.Y * 1080f / level.Height);
                    Vector2 radius = new Vector2(circle.Radius * 1920f / level.Width, circle.Radius * 1080f / level.Height);
                    for (int segment = 0; segment < 64; segment++)
                    {
                        float a = segment * MathHelper.TwoPi / 64;
                        float b = (segment + 1) * MathHelper.TwoPi / 64;
                        Vector2 start = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
                        Vector2 end = center + new Vector2(MathF.Cos(b), MathF.Sin(b)) * radius;
                        Vector2 direction = end - start;
                        Globals.spriteBatch.Draw(Globals.Pixel, start, null, Color.LimeGreen,
                            MathF.Atan2(direction.Y, direction.X), Vector2.Zero,
                            new Vector2(direction.Length() + 1, 3), SpriteEffects.None, 0);
                    }
                }
            }
            if (IsCompleted) completion.Draw();
            if (ErrorMessage != null) Centered(ErrorMessage, new Vector2(960, 1030), 1);
        }

        private void DrawImage(Texture2D texture, int side)
        {
            // Use the same fitted rectangle as authoring, scaled from the saved canvas.
            Rectangle savedArea = new Rectangle(side * level.Width / 2 + 24, 220, level.Width / 2 - 48, level.Height - 244);
            Rectangle fitted = CreateMenu.FitImage(texture.Width, texture.Height, savedArea);
            var destination = new Rectangle((int)(fitted.X * 1920f / level.Width), (int)(fitted.Y * 1080f / level.Height),
                (int)(fitted.Width * 1920f / level.Width), (int)(fitted.Height * 1080f / level.Height));
            Globals.spriteBatch.Draw(texture, destination, Color.White);
        }

        private void Centered(string text, Vector2 position, float scale) =>
            Globals.spriteBatch.DrawString(font, text, position, Color.White, 0, font.MeasureString(text) / 2, scale, SpriteEffects.None, 0);

        public void Dispose()
        {
            if (!IsCompleted) Persist();
            completion.Dispose(); original.Dispose(); modified.Dispose();
        }
    }
}
