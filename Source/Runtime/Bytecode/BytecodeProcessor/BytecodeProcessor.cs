namespace LanguageCore.Runtime;

public class BytecodeProcessor
{
    public Instruction? NextInstruction
    {
        get
        {
            if (Registers.CodePointer < 0 || Registers.CodePointer >= Code.Length) return null;
            return Code[Registers.CodePointer];
        }
    }
    public bool IsDone => Registers.CodePointer == Code.Length;
    public int StackStart => ProcessorState.StackDirection > 0 ? Settings.HeapSize : Settings.HeapSize + Settings.StackSize - 1;

    public readonly CompiledDebugInformation DebugInformation;
    public readonly BytecodeInterpreterSettings Settings;

    public Registers Registers;
    public readonly byte[] Memory;
    public readonly ImmutableArray<Instruction> Code;
    public readonly FrozenDictionary<int, IExternalFunction> ExternalFunctions;

    public readonly IOHandler IO;
    readonly Queue<UserCall> UserCalls = new();
    UserCall? CurrentUserCall = null;
    bool CurrentlySyncUserCalling = false;

    public BytecodeProcessor(
        BytecodeInterpreterSettings settings,
        ImmutableArray<Instruction> program,
        byte[]? memory,
        DebugInformation? debugInformation = null,
        IEnumerable<IExternalFunction>? externalFunctions = null)
    {
        DebugInformation = debugInformation is null ? default : new CompiledDebugInformation(debugInformation);

        List<IExternalFunction> _externalFunctions = GenerateExternalFunctions();
        IO = IOHandler.Create(_externalFunctions);
        foreach (IExternalFunction item in externalFunctions ?? Enumerable.Empty<IExternalFunction>())
        {
            if (_externalFunctions.Any(v => v.Id == item.Id)) continue;
            _externalFunctions.Add(item);
        }

        Settings = settings;
        ExternalFunctions = _externalFunctions.Select(v => new KeyValuePair<int, IExternalFunction>(v.Id, v)).ToFrozenDictionary();
        Code = program;
        Memory = memory ?? new byte[settings.HeapSize + settings.StackSize];
        Registers.StackPointer = StackStart - ProcessorState.StackDirection;
    }

    public unsafe ProcessorState GetState() => new(
        Settings,
        Registers,
        Memory,
        Code.AsSpan(),
        ExternalFunctions.Values.AsSpan(),
        default,
        default
    );

    public unsafe bool Tick()
    {
        ProcessorState state = GetState();
        return Tick(ref state);
    }

    public unsafe void RunUntilCompletion()
    {
        ProcessorState state = GetState();
        while (Tick(ref state)) { }
    }

    public unsafe void RunUntilCompletion(ref ProcessorState state)
    {
        while (Tick(ref state)) { }
    }

    /// <returns>
    /// Returns <c>false</c> if it is finished, or <c>true</c> otherwise;
    /// </returns>
    public unsafe bool Tick(ref ProcessorState state)
    {
        if (IO.IsAwaitingInput) return true;

        try
        {
            state.Tick();
            HandleUserCalls(ref state);

            Registers = state.Registers;

            state.ThrowIfCrashed(DebugInformation);
            return !state.IsDone;
        }
        catch (RuntimeException runtimeException)
        {
            runtimeException.DebugInformation = DebugInformation;
            throw;
        }
    }

    public unsafe void HandleUserCalls(ref ProcessorState state)
    {
        if (!state.IsDone || CurrentlySyncUserCalling) return;

        if (CurrentUserCall is not null)
        {
            FinishUserCall(ref state, CurrentUserCall);
            CurrentUserCall = null;
        }

        if (UserCalls.TryDequeue(out UserCall? userCall))
        {
            CurrentUserCall = userCall;
            BeginUserCall(ref state, userCall);

        }
    }

    unsafe void FinishUserCall(ref ProcessorState state, UserCall userCall)
    {
        state.Pop(userCall.Arguments.Length);
        Span<byte> returnValue = state.Pop(userCall.ReturnValueSize);
        userCall.Result = returnValue.IsEmpty ? Array.Empty<byte>() : returnValue.ToArray();
    }

