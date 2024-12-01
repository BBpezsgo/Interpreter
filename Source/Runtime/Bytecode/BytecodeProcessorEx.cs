namespace LanguageCore.Runtime;

public class BytecodeProcessorEx
{
    public readonly CompiledDebugInformation DebugInformation;
    public readonly BytecodeProcessor Processor;

    public readonly IOHandler IO;
    double CurrentSleepFinishAt;

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

    public void Tick()
    {
        if (CurrentSleepFinishAt != 0d)
        {
            if (CurrentSleepFinishAt < DateTime.UtcNow.TimeOfDay.TotalSeconds) return;
            CurrentSleepFinishAt = 0d;
        }

        if (IO.IsAwaitingInput) return;

        try
        {
            Processor.Tick();
        }
        catch (RuntimeException runtimeException)
        {
            runtimeException.DebugInformation = DebugInformation;
            throw;
        }
    }
}
