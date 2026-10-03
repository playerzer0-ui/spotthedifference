using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;

namespace spotthedifference
{
    /// <summary>Two image panels for the editor, independent of Canvas.</summary>
    public sealed class CreateMenu : IDisposable
    {
        private readonly Game game;
        private readonly SpriteFont font;
        private readonly Vector2 resolution;
        private readonly PauseButton[] uploadButtons;
        private readonly Texture2D[] images = new Texture2D[2];
        public string OriginalPath { get; private set; }
        public string NewPath { get; private set; }
        public string ErrorMessage { get; private set; }
        public bool HasBothImages => images[0] != null && images[1] != null;

        public byte[] GetImagePng(int side)
        {
            if (side < 0 || side > 1) throw new ArgumentOutOfRangeException(nameof(side));
            Texture2D image = images[side] ?? throw new InvalidOperationException("Upload both images before saving.");
            using var stream = new MemoryStream();
            image.SaveAsPng(stream, image.Width, image.Height);
            return stream.ToArray();
        }
        public Func<Vector2, Vector2> ScreenToLocal
        {
            set { foreach (var button in uploadButtons) button.ScreenToLocal = value; }
        }

        public CreateMenu(Game game, SpriteFont font, Vector2 resolution)
        {
            this.game = game;
            this.font = font;
            this.resolution = resolution;
            uploadButtons = new PauseButton[4];
            for (int side = 0; side < 2; side++)
            {
                int capturedSide = side;
                Texture2D icon = Globals.Content.Load<Texture2D>(side == 0 ? "UI/original_logo" : "UI/new_logo");
                uploadButtons[side * 2] = MakeButton(
                    new Vector2(resolution.X * (side == 0 ? 0.25f : 0.75f), resolution.Y * 0.52f), 220, icon);
                uploadButtons[side * 2 + 1] = MakeButton(
                    new Vector2(resolution.X * side / 2f + 80, 110), 80, icon);
                uploadButtons[side * 2].Clicked += () => Upload(capturedSide);
                uploadButtons[side * 2 + 1].Clicked += () => Upload(capturedSide);
            }
        }

        private PauseButton MakeButton(Vector2 center, int size, Texture2D icon)
        {
            var button = new PauseButton(game, center, size)
            {
                Icon = MenuIcon.Create, IconTexture = icon,
                IdleColor = Color.Black, HoverColor = Color.Gray
            };
            button.ResetInteraction();
            return button;
        }

        private void Upload(int side)
        {
            string path;
            try
            {
                path = ImageFilePicker.Choose(side == 0 ? "Choose original image" : "Choose new image");
            }
            catch (Exception exception)
            {
                ErrorMessage = "Could not open the image picker: " + exception.Message;
                System.Diagnostics.Debug.WriteLine(exception);
                Enter();
                return;
            }
            try
            {
                if (path == null) return;
                using var stream = File.OpenRead(path);
                Texture2D replacement = Texture2D.FromStream(game.GraphicsDevice, stream);
                // Imported textures need premultiplied alpha for the default SpriteBatch blend.
                try
                {
                    Color[] pixels = new Color[replacement.Width * replacement.Height];
                    replacement.GetData(pixels);
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = Color.FromNonPremultiplied(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
                    replacement.SetData(pixels);
                }
                catch { replacement.Dispose(); throw; }
                images[side]?.Dispose();
                images[side] = replacement;
                if (side == 0) OriginalPath = path; else NewPath = path;
                ErrorMessage = null;
            }
            catch (Exception exception)
            {
                ErrorMessage = "Could not open image. Please choose a valid PNG, JPG or BMP.";
                System.Diagnostics.Debug.WriteLine(exception);
            }
            finally { Enter(); }
        }

        public void Enter()
        {
            foreach (var button in uploadButtons) button.ResetInteraction();
        }

        public void Update(GameTime gameTime)
        {
            for (int side = 0; side < 2; side++)
            {
                uploadButtons[side * 2].Enabled = images[side] == null;
                uploadButtons[side * 2 + 1].Enabled = images[side] != null;
            }
            foreach (var button in uploadButtons) button.Update(gameTime);
        }

        public bool HitTest(Vector2 position)
        {
            for (int side = 0; side < 2; side++)
                if (uploadButtons[side * 2 + (images[side] == null ? 0 : 1)].HitTest(position)) return true;
            return false;
        }

        public void PrepareDraw()
        {
            for (int side = 0; side < 2; side++)
                uploadButtons[side * 2 + (images[side] == null ? 0 : 1)].PrepareDraw();
        }

        /// <summary>Draw behind circle markers. Image bounds reserve space for the toolbar.</summary>
        public void DrawImages()
        {
            for (int side = 0; side < 2; side++)
            {
                Texture2D texture = images[side];
                if (texture == null) continue;
                Rectangle area = new Rectangle((int)(side * resolution.X / 2) + 24, 220,
                    (int)(resolution.X / 2) - 48, (int)resolution.Y - 244);
                Globals.spriteBatch.Draw(texture, FitImage(texture.Width, texture.Height, area), Color.White);
            }
            Globals.spriteBatch.Draw(Globals.Pixel,
                new Rectangle((int)(resolution.X / 2) - 5, 0, 10, (int)resolution.Y), Color.Black);
        }

        public static Rectangle FitImage(int width, int height, Rectangle area)
        {
            float scale = Math.Min((float)area.Width / width, (float)area.Height / height);
            int fittedWidth = Math.Max(1, (int)MathF.Floor(width * scale));
            int fittedHeight = Math.Max(1, (int)MathF.Floor(height * scale));
            return new Rectangle(area.X + (area.Width - fittedWidth) / 2,
                area.Y + (area.Height - fittedHeight) / 2, fittedWidth, fittedHeight);
        }

        public void Draw()
        {
            for (int side = 0; side < 2; side++)
                uploadButtons[side * 2 + (images[side] == null ? 0 : 1)].Draw();
            if (ErrorMessage != null)
                Globals.spriteBatch.DrawString(font, ErrorMessage, new Vector2(24, resolution.Y - 50), Color.White);
        }

        public void Dispose()
        {
            foreach (var button in uploadButtons) button.Dispose();
            foreach (var texture in images) texture?.Dispose();
        }
    }
}
