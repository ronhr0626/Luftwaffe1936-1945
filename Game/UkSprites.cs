using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Luftwaffe.Game;

public static class UkSprites
{
    private static readonly string[] Files =
    {
        "01_Fairey_Swordfish.png",
        "02_Hawker_Hurricane.png",
        "03_Vickers_Wellington.png",
        "04_Bristol_Beaufighter.png",
        "05_Short_Sunderland.png",
        "06_Avro_Lancaster.png",
        "07_de_Havilland_Mosquito.png",
        "08_Supermarine_Spitfire.png",
        "09_Hawker_Tempest_V.png",
        "10_Gloster_Meteor.png"
    };

    public static readonly Bitmap[] All = Load();

    private static Bitmap[] Load()
    {
        var result = new Bitmap[Files.Length];
        for (int i = 0; i < Files.Length; i++)
        {
            // Path is case-sensitive: folder is "Uk"
            var uri = new Uri($"avares://Luftwaffe/Assets/Uk/{Files[i]}");
            result[i] = new Bitmap(AssetLoader.Open(uri));
        }
        return result;
    }

    // Height / width of a sprite, so planes keep their shape
    public static double Aspect(int index) => All[index].Size.Height / All[index].Size.Width;

    // Draws a bitmap flipped horizontally inside dest
    public static void DrawMirrored(DrawingContext context, Bitmap bitmap, Rect dest)
    {
        var flip = Matrix.CreateScale(-1, 1) *
                   Matrix.CreateTranslation(dest.X * 2 + dest.Width, 0);

        using (context.PushTransform(flip))
        {
            context.DrawImage(bitmap, dest);
        }
    }
}