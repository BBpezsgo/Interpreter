using System.IO;
using System.Threading;

namespace LanguageCore.Brainfuck;

using Generator;

public delegate void OutputCallback(byte data);
public delegate byte InputCallback();

public abstract class InterpreterBase : IDisposable
{
    public const int MEMORY_SIZE = 1024;

    public byte[] Memory;

    protected OutputCallback OnOutput;
    protected InputCallback OnInput;

    protected int _codePointer;
    protected int _memoryPointer;

    protected MutableRange<int> _memoryPointerRange;

    protected bool _isPaused;
    bool _isDisposed;

    public int CodePointer => _codePointer;
    public int MemoryPointer => _memoryPointer;
    public bool IsPaused => _isPaused;

    public RuntimeContext CurrentContext => new(_memoryPointer, _codePointer);

    public static void OnDefaultOutput(byte data) => Console.Write(CharCode.GetChar(data));
    public static byte OnDefaultInput() => CharCode.GetByte(Console.ReadKey(true).KeyChar);

    public InterpreterBase(OutputCallback? onOutput = null, InputCallback? onInput = null)
    {
        Memory = new byte[MEMORY_SIZE];

        OnOutput = onOutput ?? OnDefaultOutput;
        OnInput = onInput ?? OnDefaultInput;

        _codePointer = 0;
        _memoryPointer = 0;
        _isPaused = false;
    }

    public abstract bool Step();

    public void Run()
    { while (Step()) ; }

    public void Run(int stepsBeforeSleep)
    {
        int step = 0;
        while (true)
        {
            if (!Step()) break;
            step++;
            if (step >= stepsBeforeSleep)
            {
                step = 0;
                Thread.Sleep(10);
            }
        }
    }

    public void Reset()
    {
        _codePointer = 0;
        _memoryPointer = 0;
        _memoryPointerRange = default;
        Array.Clear(Memory);
    }

    public byte[] GetRawHeap(BrainfuckGeneratorSettings settings)
    {
        int heapStart = BasicHeapCodeHelper.GetOffsettedStart(settings.HeapStart);
        // int heapEnd = brainfuckGeneratorSettings.HeapStart + brainfuckGeneratorSettings.HeapSize * BasicHeapCodeHelper.BLOCK_SIZE;

        byte[] result = new byte[(Memory.Length - heapStart) / BasicHeapCodeHelper.BLOCK_SIZE];

        for (int i = heapStart; i < Memory.Length; i += BasicHeapCodeHelper.BLOCK_SIZE)
        {
            // byte addressCarry = Memory[i + BasicHeapCodeHelper.OFFSET_ADDRESS_CARRY];
            // byte valueCarry = Memory[i + BasicHeapCodeHelper.OFFSET_VALUE_CARRY];
            byte data = Memory[i + BasicHeapCodeHelper.OFFSET_DATA];

            int heapAddress = (i - heapStart) / BasicHeapCodeHelper.BLOCK_SIZE;
            result[heapAddress] = data;
        }

        return result;
    }

    protected virtual void DisposeManaged() { }
    protected virtual void DisposeUnmanaged() { }
    void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        if (disposing)
        { DisposeManaged(); }
        DisposeUnmanaged();
        _isDisposed = true;
    }
    ~InterpreterBase() { Dispose(disposing: false); }
    void IDisposable.Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public abstract class InterpreterBase<TCode> : InterpreterBase
{
    protected TCode[] Code;

    public bool IsDone => _codePointer >= Code.Length || _codePointer < 0;

    public InterpreterBase(Uri uri, OutputCallback? onOutput = null, InputCallback? onInput = null)
        : this(onOutput, onInput)
        => LoadCode(uri);

    public InterpreterBase(FileInfo file, OutputCallback? onOutput = null, InputCallback? onInput = null)
        : this(onOutput, onInput)
        => LoadCode(file);

    public InterpreterBase(string code, OutputCallback? onOutput = null, InputCallback? onInput = null)
        : this(onOutput, onInput)
        => LoadCode(code);

    public InterpreterBase(OutputCallback? onOutput = null, InputCallback? onInput = null) : base(onOutput, onInput)
    {
        Code = Array.Empty<TCode>();
    }

    public void LoadCode(Uri uri)
    {
        using System.Net.Http.HttpClient client = new();
        client.GetStringAsync(uri).ContinueWith((code) =>
        {
            Code = ParseCode(code.Result);
            _codePointer = 0;
        }, System.Threading.Tasks.TaskScheduler.Default).Wait();
    }

    public void LoadCode(FileInfo file) => LoadCode(File.ReadAllText(file.FullName));

    public void LoadCode(string code)
    {
        Code = ParseCode(code);
        _codePointer = 0;
    }

    protected abstract TCode[] ParseCode(string code);

    public override bool Step()
    {
        if (IsDone) return false;

        Evaluate(Code[_codePointer]);

        _codePointer++;
        return !IsDone;
    }

    protected abstract void Evaluate(TCode instruction);
}
