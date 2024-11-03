namespace LanguageCore;

public interface IDiagnostic
{
    public string Message { get; }
    public Position Position { get; }
    public Uri? File { get; }
}
