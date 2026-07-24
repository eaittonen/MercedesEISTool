namespace MercedesEISTool.Core.Models;

public class EisDump
{
    public byte[] RawData { get; set; } = Array.Empty<byte>();
    public string VIN { get; set; } = string.Empty;
    public string Format { get; set; } = "Unknown";
    public string EisType { get; set; } = string.Empty;
    public string MCU { get; set; } = string.Empty;
    public string SSID { get; set; } = string.Empty;
    public List<KeyInfo> Keys { get; set; } = new();
}

public class KeyInfo
{
    public int Index { get; set; }
    public string Value { get; set; } = string.Empty;
}
