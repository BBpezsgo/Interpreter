namespace LanguageCore.TUI;

public interface IBufferWriter<TSelf> where TSelf : IBufferWriter<TSelf>
{
    public TSelf Write(ReadOnlySpan<char> text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default);

    public TSelf Write(char text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default);

    public TSelf Write(char c, int repeat, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default);
}
