using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;
using System.Collections.Generic;

namespace spotthedifference
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        Canvas canvas;
        private readonly List<ResizableCircle> circles = new List<ResizableCircle>();
        private ResizableCircle selectedCircle;
        private MouseState previousCircleMouse;
        private bool circlePointerCaptured;
        GameMenu menu;
        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            _graphics.PreferredBackBufferWidth = 1920;
            _graphics.PreferredBackBufferHeight = 1080;
            Window.AllowUserResizing = true;
            _graphics.ApplyChanges();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
            Globals.Content = Content;
            Globals.spriteBatch = _spriteBatch;
            Globals.graphics = _graphics;

            canvas = new Canvas(GraphicsDevice, Window, 1920, 1080);
            ResetCircles();
            menu = new GameMenu(this, new Vector2(1800, 120), 100)
            {
                ScreenToLocal = canvas.ScreenToCanvas
            };
            menu.AddCircleRequested += AddCircle;
            menu.ResetRequested += () =>
            {
                ResetCircles();
            };
        }

        private void AddCircle()
        {
            int index = circles.Count;
            circles.Add(new ResizableCircle(this, canvas,
                new Vector2(100 + (index % 10) * 150, 300 + (index / 10) * 150), 50));
            SelectCircle(circles[circles.Count - 1]);
        }

        private void ResetCircles()
        {
            circlePointerCaptured = false;
            foreach (ResizableCircle circle in circles) circle.Dispose();
            circles.Clear();
            circles.Add(new ResizableCircle(this, canvas, new Vector2(100, 300), 50));
            SelectCircle(circles[0]);
        }

        private void SelectCircle(ResizableCircle circle)
        {
            selectedCircle = circle;
            foreach (ResizableCircle candidate in circles)
                candidate.IsSelected = candidate == selectedCircle;
        }

        private void UpdateCircles(GameTime gameTime)
        {
            MouseState mouse = Mouse.GetState();
            if (!IsActive || mouse.LeftButton == ButtonState.Released)
                circlePointerCaptured = false;
            if (IsActive && mouse.LeftButton == ButtonState.Pressed
                && previousCircleMouse.LeftButton == ButtonState.Released)
            {
                circlePointerCaptured = false;
                Vector2 position = canvas.ScreenToCanvas(new Vector2(mouse.X, mouse.Y));
                Point point = new Point((int)position.X, (int)position.Y);
                // UI is drawn above circles, so it gets first claim on a click.
                if (!menu.HitTest(position))
                {
                    ResizableCircle hit = null;
                    // Last drawn is topmost. Stop after the first hit.
                    for (int i = circles.Count - 1; i >= 0; i--)
                    {
                        if (!circles[i].HitTest(point)) continue;
                        hit = circles[i];
                        break;
                    }
                    if (hit != null && hit.HitTestDelete(point))
                    {
                        circles.Remove(hit);
                        hit.Dispose();
                        SelectCircle(null);
                    }
                    else
                    {
                        SelectCircle(hit);
                        circlePointerCaptured = hit != null;
                    }
                }
            }

            // Only the selected circle can interact; everyone still tracks releases.
            // Selection stays fixed for the entire press, even over other circles.
            foreach (ResizableCircle circle in circles)
                circle.Update(gameTime, circlePointerCaptured && circle == selectedCircle);
            previousCircleMouse = mouse;
        }

        protected override void Update(GameTime gameTime)
        {
            Globals.Input.Update();
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            menu.Update(gameTime);
            // Circle editing stays available while the pause menu is open.
            // Always update it so mouse press/release tracking cannot become stale.
            UpdateCircles(gameTime);

            base.Update(gameTime);
        }

        protected override void UnloadContent()
        {
            foreach (ResizableCircle circle in circles) circle.Dispose();
            menu?.Dispose();
            Globals.DisposePixel();
            base.UnloadContent();
        }

        protected override void Draw(GameTime gameTime)
        {
            menu.PrepareDraw();
            canvas.Activate();
            GraphicsDevice.Clear(PicoPallete.blue);
            _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
            foreach (ResizableCircle circle in circles) circle.Draw();
            menu.Draw();
            _spriteBatch.End();

            canvas.Draw(_spriteBatch);

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }
    }
}

