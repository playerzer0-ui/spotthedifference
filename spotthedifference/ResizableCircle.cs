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
        private const int ResizeHandleSize = 20;
        private const int MinimumRadius = 10;
        private const int DeleteButtonSize = 22;
        private readonly CollisionRect deleteButton;
        private Rectangle? movementBounds;

        /// <summary>Optional area containing the entire circle during movement and resizing.</summary>
        public Rectangle? MovementBounds
        {
            get => movementBounds;
            set
            {
                if (value.HasValue && (value.Value.Width < MinimumRadius * 2 || value.Value.Height < MinimumRadius * 2))
                    throw new ArgumentException("Movement bounds must fit the minimum circle size.", nameof(value));
                movementBounds = value;
                ConstrainToBounds();
            }
        }

        private void ConstrainToBounds()
        {
            if (!movementBounds.HasValue) return;
            Rectangle bounds = movementBounds.Value;
            circle1.Radius = Math.Min(circle1.Radius, Math.Min(bounds.Width, bounds.Height) / 2);
            circle1.Center = new Vector2(
                MathHelper.Clamp(circle1.Center.X, bounds.Left + Radius, bounds.Right - Radius),
                MathHelper.Clamp(circle1.Center.Y, bounds.Top + Radius, bounds.Bottom - Radius));
        }


        public CollisionCircle Collider => circle1;
        public bool IsSelected { get; set; } = true;
        /// <summary>Tests the circle body and, when selected, its visible resize handle.</summary>
        public bool HitTest(Point point) => circle1.Contains(point)
            || HitTestControls(point);

        public bool HitTestControls(Point point) => IsSelected
            && (GetResizeHandle().Contains(point) || HitTestDelete(point));

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
        public Vector2 Center
        {
            get => circle1.Center;
            set { circle1.Center = value; ConstrainToBounds(); }
        }
        public int Radius
        {
            get => circle1.Radius;
            set { circle1.Radius = Math.Max(MinimumRadius, value); ConstrainToBounds(); }
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
                if (movementBounds.HasValue)
                {
                    Rectangle bounds = movementBounds.Value;
                    int maximumRadius = (int)System.MathF.Floor(System.MathF.Min(
                        bounds.Right - resizeAnchor.X, bounds.Bottom - resizeAnchor.Y) / 2f);
                    circle1.Radius = System.Math.Min(circle1.Radius, maximumRadius);
                }
                circle1.Center = resizeAnchor + new Vector2(circle1.Radius);
            }
            else if (isDraggingCircle)
                circle1.Center = canvasPos + circleDragOffset;

            ConstrainToBounds();

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
            DrawSelection();
        }

        /// <summary>Draw after other objects so the selected controls remain visible.</summary>
        public void DrawSelection()
        {
            if (!IsSelected) return;
            Vector2 topLeft = circle1.Center - new Vector2(circle1.Radius);
            int diameter = circle1.Radius * 2;
            int x = (int)System.MathF.Round(topLeft.X);
            int y = (int)System.MathF.Round(topLeft.Y);
            Globals.spriteBatch.Draw(selectionPixel, new Rectangle(x, y, diameter, 1), PicoPallete.white);
            Globals.spriteBatch.Draw(selectionPixel, new Rectangle(x, y + diameter, diameter, 1), PicoPallete.white);
            Globals.spriteBatch.Draw(selectionPixel, new Rectangle(x, y, 1, diameter), PicoPallete.white);
            Globals.spriteBatch.Draw(selectionPixel, new Rectangle(x + diameter, y, 1, diameter), PicoPallete.white);
            Rectangle resizeHandle = GetResizeHandle();
            Globals.spriteBatch.Draw(Globals.Pixel, resizeHandle, PicoPallete.black);
            Globals.spriteBatch.Draw(Globals.Pixel,
                new Rectangle(resizeHandle.X + 2, resizeHandle.Y + 2,
                    resizeHandle.Width - 4, resizeHandle.Height - 4), PicoPallete.white);
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
