namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class LanguageException : Exception, IDiagnostics
{
    public Position Position { get; protected set; }
    public Uri? File { get; protected set; }

    protected LanguageException(string message, Position position, Uri? uri) : base(message)
    {
        Position = position;
        File = uri;
    }

    public LanguageException(string message, Exception inner) : base(message, inner) { }

    public override string ToString()
    {
        StringBuilder result = new(Message);

        result.Append(Position.ToStringCool().Surround(" (at ", ")"));

        if (File != null)
        { result.Append($" (in {File})"); }

        if (InnerException != null)
        { result.Append($" {InnerException}"); }

        return result.ToString();
    }

    public string? GetArrows()
    {
        if (File == null) return null;
        if (!File.IsFile) return null;
        return GetArrows(Position, System.IO.File.ReadAllText(File.AbsolutePath));
    }

    public static string? GetArrows(Position position, string text)
    {
        if (position.AbsoluteRange == 0) return null;
        if (position == Position.UnknownPosition) return null;
        if (position.Range.Start.Line != position.Range.End.Line)
        { return null; }

        string[] lines = text.ReplaceLineEndings("\n").Split('\n');

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
