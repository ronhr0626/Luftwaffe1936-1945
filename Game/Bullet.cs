namespace Luftwaffe.Game;

public class Bullet
{
    public const double Width = 6;
    public const double Height = 2;
    public const double Speed = 420; // Pixels per second, to the right

    public double X { get; set; }
    public double Y { get; set; }

    public void Update(double dt) => X += Speed * dt;
}