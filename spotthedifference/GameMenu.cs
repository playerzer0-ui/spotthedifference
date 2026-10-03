using System;
using Microsoft.Xna.Framework;

namespace spotthedifference
{
    /// <summary>Animated toolbar. PrepareDraw before the scene, then Draw inside a LinearClamp batch.</summary>
    public sealed class GameMenu : IDisposable
    {
        private readonly PauseButton add;
        private readonly PauseButton reset;
        private readonly PauseButton home;
        private readonly float spacing;
        private float progress;
        public PauseButton Pause { get; }
        public bool IsPaused => Pause.IsPaused;
        public float TransitionSeconds { get; set; } = 0.4f;
        public event Action AddCircleRequested;
        public event Action ResetRequested;
        public event Action HomeRequested;

        public Func<Vector2, Vector2> ScreenToLocal
        {
            get => Pause.ScreenToLocal;
            set { Pause.ScreenToLocal = value; add.ScreenToLocal = value; reset.ScreenToLocal = value; home.ScreenToLocal = value; }
        }

        public GameMenu(Game game, Vector2 pauseCenter, int size = 100)
        {
            spacing = size * 1.6f;
            Pause = new PauseButton(game, pauseCenter, size);
            add = new PauseButton(game, pauseCenter - new Vector2(spacing, 0), size)
                { Icon = MenuIcon.AddCircle, IdleColor = Color.LightGreen, HoverColor = new Color(220, 245, 211) };
            reset = new PauseButton(game, pauseCenter, size)
                { Icon = MenuIcon.Reset, IdleColor = new Color(240, 207, 237), HoverColor = new Color(250, 229, 249), Enabled = false, SquareAmount = 1 };
            home = new PauseButton(game, pauseCenter, size)
                { Icon = MenuIcon.Home, IdleColor = Color.Yellow, HoverColor = new Color(255, 255, 170), Enabled = false, SquareAmount = 1 };
            add.Clicked += () => AddCircleRequested?.Invoke();
            reset.Clicked += () => ResetRequested?.Invoke();
            home.Clicked += () => HomeRequested?.Invoke();
        }

        public void Update(GameTime gameTime)
        {
            Pause.Update(gameTime);
            float step = (float)gameTime.ElapsedGameTime.TotalSeconds / Math.Max(0.01f, TransitionSeconds);
            progress = MathHelper.Clamp(progress + (IsPaused ? step : -step), 0, 1);
            float tween = MathHelper.SmoothStep(0, 1, progress);
            Vector2 origin = Pause.Collider.Center;
            Move(add, origin - new Vector2(spacing * (1 + 2 * tween), 0));
            Move(reset, origin - new Vector2(spacing * 2 * tween, 0));
            Move(home, origin - new Vector2(spacing * tween, 0));
            add.SquareAmount = tween;
            reset.Enabled = home.Enabled = IsPaused && progress >= 1f;
            add.Update(gameTime);
            reset.Update(gameTime);
            home.Update(gameTime);
        }

        private static void Move(PauseButton button, Vector2 center) =>
            button.Collider.UpdateRect((int)MathF.Round(center.X), (int)MathF.Round(center.Y));

        public bool HitTest(Vector2 position) => Pause.HitTest(position) || add.HitTest(position)
            || (progress > 0 && (reset.HitTest(position) || home.HitTest(position)));

        public void PrepareDraw()
        {
            add.PrepareDraw();
            if (progress > 0) { reset.PrepareDraw(); home.PrepareDraw(); }
            Pause.PrepareDraw();
        }

        public void Draw()
        {
            // Emerging buttons are drawn first, so they slide out from behind Pause.
            if (progress > 0) { reset.Draw(); home.Draw(); }
            add.Draw();
            Pause.Draw();
        }

        public void Dispose()
        {
            add.Dispose(); reset.Dispose(); home.Dispose(); Pause.Dispose();
        }
    }
}
