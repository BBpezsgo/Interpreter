namespace LanguageCore.Brainfuck;

using Compiler;

public readonly struct StackAddress : IDisposable
{
    readonly StackCodeHelper Stack;
    readonly int Address;
    readonly bool IsSomething;

    public StackAddress(StackCodeHelper stack, int address)
    {
        Stack = stack;
        Address = address;
        IsSomething = true;
    }

    void IDisposable.Dispose()
    {
        if (!IsSomething) { return; }
        if (Stack.LastAddress != Address)
        { throw new InternalException(); }
        Stack.Pop();
    }

    public static implicit operator int(StackAddress address) => address.Address;
}

public class StackCodeHelper
{
    public static readonly int Direction = 1;

    /// <summary>
    /// Adds up all the stack element's size
    /// </summary>
    public int Size => _stack.Sum();
    public int Start { get; }
    public int MaxSize { get; }
    public int NextAddress => Start + (_stack.Sum() * Direction);
    public int LastAddress
    {
        get
        {
            if (_stack.Count == 0) return Start;
            return Start + (_stack.Sum() * Direction) - (_stack[^1] * Direction);
        }
    }
    public int MaxUsedSize { get; private set; }
    public bool WillOverflow { get; private set; }

    readonly CodeHelper _code;
    readonly List<int> _stack;

    public StackCodeHelper(CodeHelper code, int start, int size)
    {
        _code = code;
        _stack = new List<int>();
        Start = start;
        MaxSize = size;
    }

    public StackCodeHelper(CodeHelper code, StackCodeHelper other)
    {
        _code = code;
        _stack = new List<int>(other._stack);
        MaxUsedSize = other.MaxUsedSize;
        WillOverflow = other.WillOverflow;
        Start = other.Start;
        MaxSize = other.MaxSize;
    }

    public StackAddress GetTemporaryAddress(int size = 1) => PushVirtual(size);

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="InternalException"/>
    public StackAddress Push(CompiledValue v) => Push(CodeHelper.GetInteger(v));

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public StackAddress Push(int v)
    {
        StackAddress address = PushVirtual(1);

        _code.SetValue(address, v);
        return address;
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public StackAddress Push(char v)
    {
        StackAddress address = PushVirtual(1);

        _code.SetValue(address, v);
        return address;
    }

    /// <summary>
    /// <b>Pointer:</b> Last state or 0
    /// </summary>
    public int PopAndStore(int target)
    {
        int size = PopVirtual();
        int address = NextAddress;
        for (int offset = 0; offset < size; offset++)
        {
            int offsettedSource = address + offset;
            int offsettedTarget = target + offset;
            _code.MoveValue(offsettedSource, offsettedTarget);
        }
        return size;
    }

    /// <summary>
    /// <b>Pointer:</b> Last state or 0
    /// </summary>
    public int PopAndAdd(int target)
    {
        int size = PopVirtual();
        int address = NextAddress;
        for (int offset = 0; offset < size; offset++)
        {
            int offsettedSource = address + offset;
            int offsettedTarget = target + offset;
            _code.MoveAddValue(offsettedSource, offsettedTarget);
        }
        return size;
    }

    /// <summary>
    /// <b>Pointer:</b> Not modified
    /// </summary>
    public int Pop(Action<int> onAddress)
    {
        int size = PopVirtual();
        int address = NextAddress;
        for (int offset = 0; offset < size; offset++)
        {
            onAddress?.Invoke(address + (offset * Direction));
        }
        return size;
    }

    /// <summary>
    /// <b>Pointer:</b> Not modified or restored to the last state
    /// </summary>
    public void Pop()
    {
        int size = PopVirtual();
        int address = NextAddress;
        for (int offset = 0; offset < size; offset++)
        {
            _code.ClearValue(address + (offset * Direction));
        }
    }

    /// <inheritdoc cref="Pop()" />
    public void Pop(int count)
    {
        for (int i = 0; i < count; i++)
        { Pop(); }
    }

    public StackAddress PushVirtual(int size)
    {
        int address = NextAddress;

        if (Size >= MaxSize)
        {
            _code.CRASH(address, "Stack overflow");
            WillOverflow = true;
            // Debugger.Break();
        }

        _stack.Push(size);

        MaxUsedSize = Math.Max(MaxUsedSize, Size);

        return new StackAddress(this, address);
    }

    public int PopVirtual() => _stack.Pop();
}
