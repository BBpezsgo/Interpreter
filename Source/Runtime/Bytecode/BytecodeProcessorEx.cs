namespace LanguageCore.Runtime;

public class BytecodeProcessorEx
{
    public readonly CompiledDebugInformation DebugInformation;
    public readonly BytecodeProcessor Processor;

    public readonly IOHandler IO;
    double CurrentSleepFinishAt;

    readonly Queue<UserCall> UserCalls = new();
    UserCall? CurrentUserCall = null;

    public BytecodeProcessorEx(
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
        Processor = new BytecodeProcessor(program, memory, _externalFunctions.Select(v => new KeyValuePair<int, IExternalFunction>(v.Id, v)).ToFrozenDictionary(), settings);
    }

    List<IExternalFunction> GenerateExternalFunctions()
    {
        List<IExternalFunction> externalFunctions = new();

        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("sleep"), "sleep", (int t) => CurrentSleepFinishAt = DateTime.UtcNow.TimeOfDay.TotalSeconds + t));

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
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId("sleep"), "sleep", static (int t) => { }));
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

    public unsafe bool Tick()
    {
        if (CurrentSleepFinishAt != 0d)
        {
            if (CurrentSleepFinishAt < DateTime.UtcNow.TimeOfDay.TotalSeconds) return true;
            CurrentSleepFinishAt = 0d;
        }

        if (IO.IsAwaitingInput) return true;

        try
        {
            ProcessorState state = new(
                Processor.Settings,
                Processor.Registers,
                Processor.Memory,
                Processor.Code.AsSpan(),
                Processor.ExternalFunctions.Values.AsSpan(),
                default,
                default
            );

            state.Tick();

            if (state.IsDone && CurrentUserCall is not null)
            {
                state.Pop(CurrentUserCall.Arguments.Length);
                Span<byte> returnValue = state.Pop(CurrentUserCall.ReturnValueSize);
                CurrentUserCall.Result = returnValue.ToArray();
                CurrentUserCall = null;
            }

            if (state.IsDone && UserCalls.TryDequeue(out UserCall? userCall))
            {
                // Global variables are on top of the stack right now

                CurrentUserCall = userCall;

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

            switch (state.Signal)
            {
                case Signal.PointerOutOfRange:
                    throw new RuntimeException($"Pointer out of range ({state.Crash})")
                    {
                        Context = state.GetContext()
                    };
                case Signal.StackOverflow:
                    throw new RuntimeException($"Stack overflow")
                    {
                        Context = state.GetContext()
                    };
                case Signal.UndefinedExternalFunction:
                    throw new RuntimeException($"Undefined external function {state.Crash}")
                    {
                        Context = state.GetContext()
                    };
                case Signal.UserCrash:
                    throw new UserException(HeapUtils.GetString(Processor.Memory, state.Crash) ?? string.Empty)
                    {
                        Context = state.GetContext()
                    };
            }

            Processor.Registers = state.Registers;

            return !state.IsDone;
        }
        catch (RuntimeException runtimeException)
        {
            runtimeException.DebugInformation = DebugInformation;
            throw;
        }
    }

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

    #region Call

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
        UserCall userCall = CallUnsafe(function, arguments);

        while (userCall.Result is null)
        {
            if (!Tick())
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
