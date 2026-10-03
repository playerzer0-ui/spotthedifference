using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace NodeTesting.models
{
    public class Globals
    {
        public static ContentManager Content;
        public static SpriteBatch spriteBatch;
        public static GraphicsDeviceManager graphics;
        public static InputManager Input = new InputManager();
        private static Texture2D pixel;

        /// <summary>Shared white pixel for drawing shapes. Initialize graphics before using it.</summary>
        public static Texture2D Pixel
        {
            get
            {
                if (pixel == null || pixel.IsDisposed || pixel.GraphicsDevice != graphics.GraphicsDevice)
                {
                    pixel?.Dispose();
                    pixel = new Texture2D(graphics.GraphicsDevice, 1, 1);
                    pixel.SetData(new[] { Color.White });
                }
                return pixel;
            }
        }

        public static void DisposePixel()
        {
            pixel?.Dispose();
            pixel = null;
        }
    }
}
