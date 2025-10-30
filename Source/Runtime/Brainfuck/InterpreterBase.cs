using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck;

public delegate void OutputCallback(byte data);
public delegate byte InputCallback();

public abstract class InterpreterBase
{
    public const int MEMORY_SIZE = 1024;

    public byte[] Memory;

    protected OutputCallback OnOutput;
    protected InputCallback OnInput;

    protected int _codePointer;
    protected int _memoryPointer;

    protected MutableRange<int> _memoryPointerRange;

    protected bool _isPaused;

    public CompiledDebugInformation DebugInfo { get; set; }
    public int CodePointer => _codePointer;
    public int MemoryPointer => _memoryPointer;
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

    public void Reset()
    {
        _codePointer = 0;
        _memoryPointer = 0;
        _memoryPointerRange = default;
#if NET_STANDARD
        Array.Clear(Memory, 0, Memory.Length);
#else
        Array.Clear(Memory);
#endif
    }
}

public abstract class InterpreterBase<TCode> : InterpreterBase
{
    public ImmutableArray<TCode> Code;

    public bool IsDone => _codePointer >= Code.Length || _codePointer < 0;

    protected InterpreterBase(OutputCallback? onOutput = null, InputCallback? onInput = null) : base(onOutput, onInput)
    {
        Code = ImmutableArray<TCode>.Empty;
    }

    public InterpreterBase<TCode> LoadCode(string code, bool showProgress, DebugInformation? debugInfo)
        => LoadCode(ParseCode(code, showProgress, debugInfo));
    public InterpreterBase<TCode> LoadCode(TCode[] code)
        => LoadCode(ImmutableArray.Create(code));
    public InterpreterBase<TCode> LoadCode(ImmutableArray<TCode> code)
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
