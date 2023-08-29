using System;
using System.Threading;

#nullable enable

namespace ProgrammingLanguage.Brainfuck.Renderer
{
    internal class ConsoleListener : IDisposable
    {
        public event ConsoleEvent<MouseEvent>? MouseEvent;
        public event ConsoleEvent<KeyEvent>? KeyEvent;
        public event ConsoleEvent<WindowBufferSizeEvent>? WindowBufferSizeEvent;

        bool Run;
        readonly IntPtr ConsoleHandle;
        readonly Thread Thread;

        public ConsoleListener()
        {
            this.Run = true;
            this.ConsoleHandle = Kernel32.GetStdHandle(Kernel32.STD_INPUT_HANDLE);
            this.Thread = new Thread(ThreadProcess);
            this.Thread.Start();
        }

        void ThreadProcess()
        {
            while (true)
            {
                uint numRead = 0;
                InputEvent[] record = new InputEvent[1];
                record[0] = new InputEvent();
                Kernel32.ReadConsoleInput(this.ConsoleHandle, record, 1, ref numRead);
                if (this.Run)
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
                    Kernel32.WriteConsoleInput(this.ConsoleHandle, record, 1, ref numWritten);
                    Console.CursorVisible = true;
                    return;
                }
            }
        }

        public void Dispose()
        {
            this.Run = false;
        }
    }

    internal delegate void ConsoleEvent<T>(T e);
}
