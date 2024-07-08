namespace LanguageCore.Brainfuck;

using Compiler;

public interface IBrainfuckGenerator
{
    public CodeHelper Code { get; }
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

    void IDisposable.Dispose()
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

    void IDisposable.Dispose()
    {
        Generator.Code.EndBlock();
    }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class CodeHelper : IDuplicatable<CodeHelper>
{
    const int HalfByte = byte.MaxValue / 2;
    const int InitialSize = 1024;
    const int IndentationSize = 2;

    public int Pointer => _pointer;
    public int BranchDepth => _branchDepth;
    public int Length => _code.Length;

    public bool AddComments { get; set; }
    public bool AddSmallComments { get; set; }

    StringBuilder _code;
    int _indent;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] int _pointer;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] int _branchDepth;

    public CodeHelper()
    {
        _code = new StringBuilder(InitialSize);
        _indent = 0;
        _pointer = 0;
        _branchDepth = 0;

        AddComments = false;
        AddSmallComments = false;
    }

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="InternalException"/>
    public static int GetInteger(CompiledValue v)
    {
        return v.Type switch
        {
            RuntimeType.Byte => v.Byte,
            RuntimeType.Integer => v.Int,
            RuntimeType.Char => CharCode.GetByte(v.Char),

            RuntimeType.Single => throw new NotImplementedException("Floats not supported by brainfuck :("),
            RuntimeType.Null => throw new InternalException(),
            _ => throw new UnreachableException(),
        };
    }

    #region Comments

    public void LineBreak()
    {
        if (!AddComments) return;
        _code.Append("\r\n");
        _code.Append(' ', _indent);
    }

    public void CommentLine(string text)
    {
        if (!AddComments) return;
        LineBreak();
        _code.Append(BrainfuckCode.ReplaceCodes(text, '_'));
        LineBreak();
    }

    public void StartBlock()
    {
        if (!AddComments) return;
        LineBreak();
        _code.Append('{');
        _indent += IndentationSize;
        LineBreak();
    }

    public void StartBlock(string label)
    {
        if (!AddComments) return;
        LineBreak();
        _code.Append(BrainfuckCode.ReplaceCodes(label, '_'));
        _code.Append(' ');
        _code.Append('{');
        _indent += IndentationSize;
        LineBreak();
    }

    public void EndBlock()
    {
        if (!AddComments) return;
        _indent -= IndentationSize;
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
    /// <b>Pointer:</b> <paramref name="target"/> + <see cref="StackCodeHelper.Direction"/>
    /// </summary>
    public void CopyValue(int source, int target)
        => CopyValue(source, target, target + StackCodeHelper.Direction);

    /// <summary>
    /// <b>Requires 1 more cell to the right of the <paramref name="target"/>!</b><br/>
    /// <b>Pointer:</b> <paramref name="target"/> + 1
    /// </summary>
    public void CopyValue(int source, int target, Func<int, StackAddress> allocator)
    {
        using StackAddress tempAddress = allocator.Invoke(1);
        CopyValue(source, target, tempAddress);
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="tempAddress"/>
    /// </summary>
    public void CopyValue(int source, int target, int tempAddress)
    {
        if (AddSmallComments) StartBlock($"CopyValueWithTemp({source}; {tempAddress}; {target})");
        MoveValue(source, target, tempAddress);
        MoveAddValue(tempAddress, source);
        if (AddSmallComments) EndBlock();
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void CopyValueUnsafe(int source, int target, int tempAddress)
    {
        MoveValueUnsafe(source, target, tempAddress);
        MoveAddValueUnsafe(tempAddress, source);
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
    public void AddValue(CompiledValue value) => AddValue(GetInteger(value));

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
    public void AddValue(int address, CompiledValue value)
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
    public void SetValue(int address, CompiledValue value) => SetValue(address, GetInteger(value));

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
    public void ClearValue(params ReadOnlySpan<int> addresses)
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
    public void MoveValue(int from, int to)
    {
        if (AddSmallComments) StartBlock($"MoveValue({from}; {string.Join("; ", to)})");

        if (AddSmallComments) CommentLine($"Clear the destination {string.Join("; ", to)}:");
        ClearValue(to);

        if (AddSmallComments) CommentLine($"Move value from {from} to {string.Join("; ", to)}:");
        MoveAddValue(from, to);

        if (AddSmallComments) EndBlock();
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="from"/>
    /// </para>
    /// <para>
    /// <b>Code:</b>
    /// <code>
    /// <paramref name="to1"/>[-] <paramref name="to2"/>[-]
    /// <paramref name="from"/>[-<paramref name="to1"/>+<paramref name="to2"/>+<paramref name="from"/>]
    /// </code>
    /// </para>
    /// </summary>
    public void MoveValue(int from, int to1, int to2)
    {
        if (AddSmallComments) StartBlock($"MoveValue({from}; {to1}; {to2})");

        if (AddSmallComments) CommentLine($"Clear the destination {to1}; {to2}:");
        ClearValue(to1);
        ClearValue(to2);

        if (AddSmallComments) CommentLine($"Move value from {from} to {to1}; {to2}:");
        MoveAddValue(from, to1, to2);

        if (AddSmallComments) EndBlock();
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
    public void MoveAddValue(int from, int to)
    {
        JumpStart(from);
        AddValue(from, -1);
        AddValue(to, 1);
        JumpEnd(from);
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="from"/>
    /// </para>
    /// <para>
    /// <code>
    /// <paramref name="from"/>[-<paramref name="to1"/>+<paramref name="to2"/>+<paramref name="from"/>]
    /// </code>
    /// </para>
    /// </summary>
    public void MoveAddValue(int from, int to1, int to2)
    {
        JumpStart(from);
        AddValue(from, -1);
        AddValue(to1, 1);
        AddValue(to2, 1);
        JumpEnd(from);
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="from"/>
    /// </summary>
    public void MoveSubValue(int from, int to)
    {
        JumpStart(from);
        AddValue(from, -1);
        AddValue(to, -1);
        JumpEnd(from);
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

    public static CodeHelper operator +(CodeHelper a, string b)
    {
        a.Append(b);
        return a;
    }

    public static CodeHelper operator +(CodeHelper a, char b)
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
                if ((index = result.IndexOf("\r\n\r\n")) != -1)
                {
                    result.Remove(index, 2);
                    debugInformation?.OffsetCodeFrom(index, -2);
                    continue;
                }

                if ((index = result.IndexOf(" \r\n")) != -1)
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
        => $"{{{nameof(CodeHelper)}}}";

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
    public void MoveValueUnsafe(int from, int to)
        => MoveValueUnsafe(from, true, to);

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveValueUnsafe(int from, int to1, int to2)
        => MoveValueUnsafe(from, true, to1, to2);

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveValueUnsafe(int from, bool clearDestination, int to)
    {
        if (clearDestination)
        {
            if (AddSmallComments) CommentLine($"Clear the destination ({to}) :");
            ClearValueUnsafe(to);
        }

        if (AddSmallComments) CommentLine($"Move the value (from {from}):");
        MoveAddValueUnsafe(from, to);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveValueUnsafe(int from, bool clearDestination, int to1, int to2)
    {
        if (clearDestination)
        {
            if (AddSmallComments) CommentLine($"Clear the destination ({to1}; {to2}) :");
            ClearValueUnsafe(to1);
            ClearValueUnsafe(to2);
        }

        if (AddSmallComments) CommentLine($"Move the value (from {from}):");
        MoveAddValueUnsafe(from, to1, to2);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveAddValueUnsafe(int from, int to)
    {
        JumpStartUnsafe(from);
        AddValueUnsafe(from, -1);

        AddValueUnsafe(to, 1);

        JumpEndUnsafe(from);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveAddValueUnsafe(int from, int to1, int to2)
    {
        JumpStartUnsafe(from);
        AddValueUnsafe(from, -1);

        AddValueUnsafe(to1, 1);
        AddValueUnsafe(to2, 1);

        JumpEndUnsafe(from);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void MoveSubValueUnsafe(int from, int to)
    {
        JumpStartUnsafe(from);
        AddValueUnsafe(from, -1);
        AddValueUnsafe(to, -1);
        JumpEndUnsafe(from);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void JumpStartUnsafe(int conditionOffset)
    {
        MovePointerUnsafe(conditionOffset);
        Append('[');
        MovePointerUnsafe(-conditionOffset);
    }

    /// <summary>
    /// <b>Pointer:</b> Restored to the last state
    /// </summary>
    public void JumpEndUnsafe(int conditionOffset)
    {
        MovePointerUnsafe(conditionOffset);
        Append(']');
        MovePointerUnsafe(-conditionOffset);
    }

    public void Append(string code)
    {
        if (!AddComments)
        { foreach (char c in code) Append(c); }
        else
        { _code.Append(code); }
    }
    public void Append(char code)
    {
        if (!AddComments && !BrainfuckCode.IsCode(code))
        { return; }
        _code.Append(code);
    }
    public void Append(char code, int count) => _code.Append(code, count);
    public void Insert(int index, string? value) => _code.Insert(index, value);

    public CodeHelper Duplicate() => new()
    {
        _branchDepth = BranchDepth,
        _code = new StringBuilder(_code.ToString()),
        _indent = _indent,
        _pointer = _pointer,
        AddComments = AddComments,
        AddSmallComments = AddSmallComments,
    };
}
