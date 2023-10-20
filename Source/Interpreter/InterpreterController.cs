﻿using System;
using System.Collections.Generic;
using System.IO;
using LanguageCore.BBCode.Compiler;

namespace LanguageCore.Runtime
{
    /// <summary>
    /// This compiles and runs the code
    /// </summary>
    public class Interpreter
    {
        public enum InterpreterState
        {
            Initialized,
            Destroyed,
            Running,
            CodeExecuted,
        }

        protected abstract class Stream
        {
            internal int ID;
            internal int MemoryAddress;
            internal int BufferSize;

            public Stream(int id, int memoryAddress, int bufferSize)
            {
                ID = id;
                MemoryAddress = memoryAddress;
                BufferSize = bufferSize;
            }

            internal abstract void Dispose();

            internal abstract void Tick(IHeap heap);
        }

        protected class InputStream : Stream
        {
            internal int Length;

            internal System.IO.Stream SystemStream;

            internal bool SystemHasData => (SystemStream.Position >= SystemStream.Length);
            internal int RemainingBufferSize => (BufferSize - Length);

            public InputStream(int id, int memoryAddress, int bufferSize, System.IO.Stream stream)
                : base(id, memoryAddress, bufferSize)
            {
                SystemStream = stream ?? throw new ArgumentNullException(nameof(stream));
            }

            internal override void Dispose()
            {
                this.SystemStream?.Close();
                this.SystemStream?.Dispose();

                Debug.Log($"[STREAM {ID}]: Disposed");
            }

            internal void ClearBuffer()
            {
                this.Length = 0;

                Debug.Log($"[STREAM {ID}]: Buffer cleared");
            }

            internal override void Tick(IHeap heap)
            {
                if (RemainingBufferSize == 0) return;
                if (SystemHasData) return;

                byte[] buffer = new byte[RemainingBufferSize];
                int readCount = SystemStream.Read(buffer, 0, RemainingBufferSize);

                for (int i = 0; i < readCount; i++)
                {
                    heap[i + MemoryAddress] = new DataItem(buffer[i]);
                }
                Length += readCount;

                Debug.Log($"[STREAM {ID}]: (AUTO) Read {readCount} bytes");
            }
        }

        protected class OutputStream : Stream
        {
            internal int Pointer;

            internal System.IO.Stream SystemStream;

            public OutputStream(int id, int memoryAddress, int bufferSize, System.IO.Stream stream)
                : base(id, memoryAddress, bufferSize)
            {
                SystemStream = stream ?? throw new ArgumentNullException(nameof(stream));
            }

            internal override void Dispose()
            {
                this.SystemStream?.Close();
                this.SystemStream?.Dispose();

                Debug.Log($"[STREAM {ID}]: Disposed");
            }

            internal void Flush(byte[] buffer)
            {
                this.Pointer = 0;

                this.SystemStream.Write(buffer, 0, buffer.Length);
                this.SystemStream.Flush();

                Debug.Log($"[STREAM {ID}]: Write {buffer.Length} bytes");
            }

            internal override void Tick(IHeap heap) { }
        }

        public delegate void OnOutputEventHandler(Interpreter sender, string message, LogType logType);
        public delegate void OnStdErrorEventHandler(Interpreter sender, string data);
        public delegate void OnStdOutEventHandler(Interpreter sender, string data);
        public delegate void OnInputEventHandler(Interpreter sender);
        public delegate void OnExecutedEventHandler(Interpreter sender, OnExecutedEventArgs e);

        public event OnOutputEventHandler OnOutput;
        public event OnStdOutEventHandler OnStdOut;
        public event OnStdErrorEventHandler OnStdError;
        /// <summary>
        /// Will be invoked when the code needs input<br/>
        /// Call <see cref="OnInput(char)"/> after this invoked
        /// </summary>
        public event OnInputEventHandler OnNeedInput;
        public event OnExecutedEventHandler OnExecuted;

        public struct OnExecutedEventArgs
        {
            public double ElapsedMilliseconds;

