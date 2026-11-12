using System;
using System.Collections.Generic;

namespace Luftwaffe.Game;

public class GameWorld
{
    // Virtual resolution: everything is drawn at 640x360 and scaled up to the window
    public const double Width = 640;
    public const double Height = 360;

    public const double TopLimit = 30;    // Keep planes below the HUD
    public const double SeaLevel = 330;   // Sea at the bottom of the screen
    public const double ScrollSpeed = 60; // How fast the world moves past, pixels per second

    // Game rules
    public const int MaxLevel = 10;
    public const int EnemiesPerWave = 15;      // British planes before the next enemy level
    public const int KillsPerUpgrade = 20;     // Kills needed for the next German plane
    public const int KillsToWin = 20;          // Kills needed at max level to win
    public const int PointsPerKill = 10;
    public const int PointsLostPerEscape = 2;
    public const int StartLives = 3;
    public const double RespawnInvulnerability = 5.0; // Seconds
    public const double HitInvulnerability = 0.3;     // Short pause so one burst doesn't stack

    private const double StartX = 60;
    private const double StartY = 160;

    public Player Player { get; } = new() { X = StartX, Y = StartY };
    public List<Bullet> Bullets { get; } = new();
    public List<Enemy> Enemies { get; } = new();
    public List<EnemyBullet> EnemyBullets { get; } = new();

    // How far the world has scrolled
    public double ScrollX { get; private set; }

    public int Score { get; private set; }
    public int Kills { get; private set; }
    public int KillsAtMaxLevel { get; private set; }  // Kills made while flying the last plane
    public int EnemyLevel { get; private set; } = 1;  // Level of the current British wave
    public int SpawnedInWave { get; private set; }

    public int Lives { get; private set; } = StartLives;
    public int PlayerHp { get; private set; } = 1;
    public bool IsGameOver { get; private set; }

    public double InvulnerableTime { get; private set; }
    public bool IsInvulnerable => InvulnerableTime > 0;

    public bool EnemiesAtMaxLevel => EnemyLevel >= MaxLevel;
    public bool IsVictory => KillsAtMaxLevel >= KillsToWin;

    // Rule: player level = position in the plane list. Damage per bullet and max HP = level.
    public int PlayerLevel => Array.IndexOf(PlaneModels.All, Player.Model) + 1;
    public int KillsToNextUpgrade => KillsPerUpgrade - Kills % KillsPerUpgrade;
    public int KillsLeftToWin => KillsToWin - KillsAtMaxLevel;

    private readonly Random _rng = new();
    private double _spawnTimer = 1.0;

    public void Update(double dt, InputState input)
    {
        ScrollX += ScrollSpeed * dt;

        // Freeze the action when the game has ended
        if (IsGameOver || IsVictory)
            return;

        if (input.Upgrade)
            UpgradePlane();

        if (InvulnerableTime > 0)
            InvulnerableTime -= dt;

        Player.Update(dt, input, Bullets);
        Player.X = Math.Clamp(Player.X, 0, Width - Player.Width);
        Player.Y = Math.Clamp(Player.Y, TopLimit, SeaLevel - Player.Height);

        foreach (var bullet in Bullets)
            bullet.Update(dt);
        Bullets.RemoveAll(b => b.X > Width);

        UpdateEnemies(dt);
        UpdateEnemyBullets(dt);
        CheckHits();
        CheckPlayerDamage();
    }

    private void UpdateEnemies(double dt)
    {
        // Enemies keep coming until the player wins
        _spawnTimer -= dt;
        if (_spawnTimer <= 0)
        {
            SpawnEnemy();
            _spawnTimer = SpawnInterval();
        }

        foreach (var enemy in Enemies)
        {
            enemy.Update(dt);

            // Only shoot when fully on screen and still in front of the player
            bool onScreen = enemy.X + enemy.Width < Width && enemy.X > Player.X + Player.Width;
            if (enemy.ReadyToFire(dt) && onScreen)
                EnemyFire(enemy);
        }

        // Enemies that fly off the left edge escape: -2 points each, never below 0
        int escaped = Enemies.RemoveAll(e => e.X + e.Width < 0);
        if (escaped > 0)
            Score = Math.Max(0, Score - escaped * PointsLostPerEscape);
    }

