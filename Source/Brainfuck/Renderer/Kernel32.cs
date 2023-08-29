#pragma warning disable IDE0044 // Add readonly modifier

using Microsoft.Win32.SafeHandles;

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

#nullable enable

namespace ProgrammingLanguage.Brainfuck.Renderer
{
    internal static class Kernel32
    {
        internal const uint STD_INPUT_HANDLE = unchecked((uint)-10);
        internal const uint STD_OUTPUT_HANDLE = unchecked((uint)-11);
        internal const uint STD_ERROR_HANDLE = unchecked((uint)-12);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool ReadConsoleInput(IntPtr hConsoleInput, [Out] InputEvent[] lpBuffer, uint nLength, ref uint lpNumberOfEventsRead);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool WriteConsoleInput(IntPtr hConsoleInput, InputEvent[] lpBuffer, uint nLength, ref uint lpNumberOfEventsWritten);

        [DllImport("Kernel32.dll")]
        public static extern uint GetConsoleCP();

        [DllImport("Kernel32.dll")]
        public static extern uint GetConsoleOutputCP();

        [DllImport("Kernel32.dll")]
        public static extern bool SetConsoleCP(uint wCodePageID);

        [DllImport("Kernel32.dll")]
        public static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetStdHandle(uint nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFileHandle CreateFile(
            string fileName,
            [MarshalAs(UnmanagedType.U4)] uint fileAccess,
            [MarshalAs(UnmanagedType.U4)] uint fileShare,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] int flags,
            IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool WriteConsoleOutput(
            IntPtr hConsoleOutput,
            CharInfo[] lpBuffer,
            Coord dwBufferSize,
            Coord dwBufferCoord,
            ref SmallRect lpWriteRegion);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool WriteConsoleOutputW(
            SafeFileHandle hConsoleOutput,
            CharInfo[] lpBuffer,
            Coord dwBufferSize,
            Coord dwBufferCoord,
            ref SmallRect lpWriteRegion);

        [DllImport("kernel32.dll")]
        internal static extern bool GetConsoleMode(IntPtr hConsoleInput, ref uint lpMode);

        [DllImport("kernel32.dll")]
        internal static extern bool SetConsoleMode(IntPtr hConsoleInput, uint dwMode);
    }

    public struct EventType
    {
        public const ushort KEY = 0x0001;
        public const ushort MOUSE = 0x0002;
        public const ushort WINDOW_BUFFER_SIZE = 0x0004;
    }

    internal enum ForegroundColor : short
    {
        Black = 0,
        DarkGray = CharInfoAttributes.FOREGROUND_BRIGHT,
        Gray = CharInfoAttributes.FOREGROUND_RED | CharInfoAttributes.FOREGROUND_GREEN | CharInfoAttributes.FOREGROUND_BLUE,
        White = CharInfoAttributes.FOREGROUND_RED | CharInfoAttributes.FOREGROUND_GREEN | CharInfoAttributes.FOREGROUND_BLUE | CharInfoAttributes.FOREGROUND_BRIGHT,

        DarkRed = CharInfoAttributes.FOREGROUND_RED,
        DarkGreen = CharInfoAttributes.FOREGROUND_GREEN,
        DarkBlue = CharInfoAttributes.FOREGROUND_BLUE,
        DarkYellow = CharInfoAttributes.FOREGROUND_RED | CharInfoAttributes.FOREGROUND_GREEN,
        DarkCyan = CharInfoAttributes.FOREGROUND_BLUE | CharInfoAttributes.FOREGROUND_GREEN,
        DarkMagenta = CharInfoAttributes.FOREGROUND_RED | CharInfoAttributes.FOREGROUND_BLUE,

        Red = CharInfoAttributes.FOREGROUND_RED | CharInfoAttributes.FOREGROUND_BRIGHT,
        Green = CharInfoAttributes.FOREGROUND_GREEN | CharInfoAttributes.FOREGROUND_BRIGHT,
        Blue = CharInfoAttributes.FOREGROUND_BLUE | CharInfoAttributes.FOREGROUND_BRIGHT,
        Yellow = CharInfoAttributes.FOREGROUND_RED | CharInfoAttributes.FOREGROUND_GREEN | CharInfoAttributes.FOREGROUND_BRIGHT,
        Cyan = CharInfoAttributes.FOREGROUND_BLUE | CharInfoAttributes.FOREGROUND_GREEN | CharInfoAttributes.FOREGROUND_BRIGHT,
        Magenta = CharInfoAttributes.FOREGROUND_RED | CharInfoAttributes.FOREGROUND_BLUE | CharInfoAttributes.FOREGROUND_BRIGHT,
    }

    internal enum BackgroundColor : short
    {
        Black = 0,
        DarkGray = CharInfoAttributes.BACKGROUND_BRIGHT,
        Gray = CharInfoAttributes.BACKGROUND_RED | CharInfoAttributes.BACKGROUND_GREEN | CharInfoAttributes.BACKGROUND_BLUE,
        White = CharInfoAttributes.BACKGROUND_RED | CharInfoAttributes.BACKGROUND_GREEN | CharInfoAttributes.BACKGROUND_BLUE | CharInfoAttributes.BACKGROUND_BRIGHT,

