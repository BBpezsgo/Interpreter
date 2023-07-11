#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649

using Microsoft.Win32.SafeHandles;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleDrawer
{
    public static class Kernel32
    {
        internal const uint
            STD_INPUT_HANDLE = unchecked((uint)-10),
            STD_OUTPUT_HANDLE = unchecked((uint)-11),
            STD_ERROR_HANDLE = unchecked((uint)-12);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFileHandle CreateFile(
            string fileName,
            [MarshalAs(UnmanagedType.U4)] uint fileAccess,
            [MarshalAs(UnmanagedType.U4)] uint fileShare,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] int flags,
            IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteConsoleOutputW(
          SafeFileHandle hConsoleOutput,
          CharInfo[] lpBuffer,
          Coord dwBufferSize,
          Coord dwBufferCoord,
          ref SmallRect lpWriteRegion);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetStdHandle(uint nStdHandle);

        [DllImport("kernel32.dll")]
        internal static extern bool GetConsoleMode(IntPtr hConsoleInput, ref uint lpMode);

        [DllImport("kernel32.dll")]
        internal static extern bool SetConsoleMode(IntPtr hConsoleInput, uint dwMode);


        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool ReadConsoleInput(IntPtr hConsoleInput, [Out] INPUT_RECORD[] lpBuffer, uint nLength, ref uint lpNumberOfEventsRead);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool WriteConsoleInput(IntPtr hConsoleInput, INPUT_RECORD[] lpBuffer, uint nLength, ref uint lpNumberOfEventsWritten);

    }

    public static class ConsoleHandler
    {
        public static void SetupConsole()
        {
            IntPtr inputHandle = Kernel32.GetStdHandle(Kernel32.STD_INPUT_HANDLE);
            uint mode = 0;
            Kernel32.GetConsoleMode(inputHandle, ref mode);
            InputMode.Default(ref mode);
            Kernel32.SetConsoleMode(inputHandle, mode);

            Console.CursorVisible = false;
        }
    }

    public static class ConsoleListener
    {
        public static event ConsoleEvent<MouseEvent> MouseEvent;
        public static event ConsoleEvent<KeyEvent> KeyEvent;
        public static event ConsoleEvent<WindowBufferSizeEvent> WindowBufferSizeEvent;

        static bool Run;

        public static void Start()
        {
            if (Run) return;
            Run = true;
            IntPtr handleIn = Kernel32.GetStdHandle(Kernel32.STD_INPUT_HANDLE);
            new Thread(() =>
            {
                while (true)
                {
                    uint numRead = 0;
                    INPUT_RECORD[] record = new INPUT_RECORD[1];
                    record[0] = new INPUT_RECORD();
                    Kernel32.ReadConsoleInput(handleIn, record, 1, ref numRead);
                    if (Run)
                    {
                        switch (record[0].EventType)
                        {
                            case EventType.MOUSE:
                                MouseEvent?.Invoke(record[0].MouseEvent);
                                break;
                            case EventType.KEY:
                                KeyEvent?.Invoke(record[0].KeyEvent);
                                break;
                            case EventType.WINDOW_BUFFER_SIZE:
                                WindowBufferSizeEvent?.Invoke(record[0].WindowBufferSizeEvent);
                                break;
                        }
                    }
                    else
                    {
                        uint numWritten = 0;
                        Kernel32.WriteConsoleInput(handleIn, record, 1, ref numWritten);
                        Console.CursorVisible = true;
                        return;
                    }
                }
            }).Start();
        }

        public static void Stop() => Run = false;
    }

    public delegate void ConsoleEvent<T>(T e);

    public struct InputMode
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

    [Flags]
    public enum CharInfoAttributes : short
    {
        FOREGROUND_BLUE = 0x0001,           // File color contains blue.
        FOREGROUND_GREEN = 0x0002,          // File color contains green.
        FOREGROUND_RED = 0x0004,            // File color contains red.
        FOREGROUND_INTENSITY = 0x0008,      // File color is intensified.

        BACKGROUND_BLUE = 0x0010,           // Background color contains blue.
        BACKGROUND_GREEN = 0x0020,          // Background color contains green.
        BACKGROUND_RED = 0x0040,            // Background color contains red.
        BACKGROUND_INTENSITY = 0x0080,      // Background color is intensified.

        COMMON_LVB_LEADING_BYTE = 0x0100,   // Leading byte.
        COMMON_LVB_TRAILING_BYTE = 0x0200,  // Trailing byte.
        COMMON_LVB_GRID_HORIZONTAL = 0x0400,// Top horizontal.
        COMMON_LVB_GRID_LVERTICAL = 0x0800, // Left vertical.
        COMMON_LVB_GRID_RVERTICAL = 0x1000, // Right vertical.
        COMMON_LVB_REVERSE_VIDEO = 0x4000,  // Reverse foreground and background attribute.
        // COMMON_LVB_UNDERSCORE = 0x8000,  // Underscore.
    }

    public struct EventType
    {
        public const ushort KEY = 0x0001;
        public const ushort MOUSE = 0x0002;
        public const ushort WINDOW_BUFFER_SIZE = 0x0004;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUT_RECORD
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

    public enum MouseButtonState : ulong
    {
        Left = 1,
        Right = 2,
        Middle = 4,
        ScrollUp = 7864320,
        ScrollDown = 4287102976,
    }

    public struct MouseEvent
    {
        Coord dwMousePosition;
        uint dwButtonState;
        uint dwControlKeyState;
        uint dwEventFlags;

        public readonly MouseButtonState ButtonState => (MouseButtonState)dwButtonState;
        public readonly uint ControlKeyState => dwControlKeyState;
        public readonly uint EventFlags => dwEventFlags;

        public readonly Coord Position => dwMousePosition;

        public readonly short X => dwMousePosition.X;
        public readonly short Y => dwMousePosition.Y;

    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct KeyEvent
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
        char unicodeChar;
        [FieldOffset(10)]
        byte asciiChar;
        [FieldOffset(12)]
        uint dwControlKeyState;

        public readonly bool KeyDown => bKeyDown;
        public readonly char UnicodeChar => unicodeChar;
        public readonly uint ControlKeyState => dwControlKeyState;
        public readonly int AsciiChar => asciiChar;

        public override readonly string ToString()
        {
            return $"({(KeyDown ? " Down" : "")} Char: \'{unicodeChar}\' Control: {dwControlKeyState} )";
        }
    }

    public struct WindowBufferSizeEvent
    {
        Coord dwSize;

        public readonly short Width => dwSize.X;
        public readonly short Height => dwSize.Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct Coord
    {
        public short X;
        public short Y;

        public Coord(short x, short y)
        {
            this.X = x;
            this.Y = y;
        }

        public Coord(int x, int y)
        {
            this.X = (short)x;
            this.Y = (short)y;
        }

        public static Coord operator +(Coord a, Coord b) => new(a.X + b.X, a.Y + b.Y);
        public static Coord operator -(Coord a, Coord b) => new(a.X - b.X, a.Y - b.Y);

        public static Coord operator +(Coord a, System.Drawing.Point b) => new(a.X + b.X, a.Y + b.Y);
        public static Coord operator -(Coord a, System.Drawing.Point b) => new(a.X - b.X, a.Y - b.Y);

        public static Coord operator +(Coord a, short b) => new(a.X + b, a.Y + b);
        public static Coord operator -(Coord a, short b) => new(a.X - b, a.Y - b);

        public static Coord operator +(Coord a, int b) => new(a.X + b, a.Y + b);
        public static Coord operator -(Coord a, int b) => new(a.X - b, a.Y - b);

        readonly string GetDebuggerDisplay() => $"({X} , {Y})";
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct CharUnion
    {
        [FieldOffset(0)] public ushort UnicodeChar;
        [FieldOffset(0)] public byte AsciiChar;

        public CharUnion(char unicodeChar)
        {
            UnicodeChar = (ushort)unicodeChar;
            AsciiChar = default;
        }

        public CharUnion(ushort unicodeChar)
        {
            UnicodeChar = unicodeChar;
            AsciiChar = default;
        }

        public CharUnion(byte asciiChar)
        {
            UnicodeChar = default;
            AsciiChar = asciiChar;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct CharInfo
    {
        [FieldOffset(0)] public CharUnion Char;
        [FieldOffset(2)] public short Attributes;

        public CharInfo(CharUnion @char, short attributes)
        {
            Char = @char;
            Attributes = attributes;
        }

        public CharInfo(CharUnion @char, CharInfoAttributes attributes)
        {
            Char = @char;
            Attributes = (short)attributes;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SmallRect
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }
}