    unsafe void FinishUserCall(ref ProcessorState state, ref UserCallSync userCall)
    {
        state.Pop(userCall.Arguments.Length);
        Span<byte> returnValue = state.Pop(userCall.ReturnValueSize);
        userCall.Result = returnValue.IsEmpty ? Array.Empty<byte>() : returnValue.ToArray();
    }

    unsafe void BeginUserCall(ref ProcessorState state, UserCall userCall)
    {
        // Global variables are on top of the stack right now

        // this is pointing to the last global variable's address
        int globalVariablesAddress = state.Registers.StackPointer;

        state.Registers.StackPointer -= userCall.ReturnValueSize;

        state.Push(userCall.Arguments.AsSpan());

        // Push the return instruction address
        state.Push(state.Registers.CodePointer, Register.CodePointer.BitWidth());

        // Push the absolute global address
        state.Push(globalVariablesAddress, Register.StackPointer.BitWidth());
        // Push the previous base pointer
        state.Push(state.Registers.BasePointer, Register.BasePointer.BitWidth());

        state.Registers.BasePointer = state.Registers.StackPointer;
        state.Registers.CodePointer = userCall.InstructionOffset;
    }

    unsafe void BeginUserCall(ref ProcessorState state, ref UserCallSync userCall)
    {
        // Global variables are on top of the stack right now

        // this is pointing to the last global variable's address
        int globalVariablesAddress = state.Registers.StackPointer;

        state.Registers.StackPointer -= userCall.ReturnValueSize;

        state.Push(userCall.Arguments);

        // Push the return instruction address
        state.Push(state.Registers.CodePointer, Register.CodePointer.BitWidth());

        // Push the absolute global address
        state.Push(globalVariablesAddress, Register.StackPointer.BitWidth());
        // Push the previous base pointer
        state.Push(state.Registers.BasePointer, Register.BasePointer.BitWidth());

        state.Registers.BasePointer = state.Registers.StackPointer;
        state.Registers.CodePointer = userCall.InstructionOffset;
    }

    static List<IExternalFunction> GenerateExternalFunctions()
    {
        List<IExternalFunction> externalFunctions = new();

        AddStaticExternalFunctions(externalFunctions);

        return externalFunctions;
    }

    public static List<IExternalFunction> GetExternalFunctions()
    {
        List<IExternalFunction> externalFunctions = new();

        AddRuntimeExternalFunctions(externalFunctions);

        AddStaticExternalFunctions(externalFunctions);

        return externalFunctions;
    }

