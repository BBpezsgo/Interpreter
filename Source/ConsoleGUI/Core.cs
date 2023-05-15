using Microsoft.Win32.SafeHandles;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/*
 * Copy-pasted from https://stackoverflow.com/questions/2754518/how-can-i-write-fast-colored-output-to-console
 */

namespace ConsoleGUI
{
    public enum MouseButtonState : ulong
    {
        Left = 1,
        Right = 2,
        Middle = 4,
        ScrollUp = 7864320,
        ScrollDown = 4287102976,
    }

    public enum CharColors : short
    {
        FgBlack = 0,
        FgGray = CharInfoAttributes.FOREGROUND_INTENSITY,
        FgRed = CharInfoAttributes.FOREGROUND_RED,
        FgDarkGreen = CharInfoAttributes.FOREGROUND_GREEN,
        FgDarkBlue = CharInfoAttributes.FOREGROUND_BLUE,
        FgOrange = FgRed | FgDarkGreen,
        FgLightBlue = FgDarkGreen | FgDarkBlue,
        FgMagenta = FgDarkBlue | FgRed,
        FgDefault = FgRed | FgDarkGreen | FgDarkBlue,

        FgLightRed = FgRed | CharInfoAttributes.FOREGROUND_INTENSITY,
        FgGreen = FgDarkGreen | CharInfoAttributes.FOREGROUND_INTENSITY,
        FgBlue = FgDarkBlue | CharInfoAttributes.FOREGROUND_INTENSITY,
        FgYellow = FgOrange | CharInfoAttributes.FOREGROUND_INTENSITY,
        FgCyan = FgLightBlue | CharInfoAttributes.FOREGROUND_INTENSITY,
        FgWhite = FgDefault | CharInfoAttributes.FOREGROUND_INTENSITY,

        BgBlack = FgBlack << 4,
        BgGray = FgGray << 4,
        BgRed = FgRed << 4,
        BgGreen = FgDarkGreen << 4,
        BgBlue = FgDarkBlue << 4,
        BgYellow = FgOrange << 4,
        BgCyan = FgLightBlue << 4,
        BgMagenta = FgMagenta << 4,
        BgWhite = FgDefault << 4,
    }

    public readonly struct MouseEvent
    {
        public readonly Position Position;
        public readonly MouseButtonState ButtonState;
        public readonly uint ControlKeyState;
        public readonly uint EventFlags;

        public short X => Position.X;
        public short Y => Position.Y;

        internal MouseEvent(ConsoleLib.NativeMethods.MOUSE_EVENT_RECORD e)
        {
            Position = new Position(e.dwMousePosition);
            ButtonState = (MouseButtonState)e.dwButtonState;
            ControlKeyState = e.dwControlKeyState;
            EventFlags = e.dwEventFlags;
        }
    }

    public readonly struct KeyEvent
    {
        public readonly bool KeyDown;

        public readonly ushort RepeatCount;
        public readonly ushort VirtualKeyCode;
        public readonly ushort VirtualScanCode;

        public readonly char UnicodeChar;
        public readonly byte AsciiChar;
        public readonly uint ControlKeyState;

        internal KeyEvent(ConsoleLib.NativeMethods.KEY_EVENT_RECORD e)
        {
            KeyDown = e.bKeyDown;
            RepeatCount = e.wRepeatCount;
            VirtualKeyCode = e.wVirtualKeyCode;
            VirtualScanCode = e.wVirtualScanCode;
            UnicodeChar = e.UnicodeChar;
            AsciiChar = e.AsciiChar;
            ControlKeyState = e.dwControlKeyState;
        }
    }

    public readonly struct WindowBufferSizeEvent
    {
        public readonly short Width;
        public readonly short Height;

        internal WindowBufferSizeEvent(ConsoleLib.NativeMethods.WINDOW_BUFFER_SIZE_RECORD e)
        {
            Width = e.dwSize.X;
            Height = e.dwSize.Y;
        }
    }

    public readonly struct Position
    {
        public readonly short X;
        public readonly short Y;

        internal Position(ConsoleLib.NativeMethods.COORD v)
        {
            X = v.X;
            Y = v.Y;
        }
        internal Position(short x, short y)
        {
            X = x;
            Y = y;
        }
    }

    [Flags]
    public enum CharInfoAttributes : short
    {
        FOREGROUND_BLUE = 0x0001, // File color contains blue.
        FOREGROUND_GREEN = 0x0002, // File color contains green.
        FOREGROUND_RED = 0x0004, // File color contains red.
        FOREGROUND_INTENSITY = 0x0008, // File color is intensified.

        BACKGROUND_BLUE = 0x0010,// Background color contains blue.
        BACKGROUND_GREEN = 0x0020, // Background color contains green.
        BACKGROUND_RED = 0x0040,// Background color contains red.
        BACKGROUND_INTENSITY = 0x0080, // Background color is intensified.

