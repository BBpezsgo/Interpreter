namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class LanguageException : Exception
{
    public delegate string? GetFileContent(Uri uri);

    public Position Position { get; protected set; }
    public Uri? File { get; protected set; }

    public LanguageException(string message, Position position, Uri? file) : base(message)
    {
        Position = position;
        File = file;
    }

    public override string ToString()
    {
        StringBuilder result = new(LanguageException.Format(Message, Position, File));

        if (InnerException != null)
        { result.Append($" {InnerException}"); }

        return result.ToString();
    }

    public static string Format(string? message, Location location)
        => LanguageException.Format(message, location.Position, location.File);

    public static string Format(string? message, Position position, Uri? file)
    {
        StringBuilder result = new();
        if (!string.IsNullOrEmpty(message))
        {
            result.Append(message);
        }

        if (file is not null)
        {
            if (result.Length > 0) result.Append(' ');
            result.Append("(at ");
            result.Append(file);

            if (position.Range.Start.Line >= 0)
            {
                result.Append(':');
                result.Append(position.Range.Start.Line + 1);

                if (position.Range.Start.Character >= 0)
                {
                    result.Append(':');
                    result.Append(position.Range.Start.Character + 1);
                }
            }

            result.Append(')');
        }

        return result.ToString();
    }

    public (string SourceCode, string Arrows)? GetArrows(GetFileContent? getFileContent = null)
    {
        if (File == null) return null;
        if (!File.IsFile) return null;
        return GetArrows(Position, getFileContent?.Invoke(File) ?? System.IO.File.ReadAllText(File.AbsolutePath));
    }

    public static (string SourceCode, string Arrows)? GetArrows(Position position, string text)
    {
        if (position.AbsoluteRange == 0) return null;
        if (position == Position.UnknownPosition) return null;
        if (position.Range.Start.Line != position.Range.End.Line)
        { return null; }

        string[] lines = text.ReplaceLineEndings("\n").Split('\n');

        if (position.Range.Start.Line >= lines.Length)
        { return null; }

        string line = lines[position.Range.Start.Line];

        line = line.Replace('\t', ' ');

        int removedLeadingWhitespaces;
        {
            string trimmedLine = line.TrimStart();
            removedLeadingWhitespaces = line.Length - trimmedLine.Length;
            line = trimmedLine.Trim();
        }

        StringBuilder result = new();
        result.Append(' ', Math.Max(0, position.Range.Start.Character - removedLeadingWhitespaces));
        result.Append('^', Math.Max(1, position.Range.End.Character - position.Range.Start.Character));
        return (line, result.ToString());
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
            { lineBuilder.Append(' ', Math.Max(0, token.Position.Range.Start.Character - lineBuilder.Length)); }

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

    public static explicit operator Diagnostic(LanguageException exception) => new(
        DiagnosticsLevel.Error,
        exception.Message,
        exception.Position,
        exception.File,
        exception.InnerException is LanguageException innerLanguageException
            ? Enumerable.Repeat((Diagnostic)innerLanguageException, 1)
            : Enumerable.Empty<Diagnostic>()
    );
}
