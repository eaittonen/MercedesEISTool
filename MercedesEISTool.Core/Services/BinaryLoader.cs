namespace MercedesEISTool.Core.Services;

public class BinaryLoader
{
    public byte[] LoadBinFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A valid BIN file path is required.", nameof(path));
        }

        var data = File.ReadAllBytes(path);
        if (data.Length != 256)
        {
            throw new InvalidOperationException($"Mercedes EIS EEPROM dumps must be exactly 256 bytes. Received {data.Length} bytes from '{path}'.");
        }

        return data;
    }
}
