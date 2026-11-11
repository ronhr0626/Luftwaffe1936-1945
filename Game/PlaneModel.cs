namespace Luftwaffe.Game;

// Stats for one aircraft type. Upgrading = switching to the next model.
public record PlaneModel(
    string Name,
    int Year,
    string Sprite,       // Path inside Assets/
    double Width,        // Size on screen in pixels (height follows the image's shape)
    double Speed,        // Pixels per second
    double FireInterval, // Seconds between shots (lower = faster)
    int Guns)            // Bullets per shot
{
    // Height / width of the sprite image. Set once when the image is loaded.
    public double Aspect { get; set; } = 0.35;
}

public static class PlaneModels
{
    // In order: the player starts with the first and upgrades through the list
    public static readonly PlaneModel[] All =
    {
        new("Ju 52",        1936, "Germany/01_Ju_52.png",        70,  80, 0.35, 1),
        new("Ju 87 Stuka",  1937, "Germany/02_Ju_87_Stuka.png",  50,  90, 0.30, 2),
        new("Ju 88",        1939, "Germany/03_Ju_88.png",        64, 100, 0.28, 2),
        new("Bf 109",       1939, "Germany/04_Bf_109.png",       44, 115, 0.24, 2),
        new("Fw 190 D-9",   1944, "Germany/05_Fw_190_D9.png",    46, 135, 0.15, 4),
        new("He 219 Uhu",   1943, "Germany/06_He_219_Uhu.png",   64, 130, 0.16, 4),
        new("Ta 152",       1945, "Germany/07_Ta_152.png",       48, 150, 0.13, 4),
        new("Ar 234",       1944, "Germany/08_Ar_234.png",       60, 165, 0.14, 2),
        new("Me 163 Komet", 1944, "Germany/09_Me_163_Komet.png", 36, 200, 0.18, 2),
        new("Me 262",       1944, "Germany/10_Me_262.png",       54, 185, 0.10, 4),
    };
}