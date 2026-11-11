using System.Collections.Generic;
using Avalonia.Input;

namespace Luftwaffe.Game;

// Tracks keys held down (movement, auto-fire) and keys pressed this frame (one-shot actions)
public class InputState
{
    private readonly HashSet<PhysicalKey> _down = new();
    private readonly HashSet<PhysicalKey> _pressed = new();

    public void Press(PhysicalKey key)
    {
        // Add returns false if the key was already down (Windows key repeat), so we ignore repeats
        if (_down.Add(key))
            _pressed.Add(key);
    }

    public void Release(PhysicalKey key) => _down.Remove(key);

    // Called once at the end of every frame
    public void EndFrame() => _pressed.Clear();

    // Held keys
    public bool Left => _down.Contains(PhysicalKey.A) || _down.Contains(PhysicalKey.ArrowLeft);
    public bool Right => _down.Contains(PhysicalKey.D) || _down.Contains(PhysicalKey.ArrowRight);
    public bool Up => _down.Contains(PhysicalKey.W) || _down.Contains(PhysicalKey.ArrowUp);
    public bool Down => _down.Contains(PhysicalKey.S) || _down.Contains(PhysicalKey.ArrowDown);

    // Hold Space to keep firing
    public bool Fire => _down.Contains(PhysicalKey.Space);

    // One-shot keys
    public bool Upgrade => _pressed.Contains(PhysicalKey.U); // Test key
    public bool Restart => _pressed.Contains(PhysicalKey.R);
}