        DarkRed = CharInfoAttributes.BACKGROUND_RED,
        DarkGreen = CharInfoAttributes.BACKGROUND_GREEN,
        DarkBlue = CharInfoAttributes.BACKGROUND_BLUE,
        DarkYellow = CharInfoAttributes.BACKGROUND_RED | CharInfoAttributes.BACKGROUND_GREEN,
        DarkCyan = CharInfoAttributes.BACKGROUND_BLUE | CharInfoAttributes.BACKGROUND_GREEN,
        DarkMagenta = CharInfoAttributes.BACKGROUND_RED | CharInfoAttributes.BACKGROUND_BLUE,

        Red = CharInfoAttributes.BACKGROUND_RED | CharInfoAttributes.BACKGROUND_BRIGHT,
        Green = CharInfoAttributes.BACKGROUND_GREEN | CharInfoAttributes.BACKGROUND_BRIGHT,
        Blue = CharInfoAttributes.BACKGROUND_BLUE | CharInfoAttributes.BACKGROUND_BRIGHT,
        Yellow = CharInfoAttributes.BACKGROUND_RED | CharInfoAttributes.BACKGROUND_GREEN | CharInfoAttributes.BACKGROUND_BRIGHT,
        Cyan = CharInfoAttributes.BACKGROUND_BLUE | CharInfoAttributes.BACKGROUND_GREEN | CharInfoAttributes.BACKGROUND_BRIGHT,
        Magenta = CharInfoAttributes.BACKGROUND_RED | CharInfoAttributes.BACKGROUND_BLUE | CharInfoAttributes.BACKGROUND_BRIGHT,
    }

    [Flags]
    enum CharInfoAttributes : short
    {
        FOREGROUND_BLUE = 0x0001, // File color contains blue.
        FOREGROUND_GREEN = 0x0002, // File color contains green.
        FOREGROUND_RED = 0x0004, // File color contains red.
        FOREGROUND_BRIGHT = 0x0008, // File color is intensified.

        BACKGROUND_BLUE = 0x0010,// Background color contains blue.
        BACKGROUND_GREEN = 0x0020, // Background color contains green.
        BACKGROUND_RED = 0x0040,// Background color contains red.
        BACKGROUND_BRIGHT = 0x0080, // Background color is intensified.

