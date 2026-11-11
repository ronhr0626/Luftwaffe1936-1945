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
    private static readonly IBrush BulletBrush = new SolidColorBrush(Color.Parse("#FFE066"));
    private static readonly IBrush EnemyBulletBrush = new SolidColorBrush(Color.Parse("#FF5A3C"));
    private static readonly IBrush HpBackBrush = new SolidColorBrush(Color.Parse("#80000000"));
    private static readonly IBrush HpBrush = new SolidColorBrush(Color.Parse("#5CD65C"));
    private static readonly IBrush OverlayBrush = new SolidColorBrush(Color.Parse("#99000000"));
    private static readonly IBrush HeadingBrush = new SolidColorBrush(Color.Parse("#FFE066"));

    private static readonly Typeface HudFont = new("Consolas", FontStyle.Normal, FontWeight.Bold);

    // How fast the background moves compared to the world (0.5 = half speed, feels far away)
    private const double BackgroundParallax = 0.5;

    private static readonly string[] ControlLines =
    {
        "Arrows / WASD    Fly",
        "SPACE (hold)     Fire",
        "R                Play again",
        "ENTER            Title screen"
    };

    private static readonly string[] RuleLines =
    {
        "Shoot down a British plane   +10",
        "A plane escapes               -2",
        "Every 10 kills = next German plane",
        "20 enemies per wave",
        "Each wave is one level stronger",
        "3 lives - crashes hurt too"
    };

    private GameWorld _world = new();
    private readonly HighScores _highScores = new();
    private readonly InputState _input = new();
    private readonly Dictionary<string, Bitmap> _planeSprites = new(); // Key = sprite path
    private readonly Bitmap _sky;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private TopLevel? _topLevel;

    private bool _onTitle = true;
    private bool _scoreSaved;
    private int _lastPlace;     // Place on the high score list for the last game, 0 = not on the list
    private double _bgScroll;   // Background keeps moving on every screen

    public GameCanvas()
    {
        // Load every plane image once, and remember its shape so the plane isn't stretched
        foreach (var model in PlaneModels.All)
        {
            var bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://Luftwaffe/Assets/{model.Sprite}")));
            model.Aspect = bitmap.Size.Height / bitmap.Size.Width;
            _planeSprites[model.Sprite] = bitmap;
        }

        // Scrolling background image
        _sky = new Bitmap(AssetLoader.Open(new Uri("avares://Luftwaffe/Assets/sky.png")));

        // The images are large, so smooth scaling looks best when shrinking them
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

        _bgScroll += GameWorld.ScrollSpeed * dt;

        if (_onTitle)
        {
            if (_input.Start)
                StartNewGame();
        }
        else
        {
            _world.Update(dt, _input);

            bool ended = _world.IsGameOver || _world.IsVictory;

            // Save the score once, the moment the game ends
            if (ended && !_scoreSaved)
            {
                _lastPlace = _highScores.Add(_world.Score);
                _scoreSaved = true;
            }

            if (ended && _input.Restart)
                StartNewGame();
            else if (ended && _input.Start)
                _onTitle = true;
        }

        _input.EndFrame();
        InvalidateVisual();
    }

    private void StartNewGame()
    {
        _world = new GameWorld();
        _scoreSaved = false;
        _lastPlace = 0;
        _onTitle = false;
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

            if (_onTitle)
            {
                DrawTitle(context);
                return;
            }

            DrawEnemies(context);
            DrawBullets(context);
            DrawEnemyBullets(context);
            DrawPlane(context, _world.Player);
            DrawHud(context);
        }
    }

    // Draws the sky image side by side so it loops forever while scrolling
    private void DrawBackground(DrawingContext context)
    {
        // Fit the image to the world height and keep its shape
        double h = GameWorld.Height;
        double w = Math.Round(h * _sky.Size.Width / _sky.Size.Height);

        // Round to whole pixels so the copies line up exactly
        double x = -Math.Floor(_bgScroll * BackgroundParallax % w);
        while (x < GameWorld.Width)
        {
            // Draw 1 pixel wider so neighbouring copies overlap and leave no gap
            context.DrawImage(_sky, new Rect(x, 0, w + 1, h));
            x += w;
        }
    }

    private void DrawTitle(DrawingContext context)
    {
        context.FillRectangle(OverlayBrush, new Rect(0, 0, GameWorld.Width, GameWorld.Height));

        double cx = GameWorld.Width / 2;
        DrawText(context, "LUFTWAFFE 1936-1945", cx, 20, 28, TextAlignment.Center);

        // Left column: controls and rules
        double lx = 50;
        double y = 80;

        DrawText(context, "CONTROLS", lx, y, 12, brush: HeadingBrush);
        y += 18;
        foreach (var line in ControlLines)
        {
            DrawText(context, line, lx, y, 10);
            y += 14;
        }

        y += 10;
        DrawText(context, "HOW TO PLAY", lx, y, 12, brush: HeadingBrush);
        y += 18;
        foreach (var line in RuleLines)
        {
            DrawText(context, line, lx, y, 10);
            y += 14;
        }

        // Right column: high scores
        double rx = 430;
        y = 80;

        DrawText(context, "HIGH SCORES", rx, y, 12, brush: HeadingBrush);
        y += 18;
        for (int i = 0; i < HighScores.MaxEntries; i++)
        {
            string score = i < _highScores.Scores.Count ? $"{_highScores.Scores[i],6}" : "     -";
            DrawText(context, $"{i + 1}.  {score}", rx, y, 12);
            y += 18;
        }

        //  start prompt
                // Start prompt
        DrawText(context, "Press ENTER to start", cx, 320, 14, TextAlignment.Center, HeadingBrush);
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
        double cx = GameWorld.Width / 2;

        DrawText(context, title, cx, 120, 32, TextAlignment.Center);
        DrawText(context, $"Final score: {_world.Score}", cx, 165, 16, TextAlignment.Center);

        if (_lastPlace == 1)
            DrawText(context, "NEW HIGH SCORE!", cx, 190, 14, TextAlignment.Center, HeadingBrush);
        else if (_lastPlace > 1)
            DrawText(context, $"#{_lastPlace} on the high score list", cx, 190, 12, TextAlignment.Center, HeadingBrush);

        DrawText(context, "R = play again    ENTER = title screen", cx, 220, 11, TextAlignment.Center);
    }

    private static FormattedText MakeText(string text, double size, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, HudFont, size, brush);

    // Text with a dark shadow. x is the left edge, center or right edge depending on align.
    private static void DrawText(DrawingContext context, string text, double x, double y, double size,
        TextAlignment align = TextAlignment.Left, IBrush? brush = null)
    {
        var main = MakeText(text, size, brush ?? Brushes.White);
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