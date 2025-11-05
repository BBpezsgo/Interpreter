using System.Diagnostics;

namespace LanguageCore.TUI;

public struct BufferWriter : IBufferWriter<BufferWriter>
{
    readonly AnsiBufferSlice _buffer;
    int _x = 0;
    int _y = 0;
    int _offset = 0;

    public int Offset => _offset;

    [DebuggerStepThrough]
    public BufferWriter(AnsiBufferSlice buffer, int x, int y)
    {
        _buffer = buffer;
        _x = x;
        _y = y;
    }

    public BufferWriter PadTo(int to, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        if (_offset < to)
        {
            Write(' ', to - _offset, foreground, background);
        }
        return this;
    }

    public BufferWriter Write(ReadOnlySpan<char> text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        _buffer.Text(_x + _offset, _y, text, foreground, background);
        _offset += text.Length;
        return this;
    }

    public BufferWriter Write(char text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        _buffer.Text(_x + _offset, _y, text, foreground, background);
        _offset++;
        return this;
    }

    public BufferWriter Write(char c, int repeat, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        for (int i = 0; i < repeat; i++)
        {
            _buffer.Text(_x + _offset + i, _y, c, foreground, background);
        }
        _offset += repeat;
        return this;
    }
}
