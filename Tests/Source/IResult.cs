namespace LanguageCore.Tests;

public interface IResult
{
    string StdOutput { get; }
    int ExitCode { get; }
}
