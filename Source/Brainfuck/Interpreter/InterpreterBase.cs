using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck;

public delegate void OutputCallback(byte data);
public delegate byte InputCallback();

public abstract partial class InterpreterBase
{
    public const int MEMORY_SIZE = 1024;

    public byte[] Memory;

    protected OutputCallback OnOutput;
    protected InputCallback OnInput;

    protected int _codePointer;
    protected int _memoryPointer;

    protected MutableRange<int> _memoryPointerRange;

    protected bool _isPaused;

    public DebugInformation? DebugInfo { get; set; }
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

    /// <exception cref="BrainfuckRuntimeException"/>
    public abstract bool Step();

    /// <exception cref="BrainfuckRuntimeException"/>
    public void Run()
    { while (Step()) ; }

    public void Reset()
    {
        _codePointer = 0;
        _memoryPointer = 0;
        _memoryPointerRange = default;
        Array.Clear(Memory);
    }
}

public abstract partial class InterpreterBase<TCode> : InterpreterBase
{
    protected ImmutableArray<TCode> Code;

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

    /// <exception cref="BrainfuckRuntimeException"/>
    public override bool Step()
    {
        if (IsDone) return false;

        Evaluate(Code[_codePointer]);

        _codePointer++;
        return !IsDone;
    }

    /// <exception cref="BrainfuckRuntimeException"/>
    protected abstract void Evaluate(TCode instruction);
}
