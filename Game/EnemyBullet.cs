namespace Luftwaffe.Game;

// A bullet fired by a British plane
public class EnemyBullet
{
    public const double Size = 4;

    public double X { get; set; }
    public double Y { get; set; }
    public double VX { get; init; } // Pixels per second
    public double VY { get; init; }
    public int Damage { get; init; }

    public void Update(double dt)
    {
        X += VX * dt;
        Y += VY * dt;
    }
}