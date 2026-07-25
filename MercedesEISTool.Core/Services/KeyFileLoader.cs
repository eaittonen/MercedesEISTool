using System.Text.Json;

namespace MercedesEISTool.Core.Services;

public class KeyFileLoader
{
    public const int MaxBytes = 1024 * 1024;

    public byte[] LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A valid file path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The selected key file could not be found.", path);
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("Key files must not be empty.");
        }

        if (bytes.Length > MaxBytes)
        {
            throw new InvalidOperationException($"Key files must be at most {MaxBytes} bytes.");
        }

        return (byte[])bytes.Clone();
    }
}
