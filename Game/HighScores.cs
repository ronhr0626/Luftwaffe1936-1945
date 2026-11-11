using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Luftwaffe.Game;

// Keeps the top scores in a small text file, one score per line
public class HighScores
{
    public const int MaxEntries = 5;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Luftwaffe1936-1945",
        "highscores.txt");

    public List<int> Scores { get; } = new();

    public HighScores() => Load();

    // Adds a score. Returns its place (1 = best), or 0 if it didn't make the list.
    public int Add(int score)
    {
        if (score <= 0)
            return 0;

        Scores.Add(score);
        Scores.Sort((a, b) => b.CompareTo(a)); // Highest first

        if (Scores.Count > MaxEntries)
            Scores.RemoveRange(MaxEntries, Scores.Count - MaxEntries);

        Save();
        return Scores.IndexOf(score) + 1; // IndexOf gives -1 if it was cut, so this returns 0
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;

            foreach (var line in File.ReadAllLines(FilePath))
            {
                if (int.TryParse(line, out int score))
                    Scores.Add(score);
            }

            Scores.Sort((a, b) => b.CompareTo(a));
        }
        catch (IOException)
        {
            // A broken or locked file should never stop the game
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, Scores.Select(s => s.ToString()));
        }
        catch (IOException)
        {
            // If saving fails, the scores still work for this session
        }
    }
}