            public OnExecutedEventArgs(double elapsedMilliseconds)
            {
                ElapsedMilliseconds = elapsedMilliseconds;
            }

            public override readonly string ToString() => $"Code executed in {ElapsedTime}";
            public readonly string ElapsedTime => LanguageCore.Utils.GetElapsedTime(ElapsedMilliseconds);
        }

        protected readonly Dictionary<string, ExternalFunctionBase> externalFunctions = new();

        public CodeGenerator.Result CompilerResult;
        public Instruction NextInstruction
        {
            get
            {
                if (this.BytecodeInterpreter == null) return null;
                if (this.BytecodeInterpreter.CodePointer < 0 || this.BytecodeInterpreter.CodePointer >= this.CompilerResult.Code.Length) return null;
                return this.CompilerResult.Code[this.BytecodeInterpreter.CodePointer];
            }
        }

        public InterpreterState State;

        public bool IsExecutingCode;

        public BytecodeInterpreter BytecodeInterpreter;
        protected TimeSpan CodeStartedTimespan;

        protected bool IsPaused;
        IReturnValueConsumer ReturnValueConsumer;

        protected List<Stream> Streams;

        protected bool HandleErrors = true;

        /// <summary>
        /// It prepares the interpreter to run some code
        /// </summary>
        public virtual void ExecuteProgram(Instruction[] program, BytecodeInterpreterSettings settings)
        {
            CodeStartedTimespan = DateTime.Now.TimeOfDay;
            BytecodeInterpreter = new BytecodeInterpreter(program, externalFunctions, settings);
            externalFunctions.SetInterpreter(BytecodeInterpreter);

            State = InterpreterState.Initialized;

            OnOutput?.Invoke(this, "Start code ...", LogType.Debug);
        }

        /// <summary>
        /// Initializes the compiler
        /// <list type="bullet">
        /// <item>It checks if any code is running</item>
        /// <item>Adds external functions</item>
        /// </list>
        /// </summary>
        /// <returns>
        /// True, if you can run some code
        /// </returns>
        public bool Initialize()
        {
            if (IsExecutingCode)
            {
                OnOutput?.Invoke(this, "Can't run the program: currently running another", LogType.Warning);
                return false;
            }
            IsExecutingCode = true;

            if (Streams == null)
            { Streams = new List<Stream>(); }
            else
            {
                for (int i = 0; i < Streams.Count; i++)
                { Streams[i]?.Dispose(); }
                Streams.Clear();
            }

            externalFunctions.Clear();
            externalFunctions.AddRange(GenerateExternalFunctions());

            State = InterpreterState.Initialized;

            return true;
        }

        public void Destroy()
        {
            IsExecutingCode = false;

            if (Streams != null)
            {
                for (int i = 0; i < Streams.Count; i++)
                {
                    Streams[i].Dispose();
                }
                Streams.Clear();
            }

            State = InterpreterState.Destroyed;
        }

        /// <summary>
        /// Provides input to the interpreter<br/>
        /// <lv>WARNING:</lv> Call it only after <see cref="OnNeedInput"/> invoked!
        /// </summary>
        /// <param name="key">
        /// The input value
        /// </param>
        public void OnInput(char key)
        {
            if (ReturnValueConsumer != null)
            {
                ReturnValueConsumer.Return(new DataItem(key));
                ReturnValueConsumer = null;
            }

            IsPaused = false;
        }

        public void LoadDLL(string path)
        {
            OnOutput?.Invoke(this, $"Load DLL \"{path}\" ...", LogType.Debug);
            System.Reflection.Assembly dll = System.Reflection.Assembly.LoadFile(path);
            System.Type[] exportedTypes = dll.GetExportedTypes();
            int functionsAdded = 0;

            foreach (System.Type type in exportedTypes)
            {
                System.Reflection.MethodInfo[] methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                foreach (System.Reflection.MethodInfo method in methods)
                {
                    ExternalFunctionSimple newFunction = externalFunctions.AddExternalFunction(method);
                    OnOutput?.Invoke(this, $" Added function {newFunction.ID}", LogType.Debug);
                    functionsAdded++;
                }
            }

            OnOutput?.Invoke(this, $"DLL loaded with {functionsAdded} functions", LogType.Debug);
        }