        COMMON_LVB_LEADING_BYTE = 0x0100, // Leading byte.
        COMMON_LVB_TRAILING_BYTE = 0x0200, // Trailing byte.
        COMMON_LVB_GRID_HORIZONTAL = 0x0400, // Top horizontal.
        COMMON_LVB_GRID_LVERTICAL = 0x0800, // Left vertical.
        COMMON_LVB_GRID_RVERTICAL = 0x1000, // Right vertical.
        COMMON_LVB_REVERSE_VIDEO = 0x4000, // Reverse foreground and background attribute.
                                           // COMMON_LVB_UNDERSCORE = 0x8000, // Underscore.
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Coord
    {
        public short X;
        public short Y;

        public Coord(short x, short y)
        {
            this.X = x;
            this.Y = y;
        }
        public Coord(int x, int y) : this((short)x, (short)y)
        { }
        public Coord(Point p) : this(p.X, p.Y)
        { }

        public override readonly bool Equals(object? obj) => obj is Coord coord && Equals(coord);
        public readonly bool Equals(Coord other) =>
            this.X == other.X &&
            this.Y == other.Y;

        public override readonly int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(Coord a, Coord b) => a.Equals(b);
        public static bool operator !=(Coord a, Coord b) => !(a == b);

        public override readonly string ToString() => $"{{ {X} ; {Y} }}";
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    internal struct CharUnion
    {
        [FieldOffset(0)] public char UnicodeChar;
        // [FieldOffset(0)] public byte AsciiChar;

        public CharUnion(char @char)
        {
            UnicodeChar = @char;
            // AsciiChar = 0;
        }

        public override readonly bool Equals(object? obj) => obj is CharUnion charUnion && Equals(charUnion);
        public readonly bool Equals(CharUnion other) =>
            this.UnicodeChar == other.UnicodeChar;

        public override readonly int GetHashCode() => HashCode.Combine(UnicodeChar);

        public static bool operator ==(CharUnion a, CharUnion b) => a.Equals(b);
        public static bool operator !=(CharUnion a, CharUnion b) => !(a == b);

        public override readonly string ToString() => $"{{ UnicodeChar: '{UnicodeChar}' }}";
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct CharInfo
    {
        [FieldOffset(0)] public CharUnion Char;
        [FieldOffset(2)] public short Attributes;

        public CharInfo(CharUnion @char, short attributes)
        {
            this.Char = @char;
            this.Attributes = attributes;
        }
        public CharInfo(char @char, short attributes) : this(new CharUnion(@char), attributes)
        { }

        public CharInfo(CharUnion @char) : this(@char, 0)
        { }
        public CharInfo(char @char) : this(new CharUnion(@char), 0)
        { }

        public CharInfo(CharUnion @char, ForegroundColor fg, BackgroundColor bg) : this(@char, (short)((short)fg | (short)bg))
        { }
        public CharInfo(char @char, ForegroundColor fg, BackgroundColor bg) : this(new CharUnion(@char), fg, bg)
        { }

        public ForegroundColor ForegroundColor
        {
            readonly get => (ForegroundColor)(Attributes & (0x0001 | 0x0002 | 0x0004 | 0x0008));
            set => Attributes = (short)((short)BackgroundColor & (short)value);
        }

        public BackgroundColor BackgroundColor
        {
            readonly get => (BackgroundColor)(Attributes & (0x0010 | 0x0020 | 0x0040 | 0x0080));
            set => Attributes = (short)((short)ForegroundColor & (short)value);
        }

        public override readonly bool Equals(object? obj) => obj is CharInfo charInfo && Equals(charInfo);
        public readonly bool Equals(CharInfo other) =>
            this.Attributes == other.Attributes &&
            this.Char == other.Char;

        public override readonly int GetHashCode() => HashCode.Combine(Attributes, Char);

        public static bool operator ==(CharInfo a, CharInfo b) => a.Equals(b);
        public static bool operator !=(CharInfo a, CharInfo b) => !(a == b);

        public override readonly string ToString() => $"{{ Attributes: {Attributes} Char: {Char} }}";
    }

    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    internal struct SmallRect : IEquatable<SmallRect>
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;

        public readonly short Width => (short)(Right - Left + 1);
        public readonly short Height => (short)(Bottom - Top + 1);

        public override readonly bool Equals(object? obj) => obj is SmallRect rect && Equals(rect);
        public readonly bool Equals(SmallRect other) =>
            Left == other.Left &&
            Top == other.Top &&
            Right == other.Right &&
            Bottom == other.Bottom;

        public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

        public static bool operator ==(SmallRect a, SmallRect b) => a.Equals(b);
        public static bool operator !=(SmallRect a, SmallRect b) => !(a == b);

        public override readonly string ToString() => $"{{ Left: {Left} Top: {Top} Bottom: {Bottom} Right: {Right} }}";
    }

    internal struct InputMode
    {
        internal const uint
            ENABLE_MOUSE_INPUT = 0x0010,
            ENABLE_QUICK_EDIT_MODE = 0x0040,
            ENABLE_EXTENDED_FLAGS = 0x0080,
            ENABLE_ECHO_INPUT = 0x0004,
            ENABLE_WINDOW_INPUT = 0x0008;

        public static void Default(ref uint mode)
        {
            mode &= ~InputMode.ENABLE_QUICK_EDIT_MODE;
            mode |= InputMode.ENABLE_WINDOW_INPUT;
            mode |= InputMode.ENABLE_MOUSE_INPUT;
        }
    }

    internal enum MouseButtonState : ulong
    {
        Left = 1,
        Right = 2,
        Middle = 4,
        ScrollUp = 7864320,
        ScrollDown = 4287102976,
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputEvent
    {
        [FieldOffset(0)]
        public ushort EventType;
        [FieldOffset(4)]
        public KeyEvent KeyEvent;
        [FieldOffset(4)]
        public MouseEvent MouseEvent;
        [FieldOffset(4)]
        public WindowBufferSizeEvent WindowBufferSizeEvent;
        /*
         MENU_EVENT_RECORD MenuEvent;
         FOCUS_EVENT_RECORD FocusEvent;
         */
    }

#pragma warning disable CS0649
    internal struct MouseEvent
    {
        Coord dwMousePosition;
        uint dwButtonState;
        uint dwControlKeyState;
        uint dwEventFlags;

        public readonly Coord Position => dwMousePosition;
        public readonly MouseButtonState ButtonState => (MouseButtonState)dwButtonState;
        public readonly uint ControlKeyState => dwControlKeyState;
        public readonly uint EventFlags => dwEventFlags;
    }
#pragma warning restore CS0649

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    internal struct KeyEvent
    {
        [FieldOffset(0)]
        bool bKeyDown;
        [FieldOffset(4)]
        ushort wRepeatCount;
        [FieldOffset(6)]
        ushort wVirtualKeyCode;
        [FieldOffset(8)]
        ushort wVirtualScanCode;
        [FieldOffset(10)]
        public readonly char UnicodeChar;
        [FieldOffset(10)]
        byte AsciiChar;
        [FieldOffset(12)]
        uint dwControlKeyState;

        public readonly bool IsDown => bKeyDown;
        public readonly ushort RepeatCount => wRepeatCount;
        public readonly ushort VirtualKeyCode => wVirtualKeyCode;
        public readonly uint ControlKeyState => dwControlKeyState;
    }

    internal struct WindowBufferSizeEvent
    {
#pragma warning disable CS0649
        Coord dwSize;
#pragma warning restore CS0649

        public readonly short Width => dwSize.X;
        public readonly short Height => dwSize.Y;
    }
}
