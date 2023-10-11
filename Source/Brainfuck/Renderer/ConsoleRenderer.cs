using System;
using System.Text;
using Win32;

#nullable enable

namespace LanguageCore.Brainfuck.Renderer
{
    internal class ConsoleRenderer : IDisposable
    {
        public static CharInfo NullCharacter => new()
        {
            Char = ' ',
            Background = ByteColor.Black,
            Foreground = ByteColor.Silver,
        };

        readonly Microsoft.Win32.SafeHandles.SafeFileHandle? ConsoleHandleW;
        readonly IntPtr ConsoleHandle;

        protected readonly short width;
        protected readonly short height;
        readonly CharInfo[] ConsoleBuffer;
        readonly SmallRect ConsoleRect;

        public virtual int Width => width;
        public virtual int Height => height;

        public ConsoleRenderer(int width, int height)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Kernel32.SetConsoleOutputCP(65001);
            Kernel32.SetConsoleCP(65001);
            Console.OutputEncoding = Encoding.Unicode;

            this.ConsoleHandle = Kernel32.GetStdHandle(StdHandle.STD_OUTPUT_HANDLE);
            this.ConsoleHandleW = null; // Kernel32.CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

            this.width = (short)width;
            this.height = (short)height;

            this.ConsoleBuffer = new CharInfo[width * height];
            this.ConsoleRect = new SmallRect() { Left = 0, Top = 0, Right = this.width, Bottom = this.height };
        }

        #region Public Methods

        public CharInfo this[int i]
        {
            get => ConsoleBuffer[i];
            set
            {
                if (i < 0 || i >= ConsoleBuffer.Length) return;
                ConsoleBuffer[i] = value;
            }
        }

        public CharInfo this[int x, int y]
        {
            get => this[(y * width) + x];
            set => this[(y * width) + x] = value;
        }

        public void DrawText(int x, int y, string text, byte foregroundColor = ByteColor.White, byte backgroundColor = ByteColor.Black)
        {
            if (x < 0 || y < 0) return;
            if (x >= width || y >= height) return;

            for (int offset = 0; offset < text.Length; offset++)
            {
                if (x + offset >= width) return;
                this[x + offset, y] = new CharInfo(text[offset], foregroundColor, backgroundColor);
            }
        }

        public void Dispose()
        {
            ConsoleHandleW?.Dispose();
        }

        #endregion

        #region Private Methods

        public void Clear()
        {
            for (int i = 0; i < ConsoleBuffer.Length; i++)
            { ConsoleBuffer[i] = new CharInfo(); }
        }

        public void RefreshConsole()
        {
            if (ConsoleHandleW != null && ConsoleHandleW.IsInvalid)
            {
                Console.WriteLine("Console handler is invalid");
                return;
            }

            WriteConsole();
        }

        void WriteConsole()
        {
            if (ConsoleHandleW != null && ConsoleHandleW.IsInvalid)
            {
                System.Diagnostics.Debug.Fail("Console handle is invalid");
                return;
            }
            if (ConsoleHandleW != null && ConsoleHandleW.IsClosed)
            {
                System.Diagnostics.Debug.Fail("Console handle is closed");
                return;
            }

            SmallRect rect = ConsoleRect;

            if (ConsoleHandleW != null)
            {
                _ = Kernel32.WriteConsoleOutputW(ConsoleHandleW, ConsoleBuffer,
                    new Coord(width, height),
                    new Coord(0, 0),
                    ref rect);
            }
            else
            {
                _ = Kernel32.WriteConsoleOutput(ConsoleHandle, ConsoleBuffer,
                    new Coord(width, height),
                    new Coord(0, 0),
                    ref rect);
            }
        }

        #endregion
    }
}