    static void AddRuntimeExternalFunctions(List<IExternalFunction> externalFunctions)
    {
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdIn), ExternalFunctionNames.StdIn, static () => '\0'));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdOut), ExternalFunctionNames.StdOut, static (char @char) => { }));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("console-set"), "console-set", static (char @char, int x, int y) => { }));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("console-clear"), "console-clear", static () => { }));
    }

    static void AddStaticExternalFunctions(List<IExternalFunction> externalFunctions)
    {
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("utc-time"), "utc-time", static () => (int)DateTime.UtcNow.TimeOfDay.TotalMilliseconds));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("local-time"), "local-time", static () => (int)DateTime.Now.TimeOfDay.TotalMilliseconds));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("utc-date-day"), "utc-date-day", static () => (int)DateTime.UtcNow.DayOfYear));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("local-date-day"), "local-date-day", static () => (int)DateTime.Now.DayOfYear));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("utc-date-year"), "utc-date-year", static () => (int)DateTime.UtcNow.Year));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("local-date-year"), "local-date-year", static () => (int)DateTime.Now.Year));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create<float, float, float>(externalFunctions.GenerateId("atan2"), "atan2", MathF.Atan2));
    }

    public bool ResolveAddress(InstructionOperand operand, out int address)
    {
        switch (operand.Type)
        {
            case InstructionOperandType.Pointer8:
            case InstructionOperandType.Pointer16:
            case InstructionOperandType.Pointer32:
                address = operand.Int;
                return true;
            case InstructionOperandType.PointerBP8:
            case InstructionOperandType.PointerBP16:
            case InstructionOperandType.PointerBP32:
            case InstructionOperandType.PointerBP64:
                address = Registers.BasePointer + operand.Int;
                return true;
            case InstructionOperandType.PointerSP8:
            case InstructionOperandType.PointerSP16:
            case InstructionOperandType.PointerSP32:
                address = Registers.StackPointer + operand.Int;
                return true;
            case InstructionOperandType.PointerEAX8:
            case InstructionOperandType.PointerEAX16:
            case InstructionOperandType.PointerEAX32:
            case InstructionOperandType.PointerEAX64:
                address = Registers.EAX + operand.Int;
                return true;
            case InstructionOperandType.PointerEBX8:
            case InstructionOperandType.PointerEBX16:
            case InstructionOperandType.PointerEBX32:
            case InstructionOperandType.PointerEBX64:
                address = Registers.EBX + operand.Int;
                return true;
            case InstructionOperandType.PointerECX8:
            case InstructionOperandType.PointerECX16:
            case InstructionOperandType.PointerECX32:
            case InstructionOperandType.PointerECX64:
                address = Registers.ECX + operand.Int;
                return true;
            case InstructionOperandType.PointerEDX8:
            case InstructionOperandType.PointerEDX16:
            case InstructionOperandType.PointerEDX32:
            case InstructionOperandType.PointerEDX64:
                address = Registers.EDX + operand.Int;
                return true;

            // NOTE: There is no R_X registers so I used E_X
            case InstructionOperandType.PointerRAX8:
            case InstructionOperandType.PointerRAX16:
            case InstructionOperandType.PointerRAX32:
            case InstructionOperandType.PointerRAX64:
                address = Registers.EAX + operand.Int;
                return true;
            case InstructionOperandType.PointerRBX8:
            case InstructionOperandType.PointerRBX16:
            case InstructionOperandType.PointerRBX32:
            case InstructionOperandType.PointerRBX64:
                address = Registers.EBX + operand.Int;
                return true;
            case InstructionOperandType.PointerRCX8:
            case InstructionOperandType.PointerRCX16:
            case InstructionOperandType.PointerRCX32:
            case InstructionOperandType.PointerRCX64:
                address = Registers.ECX + operand.Int;
                return true;
            case InstructionOperandType.PointerRDX8:
            case InstructionOperandType.PointerRDX16:
            case InstructionOperandType.PointerRDX32:
            case InstructionOperandType.PointerRDX64:
                address = Registers.EDX + operand.Int;
                return true;
            default:
                address = default;
                return false;
        }
    }

    public RuntimeContext GetContext() => new(
        Registers,
        ImmutableArray.Create(Memory),
        Code,
        StackStart
    );

    #region Call

    public UserCall Call(in ExposedFunction function)
        => CallUnsafe(function, Array.Empty<byte>());

    public UserCall Call<T0>(in ExposedFunction function, T0 arg0)
        where T0 : unmanaged
        => CallUnsafe(function, MemoryUtils.ToBytes(PackedValues.Create(arg0)));

    public UserCall Call<T0, T1>(in ExposedFunction function, T0 arg0, T1 arg1)
        where T0 : unmanaged
        where T1 : unmanaged
        => CallUnsafe(function, MemoryUtils.ToBytes(PackedValues.Create(arg1, arg0)));

    public UserCall Call<T0, T1, T2>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => CallUnsafe(function, MemoryUtils.ToBytes(PackedValues.Create(arg2, arg1, arg0)));

    public UserCall Call<T0, T1, T2, T3>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => CallUnsafe(function, MemoryUtils.ToBytes(PackedValues.Create(arg3, arg2, arg1, arg0)));

    public UserCall Call<T0, T1, T2, T3, T4>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => CallUnsafe(function, MemoryUtils.ToBytes(PackedValues.Create(arg4, arg3, arg2, arg1, arg0)));

    public UserCall Call<T0, T1, T2, T3, T4, T5>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => CallUnsafe(function, MemoryUtils.ToBytes(PackedValues.Create(arg5, arg4, arg3, arg2, arg1, arg0)));

    public UserCall Call<T0, T1, T2, T3, T4, T5, T6>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
        => CallUnsafe(function, MemoryUtils.ToBytes(PackedValues.Create(arg6, arg5, arg4, arg3, arg2, arg1, arg0)));

    #endregion

    #region CallSync

    public byte[] CallSync(in ExposedFunction function)
        => CallUnsafeSync(function, Array.Empty<byte>());

    public byte[] CallSync<T0>(in ExposedFunction function, T0 arg0)
        where T0 : unmanaged
        => CallUnsafeSync(function, MemoryUtils.ToBytes(PackedValues.Create(arg0)));

    public byte[] CallSync<T0, T1>(in ExposedFunction function, T0 arg0, T1 arg1)
        where T0 : unmanaged
        where T1 : unmanaged
        => CallUnsafeSync(function, MemoryUtils.ToBytes(PackedValues.Create(arg1, arg0)));

    public byte[] CallSync<T0, T1, T2>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => CallUnsafeSync(function, MemoryUtils.ToBytes(PackedValues.Create(arg2, arg1, arg0)));

    public byte[] CallSync<T0, T1, T2, T3>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => CallUnsafeSync(function, MemoryUtils.ToBytes(PackedValues.Create(arg3, arg2, arg1, arg0)));

    public byte[] CallSync<T0, T1, T2, T3, T4>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => CallUnsafeSync(function, MemoryUtils.ToBytes(PackedValues.Create(arg4, arg3, arg2, arg1, arg0)));

    public byte[] CallSync<T0, T1, T2, T3, T4, T5>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => CallUnsafeSync(function, MemoryUtils.ToBytes(PackedValues.Create(arg5, arg4, arg3, arg2, arg1, arg0)));

    public byte[] CallSync<T0, T1, T2, T3, T4, T5, T6>(in ExposedFunction function, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
        => CallUnsafeSync(function, MemoryUtils.ToBytes(PackedValues.Create(arg6, arg5, arg4, arg3, arg2, arg1, arg0)));

    #endregion

    public UserCall CallUnsafe(in ExposedFunction function, byte[] arguments)
    {
        if (function.ArgumentsSize != arguments.Length) throw new ArgumentException($"Invalid number of bytes passed to exposed function \"{function.Identifier}\": expected {function.ArgumentsSize} passed {arguments.Length}");
        UserCall userCall = new(function.InstructionOffset, arguments, function.ReturnValueSize);
        UserCalls.Enqueue(userCall);
        return userCall;
    }

    public byte[] CallUnsafeSync(in ExposedFunction function, byte[] arguments)
    {
        if (function.ArgumentsSize != arguments.Length) throw new ArgumentException($"Invalid number of bytes passed to exposed function \"{function.Identifier}\": expected {function.ArgumentsSize} passed {arguments.Length}");

        ProcessorState state = GetState();
        RunUntilCompletion(ref state);

        UserCallSync userCall = new(function.InstructionOffset, arguments, function.ReturnValueSize);
        BeginUserCall(ref state, ref userCall);
        CurrentlySyncUserCalling = true;

        while (userCall.Result is null)
        {
            Tick(ref state);

            if (state.IsDone)
            {
                FinishUserCall(ref state, ref userCall);
                CurrentlySyncUserCalling = false;
            }

            if (userCall.Result is null && state.IsDone)
            { throw new RuntimeException($"Failed to execute the user call for some reason..."); }
        }

        return userCall.Result;
    }

    public UserCall CallUnsafe(int instructionOffset, byte[] arguments, int returnValueSize)
    {
        UserCall userCall = new(instructionOffset, arguments, returnValueSize);
        UserCalls.Enqueue(userCall);
        return userCall;
    }
}
