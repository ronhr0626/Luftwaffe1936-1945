using System;

namespace Luftwaffe.Game;

// A British plane flying from right to left. Level 1 = Swordfish ... 10 = Meteor.
public class Enemy
{
    // Width on screen per level, height follows the image's shape
    private static readonly double[] Widths = { 56, 58, 76, 66, 88, 90, 68, 82, 84, 88 };

    // Chance that a plane is a shooter (0.3 = about every 3rd to 4th plane)
    private const double ShooterChance = 0.3;

    public int Level { get; }
    public int Hp { get; private set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; }
    public double Height { get; }
    public double Speed { get; } // Pixels per second, to the left
    public bool CanShoot { get; }

    public bool IsDead => Hp <= 0;
    public int SpriteIndex => Level - 1;

    // Higher levels shoot more often
    private double FireInterval => Math.Max(0.8, 3.0 - Level * 0.2);
    private double _fireTimer;

    public Enemy(int level, Random rng)
    {
        Level = level;
        Hp = level; // Rule: 1 HP per level
        Width = Widths[level - 1];
        Height = Width * UkSprites.Aspect(level - 1);
        Speed = 70 + level * 10;
        CanShoot = rng.NextDouble() < ShooterChance;

        // Random first shot so the planes don't all fire at the same time
        _fireTimer = 0.5 + rng.NextDouble() * FireInterval;
    }

    public void Update(double dt) => X -= Speed * dt;

    public void Hit(int damage) => Hp -= damage;

    public void Destroy() => Hp = 0;

    // Returns true when it's time to shoot. Non-shooters never fire.
    public bool ReadyToFire(double dt)
    {
        if (!CanShoot)
            return false;

        _fireTimer -= dt;
        if (_fireTimer > 0)
            return false;

        _fireTimer = FireInterval;
        return true;
    }
}