    private void SpawnEnemy()
    {
        var enemy = new Enemy(EnemyLevel, _rng) { X = Width };
        enemy.Y = TopLimit + _rng.NextDouble() * (SeaLevel - TopLimit - enemy.Height);
        Enemies.Add(enemy);

        // After 15 planes, the next wave is one level higher. Level 10 is the last.
        if (EnemiesAtMaxLevel)
            return;

        SpawnedInWave++;
        if (SpawnedInWave >= EnemiesPerWave)
        {
            SpawnedInWave = 0;
            EnemyLevel++;
        }
    }

    // Higher waves come a bit faster
    private double SpawnInterval() => Math.Max(0.5, 1.4 - EnemyLevel * 0.08);

    // Fires one bullet from the enemy's nose, aimed at the player
    private void EnemyFire(Enemy enemy)
    {
        double startX = enemy.X;
        double startY = enemy.Y + enemy.Height / 2;

        double dx = (Player.X + Player.Width / 2) - startX;
        double dy = (Player.Y + Player.Height / 2) - startY;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1)
            return;

        double speed = 200 + enemy.Level * 10;
        EnemyBullets.Add(new EnemyBullet
        {
            X = startX,
            Y = startY,
            VX = dx / length * speed,
            VY = dy / length * speed,
            Damage = enemy.Level // Rule: damage = enemy level
        });
    }

    private void UpdateEnemyBullets(double dt)
    {
        foreach (var b in EnemyBullets)
            b.Update(dt);

        EnemyBullets.RemoveAll(b => b.X < -10 || b.X > Width + 10 || b.Y < -10 || b.Y > Height + 10);
    }

    // Player bullets against enemies
    private void CheckHits()
    {
        for (int i = Bullets.Count - 1; i >= 0; i--)
        {
            var b = Bullets[i];

            foreach (var e in Enemies)
            {
                if (e.IsDead)
                    continue;

                if (!Overlaps(b.X, b.Y, Bullet.Width, Bullet.Height, e.X, e.Y, e.Width, e.Height))
                    continue;

                e.Hit(PlayerLevel);
                Bullets.RemoveAt(i);

                if (e.IsDead)
                    OnEnemyKilled();

                break; // One bullet hits one enemy
            }
        }

        Enemies.RemoveAll(e => e.IsDead);
    }

    // Enemy bullets and crashes against the player
    private void CheckPlayerDamage()
    {
        if (IsInvulnerable)
            return;

        for (int i = EnemyBullets.Count - 1; i >= 0; i--)
        {
            var b = EnemyBullets[i];
            if (!Overlaps(b.X, b.Y, EnemyBullet.Size, EnemyBullet.Size,
                    Player.X, Player.Y, Player.Width, Player.Height))
                continue;

            EnemyBullets.RemoveAt(i);
            DamagePlayer(b.Damage);
            return; // One hit per frame is enough
        }

        foreach (var e in Enemies)
        {
            if (!Overlaps(e.X, e.Y, e.Width, e.Height, Player.X, Player.Y, Player.Width, Player.Height))
                continue;

            // Crash: the enemy is destroyed (no points), the player takes damage
            e.Destroy();
            DamagePlayer(e.Level);
            break;
        }

        Enemies.RemoveAll(e => e.IsDead);
    }

    private void DamagePlayer(int amount)
    {
        PlayerHp -= amount;

        if (PlayerHp > 0)
        {
            InvulnerableTime = HitInvulnerability;
            return;
        }

        Lives--;
        if (Lives <= 0)
        {
            IsGameOver = true;
            return;
        }

        // Respawn at the start position with full HP
        Player.X = StartX;
        Player.Y = StartY;
        PlayerHp = PlayerLevel;
        EnemyBullets.Clear();
        InvulnerableTime = RespawnInvulnerability;
    }

    private void OnEnemyKilled()
    {
        Score += PointsPerKill;
        bool wasAtMaxLevel = PlayerLevel >= MaxLevel;
        Kills++;

        // With the last plane, kills count towards victory
        if (wasAtMaxLevel)
        {
            KillsAtMaxLevel++;
            return;
        }

        // Every 20th kill gives the next German plane
        if (Kills % KillsPerUpgrade == 0)
            UpgradePlane();
    }

    // Switch to the next plane in the list, and refill HP to the new level
    public void UpgradePlane()
    {
        int index = Array.IndexOf(PlaneModels.All, Player.Model);
        if (index < PlaneModels.All.Length - 1)
            Player.Model = PlaneModels.All[index + 1];

        PlayerHp = PlayerLevel;
    }

    private static bool Overlaps(double ax, double ay, double aw, double ah,
                                 double bx, double by, double bw, double bh) =>
        ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
}