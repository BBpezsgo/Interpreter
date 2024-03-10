namespace LanguageCore.Brainfuck;

using Ansi = Win32.Console.Ansi;

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

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class CompiledCode : IDuplicatable<CompiledCode>
{
    const int HALF_BYTE = byte.MaxValue / 2;

    public StringBuilder Code;
    StringBuilder CachedFinalCode;

    const int InitialSize = 1024;

    int indent;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] int pointer;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] int branchDepth;

    public int Pointer => pointer;
    public int BranchDepth => branchDepth;

    public CompiledCode()
    {
        this.Code = new StringBuilder(InitialSize);
        this.CachedFinalCode = new StringBuilder();
        this.indent = 0;
        this.pointer = 0;
        this.branchDepth = 0;
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
        this.indent += indent;
        return this.indent;
    }
    public void LineBreak()
    {
        Code.Append("\r\n");
        Code.Append(' ', indent);
    }
    public void CommentLine(string text)
    {
        LineBreak();
        Code.Append(BrainfuckCode.ReplaceCodes(text, '_'));
        LineBreak();
    }
    public void StartBlock()
    {
        LineBreak();
        Code.Append('{');
        this.indent += 2;
        LineBreak();
    }
    public void StartBlock(string label)
    {
        LineBreak();
        this.Code.Append(BrainfuckCode.ReplaceCodes(label, '_'));
        this.Code.Append(' ');
        this.Code.Append('{');
        this.indent += 2;
        LineBreak();
    }
    public void EndBlock()
    {
        this.indent -= 2;
        LineBreak();
        Code.Append('}');
        LineBreak();
    }

    #endregion

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

    public void SetPointer(int address) => MovePointer(address - pointer);

    public void MovePointer(int offset)
    {
        if (offset < 0)
        {
            for (int i = 0; i < (-offset); i++)
            {
                Code.Append('<');
                CachedFinalCode.Append('<');
                pointer--;
            }
            return;
        }
        if (offset > 0)
        {
            for (int i = 0; i < offset; i++)
            {
                Code.Append('>');
                CachedFinalCode.Append('>');
                pointer++;
            }
            return;
        }
    }

    /// <summary>
    /// <b>Pointer:</b> Not modified
    /// </summary>
    public void AddValue(int value)
    {
        if (value < 0)
        {
            for (int i = 0; i < (-value); i++)
            {
                Code.Append('-');
                CachedFinalCode.Append('-');
            }
            return;
        }
        if (value > 0)
        {
            for (int i = 0; i < value; i++)
            {
                Code.Append('+');
                CachedFinalCode.Append('+');
            }
            return;
        }
    }

    /// <summary>
    /// <b>Pointer:</b> Not modified
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="InternalException"/>
    public void AddValue(Runtime.DataItem value) => AddValue(GetInteger(value));

    /// <summary>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </summary>
    public void AddValue(int address, int value)
    {
        SetPointer(address);
        AddValue(value);
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </summary>
    public void AddValue(int address, Runtime.DataItem value)
    {
        SetPointer(address);
        AddValue(value);
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </summary>
    public void SetValue(int address, char value)
        => SetValue(address, CharCode.GetByte(value));

    /// <summary>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </summary>
    public void SetValue(int address, int value)
    {
        SetPointer(address);
        ClearCurrent();

        if (value > HALF_BYTE)
        {
            value -= 256;
        }

        AddValue(value);
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    /// <exception cref="InternalException"/>
    public void SetValue(int address, Runtime.DataItem value) => SetValue(address, GetInteger(value));

    /// <summary>
    /// <b>Pointer:</b> <paramref name="address"/>
    /// </summary>
    public void ClearValue(int address)
    {
        SetPointer(address);
        ClearCurrent();
    }

    /// <summary>
    /// <b>Pointer:</b> Last of <paramref name="addresses"/>
    /// </summary>
    public void ClearValue(params int[] addresses)
    {
        for (int i = 0; i < addresses.Length; i++)
        { ClearValue(addresses[i]); }
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="from"/>
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
    /// <b>Pointer:</b> <paramref name="from"/>
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
    /// <b>Pointer:</b> Not modified
    /// </summary>
    public void ClearCurrent()
    {
        Code.Append("[-]");
        CachedFinalCode.Append("[-]");
    }

    /// <summary>
    /// <b>Pointer:</b> not modified
    /// </summary>
    public void JumpStart()
    {
        Code.Append('[');
        CachedFinalCode.Append('[');
        branchDepth++;
    }

    /// <summary>
    /// <b>Pointer:</b> not modified
    /// </summary>
    public void JumpEnd()
    {
        Code.Append(']');
        CachedFinalCode.Append(']');
        branchDepth--;
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
        a.Code.Append(b);
        a.CachedFinalCode.Append(b);
        return a;
    }

    public static CompiledCode operator +(CompiledCode a, char b)
    {
        a.Code.Append(b);
        a.CachedFinalCode.Append(b);
        return a;
    }

    public override int GetHashCode() => HashCode.Combine(Code);
    public override string ToString()
    {
        string result = Code.ToString();

        while (true)
        {
            if (result.Contains("\r\n\r\n", StringComparison.Ordinal))
            { result = result.Replace("\r\n\r\n", "\r\n", StringComparison.Ordinal); }
            if (result.Contains(" \r\n", StringComparison.Ordinal))
            { result = result.Replace(" \r\n", "\r\n", StringComparison.Ordinal); }
            else
            { break; }
        }

        return result;
    }

    /// <summary>
    /// <b>Try not to use this</b>
    /// </summary>
    public void FixPointer(int pointer)
    {
        this.pointer = pointer;
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

        Code.Append('[');
        Code.Append(_code, _step);
        Code.Append(']');

        CachedFinalCode.Append('[');
        CachedFinalCode.Append(_code, _step);
        CachedFinalCode.Append(']');
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
            Code.Append('<', -offset);
            CachedFinalCode.Append('<', -offset);
        }
        else if (offset > 0)
        {
            Code.Append('>', offset);
            CachedFinalCode.Append('>', offset);
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

        if (value > HALF_BYTE)
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
        this.Code.Append('[');
        this.CachedFinalCode.Append('[');
        this.MovePointerUnsafe(-conditionOffset);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void JumpEndUnsafe(int conditionOffset)
    {
        this.MovePointerUnsafe(conditionOffset);
        this.Code.Append(']');
        this.CachedFinalCode.Append(']');
        this.MovePointerUnsafe(-conditionOffset);
    }

    public string GetFinalCode(bool showProgress)
    {
        string result = CachedFinalCode.ToString();
        result = BrainfuckCode.RemoveNoncodes(result, showProgress);
        result = Minifier.Minify(result);
        CachedFinalCode = new StringBuilder(result);
        return result;
    }

    public void Append(string code)
    {
        this.Code.Append(code);
        this.CachedFinalCode.Append(code);
    }
    public void Append(char code)
    {
        this.Code.Append(code);
        this.CachedFinalCode.Append(code);
    }

    public CompiledCode Duplicate() => new()
    {
        branchDepth = BranchDepth,
        CachedFinalCode = new(CachedFinalCode.ToString()),
        Code = new(Code.ToString()),
        indent = indent,
        pointer = pointer,
    };
}

public class StackCodeHelper
{
    readonly CompiledCode Code;
    readonly Stack<int> TheStack;

    /// <summary>
    /// Adds up all the stack element's size
    /// </summary>
    public int Size => TheStack.Sum();
    public readonly int Start;
    public readonly int MaxSize;

    public int NextAddress => Start + TheStack.Sum();

    public int LastAddress
    {
        get
        {
            if (TheStack.Count == 0) return Start;
            return Start + TheStack.Sum() - TheStack[^1];
        }
    }

    public StackCodeHelper(CompiledCode code, int start, int size)
    {
        this.Code = code;
        this.TheStack = new Stack<int>();
        this.Start = start;
        this.MaxSize = size;
    }

    public StackCodeHelper(CompiledCode code, StackCodeHelper other)
    {
        this.Code = code;
        this.TheStack = new Stack<int>(other.TheStack);
        this.Start = other.Start;
        this.MaxSize = other.MaxSize;
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

        if (Size > MaxSize)
        {
            Code.OUT_STRING(address, $"\n{Ansi.StyleText(Ansi.BrightForegroundRed, "Stack overflow")}\n");
            Code.Append("[-]+[]");
        }

        Code.SetValue(address, v);
        return address;
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public int Push(char v)
    {
        int address = PushVirtual(1);

        if (Size > MaxSize)
        {
            Code.OUT_STRING(address, $"\n{Ansi.StyleText(Ansi.BrightForegroundRed, "Stack overflow")}\n");
            Code.Append("[-]+[]");
        }

        Code.SetValue(address, v);
        return address;
    }

    public int PushVirtual(int size)
    {
        int address = NextAddress;

        if (Size >= MaxSize)
        {
            Code.OUT_STRING(address, $"\n{Ansi.StyleText(Ansi.BrightForegroundRed, "Stack overflow")}\n");
            Code.Append("[-]+[]");
        }

        TheStack.Push(size);
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
            Code.MoveValue(offsettedSource, offsettedTarget);
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
            Code.MoveAddValue(offsettedSource, offsettedTarget);
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
            Code.ClearValue(address + offset);
        }
    }

    /// <inheritdoc cref="Pop()" />
    public void Pop(int count)
    {
        for (int i = 0; i < count; i++)
        { Pop(); }
    }

    public int PopVirtual() => TheStack.Pop();
}

public class HeapCodeHelper
{
    CompiledCode Code;
    bool _isInitialized;

    public readonly int Start;
    public readonly int Size;

    public bool IsInitialized => _isInitialized;
    public int OffsettedStart => GetOffsettedStart(Start);

    public static int GetOffsettedStart(int start) => start + BLOCK_SIZE;

    public const int BLOCK_SIZE = 3;
    public const int OFFSET_ADDRESS_CARRY = 0;
    public const int OFFSET_VALUE_CARRY = 1;
    public const int OFFSET_DATA = 2;

    public HeapCodeHelper(CompiledCode code, int start, int size)
    {
        Code = code;
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

    void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        { throw new InternalException($"Heap isn't initialized"); }
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OffsettedStart</c>
    /// </summary>
    void GoBack()
    {
        // Go back
        Code += '[';
        Code.ClearCurrent();
        Code.MovePointerUnsafe(-BLOCK_SIZE);
        Code += ']';

        // Fix overshoot
        Code.MovePointerUnsafe(BLOCK_SIZE);
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OffsettedStart</c>
    /// </summary>
    void CarryBack()
    {
        // Go back
        Code += '[';
        Code.ClearCurrent();
        Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_VALUE_CARRY - BLOCK_SIZE);
        Code.MovePointerUnsafe(-BLOCK_SIZE);
        Code += ']';

        // Fix overshoot
        Code.MovePointerUnsafe(BLOCK_SIZE);
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// </summary>
    void GoTo()
    {
        // Condition on carrying address
        Code += '[';

        // Copy the address and leave 1 behind
        Code.MoveValueUnsafe(OFFSET_ADDRESS_CARRY, false, BLOCK_SIZE + OFFSET_ADDRESS_CARRY);
        Code.AddValue(1);

        // Move to the next block
        Code.MovePointerUnsafe(BLOCK_SIZE);

        // Decrement 1 and check if zero
        //   Yes => Destination reached -> leave 1
        //   No => Repeat
        Code += "- ] +";
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// </summary>
    void CarryTo()
    {
        // Condition on carrying address
        Code += '[';

        // Copy the address and leave 1 behind
        Code.MoveValueUnsafe(OFFSET_ADDRESS_CARRY, false, BLOCK_SIZE + OFFSET_ADDRESS_CARRY);
        Code.AddValue(1);

        // Copy the value
        Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, false, BLOCK_SIZE + OFFSET_VALUE_CARRY);

        // Move to the next block
        Code.MovePointerUnsafe(BLOCK_SIZE);

        // Decrement 1 and check if zero
        //   Yes => Destination reached -> leave 1
        //   No => Repeat
        Code += "- ] +";
    }

    public void Init()
    {
        if (_isInitialized) return;
        if (Size <= 0) return;

        Code.StartBlock("Initialize HEAP");

        // SetAbsolute(0, 126);
        Code.SetValue(OffsettedStart + OFFSET_DATA, 126);
        Code.SetPointer(0);

        _isInitialized = true;

        Code.EndBlock();
    }

    public void InitVirtual()
    {
        _isInitialized = true;
    }

    public void Destroy()
    {
        Code.StartBlock("Destroy HEAP");

        int start = OffsettedStart;
        int end = start + (BLOCK_SIZE * (Size + 4));

        for (int i = start; i < end; i += BLOCK_SIZE)
        { Code.ClearValue(i + OFFSET_DATA); }

        Code.SetPointer(0);

        Code.EndBlock();
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void Set(int pointerAddress, int valueAddress)
    {
        ThrowIfNotInitialized();

        Code.MoveValue(pointerAddress, OffsettedStart);
        Code.MoveValue(valueAddress, OffsettedStart + 1);

        Code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, true, OFFSET_DATA);

        GoBack();
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void SetAbsolute(int pointer, int value)
    {
        ThrowIfNotInitialized();

        Code.SetValue(OffsettedStart, pointer);
        Code.SetValue(OffsettedStart + 1, value);

        Code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, true, OFFSET_DATA);

        GoBack();
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void Add(int pointerAddress, int valueAddress)
    {
        ThrowIfNotInitialized();

        Code.MoveValue(pointerAddress, OffsettedStart);
        Code.MoveValue(valueAddress, OffsettedStart + 1);

        Code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        Code.MoveAddValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_DATA);

        GoBack();
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void Subtract(int pointerAddress, int valueAddress)
    {
        ThrowIfNotInitialized();

        Code.MoveValue(pointerAddress, OffsettedStart);
        Code.MoveValue(valueAddress, OffsettedStart + 1);

        Code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        Code.MoveSubValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_DATA);

        GoBack();
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
            Code.AddValue(pointerAddress, 1);
            Get(pointerAddress, resultAddress + offset);
        }
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="resultAddress"/>
    /// </summary>
    public void Get(int pointerAddress, int resultAddress)
    {
        ThrowIfNotInitialized();

        Code.ClearValue(OffsettedStart, OffsettedStart + 1);

        Code.MoveValue(pointerAddress, OffsettedStart);

        Code.SetPointer(OffsettedStart);

        GoTo();

        Code.SetValueUnsafe(OFFSET_ADDRESS_CARRY, 0);
        Code.CopyValueWithTempUnsafe(OFFSET_DATA, OFFSET_ADDRESS_CARRY, OFFSET_VALUE_CARRY);
        Code.SetValueUnsafe(OFFSET_ADDRESS_CARRY, 1);

        CarryBack();

        Code.MoveValue(Start + OFFSET_VALUE_CARRY, resultAddress);
        Code.SetPointer(resultAddress);
    }
}
