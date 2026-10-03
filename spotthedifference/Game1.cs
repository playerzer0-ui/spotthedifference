using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace spotthedifference
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        Canvas canvas;
        ResizableCircle circle1;
        TextInput textInput;
        PauseButton pauseButton;
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
            circle1 = new ResizableCircle(this, canvas, new Vector2(100, 100), 50);
            textInput = new TextInput(this, Content.Load<SpriteFont>("InputFont"),
                new CollisionRect(400, 106, 360, 52), "Enter your name...")
            {
                ScreenToLocal = canvas.ScreenToCanvas
            };
            pauseButton = new PauseButton(this, new Vector2(1800, 120), 100)
            {
                ScreenToLocal = canvas.ScreenToCanvas
            };
        }

        protected override void Update(GameTime gameTime)
        {
            Globals.Input.Update();
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            pauseButton.Update(gameTime);
            if (!pauseButton.IsPaused)
            {
                circle1.Update(gameTime);
                textInput.Update(gameTime);
            }
            else
                textInput.Blur();

            base.Update(gameTime);
        }

        protected override void UnloadContent()
        {
            circle1?.Dispose();
            textInput?.Dispose();
            pauseButton?.Dispose();
            Globals.DisposePixel();
            base.UnloadContent();
        }

        protected override void Draw(GameTime gameTime)
        {
            pauseButton.PrepareDraw();
            canvas.Activate();
            GraphicsDevice.Clear(PicoPallete.blue);
            _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
            circle1.Draw();
            textInput.Draw();
            pauseButton.Draw();
            _spriteBatch.End();

            canvas.Draw(_spriteBatch);

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }
    }
}

