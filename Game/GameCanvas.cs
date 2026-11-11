using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Luftwaffe.Game;

namespace Luftwaffe;

// Draws the game and runs the game loop
public class GameCanvas : Control
{
    private static readonly IBrush SkyBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse("#3D7CC9"), 0),
            new GradientStop(Color.Parse("#BFE3FF"), 1)
        }
    };
    private static readonly IBrush SeaBrush = new SolidColorBrush(Color.Parse("#2E5E8C"));
    private static readonly IBrush CloudBrush = new SolidColorBrush(Color.Parse("#E6FFFFFF"));
    private static readonly IBrush BulletBrush = new SolidColorBrush(Color.Parse("#FFE066"));
    private static readonly IBrush EnemyBulletBrush = new SolidColorBrush(Color.Parse("#FF5A3C"));
    private static readonly IBrush HpBackBrush = new SolidColorBrush(Color.Parse("#80000000"));
    private static readonly IBrush HpBrush = new SolidColorBrush(Color.Parse("#5CD65C"));

    private static readonly Typeface HudFont = new("Consolas", FontStyle.Normal, FontWeight.Bold);

    // Cloud positions in the world: x, y, width
    private static readonly (double X, double Y, double W)[] Clouds =
    {
        (40, 50, 70), (200, 90, 50), (330, 40, 90), (470, 110, 60), (600, 70, 80)
    };

    private GameWorld _world = new();
    private readonly InputState _input = new();
    private readonly Dictionary<string, Bitmap> _planeSprites = new(); // Key = sprite path
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private TopLevel? _topLevel;

    public GameCanvas()
    {
        // Load every plane image once, and remember its shape so the plane isn't stretched
        foreach (var model in PlaneModels.All)
        {
            var bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://Luftwaffe/Assets/{model.Sprite}")));
            model.Aspect = bitmap.Size.Height / bitmap.Size.Width;
            _planeSprites[model.Sprite] = bitmap;
        }

        // The plane images are large, so smooth scaling looks best when shrinking them
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);

        // ~60 frames per second
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => GameLoop();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Listen for keys on the whole window, not just this control
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        _topLevel?.AddHandler(KeyUpEvent, HandleKeyUp, RoutingStrategies.Tunnel);

        _clock.Start();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        _topLevel?.RemoveHandler(KeyDownEvent, HandleKeyDown);
        _topLevel?.RemoveHandler(KeyUpEvent, HandleKeyUp);
        base.OnDetachedFromVisualTree(e);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        _input.Press(e.PhysicalKey);
        e.Handled = true;
    }

    private void HandleKeyUp(object? sender, KeyEventArgs e) => _input.Release(e.PhysicalKey);

    private void GameLoop()
    {
        // Time since last frame in seconds
        double dt = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        dt = Math.Min(dt, 0.05); // Avoid huge jumps if the window freezes

        // R starts a new game after game over or victory
        if ((_world.IsGameOver || _world.IsVictory) && _input.Restart)
            _world = new GameWorld();

        _world.Update(dt, _input);
        _input.EndFrame();

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // Black bars around the game if the window has a different shape
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

        // Scale the 640x360 world to fit the window, centered
        double scale = Math.Min(Bounds.Width / GameWorld.Width, Bounds.Height / GameWorld.Height);
        double offsetX = (Bounds.Width - GameWorld.Width * scale) / 2;
        double offsetY = (Bounds.Height - GameWorld.Height * scale) / 2;

        var transform = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY);

        using (context.PushTransform(transform))
        using (context.PushClip(new Rect(0, 0, GameWorld.Width, GameWorld.Height)))
        {
            DrawBackground(context);
            DrawEnemies(context);
            DrawBullets(context);
            DrawEnemyBullets(context);
            DrawPlane(context, _world.Player);
            DrawHud(context);
        }
    }

    private void DrawBackground(DrawingContext context)
    {
        context.FillRectangle(SkyBrush, new Rect(0, 0, GameWorld.Width, GameWorld.SeaLevel));
        context.FillRectangle(SeaBrush,
            new Rect(0, GameWorld.SeaLevel, GameWorld.Width, GameWorld.Height - GameWorld.SeaLevel));

        // Clouds move at half the scroll speed (parallax = feeling of depth)
        double loopWidth = GameWorld.Width + 100;
        foreach (var c in Clouds)
        {
            double x = (c.X - _world.ScrollX * 0.5) % loopWidth;
            if (x < -100) x += loopWidth;

            context.DrawEllipse(CloudBrush, null, new Point(x, c.Y), c.W / 2, c.W / 6);
            context.DrawEllipse(CloudBrush, null, new Point(x + c.W * 0.15, c.Y - c.W / 10), c.W / 4, c.W / 6);
        }
    }

    private void DrawPlane(DrawingContext context, Player p)
    {
        if (_world.IsGameOver)
            return;

        // Blink while invulnerable
        if (_world.IsInvulnerable && (int)(_world.InvulnerableTime * 10) % 2 == 0)
            return;

        var sprite = _planeSprites[p.Model.Sprite];
        context.DrawImage(sprite, new Rect(p.X, p.Y, p.Width, p.Height));
    }

    // British planes face left, so they are drawn mirrored
    private void DrawEnemies(DrawingContext context)
    {
        foreach (var e in _world.Enemies)
            UkSprites.DrawMirrored(context, UkSprites.All[e.SpriteIndex], new Rect(e.X, e.Y, e.Width, e.Height));
    }

    private void DrawBullets(DrawingContext context)
    {
        foreach (var b in _world.Bullets)
            context.FillRectangle(BulletBrush, new Rect(b.X, b.Y, Bullet.Width, Bullet.Height));
    }

    private void DrawEnemyBullets(DrawingContext context)
    {
        foreach (var b in _world.EnemyBullets)
            context.FillRectangle(EnemyBulletBrush, new Rect(b.X, b.Y, EnemyBullet.Size, EnemyBullet.Size));
    }

    private void DrawHud(DrawingContext context)
    {
        var model = _world.Player.Model;
        int level = _world.PlayerLevel;

        // Left: player plane, level and progress to next plane
        DrawText(context, $"{model.Name}  ({model.Year})  Lv {level}", 10, 4, 12);
        string next = level >= GameWorld.MaxLevel ? "MAX" : $"{_world.KillsToNextUpgrade}";
        DrawText(context, $"Kills: {_world.Kills}   Next plane in: {next}", 10, 18, 9);

        // Center: HP bar and lives
        const double barWidth = 100;
        double barX = GameWorld.Width / 2 - barWidth / 2;
        double hpPart = Math.Clamp((double)_world.PlayerHp / level, 0, 1);
        context.FillRectangle(HpBackBrush, new Rect(barX, 6, barWidth, 8));
        context.FillRectangle(HpBrush, new Rect(barX, 6, barWidth * hpPart, 8));
        DrawText(context, $"HP {Math.Max(0, _world.PlayerHp)}/{level}   Lives {_world.Lives}",
            GameWorld.Width / 2, 17, 9, TextAlignment.Center);

        // Right: score and current British wave
        DrawText(context, $"Score: {_world.Score}", GameWorld.Width - 10, 4, 12, TextAlignment.Right);
        string wave = _world.AllWavesSpawned
            ? "Last wave"
            : $"Enemy Lv {_world.EnemyLevel}  ({_world.SpawnedInWave}/{GameWorld.EnemiesPerWave})";
        DrawText(context, wave, GameWorld.Width - 10, 18, 9, TextAlignment.Right);

        if (_world.IsGameOver)
            DrawEndScreen(context, "GAME OVER");
        else if (_world.IsVictory)
            DrawEndScreen(context, "VICTORY!");
    }

    private void DrawEndScreen(DrawingContext context, string title)
    {
        DrawText(context, title, GameWorld.Width / 2, 130, 32, TextAlignment.Center);
        DrawText(context, $"Final score: {_world.Score}", GameWorld.Width / 2, 175, 16, TextAlignment.Center);
        DrawText(context, "Press R to restart", GameWorld.Width / 2, 200, 12, TextAlignment.Center);
    }

    private static FormattedText MakeText(string text, double size, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, HudFont, size, brush);

    // Text with a dark shadow. x is the left edge, center or right edge depending on align.
    private static void DrawText(DrawingContext context, string text, double x, double y, double size,
        TextAlignment align = TextAlignment.Left)
    {
        var main = MakeText(text, size, Brushes.White);
        var shadow = MakeText(text, size, Brushes.Black);

        double drawX = align switch
        {
            TextAlignment.Center => x - main.Width / 2,
            TextAlignment.Right => x - main.Width,
            _ => x
        };

        context.DrawText(shadow, new Point(drawX + 1, y + 1));
        context.DrawText(main, new Point(drawX, y));
    }
}