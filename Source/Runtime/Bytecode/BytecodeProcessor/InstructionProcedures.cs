#define _UNITY_PROFILER

namespace LanguageCore.Runtime;

public ref partial struct ProcessorState
{
#if UNITY_PROFILER
    static readonly Unity.Profiling.ProfilerMarker _ProcessMarkerDataMove = new("Processor.Instructions.DataMove");
    static readonly Unity.Profiling.ProfilerMarker _ProcessMarkerPush = new("Processor.Instructions.Push");
    static readonly Unity.Profiling.ProfilerMarker _ProcessMarkerPop = new("Processor.Instructions.Pop");
    static readonly Unity.Profiling.ProfilerMarker _ProcessMarkerMath = new("Processor.Instructions.Math");
    static readonly Unity.Profiling.ProfilerMarker _ProcessMarkerMathF = new("Processor.Instructions.MathF");
    static readonly Unity.Profiling.ProfilerMarker _ProcessMarkerLogic = new("Processor.Instructions.Logic");
    static readonly Unity.Profiling.ProfilerMarker _ProcessMarkerControlFlow = new("Processor.Instructions.ControlFlow");
    static readonly Unity.Profiling.ProfilerMarker _ProcessMarkerExternal = new("Processor.Instructions.External");
#endif

    #region Memory Operations

    void Move()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerDataMove.Auto();
#endif

        int value = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, value);

        Step();
    }

    #endregion

    #region Flow Control

    void CRASH()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        int pointer = GetData(CurrentInstruction.Operand1);
        Crash = pointer;
        Signal = Signal.UserCrash;
#if !UNITY_BURST
        string? value = HeapUtils.GetString(Memory, pointer);
        throw new UserException(value ?? string.Empty);
#endif
    }

    void CALL()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        int relativeAddress = GetData(CurrentInstruction.Operand1);

        Push(Registers.CodePointer, Register.CodePointer.BitWidth());

        Step(relativeAddress);
    }

    void RETURN()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        int codePointer = Pop(BitWidth._32);

        Registers.CodePointer = codePointer;
    }

    void JUMP_BY()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        int relativeAddress = GetData(CurrentInstruction.Operand1);

        Step(relativeAddress);
    }

    void EXIT()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        Registers.CodePointer = Code.Length;
        Signal = Signal.Halt;
    }

    void JumpIfEqual()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        if (Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfNotEqual()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        if (!Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfGreater()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        if ((!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))) && !Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfGreaterOrEqual()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        if (!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfLess()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        if (Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    void JumpIfLessOrEqual()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerControlFlow.Auto();
#endif

        if ((Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)) || Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1)); }
        else
        { Step(); }
    }

    #endregion

    #region Comparison Operations

    void Compare()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMath.Auto();
#endif

        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);

        ALU.SubtractI(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        Step();
    }

    void CompareF()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMathF.Auto();
#endif

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
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerLogic.Auto();
#endif

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
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerLogic.Auto();
#endif

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
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerLogic.Auto();
#endif

        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst << src);

        Step();
    }

    void BitsShiftRight()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerLogic.Auto();
#endif

        int dst = GetData(CurrentInstruction.Operand1);
        int src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst >> src);

        Step();
    }

    void BitsOR()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerLogic.Auto();
#endif

        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);
        a = ALU.BitwiseOr(a, b, ref Registers.Flags, CurrentInstruction.BitWidth);
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void BitsXOR()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerLogic.Auto();
#endif

        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);
        a = ALU.BitwiseXor(a, b, ref Registers.Flags, CurrentInstruction.BitWidth);
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void BitsNOT()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerLogic.Auto();
#endif

        int a = GetData(CurrentInstruction.Operand1);
        a = ALU.BitwiseNot(a, ref Registers.Flags, CurrentInstruction.BitWidth);
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void BitsAND()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerLogic.Auto();
#endif

        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);
        a = ALU.BitwiseAnd(a, b, ref Registers.Flags, CurrentInstruction.BitWidth);
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    #endregion

    #region Math Operations

    void MathAdd()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMath.Auto();
