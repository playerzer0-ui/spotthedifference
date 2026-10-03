using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace spotthedifference
{
    public sealed class SaveBox : IDisposable
    {
        private readonly TextInput nameInput;
        private readonly PauseButton cancel;
        private readonly PauseButton save;
        private readonly SpriteFont font;
        private readonly Rectangle panel;
        private readonly Rectangle screen;
        private KeyboardState previousKeys;
        public bool IsOpen { get; private set; }
        public string ErrorMessage { get; private set; }
        public Func<string, string> SaveLevel { get; set; }
        public event Action<string> Saved;
        public Func<Vector2, Vector2> ScreenToLocal
        {
            set { nameInput.ScreenToLocal = value; cancel.ScreenToLocal = value; save.ScreenToLocal = value; }
        }

        public SaveBox(Game game, SpriteFont font, Vector2 resolution)
        {
            this.font = font;
            screen = new Rectangle(0, 0, (int)resolution.X, (int)resolution.Y);
            panel = new Rectangle((int)resolution.X / 2 - 370, (int)resolution.Y / 2 - 175, 740, 350);
            nameInput = new TextInput(game, font,
                new CollisionRect(panel.Center.X, panel.Y + 64, 680, 54), "Name your level...") { MaxLength = 60 };
            cancel = new PauseButton(game, new Vector2(panel.Center.X - 150, panel.Y + 232), 100)
                { Icon = MenuIcon.Cancel, IdleColor = Color.Red, HoverColor = new Color(230, 60, 60) };
            save = new PauseButton(game, new Vector2(panel.Center.X + 150, panel.Y + 232), 100)
                { Icon = MenuIcon.Save, IconTexture = Globals.Content.Load<Texture2D>("UI/save"), IdleColor = Color.Black, HoverColor = Color.Gray };
            cancel.Clicked += Close;
            save.Clicked += Submit;
            nameInput.Submitted += _ => Submit();
        }

        public void Open()
        {
            ErrorMessage = null;
            IsOpen = true;
            previousKeys = Keyboard.GetState();
            cancel.ResetInteraction();
            save.ResetInteraction();
            nameInput.Focus();
        }

        public void Close()
        {
            IsOpen = false;
            nameInput.Blur();
        }

        private void Submit()
        {
            if (!IsOpen) return;
            try
            {
                if (string.IsNullOrWhiteSpace(nameInput.Text))
                    throw new ArgumentException("Enter a level name.");
                if (SaveLevel == null) throw new InvalidOperationException("Saving is not connected.");
                string path = SaveLevel(nameInput.Text);
                Close();
                Saved?.Invoke(path);
            }
            catch (Exception exception)
            {
                ErrorMessage = exception is ArgumentException ? exception.Message : "Could not save. Check folder permissions and disk space.";
                System.Diagnostics.Debug.WriteLine(exception);
                nameInput.Focus();
            }
        }

        public void Update(GameTime gameTime)
        {
            if (!IsOpen) return;
            KeyboardState keys = Keyboard.GetState();
            if (keys.IsKeyDown(Keys.Escape) && previousKeys.IsKeyUp(Keys.Escape)) Close();
            previousKeys = keys;
            if (!IsOpen) return;
            nameInput.Update(gameTime);
            if (!IsOpen) return;
            cancel.Update(gameTime);
            if (IsOpen) save.Update(gameTime);
        }

        public void PrepareDraw()
        {
            if (!IsOpen) return;
            cancel.PrepareDraw(); save.PrepareDraw();
        }

        public void Draw()
        {
            if (!IsOpen) return;
            Globals.spriteBatch.Draw(Globals.Pixel, screen, Color.Black * 0.55f);
            Globals.spriteBatch.Draw(Globals.Pixel, panel, Color.Black);
            Globals.spriteBatch.Draw(Globals.Pixel, new Rectangle(panel.X + 8, panel.Y + 8,
                panel.Width - 16, panel.Height - 16), Color.White);
            nameInput.Draw(); cancel.Draw(); save.Draw();
            if (ErrorMessage != null)
            {
                string displayed = ErrorMessage;
                while (displayed.Length > 0 && font.MeasureString(displayed).X > panel.Width - 48)
                    displayed = displayed.Substring(0, displayed.Length - 1);
                Globals.spriteBatch.DrawString(font, displayed, new Vector2(panel.X + 24, panel.Y + 105), Color.Red);
            }
        }

        public void Dispose()
        {
            nameInput.Dispose(); cancel.Dispose(); save.Dispose();
        }
    }
}
