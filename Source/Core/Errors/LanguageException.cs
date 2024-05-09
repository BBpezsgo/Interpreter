using LanguageCore.Parser;

namespace LanguageCore;

public class LanguageException : Exception, IInFile
{
    public Position Position { get; }
    public Uri? Uri { get; }

    Uri? IInFile.FilePath => Uri;

    protected LanguageException(string message, Position position, Uri? uri) : base(message)
    {
        Position = position;
        Uri = uri;
    }

    public LanguageException(Error error) : this(error.Message, error.Position, error.Uri) { }

    public LanguageException(string message, Exception inner) : base(message, inner) { }

    public override string ToString()
    {
        StringBuilder result = new(Message);

        result.Append(Position.ToStringCool(" (at ", ")"));

        if (Uri != null)
        { result.Append($" (in {Uri})"); }

        if (InnerException != null)
        { result.Append($" {InnerException}"); }

        return result.ToString();
    }

    public string? GetArrows()
    {
        if (Uri == null) return null;
        if (!Uri.IsFile) return null;
        return GetArrows(Position, System.IO.File.ReadAllText(Uri.LocalPath));
    }

    public static string? GetArrows(Position position, string text)
    {
        if (position.AbsoluteRange == 0) return null;
        if (position == Position.UnknownPosition) return null;
        if (position.Range.Start.Line != position.Range.End.Line)
        { return null; }

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

        if (position.Range.Start.Line >= lines.Length)
        { return null; }

        string line = lines[position.Range.Start.Line];

        StringBuilder result = new();

        line = line.Replace('\t', ' ');

        int removedLeadingWhitespaces;
        {
            string trimmedLine = line.TrimStart();
            removedLeadingWhitespaces = line.Length - trimmedLine.Length;
            line = trimmedLine.Trim();
        }

        result.Append(line);
        result.AppendLine();
        result.Append(' ', Math.Max(0, position.Range.Start.Character - removedLeadingWhitespaces));
        result.Append('^', Math.Max(1, position.Range.End.Character - position.Range.Start.Character));
        return result.ToString();
    }

    public static string? GetArrows(Position position, IEnumerable<Tokenizing.Token> text)
    {
        if (position.AbsoluteRange == 0) return null;
        if (position == Position.UnknownPosition) return null;
        if (position.Range.Start.Line != position.Range.End.Line)
        { return null; }

        StringBuilder lineBuilder = new();
        Tokenizing.Token? prevToken = null;
        foreach (Tokenizing.Token token in text)
        {
            if (token.Position.Range.Start.Line != position.Range.Start.Line)
            {
                if (lineBuilder.Length > 0)
                { break; }
                else
                { continue; }
            }

            if (prevToken is null)
            { lineBuilder.Append(' ', token.Position.Range.Start.Character); }
            else
            { lineBuilder.Append(' ', token.Position.Range.Start.Character - lineBuilder.Length); }

            lineBuilder.Append(token.ToOriginalString());

            prevToken = token;
        }

        lineBuilder.Replace('\t', ' ');
        string line = lineBuilder.ToString();

        StringBuilder result = new();

        int removedLeadingWhitespaces;
        {
            string trimmedLine = line.TrimStart();
            removedLeadingWhitespaces = line.Length - trimmedLine.Length;
            line = trimmedLine.Trim();
        }

        result.Append(line);
        result.AppendLine();
        result.Append(' ', Math.Max(0, position.Range.Start.Character - removedLeadingWhitespaces));
        result.Append('^', Math.Max(1, position.Range.End.Character - position.Range.Start.Character));
        return result.ToString();
    }
}
