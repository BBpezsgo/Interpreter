namespace LanguageCore;

public interface IDiagnostics
{
    public string Message { get; }
    public Position Position { get; }
    public Uri? File { get; }
}
