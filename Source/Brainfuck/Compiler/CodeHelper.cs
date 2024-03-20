namespace LanguageCore.Brainfuck;

using Ansi = Win32.Console.Ansi;

public interface IBrainfuckGenerator
{
    public CompiledCode Code { get; }
}

public readonly struct AutoPrintCodeString
{
    readonly StringBuilder v;

    AutoPrintCodeString(StringBuilder v)
    {
        this.v = v;
    }

    public static implicit operator StringBuilder(AutoPrintCodeString v) => v.v;
    public static implicit operator AutoPrintCodeString(StringBuilder v) => new(v);

    public static AutoPrintCodeString operator +(AutoPrintCodeString a, char b)
    {
        a.v.Append(b);
        BrainfuckCode.PrintCodeChar(b);
        return a;
    }
    public static AutoPrintCodeString operator +(AutoPrintCodeString a, string b)
    {
        a.v.Append(b);
        BrainfuckCode.PrintCode(b);
        return a;
    }
    public static AutoPrintCodeString operator +(AutoPrintCodeString a, AutoPrintCodeString b)
    {
        a.v.Append(b.v);
        return a;
    }

    public void Append(string value)
    {
        this.v.Append(value);
        BrainfuckCode.PrintCode(value);
    }

    public void Append(char value)
    {
        this.v.Append(value);
        BrainfuckCode.PrintCodeChar(value);
    }

    public void Append(char value, int repeatCount)
    {
        this.v.Append(value, repeatCount);
        BrainfuckCode.PrintCode(new string(value, repeatCount));
    }

    public override string ToString() => v.ToString();
}

public readonly struct AutoJumpBlock : IDisposable
{
    readonly IBrainfuckGenerator Generator;
    readonly int ConditionAddress;
    readonly bool ClearCondition;

    public AutoJumpBlock(IBrainfuckGenerator generator, int conditionAddress, bool clearCondition)
    {
        Generator = generator;
        ConditionAddress = conditionAddress;
        ClearCondition = clearCondition;
    }

    public void Dispose()
    {
        Generator.Code.JumpEnd(ConditionAddress, ClearCondition);
    }
}

