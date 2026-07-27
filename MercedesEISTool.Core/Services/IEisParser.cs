namespace MercedesEISTool.Core.Services;

public interface IEisParser
{
    string Format { get; }
    bool CanHandle(byte[] data);
    EisParserResult Parse(byte[] data);
}
