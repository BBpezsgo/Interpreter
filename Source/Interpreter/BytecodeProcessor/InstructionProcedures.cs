namespace LanguageCore.Runtime;

public partial class BytecodeProcessor
{
    #region Memory Operations

    void Move()
    {
        RuntimeValue value = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, value);

        Step();
    }

    #endregion

    #region Flow Control

    void THROW()
    {
        int pointer = GetData(CurrentInstruction.Operand1).I32;
        string? value = HeapUtils.GetString(Memory, pointer);
        throw new UserException(value ?? string.Empty);
    }

    void CALL()
    {
        int relativeAddress = GetData(CurrentInstruction.Operand1).I32;

        Push(Registers.CodePointer, Register.CodePointer.BitWidth());

        Step(relativeAddress);
    }

    void RETURN()
    {
        RuntimeValue codePointer = Pop(BitWidth._32);

        Registers.CodePointer = codePointer.I32;
    }

    void JUMP_BY()
    {
        int relativeAddress = GetData(CurrentInstruction.Operand1).I32;

        Step(relativeAddress);
    }

    void EXIT()
    {
        Registers.CodePointer = Code.Length;
    }

    void JumpIfEqual()
    {
        if (Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).I32); }
        else
        { Step(); }
    }

    void JumpIfNotEqual()
    {
        if (!Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).I32); }
        else
        { Step(); }
    }

    void JumpIfGreater()
    {
        if ((!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))) && !Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).I32); }
        else
        { Step(); }
    }

    void JumpIfGreaterOrEqual()
    {
        if (!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)))
        { Step(GetData(CurrentInstruction.Operand1).I32); }
        else
        { Step(); }
    }

    void JumpIfLess()
    {
        if (Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))
        { Step(GetData(CurrentInstruction.Operand1).I32); }
        else
        { Step(); }
    }

    void JumpIfLessOrEqual()
    {
        if ((Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)) || Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).I32); }
        else
        { Step(); }
    }

    #endregion

    #region Comparison Operations

    void Compare()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        ALU.Subtract(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        Step();
    }

    #endregion

    #region Logic Operations

    void LogicAND()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, ((dst.I32 != 0) && (src.I32 != 0)) ? 1 : 0);

        Registers.Flags.SetSign(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void LogicOR()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, ((dst.I32 != 0) || (src.I32 != 0)) ? 1 : 0);

        Registers.Flags.SetSign(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsShiftLeft()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.I32 << src.I32);

        Step();
    }

    void BitsShiftRight()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.I32 >> src.I32);

        Step();
    }

    void BitsOR()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.I32 | src.I32);

        Registers.Flags.SetSign(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsXOR()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.I32 ^ src.I32);

        Registers.Flags.SetSign(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsNOT()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        SetData(CurrentInstruction.Operand1, ~dst.I32);

        Step();
    }

    void BitsAND()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.I32 & src.I32);

        Registers.Flags.SetSign(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.I32, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    #endregion

    #region Math Operations

    void MathAdd()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = ALU.Add(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    void MathDiv()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(dst.U8 / src.U8)),
            BitWidth._16 => new RuntimeValue((char)(dst.U16 / src.U16)),
            BitWidth._32 => new RuntimeValue((int)(dst.I32 / src.I32)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    void MathSub()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = ALU.Subtract(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    void MathMult()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(dst.U8 * src.U8)),
            BitWidth._16 => new RuntimeValue((char)(dst.U16 * src.U16)),
            BitWidth._32 => new RuntimeValue((int)(dst.I32 * src.I32)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Registers.Flags.SetCarry(dst.I32, CurrentInstruction.BitWidth);

        Step();
    }

    void MathMod()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(dst.U8 % src.U8)),
            BitWidth._16 => new RuntimeValue((char)(dst.U16 % src.U16)),
            BitWidth._32 => new RuntimeValue((int)(dst.I32 % src.I32)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    #endregion

    #region Float Math Operations

    void FMathAdd()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.F32 + src.F32));

        Step();
    }

    void FMathDiv()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.F32 / src.F32));

        Step();
    }

    void FMathSub()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.F32 - src.F32));

        Step();
    }

    void FMathMult()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.F32 * src.F32));

        Step();
    }

    void FMathMod()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.F32 % src.F32));

        Step();
    }

    #endregion

    #region Stack Operations

    void PUSH_VALUE()
    {
        RuntimeValue v = GetData(CurrentInstruction.Operand1);
        Push(v, CurrentInstruction.Operand1.BitWidth);

        Step();
    }

    void POP_VALUE(BitWidth size)
    {
        Pop(size);

        Step();
    }

    void POP_TO_VALUE(BitWidth size)
    {
        RuntimeValue v = Pop(size);
        SetData(CurrentInstruction.Operand1, v);

        Step();
    }

    #endregion

    #region Utility Operations

    void FTo()
    {
        RuntimeValue data = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue((float)data.I32));

        Step();
    }

    void FFrom()
    {
        RuntimeValue data = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, (int)data.F32);

        Step();
    }

    #endregion

    #region External Calls

    void CALL_EXTERNAL()
    {
        int functionId = GetData(CurrentInstruction.Operand1).I32;

        if (!ExternalFunctions.TryGetValue(functionId, out IExternalFunction? function))
        { throw new RuntimeException($"Undefined external function {functionId}"); }

        Span<byte> parameters = Memory.AsSpan().Slice(Registers.StackPointer, function.ParametersSize);

        if (function is ExternalFunctionAsyncBlock managedFunction)
        {
            managedFunction.Callback(parameters, Push);
        }
        else if (function is ExternalFunctionSync simpleFunction)
        {
            if (function.ReturnValueSize > 0)
            {
                ReadOnlySpan<byte> returnValue = simpleFunction.Callback(parameters);
                Push(returnValue);
            }
            else
            {
                simpleFunction.Callback(parameters);
            }
        }

        Step();
    }

    #endregion
}
