#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649

using System;
using System.Threading;
using Win32;

namespace ConsoleDrawer
{
    public static class ConsoleHandler
    {
        static uint SavedMode;
        static IntPtr StdinHandle = Kernel32.INVALID_HANDLE_VALUE;

        /// <exception cref="WindowsException"/>
        public static void Setup()
        {
            StdinHandle = Kernel32.GetStdHandle(StdHandle.STD_INPUT_HANDLE);

            if (StdinHandle == Kernel32.INVALID_HANDLE_VALUE)
            { throw WindowsException.Get(); }

            uint mode = 0;
            if (Kernel32.GetConsoleMode(StdinHandle, ref mode) == 0)
            { throw WindowsException.Get(); }

            SavedMode = mode;

            mode &= ~InputMode.ENABLE_QUICK_EDIT_MODE;
            mode |= InputMode.ENABLE_WINDOW_INPUT;
            mode |= InputMode.ENABLE_MOUSE_INPUT;

            if (Kernel32.SetConsoleMode(StdinHandle, mode) == 0)
            { throw WindowsException.Get(); }

            Console.CursorVisible = false;
        }

        /// <exception cref="WindowsException"/>
        /// <exception cref="Exception"/>
        public static void Restore()
        {
            Console.CursorVisible = true;

            if (StdinHandle == Kernel32.INVALID_HANDLE_VALUE)
            { throw new Exception($"Invalid handle"); }

            if (Kernel32.SetConsoleMode(StdinHandle, SavedMode) == 0)
            { throw WindowsException.Get(); }
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
            IntPtr handleIn = Kernel32.GetStdHandle(StdHandle.STD_INPUT_HANDLE);
            new Thread(() =>
            {
                while (true)
                {
                    uint numRead = 0;
                    InputEvent[] record = new InputEvent[1];
                    record[0] = new InputEvent();
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
}
