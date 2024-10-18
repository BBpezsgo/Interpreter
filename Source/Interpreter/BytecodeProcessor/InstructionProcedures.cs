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

    void CRASH()
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

        ALU.SubtractI(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        Step();
    }

    void CompareF()
    {
        float a = GetData(CurrentInstruction.Operand1).F32;
        float b = GetData(CurrentInstruction.Operand2).F32;

        float result = a - b;

        Registers.Flags.Set(Flags.Sign, result < 0);
        Registers.Flags.Set(Flags.Zero, result == 0f);
        Registers.Flags.Set(Flags.Carry, false);
        // Registers.Flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)SignBit32) == (long)SignBit32);

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
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);

        a = ALU.AddI(a, b, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void MathDiv()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);

        a = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(a.U8 / b.U8)),
            BitWidth._16 => new RuntimeValue((char)(a.U16 / b.U16)),
            BitWidth._32 => new RuntimeValue((int)(a.I32 / b.I32)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void MathSub()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);

        a = ALU.SubtractI(a, b, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void MathMult()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);

        a = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(a.U8 * b.U8)),
            BitWidth._16 => new RuntimeValue((char)(a.U16 * b.U16)),
            BitWidth._32 => new RuntimeValue((int)(a.I32 * b.I32)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, a);

        Registers.Flags.SetCarry(a.I32, CurrentInstruction.BitWidth);

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

    #region Unsigned Math Operations

    void UMathAdd()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);

        a = ALU.AddU(a, b, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void UMathDiv()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);

        a = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(a.U8 / b.U8)),
            BitWidth._16 => new RuntimeValue((ushort)(a.U16 / b.U16)),
            BitWidth._32 => new RuntimeValue((uint)(a.U32 / b.U32)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void UMathSub()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);

        a = ALU.SubtractU(a, b, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void UMathMult()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);

        a = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(a.U8 * b.U8)),
            BitWidth._16 => new RuntimeValue((ushort)(a.U16 * b.U16)),
            BitWidth._32 => new RuntimeValue((uint)(a.U32 * b.U32)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, a);

        Registers.Flags.SetCarry(a.I32, CurrentInstruction.BitWidth);

        Step();
    }

    void UMathMod()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);

        a = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(a.U8 % b.U8)),
            BitWidth._16 => new RuntimeValue((ushort)(a.U16 % b.U16)),
            BitWidth._32 => new RuntimeValue((uint)(a.U32 % b.U32)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    #endregion

    #region Float Math Operations

    void FMathAdd()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(a.F32 + b.F32));

        Step();
    }

    void FMathDiv()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(a.F32 / b.F32));

        Step();
    }

    void FMathSub()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(a.F32 - b.F32));

        Step();
    }

    void FMathMult()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(a.F32 * b.F32));

        Step();
    }

    void FMathMod()
    {
        RuntimeValue a = GetData(CurrentInstruction.Operand1);
        RuntimeValue b = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(a.F32 % b.F32));

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
            throw new NotImplementedException();
            // managedFunction.Callback(parameters, Push);
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
