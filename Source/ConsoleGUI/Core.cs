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
    using static ConsoleLib.NativeMethods;

    struct MouseInfo
    {
        internal short X;
        internal short Y;
        internal uint Flags;
        internal uint ControlKeyState;
        internal ButtonStateEnum ButtonState;

        internal enum ButtonStateEnum : ulong
        {
            Left = 1,
            Right = 2,
            Middle = 4,
            ScrollUp = 7864320,
            ScrollDown = 4287102976,
        }
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

    public static class Core
    {
        public static void SetupConsole()
        {
            IntPtr inHandle = GetStdHandle(STD_INPUT_HANDLE);
            uint mode = 0;
            GetConsoleMode(inHandle, ref mode);
            mode &= ~ENABLE_QUICK_EDIT_MODE; //disable
            mode |= ENABLE_WINDOW_INPUT; //enable (if you want)
            mode |= ENABLE_MOUSE_INPUT; //enable
            SetConsoleMode(inHandle, mode);

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

            public Coord(short X, short Y)
            {
                this.X = X;
                this.Y = Y;
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

    public static class Extensions
    {
        public static string Repeat(this string v, int n)
        {
            string res = "";
            for (int i = 0; i < n; i++) res += v;
            return res;
        }

        public static byte ToByte(this char v) => Encoding.ASCII.GetBytes(new char[] { v })[0];
        public static byte[] ToBytes(this string v) => Encoding.ASCII.GetBytes(v);

        internal static Side GetSide(this System.Drawing.Rectangle v, int x, int y)
        {
            if (v.Left == x)
            {
                if (v.Top == y)
                {
                    return Side.TopLeft;
                }
                else if (v.Bottom == y)
                {
                    return Side.BottomLeft;
                }
                return Side.Left;
            }
            else if (v.Right == x)
            {
                if (v.Top == y)
                {
                    return Side.TopRight;
                }
                else if (v.Bottom == y)
                {
                    return Side.BottomRight;
                }
                return Side.Right;
            }
            else if (v.Bottom == y)
            {
                return Side.Bottom;
            }
            else if (v.Top == y)
            {
                return Side.Top;
            }
            return Side.None;
        }

        internal static Character Details(this char v) => new()
        {
            Char = v,
            Color = CharColors.FgDefault,
        };
    }
    internal enum Side
    {
        None,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left,
    }

#if NET6_0
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
#endif
    struct Character
    {
        public char Char;
        public CharColors Color;

        public override string ToString() => Char.ToString();

        private string GetDebuggerDisplay()
        {
            return ToString();
        }
    }

    namespace ConsoleLib
    {
        using System.Runtime.InteropServices;
        using System.Threading;

        using static ConsoleLib.NativeMethods;

        public static class ConsoleListener
        {
            internal static event ConsoleMouseEvent MouseEvent;

            internal static event ConsoleKeyEvent KeyEvent;

            internal static event ConsoleWindowBufferSizeEvent WindowBufferSizeEvent;

            private static bool Run;


            public static void Start()
            {
                if (!Run)
                {
                    Run = true;
                    IntPtr handleIn = GetStdHandle(STD_INPUT_HANDLE);
                    new Thread(() =>
                    {
                        while (true)
                        {
                            uint numRead = 0;
                            INPUT_RECORD[] record = new INPUT_RECORD[1];
                            record[0] = new INPUT_RECORD();
                            ReadConsoleInput(handleIn, record, 1, ref numRead);
                            if (Run)
                                switch (record[0].EventType)
                                {
                                    case INPUT_RECORD.MOUSE_EVENT:
                                        MouseEvent?.Invoke(record[0].MouseEvent);
                                        break;
                                    case INPUT_RECORD.KEY_EVENT:
                                        KeyEvent?.Invoke(record[0].KeyEvent);
                                        break;
                                    case INPUT_RECORD.WINDOW_BUFFER_SIZE_EVENT:
                                        WindowBufferSizeEvent?.Invoke(record[0].WindowBufferSizeEvent);
                                        break;
                                }
                            else
                            {
                                uint numWritten = 0;
                                WriteConsoleInput(handleIn, record, 1, ref numWritten);
                                return;
                            }
                        }
                    }).Start();
                }
            }

            public static void Stop() => Run = false;


            internal delegate void ConsoleMouseEvent(MOUSE_EVENT_RECORD r);

            internal delegate void ConsoleKeyEvent(KEY_EVENT_RECORD r);

            internal delegate void ConsoleWindowBufferSizeEvent(WINDOW_BUFFER_SIZE_RECORD r);
        }


        public static class NativeMethods
        {
            internal struct COORD
            {
                public short X;
                public short Y;

                public COORD(short x, short y)
                {
                    X = x;
                    Y = y;
                }
            }

            [StructLayout(LayoutKind.Explicit)]
            internal struct INPUT_RECORD
            {
                public const ushort KEY_EVENT = 0x0001,
                    MOUSE_EVENT = 0x0002,
                    WINDOW_BUFFER_SIZE_EVENT = 0x0004; //more

                [FieldOffset(0)]
                public ushort EventType;
                [FieldOffset(4)]
                public KEY_EVENT_RECORD KeyEvent;
                [FieldOffset(4)]
                public MOUSE_EVENT_RECORD MouseEvent;
                [FieldOffset(4)]
                public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
                /*
                and:
                 MENU_EVENT_RECORD MenuEvent;
                 FOCUS_EVENT_RECORD FocusEvent;
                 */
            }

#pragma warning disable CS0649
            internal struct MOUSE_EVENT_RECORD
            {
                public COORD dwMousePosition;

                public const uint FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001,
                    FROM_LEFT_2ND_BUTTON_PRESSED = 0x0004,
                    FROM_LEFT_3RD_BUTTON_PRESSED = 0x0008,
                    FROM_LEFT_4TH_BUTTON_PRESSED = 0x0010,
                    RIGHTMOST_BUTTON_PRESSED = 0x0002;
                public uint dwButtonState;

                public const int CAPSLOCK_ON = 0x0080,
                    ENHANCED_KEY = 0x0100,
                    LEFT_ALT_PRESSED = 0x0002,
                    LEFT_CTRL_PRESSED = 0x0008,
                    NUMLOCK_ON = 0x0020,
                    RIGHT_ALT_PRESSED = 0x0001,
                    RIGHT_CTRL_PRESSED = 0x0004,
                    SCROLLLOCK_ON = 0x0040,
                    SHIFT_PRESSED = 0x0010;
                public uint dwControlKeyState;

                public const int DOUBLE_CLICK = 0x0002,
                    MOUSE_HWHEELED = 0x0008,
                    MOUSE_MOVED = 0x0001,
                    MOUSE_WHEELED = 0x0004;
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

                public const int CAPSLOCK_ON = 0x0080,
                    ENHANCED_KEY = 0x0100,
                    LEFT_ALT_PRESSED = 0x0002,
                    LEFT_CTRL_PRESSED = 0x0008,
                    NUMLOCK_ON = 0x0020,
                    RIGHT_ALT_PRESSED = 0x0001,
                    RIGHT_CTRL_PRESSED = 0x0004,
                    SCROLLLOCK_ON = 0x0040,
                    SHIFT_PRESSED = 0x0010;
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


            internal const uint ENABLE_MOUSE_INPUT = 0x0010,
                ENABLE_QUICK_EDIT_MODE = 0x0040,
                ENABLE_EXTENDED_FLAGS = 0x0080,
                ENABLE_ECHO_INPUT = 0x0004,
                ENABLE_WINDOW_INPUT = 0x0008; //more

            [DllImportAttribute("kernel32.dll")]
            internal static extern bool GetConsoleMode(IntPtr hConsoleInput, ref uint lpMode);

            [DllImportAttribute("kernel32.dll")]
            internal static extern bool SetConsoleMode(IntPtr hConsoleInput, uint dwMode);


            [DllImportAttribute("kernel32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool ReadConsoleInput(IntPtr hConsoleInput, [Out] INPUT_RECORD[] lpBuffer, uint nLength, ref uint lpNumberOfEventsRead);

            [DllImportAttribute("kernel32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool WriteConsoleInput(IntPtr hConsoleInput, INPUT_RECORD[] lpBuffer, uint nLength, ref uint lpNumberOfEventsWritten);

        }
    }
}
