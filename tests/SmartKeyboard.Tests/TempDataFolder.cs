namespace SmartKeyboard.Tests;

/// <summary>
/// Makes a throwaway folder for one test, and deletes it afterwards.
/// Tests use this so they never touch the real dictionary files.
/// </summary>
public sealed class TempDataFolder : IDisposable
{
    public TempDataFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sk_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Writes a file inside the folder and gives back its full path.</summary>
    public string Write(string fileName, params string[] lines)
    {
        string full = System.IO.Path.Combine(Path, fileName);
        File.WriteAllLines(full, lines);
        return full;
    }

    /// <summary>Reads a file from inside the folder.</summary>
    public string[] Read(string fileName)
    {
        return File.ReadAllLines(System.IO.Path.Combine(Path, fileName));
    }

    public bool Exists(string fileName) => File.Exists(System.IO.Path.Combine(Path, fileName));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A locked file should never fail a test.
        }
    }
}