        public Dictionary<string, ExternalFunctionBase> GenerateExternalFunctions()
        {
            Dictionary<string, ExternalFunctionBase> externalFunctions = new();

            #region Console

            externalFunctions.AddManagedExternalFunction("stdin", Array.Empty<BBCode.Compiler.Type>(), (DataItem[] parameters, ExternalFunctionManaged function) =>
            {
                this.IsPaused = true;
                this.ReturnValueConsumer = function;
                if (this.OnNeedInput == null)
                {
                    this.OnOutput?.Invoke(this, "Event OnNeedInput does not have listeners", LogType.Warning);
                    this.OnInput('\0');
                }
                else
                {
                    this.OnNeedInput?.Invoke(this);
                }
            });

            externalFunctions.AddExternalFunction("stdout", (char @char) => OnStdOut?.Invoke(this, @char.ToString()));

            externalFunctions.AddExternalFunction("console-set",
                (char @char, int x, int y) =>
                {
                    if (x < 0 || y < 0) return;
                    var (lx, ly) = Console.GetCursorPosition();
                    Console.SetCursorPosition(x, y);
                    Console.Write(@char);
                    Console.SetCursorPosition(lx, ly);
                });

            externalFunctions.AddExternalFunction("console-clear", Console.Clear);

            externalFunctions.AddExternalFunction("stderr", (char @char) => OnStdError?.Invoke(this, @char.ToString()));

            externalFunctions.AddExternalFunction("sleep", (int t) => BytecodeInterpreter.SleepTime(t, null));

            #endregion

            #region Math

            externalFunctions.AddExternalFunction<float, float>("cos", MathF.Cos);
            externalFunctions.AddExternalFunction<float, float>("sin", MathF.Sin);

            #endregion

            #region Enviroment

            externalFunctions.AddExternalFunction("utc-time", () => (int)DateTime.UtcNow.TimeOfDay.TotalMilliseconds);
            externalFunctions.AddExternalFunction("local-time", () => (int)DateTime.Now.TimeOfDay.TotalMilliseconds);
            externalFunctions.AddExternalFunction("utc-date-day", () => (int)DateTime.Now.DayOfYear);
            externalFunctions.AddExternalFunction("local-date-day", () => (int)DateTime.Now.DayOfYear);
            externalFunctions.AddExternalFunction("utc-date-year", () => (int)DateTime.Now.Year);
            externalFunctions.AddExternalFunction("local-date-year", () => (int)DateTime.Now.Year);

            #endregion

            #region Casts

            externalFunctions.AddExternalFunction("float-to-int", (float v) => (int)v);
            externalFunctions.AddExternalFunction("int-to-float", (int v) => (float)v);

            #endregion

            #region Streams

            externalFunctions.AddExternalFunction("stream-c",
                (int bufferSize, int bufferMemoryAddress) =>
                {
                    int newID = 1;
                    while (true)
                    {
                        bool idIsUnique = true;
                        for (int i = 0; i < Streams.Count; i++)
                        {
                            if (Streams[i].ID == newID)
                            {
                                newID++;
                                idIsUnique = false;
                                break;
                            }
                        }
                        if (idIsUnique) break;
                    }

                    Stream newStream = new InputStream(newID, bufferMemoryAddress, bufferSize, System.IO.File.Open(@"C:\Users\bazsi\Desktop\test.txt", FileMode.OpenOrCreate));

                    Streams.Add(newStream);

                    return newID;
                });
            externalFunctions.AddExternalFunction("stream-d",
                (int id) =>
                {
                    for (int i = Streams.Count - 1; i >= 0; i--)
                    {
                        if (Streams[i].ID != id) continue;

                        Stream stream = Streams[i];
                        stream.Dispose();
                        Streams.RemoveAt(i);

                        return;
                    }

                    throw new RuntimeException($"Stream {id} not found");
                });
            externalFunctions.AddExternalFunction("stream-f",
                (int id, int count) =>
                {
                    for (int i = 0; i < Streams.Count; i++)
                    {
                        if (Streams[i].ID != id) continue;

                        if (Streams[i] is not OutputStream stream)
                        { throw new RuntimeException($"Stream {id} is not OutputStream"); }

                        byte[] buffer = new byte[count];
                        for (int j = 0; j < count; j++)
                        {
                            buffer[j] = ((IHeap)BytecodeInterpreter.Memory.Heap)[j + stream.MemoryAddress].Byte ?? 0;
                        }

                        stream.Flush(buffer);

                        return;
                    }

                    throw new RuntimeException($"Stream {id} not found");
                });
            externalFunctions.AddExternalFunction("stream-l",
                (int id) =>
                {
                    for (int i = 0; i < Streams.Count; i++)
                    {
                        if (Streams[i].ID != id) continue;

                        if (Streams[i] is not InputStream stream)
                        { throw new RuntimeException($"Stream {id} is not InputStream"); }

                        return stream.Length;
                    }

                    throw new RuntimeException($"Stream {id} not found");
                });
            externalFunctions.AddExternalFunction("stream-r",
                (int id) =>
                {
                    for (int i = 0; i < Streams.Count; i++)
                    {
                        if (Streams[i].ID != id) continue;

                        if (Streams[i] is not InputStream stream)
                        { throw new RuntimeException($"Stream {id} is not InputStream"); }

                        stream.ClearBuffer();

                        return;
                    }

                    throw new RuntimeException($"Stream {id} not found");
                });

            #endregion

            #region Win32

            externalFunctions.AddExternalFunction<int, string, string, uint, Windows.Win32.MessageBoxResult>("MessageBox", Windows.Win32.MessageBoxW);

            #endregion

            return externalFunctions;
        }

