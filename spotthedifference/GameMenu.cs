using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;

namespace spotthedifference
{
    /// <summary>Animated toolbar. PrepareDraw before the scene, then Draw inside a LinearClamp batch.</summary>
    public sealed class GameMenu : IDisposable
    {
        private readonly PauseButton add;
        private readonly PauseButton reset;
        private readonly PauseButton save;
        private readonly PauseButton home;
        private readonly float spacing;
        private float progress;
        public PauseButton Pause { get; }
        public bool IsPaused => Pause.IsPaused;
        public bool EditorActionsEnabled { get; set; } = true;
        public float TransitionSeconds { get; set; } = 0.4f;
        public event Action AddCircleRequested;
        public event Action ResetRequested;
        public event Action SaveRequested;
        public event Action HomeRequested;

        public Func<Vector2, Vector2> ScreenToLocal
        {
            get => Pause.ScreenToLocal;
            set { Pause.ScreenToLocal = value; add.ScreenToLocal = value; reset.ScreenToLocal = value; save.ScreenToLocal = value; home.ScreenToLocal = value; }
        }

        public GameMenu(Game game, Vector2 pauseCenter, int size = 100)
        {
            spacing = size * 1.6f;
            Pause = new PauseButton(game, pauseCenter, size);
            add = new PauseButton(game, pauseCenter - new Vector2(spacing, 0), size)
                { Icon = MenuIcon.AddCircle, IconTexture = Globals.Content.Load<Texture2D>("UI/add_circle"), IdleColor = Color.LightGreen, HoverColor = new Color(220, 245, 211) };
            reset = new PauseButton(game, pauseCenter, size)
                { Icon = MenuIcon.Reset, IconTexture = Globals.Content.Load<Texture2D>("UI/reset"), IdleColor = new Color(240, 207, 237), HoverColor = new Color(250, 229, 249), Enabled = false, SquareAmount = 1 };
            save = new PauseButton(game, pauseCenter, size)
                { Icon = MenuIcon.Save, IconTexture = Globals.Content.Load<Texture2D>("UI/save"), IdleColor = Color.Black, HoverColor = new Color(45, 45, 45), Enabled = false, SquareAmount = 1 };
            home = new PauseButton(game, pauseCenter, size)
                { Icon = MenuIcon.Home, IconTexture = Globals.Content.Load<Texture2D>("UI/home"), IdleColor = Color.Yellow, HoverColor = new Color(255, 255, 170), Enabled = false, SquareAmount = 1 };
            add.Clicked += () => AddCircleRequested?.Invoke();
            reset.Clicked += () => ResetRequested?.Invoke();
            save.Clicked += () => SaveRequested?.Invoke();
            home.Clicked += () => HomeRequested?.Invoke();
        }

        public void Update(GameTime gameTime)
        {
            Pause.Update(gameTime);
            float step = (float)gameTime.ElapsedGameTime.TotalSeconds / Math.Max(0.01f, TransitionSeconds);
            progress = MathHelper.Clamp(progress + (IsPaused ? step : -step), 0, 1);
            float tween = MathHelper.SmoothStep(0, 1, progress);
            Vector2 origin = Pause.Collider.Center;
            Move(add, origin - new Vector2(spacing * (1 + 3 * tween), 0));
            Move(reset, origin - new Vector2(spacing * 3 * tween, 0));
            Move(save, origin - new Vector2(spacing * 2 * tween, 0));
            Move(home, origin - new Vector2(spacing * tween, 0));
            add.SquareAmount = tween;
            add.Enabled = EditorActionsEnabled;
            reset.Enabled = save.Enabled = EditorActionsEnabled && IsPaused && progress >= 1f;
            home.Enabled = IsPaused && progress >= 1f;
            add.Update(gameTime);
            reset.Update(gameTime);
            save.Update(gameTime);
            home.Update(gameTime);
        }

        private static void Move(PauseButton button, Vector2 center) =>
            button.Collider.UpdateRect((int)MathF.Round(center.X), (int)MathF.Round(center.Y));

        public void Close()
        {
            Pause.IsPaused = false;
            progress = 0;
            Pause.ResetInteraction();
            add.ResetInteraction(); reset.ResetInteraction(); save.ResetInteraction(); home.ResetInteraction();
        }

        public bool HitTest(Vector2 position) => Pause.HitTest(position) || add.HitTest(position)
            || (progress > 0 && (reset.HitTest(position) || save.HitTest(position) || home.HitTest(position)));

        public void PrepareDraw()
        {
            add.PrepareDraw();
            if (progress > 0) { reset.PrepareDraw(); save.PrepareDraw(); home.PrepareDraw(); }
            Pause.PrepareDraw();
        }

        public void Draw()
        {
            // Emerging buttons are drawn first, so they slide out from behind Pause.
            if (progress > 0)
            {
                if (EditorActionsEnabled) { reset.Draw(); save.Draw(); }
                home.Draw();
            }
            if (EditorActionsEnabled) add.Draw();
            Pause.Draw();
        }

        public void Dispose()
        {
            add.Dispose(); reset.Dispose(); save.Dispose(); home.Dispose(); Pause.Dispose();
        }
    }
}