public readonly struct AutoCodeBlock : IDisposable
{
    readonly IBrainfuckGenerator Generator;

    public AutoCodeBlock(IBrainfuckGenerator generator)
    {
        Generator = generator;
    }

    public void Dispose()
    {
        Generator.Code.EndBlock();
    }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class CompiledCode : IDuplicatable<CompiledCode>
{
    const int HalfByte = byte.MaxValue / 2;
    const int InitialSize = 1024;

    public int Pointer => _pointer;
    public int BranchDepth => _branchDepth;
    public int Length => _code.Length;

    StringBuilder _code;
    int _indent;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] int _pointer;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] int _branchDepth;

    public CompiledCode()
    {
        this._code = new StringBuilder(InitialSize);
        this._indent = 0;
        this._pointer = 0;
        this._branchDepth = 0;
    }

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="InternalException"/>
    public static int GetInteger(Runtime.DataItem v)
    {
        return v.Type switch
        {
            Runtime.RuntimeType.Byte => v.VByte,
            Runtime.RuntimeType.Integer => v.VInt,
            Runtime.RuntimeType.Char => CharCode.GetByte(v.VChar),

            Runtime.RuntimeType.Single => throw new NotImplementedException("Floats not supported by brainfuck :("),
            Runtime.RuntimeType.Null => throw new InternalException(),
            _ => throw new UnreachableException(),
        };
    }

    #region Comments

    public int Indent(int indent)
    {
        this._indent += indent;
        return this._indent;
    }

    public void LineBreak()
    {
        _code.Append("\r\n");
        _code.Append(' ', _indent);
    }

    public void CommentLine(string text)
    {
        LineBreak();
        _code.Append(BrainfuckCode.ReplaceCodes(text, '_'));
        LineBreak();
    }

    public void StartBlock()
    {
        LineBreak();
        _code.Append('{');
        this._indent += 2;
        LineBreak();
    }

    public void StartBlock(string label)
    {
        LineBreak();
        this._code.Append(BrainfuckCode.ReplaceCodes(label, '_'));
        this._code.Append(' ');
        this._code.Append('{');
        this._indent += 2;
        LineBreak();
    }

    public void EndBlock()
    {
        this._indent -= 2;
        LineBreak();
        _code.Append('}');
        LineBreak();
    }

    public AutoCodeBlock Block(IBrainfuckGenerator generator)
    {
        StartBlock();
        return new AutoCodeBlock(generator);
    }

    public AutoCodeBlock Block(IBrainfuckGenerator generator, string label)
    {
        StartBlock(label);
        return new AutoCodeBlock(generator);
    }

    #endregion

    /// <summary>
    /// <para>
    /// <b>Pointer:</b>
    /// <paramref name="conditionAddress"/>
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="conditionAddress"/>[ ... <paramref name="conditionAddress"/>]
    /// </code>
    /// </para>
    /// </summary>
    public AutoJumpBlock LoopBlock(IBrainfuckGenerator generator, int conditionAddress)
    {
        JumpStart(conditionAddress);
        return new AutoJumpBlock(generator, conditionAddress, false);
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b>
    /// <paramref name="conditionAddress"/>
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="conditionAddress"/>[ ... <paramref name="conditionAddress"/>[-]]
    /// </code>
    /// </para>
    /// </summary>
    public AutoJumpBlock ConditionalBlock(IBrainfuckGenerator generator, int conditionAddress)
    {
        JumpStart(conditionAddress);
        return new AutoJumpBlock(generator, conditionAddress, true);
    }

    /// <summary>
    /// <b>Requires 1 more cell to the right of the <paramref name="target"/>!</b><br/>
    /// <b>Pointer:</b> <paramref name="target"/> + 1
    /// </summary>
    public void CopyValue(int source, int target)
        => CopyValueWithTemp(source, target + 1, target);
    /// <summary>
    /// <b>Requires 1 more cell to the right of the <paramref name="targets"/>!</b><br/>
    /// <b>Pointer:</b> The last target + 1 or not modified
    /// </summary>
    public void CopyValue(int source, params int[] targets)
    {
        if (targets.Length == 0) return;
        for (int i = 0; i < targets.Length; i++)
        { CopyValue(source, targets[i]); }
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="tempAddress"/>
    /// </summary>
    public void CopyValueWithTemp(int source, int tempAddress, int target)
    {
        StartBlock($"CopyValueWithTemp({source}; {tempAddress}; {target})");
        MoveValue(source, target, tempAddress);
        MoveAddValue(tempAddress, source);
        EndBlock();
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="tempAddress"/> or not modified
    /// </summary>
    public void CopyValueWithTemp(int source, int tempAddress, params int[] targets)
    {
        if (targets.Length == 0) return;
        for (int i = 0; i < targets.Length; i++)
        { CopyValueWithTemp(source, tempAddress, targets[i]); }
    }

    /// <summary>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="address"/>
    /// </code>
    /// </para>
    /// </summary>
    public void SetPointer(int address) => MovePointer(address - _pointer);

    public void MovePointer(int offset)
    {
        if (offset < 0)
        {
            for (int i = 0; i < (-offset); i++)
            {
                Append('<');
                _pointer--;
            }
            return;
        }
        if (offset > 0)
        {
            for (int i = 0; i < offset; i++)
            {
                Append('>');
                _pointer++;
            }
            return;
        }
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> Not modified
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// +++++++ ... <paramref name="value"/>
    /// </code>
    /// </para>
    /// </summary>
    public void AddValue(int value)
    {
        if (value < 0)
        {
            for (int i = 0; i < (-value); i++)
            {
                Append('-');
            }
            return;
        }
        if (value > 0)
        {
            for (int i = 0; i < value; i++)
            {
                Append('+');
            }
            return;
        }
    }

    /// <inheritdoc cref="AddValue(int, int)"/>
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="InternalException"/>
    public void AddValue(Runtime.DataItem value) => AddValue(GetInteger(value));

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="address"/> +++++++ ... <paramref name="value"/>
    /// </code>
    /// </para>
    /// </summary>
    public void AddValue(int address, int value)
    {
        SetPointer(address);
        AddValue(value);
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="address"/> +++++++ ... <paramref name="value"/>
    /// </code>
    /// </para>
    /// </summary>
    public void AddValue(int address, Runtime.DataItem value)
    {
        SetPointer(address);
        AddValue(value);
    }

    /// <inheritdoc cref="SetValue(int, int)"/>
    public void SetValue(int address, char value)
        => SetValue(address, CharCode.GetByte(value));

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="address"/>[-] +++++++ ... <paramref name="value"/>
    /// </code>
    /// </para>
    /// </summary>
    public void SetValue(int address, int value)
    {
        SetPointer(address);
        ClearCurrent();

        if (value > HalfByte)
        {
            value -= 256;
        }

        AddValue(value);
    }

    /// <inheritdoc cref="SetValue(int, int)"/>
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="InternalException"/>
    public void SetValue(int address, Runtime.DataItem value) => SetValue(address, GetInteger(value));

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="address"/>[-]
    /// </code>
    /// </para>
    /// </summary>
    public void ClearValue(int address)
    {
        SetPointer(address);
        ClearCurrent();
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> Last of <paramref name="addresses"/>
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="addresses"/>[-]
    /// </code>
    /// </para>
    /// </summary>
    public void ClearValue(params int[] addresses)
    {
        for (int i = 0; i < addresses.Length; i++)
        { ClearValue(addresses[i]); }
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="from"/>
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="to"/>[-] <paramref name="from"/>[-<paramref name="to"/>+<paramref name="from"/>]
    /// </code>
    /// </para>
    /// </summary>
    public void MoveValue(int from, params int[] to)
    {
        StartBlock($"MoveValue({from}; {string.Join("; ", to)})");

        CommentLine($"Clear the destination {string.Join("; ", to)}:");
        ClearValue(to);

        CommentLine($"Move value from {from} to {string.Join("; ", to)}:");
        MoveAddValue(from, to);

        EndBlock();
    }
    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="from"/>
    /// </para>
    /// <para>
    /// <code>
    /// <paramref name="from"/>[-<paramref name="to"/>+<paramref name="from"/>]
    /// </code>
    /// </para>
    /// </summary>
    public void MoveAddValue(int from, params int[] to)
    {
        this.JumpStart(from);
        this.AddValue(from, -1);

        for (int i = 0; i < to.Length; i++)
        { AddValue(to[i], 1); }

        this.JumpEnd(from);
    }
    /// <summary>
    /// <b>Pointer:</b> <paramref name="from"/>
    /// </summary>
    public void MoveSubValue(int from, params int[] to)
    {
        this.JumpStart(from);
        this.AddValue(from, -1);

        for (int i = 0; i < to.Length; i++)
        { AddValue(to[i], -1); }

        this.JumpEnd(from);
    }
    /// <summary>
    /// <para>
    /// <b>Pointer:</b> Not modified
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// [-]
    /// </code>
    /// </para>
    /// </summary>
    public void ClearCurrent()
    {
        Append("[-]");
    }

    /// <summary>
    /// <b>Pointer:</b> not modified
    /// </summary>
    public void JumpStart()
    {
        Append('[');
        _branchDepth++;
    }

    /// <summary>
    /// <b>Pointer:</b> not modified
    /// </summary>
    public void JumpEnd()
    {
        Append(']');
        _branchDepth--;
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="conditionAddress"/>
    /// </summary>
    public void JumpStart(int conditionAddress)
    {
        this.SetPointer(conditionAddress);
        this.JumpStart();
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="conditionAddress"/>
    /// </summary>
    public void JumpEnd(int conditionAddress, bool clearCondition = false)
    {
        this.SetPointer(conditionAddress);
        if (clearCondition) this.ClearCurrent();
        this.JumpEnd();
    }

    public static CompiledCode operator +(CompiledCode a, string b)
    {
        a.Append(b);
        return a;
    }

    public static CompiledCode operator +(CompiledCode a, char b)
    {
        a.Append(b);
        return a;
    }

    public override string ToString() => _code.ToString();

    public string ToString(Runtime.DebugInformation? debugInformation)
    {
        if (debugInformation is null)
        {
            string result = _code.ToString();

            while (true)
            {
                if (result.Contains("\r\n\r\n", StringComparison.Ordinal))
                { result = result.Replace("\r\n\r\n", "\r\n", StringComparison.Ordinal); }
                else if (result.Contains(" \r\n", StringComparison.Ordinal))
                { result = result.Replace(" \r\n", "\r\n", StringComparison.Ordinal); }
                else
                { break; }
            }

            return result;
        }
        else
        {
            StringBuilder result = new(_code.ToString());
            int index;

            while (true)
            {
                if ((index = result.IndexOf("\r\n\r\n", StringComparison.Ordinal)) != -1)
                {
                    result.Remove(index, 2);
                    debugInformation?.OffsetCodeFrom(index, -2);
                    continue;
                }

                if ((index = result.IndexOf(" \r\n", StringComparison.Ordinal)) != -1)
                {
                    result.Remove(index, 1);
                    debugInformation?.OffsetCodeFrom(index, -1);
                    continue;
                }

                break;
            }

            return result.ToString();
        }
    }

    /// <summary>
    /// <b>Try not to use this</b>
    /// </summary>
    public void FixPointer(int pointer)
    {
        this._pointer = pointer;
    }

    string GetDebuggerDisplay()
        => $"{{{nameof(CompiledCode)}}}";

    /// <summary>
    /// <b>POINTER MISMATCH</b>
    /// </summary>
    public void FindZeroRight(int step = 1)
    {
        if (step == 0) throw new ArgumentException("Must be nonzero", nameof(step));
        int _step = Math.Abs(step);
        char _code = (step < 0) ? '<' : '>';

        Append('[');
        Append(_code, _step);
        Append(']');
    }

    /// <summary>
    /// <b>POINTER MISMATCH</b>
    /// </summary>
    public void FindZeroLeft(int step = 1)
        => FindZeroRight(-step);

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void CopyValueWithTempUnsafe(int source, int tempAddress, int target)
    {
        MoveValueUnsafe(source, target, tempAddress);
        MoveAddValueUnsafe(tempAddress, source);
    }
    /// <summary>
    /// <b>Pointer:</b> Restored to the last state or not modified
    /// </summary>
    public void CopyValueWithTempUnsafe(int source, int tempAddress, params int[] targets)
    {
        if (targets.Length == 0) return;
        for (int i = 0; i < targets.Length; i++)
        { CopyValueWithTempUnsafe(source, tempAddress, targets[i]); }
    }

    public void MovePointerUnsafe(int offset)
    {
        if (offset < 0)
        {
            Append('<', -offset);
        }
        else if (offset > 0)
        {
            Append('>', offset);
        }
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void AddValueUnsafe(int offset, int value)
    {
        MovePointerUnsafe(offset);
        AddValue(value);
        MovePointerUnsafe(-offset);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void SetValueUnsafe(int offset, int value)
    {
        MovePointer(offset);

        ClearCurrent();

        if (value > HalfByte)
        { value -= 256; }

        AddValue(value);

        MovePointer(-offset);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void ClearValueUnsafe(int offset)
    {
        MovePointerUnsafe(offset);
        ClearCurrent();
        MovePointerUnsafe(-offset);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void ClearValueUnsafe(params int[] addresses)
    {
        for (int i = 0; i < addresses.Length; i++)
        { ClearValueUnsafe(addresses[i]); }
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveValueUnsafe(int from, params int[] to)
        => MoveValueUnsafe(from, true, to);

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveValueUnsafe(int from, bool clearDestination, params int[] to)
    {
        if (clearDestination)
        {
            CommentLine($"Clear the destination ({string.Join("; ", to)}) :");
            for (int i = 0; i < to.Length; i++)
            { ClearValueUnsafe(to[i]); }
        }

        CommentLine($"Move the value (from {from}):");
        MoveAddValueUnsafe(from, to);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveAddValueUnsafe(int from, params int[] to)
    {
        this.JumpStartUnsafe(from);
        this.AddValueUnsafe(from, -1);

        for (int i = 0; i < to.Length; i++)
        { AddValueUnsafe(to[i], 1); }

        this.JumpEndUnsafe(from);
    }
    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveSubValueUnsafe(int from, params int[] to)
    {
        this.JumpStartUnsafe(from);
        this.AddValueUnsafe(from, -1);

        for (int i = 0; i < to.Length; i++)
        { AddValueUnsafe(to[i], -1); }

        this.JumpEndUnsafe(from);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void JumpStartUnsafe(int conditionOffset)
    {
        this.MovePointerUnsafe(conditionOffset);
        this.Append('[');
        this.MovePointerUnsafe(-conditionOffset);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void JumpEndUnsafe(int conditionOffset)
    {
        this.MovePointerUnsafe(conditionOffset);
        this.Append(']');
        this.MovePointerUnsafe(-conditionOffset);
    }

    public void Append(string code) => _code.Append(code);
    public void Append(char code) => _code.Append(code);
    public void Append(char code, int count) => _code.Append(code, count);
    public void Insert(int index, string? value) => _code.Insert(index, value);

    public CompiledCode Duplicate() => new()
    {
        _branchDepth = BranchDepth,
        _code = new(_code.ToString()),
        _indent = _indent,
        _pointer = _pointer,
    };
}

public class StackCodeHelper
{
    /// <summary>
    /// Adds up all the stack element's size
    /// </summary>
    public int Size => _stack.Sum();
    public int Start { get; }
    public int MaxSize { get; }
    public int NextAddress => Start + _stack.Sum();
    public int LastAddress
    {
        get
        {
            if (_stack.Count == 0) return Start;
            return Start + _stack.Sum() - _stack[^1];
        }
    }
    public int MaxUsedSize => _maxUsedSize;
    public bool WillOverflow => _willOverflow;

    readonly CompiledCode _code;
    readonly Stack<int> _stack;
    int _maxUsedSize;
    bool _willOverflow;

    public StackCodeHelper(CompiledCode code, int start, int size)
    {
        this._code = code;
        this._stack = new Stack<int>();
        this.Start = start;
        this.MaxSize = size;
    }

    public StackCodeHelper(CompiledCode code, StackCodeHelper other)
    {
        _code = code;
        _stack = new Stack<int>(other._stack);
        _maxUsedSize = other._maxUsedSize;
        _willOverflow = other._willOverflow;
        Start = other.Start;
        MaxSize = other.MaxSize;
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="InternalException"/>
    public int Push(Runtime.DataItem v) => Push(CompiledCode.GetInteger(v));

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public int Push(int v)
    {
        int address = PushVirtual(1);

        _code.SetValue(address, v);
        return address;
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public int Push(char v)
    {
        int address = PushVirtual(1);

        _code.SetValue(address, v);
        return address;
    }

    public int PushVirtual(int size)
    {
        int address = NextAddress;

        if (Size >= MaxSize)
        {
            _code.OUT_STRING(address, $"\n{Ansi.StyleText(Ansi.BrightForegroundRed, "Stack overflow")}\n");
            _code.Append("[-]+[]");
            _willOverflow = true;
            Debugger.Break();
        }

        _stack.Push(size);

        _maxUsedSize = Math.Max(_maxUsedSize, Size);

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
            int offsettedAddress = address + offset;
            onAddress?.Invoke(offsettedAddress);
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
            _code.ClearValue(address + offset);
        }
    }

    /// <inheritdoc cref="Pop()" />
    public void Pop(int count)
    {
        for (int i = 0; i < count; i++)
        { Pop(); }
    }

    public int PopVirtual() => _stack.Pop();
}

public class HeapCodeHelper
{
    public int Start { get; }
    public int Size { get; }
    public int OffsettedStart => GetOffsettedStart(Start);
    public bool IsUsed => _isUsed;

    public const int BlockSize = 3;
    public const int AddressCarryOffset = 0;
    public const int ValueCarryOffset = 1;
    public const int DataOffset = 2;

    bool _isUsed;
    CompiledCode _code;

    public HeapCodeHelper(CompiledCode code, int start, int size)
    {
        _code = code;
        Start = start;
        Size = size;
    }

    /*
     *  LAYOUT:
     *  START 0 0 (a c v) (a c v) ...
     *  a: Carrying address
     *  c: Carrying value
     *  v: Value
     */

    public static int GetOffsettedStart(int start) => start + BlockSize;

    void ThrowIfNotInitialized()
    {
        if (Size <= 0)
        { throw new InternalException($"Heap size is {Size}"); }
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OffsettedStart</c>
    /// </summary>
    void GoBack()
    {
        // Go back
        _code += '[';
        _code.ClearCurrent();
        _code.MovePointerUnsafe(-BlockSize);
        _code += ']';

        // Fix overshoot
        _code.MovePointerUnsafe(BlockSize);
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OffsettedStart</c>
    /// </summary>
    void CarryBack()
    {
        // Go back
        _code += '[';
        _code.ClearCurrent();
        _code.MoveValueUnsafe(ValueCarryOffset, ValueCarryOffset - BlockSize);
        _code.MovePointerUnsafe(-BlockSize);
        _code += ']';

        // Fix overshoot
        _code.MovePointerUnsafe(BlockSize);
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// </summary>
    void GoTo()
    {
        // Condition on carrying address
        _code += '[';

        // Copy the address and leave 1 behind
        _code.MoveValueUnsafe(AddressCarryOffset, false, BlockSize + AddressCarryOffset);
        _code.AddValue(1);

        // Move to the next block
        _code.MovePointerUnsafe(BlockSize);

        // Decrement 1 and check if zero
        //   Yes => Destination reached -> leave 1
        //   No => Repeat
        _code += "- ] +";
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// </summary>
    void CarryTo()
    {
        // Condition on carrying address
        _code += '[';

        // Copy the address and leave 1 behind
        _code.MoveValueUnsafe(AddressCarryOffset, false, BlockSize + AddressCarryOffset);
        _code.AddValue(1);

        // Copy the value
        _code.MoveValueUnsafe(ValueCarryOffset, false, BlockSize + ValueCarryOffset);

        // Move to the next block
        _code.MovePointerUnsafe(BlockSize);

        // Decrement 1 and check if zero
        //   Yes => Destination reached -> leave 1
        //   No => Repeat
        _code += "- ] +";
    }

    public void Init()
    {
        if (Size <= 0) return;
        if (Size > 126) throw new CompilerException($"HEAP size must be smaller than 127", null, null);

        _code.StartBlock("Initialize HEAP");

        _code.SetValue(OffsettedStart + DataOffset, Size);
        _code.SetPointer(0);

        _code.EndBlock();
    }

    public string? LateInit()
    {
        if (Size <= 0) return null;
        if (Size > 126) throw new CompilerException($"HEAP size must be smaller than 127", null, null);

        CompiledCode code = new();

        code.StartBlock("Initialize HEAP");

        code.SetValue(OffsettedStart + DataOffset, Size);
        code.SetPointer(0);

        code.EndBlock();

        return code.ToString();
    }

    public void Destroy()
    {
        _code.StartBlock("Destroy HEAP");

        int start = OffsettedStart;
        int end = start + (BlockSize * (Size + 4));

        for (int i = start; i < end; i += BlockSize)
        { _code.ClearValue(i + DataOffset); }

        _code.SetPointer(0);

        _code.EndBlock();
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void Set(int pointerAddress, int valueAddress)
    {
        ThrowIfNotInitialized();

        _code.MoveValue(pointerAddress, OffsettedStart);
        _code.MoveValue(valueAddress, OffsettedStart + 1);

        _code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        _code.MoveValueUnsafe(ValueCarryOffset, true, DataOffset);

        GoBack();

        _isUsed = true;
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void SetAbsolute(int pointer, int value)
    {
        ThrowIfNotInitialized();

        _code.SetValue(OffsettedStart, pointer);
        _code.SetValue(OffsettedStart + 1, value);

        _code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        _code.MoveValueUnsafe(ValueCarryOffset, true, DataOffset);

        GoBack();

        _isUsed = true;
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void Add(int pointerAddress, int valueAddress)
    {
        ThrowIfNotInitialized();

        _code.MoveValue(pointerAddress, OffsettedStart);
        _code.MoveValue(valueAddress, OffsettedStart + 1);

        _code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        _code.MoveAddValueUnsafe(ValueCarryOffset, DataOffset);

        GoBack();

        _isUsed = true;
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void Subtract(int pointerAddress, int valueAddress)
    {
        ThrowIfNotInitialized();

        _code.MoveValue(pointerAddress, OffsettedStart);
        _code.MoveValue(valueAddress, OffsettedStart + 1);

        _code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        _code.MoveSubValueUnsafe(ValueCarryOffset, DataOffset);

        GoBack();

        _isUsed = true;
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="resultAddress"/>
    /// </para>
    /// <para>
    /// <b>Note:</b> This will discard <paramref name="pointerAddress"/>
    /// </para>
    /// </summary>
    public void Get(int pointerAddress, int resultAddress, int size)
    {
        for (int offset = 0; offset < size; offset++)
        {
            _code.AddValue(pointerAddress, 1);
            Get(pointerAddress, resultAddress + offset);
        }

        _isUsed = true;
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="resultAddress"/>
    /// </summary>
    public void Get(int pointerAddress, int resultAddress)
    {
        ThrowIfNotInitialized();

        _code.ClearValue(OffsettedStart, OffsettedStart + 1);

        _code.MoveValue(pointerAddress, OffsettedStart);

        _code.SetPointer(OffsettedStart);

        GoTo();

        _code.SetValueUnsafe(AddressCarryOffset, 0);
        _code.CopyValueWithTempUnsafe(DataOffset, AddressCarryOffset, ValueCarryOffset);
        _code.SetValueUnsafe(AddressCarryOffset, 1);

        CarryBack();

        _code.MoveValue(Start + ValueCarryOffset, resultAddress);
        _code.SetPointer(resultAddress);

        _isUsed = true;
    }
}
