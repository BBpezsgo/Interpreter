using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LanguageCore.Brainfuck;

using Generator;
using Runtime;

public delegate void OutputCallback(byte data);
public delegate byte InputCallback();

public abstract partial class InterpreterBase : IDisposable
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

    public DebugInformation? DebugInfo { get; set; }
    public int CodePointer => _codePointer;
    public int MemoryPointer => _memoryPointer;
    public bool IsPaused => _isPaused;
    public RuntimeContext CurrentContext => new(_memoryPointer, _codePointer);

    public static void OnDefaultOutput(byte data) => Console.Write(CharCode.GetChar(data));
    public static byte OnDefaultInput() => CharCode.GetByte(Console.ReadKey(true).KeyChar);

    protected InterpreterBase(OutputCallback? onOutput = null, InputCallback? onInput = null)
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
        while (Step())
        {
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
        int heapStart = HeapCodeHelper.GetOffsettedStart(settings.HeapStart);
        // int heapEnd = brainfuckGeneratorSettings.HeapStart + brainfuckGeneratorSettings.HeapSize * BasicHeapCodeHelper.BLOCK_SIZE;

        byte[] result = new byte[(Memory.Length - heapStart) / HeapCodeHelper.BlockSize];

        for (int i = heapStart; i < Memory.Length; i += HeapCodeHelper.BlockSize)
        {
            // byte addressCarry = Memory[i + BasicHeapCodeHelper.OFFSET_ADDRESS_CARRY];
            // byte valueCarry = Memory[i + BasicHeapCodeHelper.OFFSET_VALUE_CARRY];
            byte data = Memory[i + HeapCodeHelper.DataOffset];

            int heapAddress = (i - heapStart) / HeapCodeHelper.BlockSize;
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

public abstract partial class InterpreterBase<TCode> : InterpreterBase
{
    protected TCode[] Code;

    public bool IsDone => _codePointer >= Code.Length || _codePointer < 0;

    protected InterpreterBase(OutputCallback? onOutput = null, InputCallback? onInput = null) : base(onOutput, onInput)
    {
        Code = Array.Empty<TCode>();
    }

    public InterpreterBase<TCode> LoadCode(Uri uri, bool showProgress, DebugInformation? debugInfo)
    {
        using System.Net.Http.HttpClient client = new();
        Task<string> task = client.GetStringAsync(uri);
        task.Wait();
        if (task.IsFaulted)
        { throw task.Exception; }
        Code = ParseCode(task.Result, showProgress, debugInfo);
        _codePointer = 0;
        return this;
    }

    public async Task LoadCodeAsync(Uri uri, bool showProgress, DebugInformation? debugInfo)
    {
        using System.Net.Http.HttpClient client = new();
        string code = await client.GetStringAsync(uri);
        Code = ParseCode(code, showProgress, debugInfo);
        _codePointer = 0;
    }

    public InterpreterBase<TCode> LoadCode(FileInfo file, bool showProgress, DebugInformation? debugInfo) => LoadCode(File.ReadAllText(file.FullName), showProgress, debugInfo);

    public InterpreterBase<TCode> LoadCode(string code, bool showProgress, DebugInformation? debugInfo) => LoadCode(ParseCode(code, showProgress, debugInfo));

    public InterpreterBase<TCode> LoadCode(TCode[] code)
    {
        Code = code;
        _codePointer = 0;
        return this;
    }

    protected abstract TCode[] ParseCode(string code, bool showProgress, DebugInformation? debugInfo);

    public override bool Step()
    {
        if (IsDone) return false;

        Evaluate(Code[_codePointer]);

        _codePointer++;
        return !IsDone;
    }

    protected abstract void Evaluate(TCode instruction);
}
