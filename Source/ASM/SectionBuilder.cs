namespace LanguageCore.ASM;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
[ExcludeFromCodeCoverage]
public class SectionBuilder
{
    public const string EOL = "\r\n";

    public readonly StringBuilder Builder;
    public int Indent;
    public const int IndentIncrement = 2;

    public SectionBuilder()
    {
        this.Builder = new StringBuilder();
        this.Indent = 0;
    }

    public void AppendText(char text) => Builder.Append(text);
    public void AppendText(char text, int repeatCount) => Builder.Append(text, repeatCount);
    public void AppendText(string text) => Builder.Append(text);
    public void AppendTextLine() => Builder.Append(EOL);
    public void AppendTextLine(string text) { Builder.Append(text); Builder.Append(EOL); }

    public void AppendComment(string? comment)
    {
        Builder.Append(' ', Indent);
        Builder.Append(';');
        if (!string.IsNullOrWhiteSpace(comment))
        {
            Builder.Append(' ');
            Builder.Append(comment);
        }
    }
    public void AppendCommentLine(string? comment)
    {
        AppendComment(comment);
        Builder.Append(EOL);
    }

    public IndentBlock Block() => new(this);

    string GetDebuggerDisplay() => Builder.ToString();
}
