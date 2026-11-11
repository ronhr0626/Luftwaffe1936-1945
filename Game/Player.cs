using System.Collections.Generic;

namespace Luftwaffe.Game;

public class Player
{
    // Size comes from the current plane, so bigger planes are bigger targets
    public double Width => Model.Width;
    public double Height => Model.Width * Model.Aspect;

    public double X { get; set; } = 60;
    public double Y { get; set; } = 160;

    public PlaneModel Model { get; set; } = PlaneModels.All[0];

    private double _fireTimer;

    public void Update(double dt, InputState input, List<Bullet> bullets)
    {
        double dx = 0, dy = 0;
        if (input.Left) dx -= 1;
        if (input.Right) dx += 1;
        if (input.Up) dy -= 1;
        if (input.Down) dy += 1;

        // Diagonal movement would otherwise be ~41% faster
        if (dx != 0 && dy != 0)
        {
            dx *= 0.7071;
            dy *= 0.7071;
        }

        X += dx * Model.Speed * dt;
        Y += dy * Model.Speed * dt;

        _fireTimer -= dt;
        if (input.Fire && _fireTimer <= 0)
        {
            Fire(bullets);
            _fireTimer = Model.FireInterval;
        }
    }

    // Spreads the guns' bullets a few pixels apart vertically, centered on the nose
    private void Fire(List<Bullet> bullets)
    {
        double noseX = X + Width;
        double centerY = Y + Height / 2;
        const double spacing = 3;

        for (int i = 0; i < Model.Guns; i++)
        {
            double offset = (i - (Model.Guns - 1) / 2.0) * spacing;
            bullets.Add(new Bullet { X = noseX, Y = centerY + offset - Bullet.Height / 2 });
        }
    }
}