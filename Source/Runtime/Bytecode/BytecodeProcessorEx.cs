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
        if (externalFunctions is not null) _externalFunctions.AddRange(externalFunctions);
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
                CurrentUserCall = null;
            }

            if (state.IsDone && UserCalls.TryDequeue(out UserCall? userCall))
            {
                CurrentUserCall = userCall;

                state.Push(userCall.Arguments.AsSpan());

                // Push the return instruction address
                state.Push(state.Registers.CodePointer, Register.CodePointer.BitWidth());
                // Push the absolute global address
                state.Push(
                    state.Registers.StackPointer + // This points to the abs global address value
                    (int)Register.CodePointer.BitWidth() + // Offset it by the pushed return instruction address
                    (int)Register.StackPointer.BitWidth(), // Offset it by the abs global itself
                //  The final value is pointing to the first global variable's address
                    Register.StackPointer.BitWidth());
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

    public UserCall Call(int instructionOffset, ImmutableArray<byte> arguments)
    {
        UserCall userCall = new(instructionOffset, arguments);
        UserCalls.Enqueue(userCall);
        return userCall;
    }
}
