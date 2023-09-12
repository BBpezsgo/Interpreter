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
using Win32;

namespace ConsoleDrawer
{
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
