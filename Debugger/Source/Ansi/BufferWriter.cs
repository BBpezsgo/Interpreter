using System.Diagnostics;

namespace LanguageCore.TUI;

struct BufferWriter
{
    readonly AnsiBufferSlice Buffer;
    int X = 0;
    int Y = 0;
    int Offset = 0;

    [DebuggerStepThrough]
    public BufferWriter(AnsiBufferSlice buffer, int x, int y)
    {
        Buffer = buffer;
        X = x;
        Y = y;
    }

    public BufferWriter PadTo(int to, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        if (Offset < to)
        {
            Write(' ', to - Offset, foreground, background);
        }
        return this;
    }

    public BufferWriter Write(ReadOnlySpan<char> text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        Buffer.Text(X + Offset, Y, text, foreground, background);
        Offset += text.Length;
        return this;
    }

    public BufferWriter Write(char text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        Buffer.Text(X + Offset, Y, text, foreground, background);
        Offset++;
        return this;
    }

    public BufferWriter Write(char c, int repeat, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        for (int i = 0; i < repeat; i++)
        {
            Buffer.Text(X + Offset + i, Y, c, foreground, background);
        }
        Offset += repeat;
        return this;
    }
}