        protected void OnCodeExecuted()
        {
            var elapsedMilliseconds = (DateTime.Now.TimeOfDay - CodeStartedTimespan).TotalMilliseconds;
            OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds));
            IsExecutingCode = false;

            State = InterpreterState.CodeExecuted;

            for (int i = 0; i < Streams.Count; i++)
            { Streams[i].Dispose(); }
            Streams.Clear();
        }

        public void Update()
        {
            if (BytecodeInterpreter != null && BytecodeInterpreter.Memory.Heap != null)
            {
                for (int i = 0; i < Streams.Count; i++)
                { Streams[i].Tick(BytecodeInterpreter.Memory.Heap); }
            }

            if (!IsExecutingCode || IsPaused) return;

            try
            {
                bool didSomething = BytecodeInterpreter.Tick();
                if (didSomething) return;
                else OnCodeExecuted();
            }
            catch (UserException error)
            {
                error.FeedDebugInfo(CompilerResult.DebugInfo);

                OnOutput?.Invoke(this, "User Exception: " + error.Value.ToString() + "\r\n" + error.ToString(), LogType.Error);

                var elapsedMilliseconds = (DateTime.Now.TimeOfDay - CodeStartedTimespan).TotalMilliseconds;
                OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds));
                IsExecutingCode = false;

                if (!HandleErrors) throw;
            }
            catch (RuntimeException error)
            {
                error.FeedDebugInfo(CompilerResult.DebugInfo);

                OnOutput?.Invoke(this, "Runtime Exception: " + error.ToString(), LogType.Error);

                var elapsedMilliseconds = (DateTime.Now.TimeOfDay - CodeStartedTimespan).TotalMilliseconds;
                OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds));
                IsExecutingCode = false;

                if (!HandleErrors) throw;
            }
            catch (System.Exception error)
            {
                OnOutput?.Invoke(this, "Internal Exception: " + error.Message, LogType.Error);

                var elapsedMilliseconds = (DateTime.Now.TimeOfDay - CodeStartedTimespan).TotalMilliseconds;
                OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds));
                IsExecutingCode = false;

                if (!HandleErrors) throw;
            }
        }
    }
}