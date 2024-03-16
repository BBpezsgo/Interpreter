using System.Collections.Frozen;
using System.IO;

namespace LanguageCore.Runtime;

using BBCode.Generator;

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
        public int ID;
        public int MemoryAddress;
        public int BufferSize;

        protected Stream(int id, int memoryAddress, int bufferSize)
        {
            ID = id;
            MemoryAddress = memoryAddress;
            BufferSize = bufferSize;
        }

        public abstract void Dispose();

        public abstract void Tick(HEAP heap);
    }

    protected class InputStream : Stream
    {
        public int Length;

        public System.IO.Stream SystemStream;

        public bool SystemHasData => SystemStream.Position >= SystemStream.Length;
        public int RemainingBufferSize => BufferSize - Length;

        public InputStream(int id, int memoryAddress, int bufferSize, System.IO.Stream stream)
            : base(id, memoryAddress, bufferSize)
        {
            SystemStream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public override void Dispose()
        {
            this.SystemStream?.Close();
            this.SystemStream?.Dispose();

            System.Diagnostics.Debug.WriteLine($"[STREAM {ID}]: Disposed");
        }

        public void ClearBuffer()
        {
            this.Length = 0;

            System.Diagnostics.Debug.WriteLine($"[STREAM {ID}]: Buffer cleared");
        }

        public override void Tick(HEAP heap)
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

            System.Diagnostics.Debug.WriteLine($"[STREAM {ID}]: (AUTO) Read {readCount} bytes");
        }
    }

    protected class OutputStream : Stream
    {
        public int Pointer;

        public System.IO.Stream SystemStream;

        public OutputStream(int id, int memoryAddress, int bufferSize, System.IO.Stream stream)
            : base(id, memoryAddress, bufferSize)
        {
            SystemStream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public override void Dispose()
        {
            this.SystemStream?.Close();
            this.SystemStream?.Dispose();

            System.Diagnostics.Debug.WriteLine($"[STREAM {ID}]: Disposed");
        }

        public void Flush(byte[] buffer)
        {
            this.Pointer = 0;

            this.SystemStream.Write(buffer, 0, buffer.Length);
            this.SystemStream.Flush();

            System.Diagnostics.Debug.WriteLine($"[STREAM {ID}]: Write {buffer.Length} bytes");
        }

        public override void Tick(HEAP heap) { }
    }

    public delegate void OnOutputEventHandler(Interpreter sender, string message, LogType logType);
    public delegate void OnStdErrorEventHandler(Interpreter sender, char data);
    public delegate void OnStdOutEventHandler(Interpreter sender, char data);
    public delegate void OnInputEventHandler(Interpreter sender);
    public delegate void OnExecutedEventHandler(Interpreter sender);

    public event OnOutputEventHandler? OnOutput;
    public event OnStdOutEventHandler? OnStdOut;
    public event OnStdErrorEventHandler? OnStdError;
    /// <summary>
    /// Will be invoked when the code needs input<br/>
    /// Call <see cref="OnInput(char)"/> after this invoked
    /// </summary>
    public event OnInputEventHandler? OnNeedInput;
    public event OnExecutedEventHandler? OnExecuted;

    public BBCodeGeneratorResult CompilerResult;
    public Instruction? NextInstruction
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

    public BytecodeInterpreter? BytecodeInterpreter;

    protected bool IsPaused;
    ExternalFunctionManaged? ReturnValueConsumer;

    protected List<Stream> Streams;

    protected bool HandleErrors;

    public Interpreter()
    {
        Streams = new List<Stream>();
        HandleErrors = true;
    }

    [MemberNotNullWhen(true, nameof(BytecodeInterpreter))]
    public bool Initialize(ImmutableArray<Instruction> program, BytecodeInterpreterSettings settings, Dictionary<string, ExternalFunctionBase> externalFunctions)
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

        BytecodeInterpreter = new BytecodeInterpreter(program, externalFunctions.ToFrozenDictionary(), settings);
        externalFunctions.SetInterpreter(BytecodeInterpreter);

        State = InterpreterState.Initialized;

        return true;
    }

    public void Dispose()
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
            ReturnValueConsumer.OnReturn?.Invoke(new DataItem(key));
            ReturnValueConsumer = null;
        }

        IsPaused = false;
    }

    public void GenerateExternalFunctions(Dictionary<string, ExternalFunctionBase> externalFunctions)
    {
        #region Console

        externalFunctions.AddManagedExternalFunction("stdin", Array.Empty<RuntimeType>(), (DataItem[] parameters, ExternalFunctionManaged function) =>
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

        externalFunctions.AddExternalFunction("stdout", (char @char) => OnStdOut?.Invoke(this, @char));

        externalFunctions.AddExternalFunction("console-set",
            (char @char, int x, int y) =>
            {
                if (x < 0 || y < 0) return;
                (int lx, int ly) = Console.GetCursorPosition();
                Console.SetCursorPosition(x, y);
                Console.Write(@char);
                Console.SetCursorPosition(lx, ly);
            });

        externalFunctions.AddExternalFunction("console-clear", Console.Clear);

        externalFunctions.AddExternalFunction("stderr", (char @char) => OnStdError?.Invoke(this, @char));

        externalFunctions.AddExternalFunction("sleep", (int t) => BytecodeInterpreter!.SleepTime(t, null));

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
                        buffer[j] = BytecodeInterpreter!.Memory.Heap[j + stream.MemoryAddress].Byte ?? 0;
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
    }

    protected void OnCodeExecuted()
    {
        OnExecuted?.Invoke(this);
        IsExecutingCode = false;

        State = InterpreterState.CodeExecuted;

        if (Streams != null)
        {
            for (int i = 0; i < Streams.Count; i++)
            { Streams[i].Dispose(); }
            Streams.Clear();
        }
    }

    public void Update()
    {
        if (BytecodeInterpreter != null && Streams != null)
        {
            for (int i = 0; i < Streams.Count; i++)
            { Streams[i].Tick(BytecodeInterpreter.Memory.Heap); }
        }

        if (!IsExecutingCode || IsPaused) return;

        try
        {
            bool didSomething = BytecodeInterpreter?.Tick() ?? false;
            if (didSomething) return;
            else OnCodeExecuted();
        }
        catch (UserException error)
        {
            error.FeedDebugInfo(CompilerResult.DebugInfo);

            OnOutput?.Invoke(this, $"User Exception: {error}", LogType.Error);

            OnExecuted?.Invoke(this);
            IsExecutingCode = false;

            if (!HandleErrors) throw;
        }
        catch (RuntimeException error)
        {
            error.FeedDebugInfo(CompilerResult.DebugInfo);

            OnOutput?.Invoke(this, $"Runtime Exception: {error}", LogType.Error);

            OnExecuted?.Invoke(this);
            IsExecutingCode = false;

            if (!HandleErrors) throw;
        }
        catch (Exception error)
        {
            OnOutput?.Invoke(this, $"Internal Exception: {error.Message}", LogType.Error);

            OnExecuted?.Invoke(this);
            IsExecutingCode = false;

            if (!HandleErrors) throw;
        }
    }
}
