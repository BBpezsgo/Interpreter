namespace LanguageCore;

[ExcludeFromCodeCoverage]
public abstract class NotExceptionBut : IDiagnostics
{
    public string Message { get; }
    public Position Position { get; }
    public Uri? File { get; }

    protected NotExceptionBut(string message, Position position, Uri? file)
    {
        Message = message;
        Position = position;
        File = file;
    }

    public string? GetArrows()
    {
        if (File == null) return null;
        if (!File.IsFile) return null;
        return LanguageException.GetArrows(Position, System.IO.File.ReadAllText(File.LocalPath));
    }

    public override string ToString()
    {
        StringBuilder result = new(Message);

        result.Append(Position.ToStringCool().Surround(" (at ", ")"));

        if (File != null)
        { result.Append($" (in {File})"); }

        return result.ToString();
    }
}
