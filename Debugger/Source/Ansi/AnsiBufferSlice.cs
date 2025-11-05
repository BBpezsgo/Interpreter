using System.Diagnostics;

namespace LanguageCore.TUI;

public readonly struct AnsiBufferSlice
{
    readonly AnsiBuffer Buffer;
    public readonly int Width;
    public readonly int Height;
    readonly int OffsetX;
    readonly int OffsetY;

    [DebuggerStepThrough]
    public AnsiBufferSlice(AnsiBuffer buffer, int width, int height, int offsetX, int offsetY)
    {
        Buffer = buffer;
        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    [DebuggerStepThrough]
    public AnsiBufferSlice(in AnsiBufferSlice buffer, int width, int height, int offsetX, int offsetY)
    {
        Buffer = buffer.Buffer;
        Width = width;
        Height = height;
        OffsetX = offsetX + buffer.OffsetX;
        OffsetY = offsetY + buffer.OffsetY;
    }

    public readonly AnsiCharacter this[int x, int y]
    {
        [DebuggerStepThrough]
        get
        {
            return Buffer[x + OffsetX, y + OffsetY];
        }
        [DebuggerStepThrough]
        set
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            Buffer[x + OffsetX, y + OffsetY] = value;
        }
    }

    public void Clear()
    {
        for (int i = 0; i < Height; i++)
        {
            Array.Clear(Buffer.Buffer, OffsetX + ((OffsetY + i) * Buffer.Width), Width);
        }
    }

    public void Text(int x, int y, ReadOnlySpan<char> text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        for (int i = 0; i < text.Length; i++)
        {
            this[x + i, y] = new(text[i], foreground, background);
        }
    }

    public void Text(int x, int y, char text, AnsiColor foreground = AnsiColor.Default, AnsiColor background = AnsiColor.Default)
    {
        this[x, y] = new(text, foreground, background);
    }

    public BufferWriter Text(int x, int y)
    {
        return new BufferWriter(this, x, y);
    }
}
