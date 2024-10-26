namespace LanguageCore.Runtime;

public ref partial struct ProcessorState
{
    #region Memory Operations

    void Move()
    {
        int value = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, value);

        Step();
    }

    #endregion

    #region Flow Control

    void CRASH()
    {
        int pointer = GetData(CurrentInstruction.Operand1);
        Crash = pointer;
        Signal = Signal.UserCrash;
#if !UNITY
        string? value = HeapUtils.GetString(Memory, pointer);
        throw new UserException(value ?? string.Empty);
#endif
    }

    void CALL()
    {
        int relativeAddress = GetData(CurrentInstruction.Operand1);

        Push(Registers.CodePointer, Register.CodePointer.BitWidth());

        Step(relativeAddress);
    }

    void RETURN()
    {
        int codePointer = Pop(BitWidth._32);

        Registers.CodePointer = codePointer;
    }

    void JUMP_BY()
    {
        int relativeAddress = GetData(CurrentInstruction.Operand1);

        Step(relativeAddress);
    }

    void EXIT()
    {
        Registers.CodePointer = Code.Length;
        Signal = Signal.Halt;
    }

    void JumpIfEqual()
    {
        if (Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfNotEqual()
    {
        if (!Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfGreater()
    {
        if ((!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))) && !Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfGreaterOrEqual()
    {
        if (!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfLess()
    {
        if (Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfLessOrEqual()
    {
        if ((Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)) || Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    #endregion

    #region Comparison Operations

    void Compare()
    {
        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);

        ALU.SubtractI(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        Step();
    }

    void CompareF()
    {
        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();

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
        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, ((dst != 0) && (src != 0)) ? 1 : 0);

        Registers.Flags.SetSign(dst, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void LogicOR()
    {
        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, ((dst != 0) || (src != 0)) ? 1 : 0);

        Registers.Flags.SetSign(dst, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsShiftLeft()
    {
        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst << src);

        Step();
    }

    void BitsShiftRight()
    {
        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst >> src);

        Step();
    }

    void BitsOR()
    {
        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst | src);

        Registers.Flags.SetSign(dst, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsXOR()
    {
        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst ^ src);

        Registers.Flags.SetSign(dst, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsNOT()
    {
        int dst = GetData(CurrentInstruction.Operand1);
        SetData(CurrentInstruction.Operand1, ~dst);

        Step();
    }

    void BitsAND()
    {
        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst & src);

        Registers.Flags.SetSign(dst, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    #endregion

    #region Math Operations

    void MathAdd()
    {
        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);

        a = ALU.AddI(a, b, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void MathDiv()
    {
        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);

        a = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => ((byte)(a.U8() / b.U8())).I32(),
            BitWidth._16 => ((char)(a.U16() / b.U16())).I32(),
            BitWidth._32 => ((int)(a.I32() / b.I32())).I32(),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void MathSub()
    {
        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);

        a = ALU.SubtractI(a, b, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void MathMult()
    {
        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);

        a = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => ((byte)(a.U8() * b.U8())).I32(),
            BitWidth._16 => ((char)(a.U16() * b.U16())).I32(),
            BitWidth._32 => ((int)(a.I32() * b.I32())).I32(),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, a);

        Registers.Flags.SetCarry(a, CurrentInstruction.BitWidth);

        Step();
    }

    void MathMod()
    {
        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => ((byte)(dst.U8() % src.U8())).I32(),
            BitWidth._16 => ((char)(dst.U16() % src.U16())).I32(),
            BitWidth._32 => ((int)(dst.I32() % src.I32())).I32(),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    #endregion

    #region Float Math Operations

    void FMathAdd()
    {
        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a + b).I32());

        Step();
    }

    void FMathDiv()
    {
        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a / b).I32());

        Step();
    }

    void FMathSub()
    {
        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a - b).I32());

        Step();
    }

    void FMathMult()
    {
        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a * b).I32());

        Step();
    }

    void FMathMod()
    {
        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a % b).I32());

        Step();
    }

    #endregion

    #region Stack Operations

    void PUSH_VALUE()
    {
        int v = GetData(CurrentInstruction.Operand1);
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
        int v = Pop(size);
        SetData(CurrentInstruction.Operand1, v);

        Step();
    }

    #endregion

    #region Utility Operations

    void FTo()
    {
        int data = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, ((float)data.I32()).I32());

        Step();
    }

    void FFrom()
    {
        int data = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, (int)data.F32());

        Step();
    }

    #endregion

    #region External Calls

    unsafe void CALL_EXTERNAL()
    {
        int functionId = GetData(CurrentInstruction.Operand1);

#if !UNITY
        IExternalFunction? function = null;

        for (int i = 0; i < ExternalFunctions.Length; i++)
        {
            if (ExternalFunctions[i].Id != functionId) continue;
            function = ExternalFunctions[i];
            break;
        }

        if (function is not null)
        {
            Span<byte> parameters = Memory.Slice(Registers.StackPointer, function.ParametersSize);

            if (function is ExternalFunctionAsync managedFunction)
            {
                PendingExternalFunction = managedFunction.Callback(
                    ref this,
                    parameters);
            }
            else if (function is ExternalFunctionSync simpleFunction)
            {
                if (function.ReturnValueSize > 0)
                {
                    Span<byte> returnValue = stackalloc byte[function.ReturnValueSize];
                    simpleFunction.Callback(parameters, returnValue);
                    Push(returnValue);
                }
                else
                {
                    simpleFunction.Callback(parameters, default);
                }
            }

            Step();
            return;
        }
#endif

        for (int i = 0; i < ScopedExternalFunctionsCount; i++)
        {
            ref readonly ExternalFunctionScopedSync scopedExternalFunction = ref ScopedExternalFunctions[i];
            if (scopedExternalFunction.Id != functionId) continue;

            Span<byte> _parameters = Memory.Slice(Registers.StackPointer, scopedExternalFunction.ParametersSize);

            if (scopedExternalFunction.ReturnValueSize > 0)
            {
                Span<byte> returnValue = stackalloc byte[scopedExternalFunction.ReturnValueSize];
#if UNITY
                fixed (byte* _parametersPtr = _parameters)
                fixed (byte* returnValuePtr = returnValue)
                {
                    scopedExternalFunction.Callback.Invoke(scopedExternalFunction.Scope, (nint)_parametersPtr, (nint)returnValuePtr);
                }
#else
                scopedExternalFunction.Callback(scopedExternalFunction.Scope, _parameters, returnValue);
#endif
                Push(returnValue);
            }
            else
            {
#if UNITY
                fixed (byte* _parametersPtr = _parameters)
                {
                    scopedExternalFunction.Callback.Invoke(scopedExternalFunction.Scope, (nint)_parametersPtr, default);
                }
#else
                scopedExternalFunction.Callback(scopedExternalFunction.Scope, _parameters, default);
#endif
            }

            Step();
            return;
        }

        Crash = functionId;
        Signal = Signal.UndefinedExternalFunction;
#if !UNITY
        throw new RuntimeException($"Undefined external function {functionId}");
#endif
    }

    #endregion
}
