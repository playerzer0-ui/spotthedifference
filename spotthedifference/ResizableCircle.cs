using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace spotthedifference
{
    /// <summary>A draggable circle with a corner handle for proportional resizing.</summary>
    public class ResizableCircle : IDisposable
    {
        private readonly Game game;
        private readonly Canvas canvas;
        private readonly CollisionCircle circle1;
        private MouseState previousMouseState;
        private bool isDraggingCircle;
        private Vector2 circleDragOffset;
        private bool isResizingCircle;
        private Vector2 resizeAnchor;
        private Vector2 resizeGrabOffset;
        private Texture2D selectionPixel;
        private const int ResizeHandleSize = 12;
        private const int MinimumRadius = 10;
        private const int DeleteButtonSize = 22;
        private readonly CollisionRect deleteButton;


        public CollisionCircle Collider => circle1;
        public bool IsSelected { get; set; } = true;
        /// <summary>Tests the circle body and, when selected, its visible resize handle.</summary>
        public bool HitTest(Point point) => circle1.Contains(point)
            || (IsSelected && (GetResizeHandle().Contains(point) || HitTestDelete(point)));

        public bool HitTestDelete(Point point)
        {
            UpdateDeleteButton();
            return IsSelected && deleteButton.Contains(point);
        }

        private void UpdateDeleteButton()
        {
            Vector2 corner = Center - new Vector2(Radius);
            deleteButton.UpdateRect((int)System.MathF.Round(corner.X), (int)System.MathF.Round(corner.Y));
        }
        public Vector2 Center { get => circle1.Center; set => circle1.Center = value; }
        public int Radius
        {
            get => circle1.Radius;
            set => circle1.Radius = Math.Max(MinimumRadius, value);
        }

        /// <summary>Create in LoadContent after initializing Globals.</summary>
        public ResizableCircle(Game game, Canvas canvas, Vector2 center, int radius)
        {
            this.game = game;
            this.canvas = canvas;
            circle1 = new CollisionCircle(0, 0, Math.Max(MinimumRadius, radius));
            circle1.Center = center;
            deleteButton = new CollisionRect(0, 0, DeleteButtonSize, DeleteButtonSize);
            selectionPixel = new Texture2D(game.GraphicsDevice, 1, 1);
            selectionPixel.SetData(new[] { Color.White });
        }

        public void Update(GameTime gameTime, bool allowInteraction = true)
        {
            MouseState currentMouseState = Mouse.GetState();
            bool leftHeld = currentMouseState.LeftButton == ButtonState.Pressed;
            bool justClicked = leftHeld && previousMouseState.LeftButton == ButtonState.Released;
            Vector2 canvasPos = canvas.ScreenToCanvas(new Vector2(currentMouseState.X, currentMouseState.Y));
            Point clickPoint = new Point((int)canvasPos.X, (int)canvasPos.Y);

            if (!allowInteraction || !game.IsActive || !leftHeld)
            {
                isDraggingCircle = false;
                isResizingCircle = false;
            }
            else if (justClicked)
            {
                Vector2 corner = circle1.Center + new Vector2(circle1.Radius);
                if (GetResizeHandle().Contains(clickPoint))
                {
                    isResizingCircle = true;
                    resizeAnchor = circle1.Center - new Vector2(circle1.Radius);
                    resizeGrabOffset = corner - canvasPos;
                }
                else if (circle1.Contains(clickPoint))
                {
                    isDraggingCircle = true;
                    circleDragOffset = circle1.Center - canvasPos;
                }
            }

            if (isResizingCircle)
            {
                // Keep the opposite corner fixed and preserve the circle's proportions.
                Vector2 size = canvasPos + resizeGrabOffset - resizeAnchor;
                circle1.Radius = System.Math.Max(MinimumRadius,
                    (int)System.MathF.Round((size.X + size.Y) / 4f));
                circle1.Center = resizeAnchor + new Vector2(circle1.Radius);
            }
            else if (isDraggingCircle)
                circle1.Center = canvasPos + circleDragOffset;

            previousMouseState = currentMouseState;
            
        }

        private Rectangle GetResizeHandle()
        {
            Vector2 corner = circle1.Center + new Vector2(circle1.Radius);
            return new Rectangle((int)System.MathF.Round(corner.X) - ResizeHandleSize / 2,
                (int)System.MathF.Round(corner.Y) - ResizeHandleSize / 2,
                ResizeHandleSize, ResizeHandleSize);
        }


        /// <summary>Call inside an active SpriteBatch using canvas coordinates.</summary>
        public void Draw()
        {
            circle1.Draw(PicoPallete.red);
            if (!IsSelected) return;
            Vector2 topLeft = circle1.Center - new Vector2(circle1.Radius);
            int diameter = circle1.Radius * 2;
            int x = (int)System.MathF.Round(topLeft.X);
            int y = (int)System.MathF.Round(topLeft.Y);
            Globals.spriteBatch.Draw(selectionPixel, new Rectangle(x, y, diameter, 1), PicoPallete.white);
            Globals.spriteBatch.Draw(selectionPixel, new Rectangle(x, y + diameter, diameter, 1), PicoPallete.white);
            Globals.spriteBatch.Draw(selectionPixel, new Rectangle(x, y, 1, diameter), PicoPallete.white);
            Globals.spriteBatch.Draw(selectionPixel, new Rectangle(x + diameter, y, 1, diameter), PicoPallete.white);
            Globals.spriteBatch.Draw(selectionPixel, GetResizeHandle(), PicoPallete.white);
            UpdateDeleteButton();
            deleteButton.Draw(PicoPallete.black);
            Rectangle button = deleteButton.Rect;
            Globals.spriteBatch.Draw(Globals.Pixel,
                new Rectangle(button.X + 2, button.Y + 2, button.Width - 4, button.Height - 4), PicoPallete.red);
            Vector2 buttonCenter = deleteButton.Center;
            Vector2 crossSize = new Vector2(14, 2);
            Globals.spriteBatch.Draw(Globals.Pixel, buttonCenter, null, Color.White,
                MathHelper.PiOver4, new Vector2(0.5f), crossSize, SpriteEffects.None, 0f);
            Globals.spriteBatch.Draw(Globals.Pixel, buttonCenter, null, Color.White,
                -MathHelper.PiOver4, new Vector2(0.5f), crossSize, SpriteEffects.None, 0f);
        }

        public void Dispose()
        {
            selectionPixel.Dispose();
        }
    }
}
