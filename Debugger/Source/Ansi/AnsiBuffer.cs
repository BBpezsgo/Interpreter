using System.Diagnostics;

namespace LanguageCore.TUI;

public readonly struct AnsiBuffer
{
    public readonly AnsiCharacter[] Buffer;
    public readonly int Width;
    public readonly int Height;

    [DebuggerStepThrough]
    public AnsiBuffer(AnsiCharacter[] buffer, int width, int height)
    {
        Buffer = buffer;
        Width = width;
        Height = height;
    }

    public readonly AnsiCharacter this[int x, int y]
    {
        [DebuggerStepThrough]
        get
        {
            return Buffer[x + (y * Width)];
        }
        [DebuggerStepThrough]
        set
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            Buffer[x + (y * Width)] = value;
        }
    }
}
