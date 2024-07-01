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
        int pointer = GetData(CurrentInstruction.Operand1).Int;
        string? value = HeapUtils.GetString(Memory, pointer);
        throw new UserException(value ?? "null");
    }

    void CALL()
    {
        int relativeAddress = GetData(CurrentInstruction.Operand1).Int;

        Push(Registers.CodePointer, Register.CodePointer.BitWidth());

        Step(relativeAddress);
    }

    void RETURN()
    {
        RuntimeValue codePointer = Pop(BitWidth._32);

        Registers.CodePointer = codePointer.Int;
    }

    void JUMP_BY()
    {
        int relativeAddress = GetData(CurrentInstruction.Operand1).Int;

        Step(relativeAddress);
    }

    void EXIT()
    {
        Registers.CodePointer = Code.Length;
    }

    void JumpIfEqual()
    {
        if (Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfNotEqual()
    {
        if (!Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfGreater()
    {
        if ((!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))) && !Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfGreaterOrEqual()
    {
        if (!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfLess()
    {
        if (Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfLessOrEqual()
    {
        if ((Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)) || Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
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
        SetData(CurrentInstruction.Operand1, ((dst.Int != 0) && (src.Int != 0)) ? 1 : 0);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void LogicOR()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, ((dst.Int != 0) || (src.Int != 0)) ? 1 : 0);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsShiftLeft()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int << src.Int);

        Step();
    }

    void BitsShiftRight()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int >> src.Int);

        Step();
    }

    void BitsOR()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int | src.Int);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsXOR()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int ^ src.Int);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsNOT()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        SetData(CurrentInstruction.Operand1, ~dst.Int);

        Step();
    }

    void BitsAND()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int & src.Int);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
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
            BitWidth._8 => new RuntimeValue((byte)(dst.Byte / src.Byte)),
            BitWidth._16 => new RuntimeValue((char)(dst.Char / src.Char)),
            BitWidth._32 => new RuntimeValue((int)(dst.Int / src.Int)),
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
            BitWidth._8 => new RuntimeValue((byte)(dst.Byte * src.Byte)),
            BitWidth._16 => new RuntimeValue((char)(dst.Char * src.Char)),
            BitWidth._32 => new RuntimeValue((int)(dst.Int * src.Int)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Registers.Flags.SetCarry(dst.Int, CurrentInstruction.BitWidth);

        Step();
    }

    void MathMod()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(dst.Byte / src.Byte)),
            BitWidth._16 => new RuntimeValue((char)(dst.Char / src.Char)),
            BitWidth._32 => new RuntimeValue((int)(dst.Int / src.Int)),
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
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single + src.Single));

        Step();
    }

    void FMathDiv()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single / src.Single));

        Step();
    }

    void FMathSub()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single - src.Single));

        Step();
    }

    void FMathMult()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single * src.Single));

        Step();
    }

    void FMathMod()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single % src.Single));

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
        SetData(CurrentInstruction.Operand1, new RuntimeValue((float)data.Int));

        Step();
    }

    void FFrom()
    {
        RuntimeValue data = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, (int)data.Single);

        Step();
    }

    #endregion

    #region External Calls

    void CALL_EXTERNAL()
    {
        int functionId = GetData(CurrentInstruction.Operand1).Int;

        if (!ExternalFunctions.TryGetValue(functionId, out ExternalFunctionBase? function))
        { throw new RuntimeException($"Undefined external function {functionId}"); }

        RuntimeValue[] parameters = new RuntimeValue[function.Parameters.Length];
        for (int i = 0; i < function.Parameters.Length; i++)
        {
            int memoryOffset = i * StackDirection;
            parameters[^(i + 1)] = Memory[Registers.StackPointer - memoryOffset];
        }

        if (function is ExternalFunctionManaged managedFunction)
        {
            managedFunction.OnReturn = (v) => Push(v);
            managedFunction.Callback(ImmutableArray.Create(parameters));
        }
        else if (function is ExternalFunctionSimple simpleFunction)
        {
            if (function.ReturnSomething)
            {
                RuntimeValue returnValue = simpleFunction.Call(this, ImmutableArray.Create(parameters));
                Push(returnValue);
            }
            else
            {
                simpleFunction.Call(this, ImmutableArray.Create(parameters));
            }
        }

        Step();
    }

    #endregion
}