        COMMON_LVB_LEADING_BYTE = 0x0100, // Leading byte.
        COMMON_LVB_TRAILING_BYTE = 0x0200, // Trailing byte.
        COMMON_LVB_GRID_HORIZONTAL = 0x0400, // Top horizontal.
        COMMON_LVB_GRID_LVERTICAL = 0x0800, // Left vertical.
        COMMON_LVB_GRID_RVERTICAL = 0x1000, // Right vertical.
        COMMON_LVB_REVERSE_VIDEO = 0x4000, // Reverse foreground and background attribute.
                                           // COMMON_LVB_UNDERSCORE = 0x8000, // Underscore.
    }

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

    public static class Core
    {
        public static void SetupConsole()
        {
            IntPtr inHandle = ConsoleLib.NativeMethods.GetStdHandle(ConsoleLib.NativeMethods.STD_INPUT_HANDLE);
            uint mode = 0;
            ConsoleLib.NativeMethods.GetConsoleMode(inHandle, ref mode);
            InputMode.Default(ref mode);
            ConsoleLib.NativeMethods.SetConsoleMode(inHandle, mode);

            Console.CursorVisible = false;
        }

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

        [StructLayout(LayoutKind.Sequential)]
        public struct Coord
        {
            public short X;
            public short Y;

            public Coord(short x, short y)
            {
                this.X = x;
                this.Y = y;
            }
        };

        [StructLayout(LayoutKind.Explicit)]
        public struct CharUnion
        {
            [FieldOffset(0)] public ushort UnicodeChar;
            [FieldOffset(0)] public byte AsciiChar;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct CharInfo
        {
            [FieldOffset(0)] public CharUnion Char;
            [FieldOffset(2)] public short Attributes;
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

    public struct EventType
    {
        public const ushort KEY = 0x0001;
        public const ushort MOUSE = 0x0002;
        public const ushort WINDOW_BUFFER_SIZE = 0x0004;
    }

#if NET6_0
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
#endif
    public struct Character
    {
        public char Char;
        public CharColors Color;

        public override string ToString() => Char.ToString();
        string GetDebuggerDisplay() => ToString();
    }

    namespace ConsoleLib
    {
        using System.Runtime.InteropServices;
        using System.Threading;

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
                IntPtr handleIn = NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE);
                new Thread(() =>
                {
                    while (true)
                    {
                        uint numRead = 0;
                        NativeMethods.INPUT_RECORD[] record = new NativeMethods.INPUT_RECORD[1];
                        record[0] = new NativeMethods.INPUT_RECORD();
                        NativeMethods.ReadConsoleInput(handleIn, record, 1, ref numRead);
                        if (Run)
                        {
                            switch (record[0].EventType)
                            {
                                case EventType.MOUSE:
                                    MouseEvent?.Invoke(new MouseEvent(record[0].MouseEvent));
                                    break;
                                case EventType.KEY:
                                    KeyEvent?.Invoke(new KeyEvent(record[0].KeyEvent));
                                    break;
                                case EventType.WINDOW_BUFFER_SIZE:
                                    WindowBufferSizeEvent?.Invoke(new WindowBufferSizeEvent(record[0].WindowBufferSizeEvent));
                                    break;
                            }
                        }
                        else
                        {
                            uint numWritten = 0;
                            NativeMethods.WriteConsoleInput(handleIn, record, 1, ref numWritten);
                            Console.CursorVisible = true;
                            return;
                        }
                    }
                }).Start();
            }

            public static void Stop() => Run = false;
        }

        public delegate void ConsoleEvent<T>(T e);

        internal static class NativeMethods
        {
            internal struct COORD
            {
                public short X;
                public short Y;
            }

            [StructLayout(LayoutKind.Explicit)]
            internal struct INPUT_RECORD
            {
                [FieldOffset(0)]
                public ushort EventType;
                [FieldOffset(4)]
                public KEY_EVENT_RECORD KeyEvent;
                [FieldOffset(4)]
                public MOUSE_EVENT_RECORD MouseEvent;
                [FieldOffset(4)]
                public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
                /*
                 MENU_EVENT_RECORD MenuEvent;
                 FOCUS_EVENT_RECORD FocusEvent;
                 */
            }

#pragma warning disable CS0649
            internal struct MOUSE_EVENT_RECORD
            {
                public COORD dwMousePosition;
                public uint dwButtonState;
                public uint dwControlKeyState;
                public uint dwEventFlags;
            }
#pragma warning restore CS0649

            [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
            internal struct KEY_EVENT_RECORD
            {
                [FieldOffset(0)]
                public bool bKeyDown;
                [FieldOffset(4)]
                public ushort wRepeatCount;
                [FieldOffset(6)]
                public ushort wVirtualKeyCode;
                [FieldOffset(8)]
                public ushort wVirtualScanCode;
                [FieldOffset(10)]
                public char UnicodeChar;
                [FieldOffset(10)]
                public byte AsciiChar;
                [FieldOffset(12)]
                public uint dwControlKeyState;
            }

            internal struct WINDOW_BUFFER_SIZE_RECORD
            {
#pragma warning disable CS0649
                public COORD dwSize;
#pragma warning restore CS0649
            }

            internal const uint STD_INPUT_HANDLE = unchecked((uint)-10),
                STD_OUTPUT_HANDLE = unchecked((uint)-11),
                STD_ERROR_HANDLE = unchecked((uint)-12);

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
    }
}