#endif

        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);

        a = ALU.AddI(a, b, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void MathDiv()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMath.Auto();
#endif

        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);

        a = ALU.DivideI(a, b, ref Registers.Flags, CurrentInstruction.BitWidth);
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void MathSub()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMath.Auto();
#endif

        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);

        a = ALU.SubtractI(a, b, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    void MathMult()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMath.Auto();
#endif

        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);

        a = ALU.MultiplyI(a, b, ref Registers.Flags, CurrentInstruction.BitWidth);
        SetData(CurrentInstruction.Operand1, a);

        Registers.Flags.SetCarry(a, CurrentInstruction.BitWidth);

        Step();
    }

    void MathMod()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMath.Auto();
#endif

        int a = GetData(CurrentInstruction.Operand1);
        int b = GetData(CurrentInstruction.Operand2);

        a = ALU.ModuloI(a, b, ref Registers.Flags, CurrentInstruction.BitWidth);
        SetData(CurrentInstruction.Operand1, a);

        Step();
    }

    #endregion

    #region Float Math Operations

    void FMathAdd()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMathF.Auto();
#endif

        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a + b).I32());

        Step();
    }

    void FMathDiv()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMathF.Auto();
#endif

        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a / b).I32());

        Step();
    }

    void FMathSub()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMathF.Auto();
#endif

        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a - b).I32());

        Step();
    }

    void FMathMult()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMathF.Auto();
#endif

        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a * b).I32());

        Step();
    }

    void FMathMod()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMathF.Auto();
#endif

        float a = GetData(CurrentInstruction.Operand1).F32();
        float b = GetData(CurrentInstruction.Operand2).F32();
        SetData(CurrentInstruction.Operand1, (a % b).I32());

        Step();
    }

    #endregion

    #region Stack Operations

    void PUSH_VALUE()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerPush.Auto();
#endif

        int v = GetData(CurrentInstruction.Operand1);
        Push(v, CurrentInstruction.Operand1.BitWidth);

        Step();
    }

    void POP_VALUE(BitWidth size)
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerPop.Auto();
#endif

        Pop(size);

        Step();
    }

    void POP_TO_VALUE(BitWidth size)
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerPop.Auto();
#endif

        int v = Pop(size);
        SetData(CurrentInstruction.Operand1, v);

        Step();
    }

    #endregion

    #region Utility Operations

    void FTo()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMathF.Auto();
#endif

        int data = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, ((float)data.I32()).I32());

        Step();
    }

    void FFrom()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerMathF.Auto();
#endif

        int data = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, (int)data.F32());

        Step();
    }

    #endregion

    #region External Calls

    unsafe void CALL_EXTERNAL()
    {
#if UNITY_PROFILER
        using Unity.Profiling.ProfilerMarker.AutoScope marker = _ProcessMarkerExternal.Auto();
#endif

        int functionId = GetData(CurrentInstruction.Operand1);

#if !UNITY_BURST
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
                    simpleFunction.MarshaledCallback(parameters, returnValue);
                    Push(returnValue);
                }
                else
                {
                    simpleFunction.MarshaledCallback(parameters, default);
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
#if UNITY_BURST
                fixed (byte* _parametersPtr = _parameters)
                fixed (byte* returnValuePtr = returnValue)
                {
                    scopedExternalFunction.Callback(scopedExternalFunction.Scope, (nint)_parametersPtr, (nint)returnValuePtr);
                }
#else
                scopedExternalFunction.Callback(scopedExternalFunction.Scope, _parameters, returnValue);
#endif
                Push(returnValue);
            }
            else
            {
#if UNITY_BURST
                fixed (byte* _parametersPtr = _parameters)
                {
                    scopedExternalFunction.Callback(scopedExternalFunction.Scope, (nint)_parametersPtr, default);
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
#if !UNITY_BURST
        throw new RuntimeException($"Undefined external function \"{functionId}\"");
#endif
    }

    #endregion
}
