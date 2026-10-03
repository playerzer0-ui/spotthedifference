using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace spotthedifference
{
    /// <summary>Searchable three-column browser for .spot files.</summary>
    public sealed class LevelBrowser : IDisposable
    {
        private sealed record Card(LevelData Data, string Path, Texture2D Preview);
        private readonly Game game;
        private readonly SpriteFont font;
        private readonly ProgressStore store;
        private readonly TextInput search;
        private readonly List<Card> cards = new List<Card>();
        private readonly Texture2D play, check, ongoing;
        private readonly Dictionary<Guid, LevelProgress> progress = new Dictionary<Guid, LevelProgress>();
        private List<Card> filtered = new List<Card>();
        private MouseState previousMouse;
        private Guid? hovered, armed;
        private bool homeArmed;
        private int scrollRow;
        private string loadMessage;
        public Func<Vector2, Vector2> ScreenToLocal { get; set; }
        public event Action<LevelData, LevelProgress> LevelSelected;
        public event Action HomeRequested;
        private readonly CollisionRect home = new CollisionRect(100, 90, 140, 52);

        public LevelBrowser(Game game, SpriteFont font, ProgressStore store)
        {
            this.game = game; this.font = font; this.store = store;
            search = new TextInput(game, font, new CollisionRect(960, 90, 700, 58), "Search for a game...");
            search.TextChanged += _ => Filter();
            play = Globals.Content.Load<Texture2D>("UI/play");
            check = Globals.Content.Load<Texture2D>("UI/check");
            ongoing = Globals.Content.Load<Texture2D>("UI/ongoing");
        }

        public void Enter()
        {
            search.Blur();
            foreach (Card card in cards) card.Preview.Dispose();
            cards.Clear(); progress.Clear();
            int skipped = 0;
            var seen = new HashSet<Guid>();
            try
            {
                Directory.CreateDirectory(LevelFile.LevelsDirectory);
                foreach (string path in Directory.EnumerateFiles(LevelFile.LevelsDirectory, "*.spot").OrderBy(path => path))
                {
                    try
                    {
                        LevelData data = LevelFile.Load(path);
                        if (!seen.Add(data.Id)) continue;
                        Texture2D source = LevelSession.LoadTexture(game.GraphicsDevice, data.OriginalPng);
                        // Keep thumbnails small instead of retaining full-resolution textures for every card.
                        Texture2D thumbnail;
                        try
                        {
                            var device = game.GraphicsDevice;
                            var oldTargets = device.GetRenderTargets();
                            using var target = new RenderTarget2D(device, 400, 280);
                            using var batch = new SpriteBatch(device);
                            try
                            {
                                device.SetRenderTarget(target);
                                device.Clear(Color.White);
                                batch.Begin(samplerState: SamplerState.LinearClamp);
                                batch.Draw(source, CreateMenu.FitImage(source.Width, source.Height, new Rectangle(0, 0, 400, 280)), Color.White);
                                batch.End();
                            }
                            finally { device.SetRenderTargets(oldTargets); }
                            Color[] pixels = new Color[400 * 280];
                            target.GetData(pixels);
                            thumbnail = new Texture2D(device, 400, 280);
                            thumbnail.SetData(pixels);
                        }
                        finally { source.Dispose(); }
                        // Keep only metadata and thumbnails; load full images when a card is opened.
                        cards.Add(new Card(data with { OriginalPng = Array.Empty<byte>(), NewPng = Array.Empty<byte>() }, path, thumbnail));
                        progress[data.Id] = store.Load(data.Id, data.Circles.Length);
                    }
                    catch (Exception exception) { skipped++; System.Diagnostics.Debug.WriteLine(exception); }
                }
                loadMessage = skipped > 0 ? $"Skipped {skipped} unreadable level file(s)." : null;
            }
            catch (Exception exception) { loadMessage = "Could not read the levels folder."; System.Diagnostics.Debug.WriteLine(exception); }
            cards.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Data.Name, b.Data.Name));
            Filter();
            previousMouse = Mouse.GetState();
            armed = hovered = null;
            homeArmed = false;
        }

        private void Filter()
        {
            filtered = cards.Where(card => card.Data.Name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            scrollRow = 0; armed = hovered = null;
        }

        private static Rectangle CardBounds(int slot) => new Rectangle(270 + slot % 3 * 490, 180 + slot / 3 * 400, 400, 280);

        public void Update(GameTime gameTime)
        {
            search.ScreenToLocal = ScreenToLocal;
            search.Update(gameTime);
            MouseState mouse = Mouse.GetState();
            Vector2 screen = new Vector2(mouse.X, mouse.Y);
            Vector2 position = ScreenToLocal?.Invoke(screen) ?? screen;
            Point point = new Point((int)position.X, (int)position.Y);
            int wheel = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
            if (game.IsActive && wheel != 0)
            {
                scrollRow = Math.Clamp(scrollRow + (wheel < 0 ? 1 : -1), 0, Math.Max(0, (filtered.Count + 2) / 3 - 2));
                armed = null;
            }
            hovered = null;
            for (int slot = 0; slot < 6 && scrollRow * 3 + slot < filtered.Count; slot++)
                if (game.IsActive && CardBounds(slot).Contains(point)) hovered = filtered[scrollRow * 3 + slot].Data.Id;
            if (!game.IsActive) { armed = null; homeArmed = false; }
            else if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            {
                armed = hovered;
                homeArmed = home.Contains(point);
            }
            else if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
            {
                if (armed.HasValue && armed == hovered)
                {
                    Card card = filtered.First(card => card.Data.Id == armed.Value);
                    search.Blur();
                    try
                    {
                        LevelData loaded = LevelFile.Load(card.Path);
                        if (loaded.Id != card.Data.Id) throw new InvalidDataException("Level changed; reopen the browser.");
                        LevelSelected?.Invoke(loaded, progress[card.Data.Id]);
                    }
                    catch (Exception exception)
                    {
                        loadMessage = "Could not open this level. It may have been moved or changed.";
                        System.Diagnostics.Debug.WriteLine(exception);
                    }
                }
                else if (homeArmed && home.Contains(point))
                {
                    search.Blur(); HomeRequested?.Invoke();
                }
                armed = null;
                homeArmed = false;
            }
            previousMouse = mouse;
        }

        public void Draw()
        {
            search.Draw();
            home.Draw(Color.Black);
            DrawCentered("Home", home.Center, Color.White, 1);
            for (int slot = 0; slot < 6 && scrollRow * 3 + slot < filtered.Count; slot++)
            {
                Card card = filtered[scrollRow * 3 + slot];
                Rectangle bounds = CardBounds(slot);
                Globals.spriteBatch.Draw(card.Preview, bounds, Color.White);
                if (hovered == card.Data.Id)
                {
                    LevelStatus status = progress[card.Data.Id].Status;
                    Color color = status == LevelStatus.Completed ? new Color(0, 180, 80)
                        : status == LevelStatus.Ongoing ? Color.Orange : Color.Black;
                    Globals.spriteBatch.Draw(Globals.Pixel, bounds, color * 0.5f);
                    Vector2 center = new Vector2(bounds.Center.X, bounds.Center.Y);
                    Globals.spriteBatch.Draw(Globals.Pixel, center, null, color, MathHelper.PiOver4,
                        new Vector2(0.5f), new Vector2(70), SpriteEffects.None, 0);
                    Texture2D icon = status == LevelStatus.Completed ? check : status == LevelStatus.Ongoing ? ongoing : play;
                    // Optical centering for the asymmetric Play triangle, matching the home button.
                    Vector2 iconPosition = center + (status == LevelStatus.New ? new Vector2(4.2f, 0) : Vector2.Zero);
                    Globals.spriteBatch.Draw(icon, iconPosition, null, Color.White, 0,
                        new Vector2(icon.Width / 2f, icon.Height / 2f), 50f / Math.Max(icon.Width, icon.Height), SpriteEffects.None, 0);
                }
                string name = card.Data.Name;
                while (name.Length > 0 && font.MeasureString(name).X * 1.35f > bounds.Width)
                    name = name.Substring(0, name.Length - 1);
                DrawCentered(name, new Vector2(bounds.Center.X, bounds.Bottom + 42), Color.Black, 1.35f);
            }
            if (filtered.Count == 0)
                DrawCentered(cards.Count == 0 ? "No levels yet. Create and save a level first." : "No matching games.", new Vector2(960, 400), Color.Black, 1);
            if (filtered.Count > 6)
                DrawCentered($"Scroll to browse - {scrollRow * 3 + 1}-{Math.Min(filtered.Count, scrollRow * 3 + 6)} of {filtered.Count}", new Vector2(960, 1000), Color.Black, 1);
            if (loadMessage != null) DrawCentered(loadMessage, new Vector2(960, 1040), Color.Black, 1);
        }

        private void DrawCentered(string text, Vector2 position, Color color, float scale) =>
            Globals.spriteBatch.DrawString(font, text, position, color, 0, font.MeasureString(text) / 2, scale, SpriteEffects.None, 0);

        public void Dispose()
        {
            search.Dispose();
            foreach (Card card in cards) card.Preview.Dispose();
        }
    }
}
