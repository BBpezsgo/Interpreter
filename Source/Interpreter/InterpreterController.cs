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

        public abstract void Tick(in ArraySegment<DataItem> memory);
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

            Debug.WriteLine($"[STREAM {ID}]: Disposed");
        }

        public void ClearBuffer()
        {
            this.Length = 0;

            Debug.WriteLine($"[STREAM {ID}]: Buffer cleared");
        }

        public override void Tick(in ArraySegment<DataItem> memory)
        {
            if (RemainingBufferSize == 0) return;
            if (SystemHasData) return;

            byte[] buffer = new byte[RemainingBufferSize];
            int readCount = SystemStream.Read(buffer, 0, RemainingBufferSize);

            for (int i = 0; i < readCount; i++)
            { memory[i + MemoryAddress] = new DataItem(buffer[i]); }
            Length += readCount;

            Debug.WriteLine($"[STREAM {ID}]: (AUTO) Read {readCount} bytes");
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

            Debug.WriteLine($"[STREAM {ID}]: Disposed");
        }

        public void Flush(byte[] buffer)
        {
            this.Pointer = 0;

            this.SystemStream.Write(buffer, 0, buffer.Length);
            this.SystemStream.Flush();

            Debug.WriteLine($"[STREAM {ID}]: Write {buffer.Length} bytes");
        }

        public override void Tick(in ArraySegment<DataItem> memory) { }
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

    public readonly DebugInformation? DebugInformation;
    public Instruction? NextInstruction
    {
        get
        {
            if (BytecodeInterpreter.Registers.CodePointer < 0 || BytecodeInterpreter.Registers.CodePointer >= BytecodeInterpreter.Code.Length) return null;
            return BytecodeInterpreter.Code[BytecodeInterpreter.Registers.CodePointer];
        }
    }

    public readonly BytecodeProcessor BytecodeInterpreter;

    protected bool IsPaused;
    ExternalFunctionManaged? ReturnValueConsumer;

    protected readonly List<Stream> Streams;

    readonly bool ThrowExceptions;

    public Interpreter(bool handleErrors, BytecodeInterpreterSettings settings, ImmutableArray<Instruction> program, DebugInformation? debugInformation)
    {
        Streams = new List<Stream>();
        ThrowExceptions = !handleErrors;
        DebugInformation = debugInformation;

        BytecodeInterpreter = new BytecodeProcessor(program, GenerateExternalFunctions().ToFrozenDictionary(), settings);
    }

    public void Dispose()
    {
        foreach (Stream stream in Streams)
        { stream.Dispose(); }
        Streams.Clear();
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

    Dictionary<int, ExternalFunctionBase> GenerateExternalFunctions()
    {
        Dictionary<int, ExternalFunctionBase> externalFunctions = new();

        #region Console

        externalFunctions.AddManagedExternalFunction(ExternalFunctionNames.StdIn, Array.Empty<RuntimeType>(), (DataItem[] parameters, ExternalFunctionManaged function) =>
        {
            this.IsPaused = true;
            this.ReturnValueConsumer = function;
            if (this.OnNeedInput == null)
            {
                this.OnOutput?.Invoke(this, $"Event {OnNeedInput} does not have listeners", LogType.Warning);
                this.OnInput('\0');
            }
            else
            {
                this.OnNeedInput?.Invoke(this);
            }
        });

        externalFunctions.AddExternalFunction(ExternalFunctionNames.StdOut, (char @char) => OnStdOut?.Invoke(this, @char));

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

        externalFunctions.AddExternalFunction("sleep", (int t) => BytecodeInterpreter! /* This can't be null */ .Sleep(new TimeSleep(t)));

        #endregion

        #region Streams

        externalFunctions.AddExternalFunction("stream-create",
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
        externalFunctions.AddExternalFunction("stream-dispose",
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
        externalFunctions.AddExternalFunction("stream-flush",
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
                        buffer[j] = BytecodeInterpreter! /* This can't be null */ .Memory[j + stream.MemoryAddress].Byte ?? 0;
                    }

                    stream.Flush(buffer);

                    return;
                }

                throw new RuntimeException($"Stream {id} not found");
            });
        externalFunctions.AddExternalFunction("stream-length",
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
        externalFunctions.AddExternalFunction("stream-clear",
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

        AddStaticExternalFunctions(externalFunctions);

        return externalFunctions;
    }

    public static Dictionary<int, ExternalFunctionBase> GetExternalFunctions()
    {
        Dictionary<int, ExternalFunctionBase> externalFunctions = new();

        AddRuntimeExternalFunctions(externalFunctions);

        AddStaticExternalFunctions(externalFunctions);

        return externalFunctions;
    }

    static void AddRuntimeExternalFunctions(Dictionary<int, ExternalFunctionBase> externalFunctions)
    {
        #region Console

        externalFunctions.AddManagedExternalFunction(ExternalFunctionNames.StdIn, Array.Empty<RuntimeType>(), (DataItem[] parameters, ExternalFunctionManaged function) => { });
        externalFunctions.AddExternalFunction(ExternalFunctionNames.StdOut, (char @char) => { });
        externalFunctions.AddExternalFunction("console-set", (char @char, int x, int y) => { });
        externalFunctions.AddExternalFunction("console-clear", () => { });
        externalFunctions.AddExternalFunction("stderr", (char @char) => { });
        externalFunctions.AddExternalFunction("sleep", (int t) => { });

        #endregion

        #region Streams

        externalFunctions.AddExternalFunction("stream-create", (int bufferSize, int bufferMemoryAddress) => 0);
        externalFunctions.AddExternalFunction("stream-dispose", (int id) => { });
        externalFunctions.AddExternalFunction("stream-flush", (int id, int count) => { });
        externalFunctions.AddExternalFunction("stream-length", (int id) => 0);
        externalFunctions.AddExternalFunction("stream-clear", (int id) => { });

        #endregion
    }

    static void AddStaticExternalFunctions(Dictionary<int, ExternalFunctionBase> externalFunctions)
    {
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
    }

    /// <exception cref="UserException"/>
    /// <exception cref="RuntimeException"/>
    /// <exception cref="Exception"/>
    public void Update()
    {
        for (int i = 0; i < Streams.Count; i++)
        { Streams[i].Tick(BytecodeInterpreter.Memory); }

        if (BytecodeInterpreter.IsDone || IsPaused) return;

        try
        {
            if (!BytecodeInterpreter.Tick())
            {
                for (int i = 0; i < Streams.Count; i++)
                { Streams[i].Dispose(); }
                Streams.Clear();
            }
        }
        catch (UserException error)
        {
            if (DebugInformation is not null) error.FeedDebugInfo(DebugInformation);

            OnOutput?.Invoke(this, $"User Exception: {error}", LogType.Error);

            if (ThrowExceptions) throw;
            else BytecodeInterpreter.Registers.CodePointer = BytecodeInterpreter.Code.Length;
        }
        catch (RuntimeException error)
        {
            if (DebugInformation is not null) error.FeedDebugInfo(DebugInformation);

            OnOutput?.Invoke(this, $"Runtime Exception: {error}", LogType.Error);

            if (ThrowExceptions) throw;
            else BytecodeInterpreter.Registers.CodePointer = BytecodeInterpreter.Code.Length;
        }
        catch (Exception error)
        {
            OnOutput?.Invoke(this, $"Internal Exception: {error.Message}", LogType.Error);

            if (ThrowExceptions) throw;
            else BytecodeInterpreter.Registers.CodePointer = BytecodeInterpreter.Code.Length;
        }
    }
}
