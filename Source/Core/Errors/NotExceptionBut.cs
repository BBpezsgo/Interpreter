namespace LanguageCore;

public abstract class NotExceptionBut
{
    public string Message { get; }
    public Position Position { get; }
    public Uri? Uri { get; }

    protected NotExceptionBut(string message, Position position, Uri? file)
    {
        Message = message;
        Position = position;
        Uri = file;
    }

    public string? GetArrows()
    {
        if (Uri == null) return null;
        if (!Uri.IsFile) return null;
        return LanguageException.GetArrows(Position, System.IO.File.ReadAllText(Uri.LocalPath));
    }

    public override string ToString()
    {
        StringBuilder result = new(Message);

        result.Append(Position.ToStringCool(" (at ", ")"));
        if (Uri != null)
        { result.Append($" (in {Uri})"); }

        return result.ToString();
    }
}
