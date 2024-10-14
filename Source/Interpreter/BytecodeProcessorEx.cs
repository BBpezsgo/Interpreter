namespace LanguageCore.Runtime;

public class BytecodeProcessorEx
{
    public readonly DebugInformation? DebugInformation;
    public readonly BytecodeProcessor Processor;

    public readonly IOHandler IO;
    double CurrentSleepFinishAt;

    public BytecodeProcessorEx(
        BytecodeInterpreterSettings settings,
        ImmutableArray<Instruction> program,
        byte[]? memory,
        DebugInformation? debugInformation = null,
        IEnumerable<KeyValuePair<int, IExternalFunction>>? externalFunctions = null)
    {
        DebugInformation = debugInformation;

        Dictionary<int, IExternalFunction> _externalFunctions = GenerateExternalFunctions();
        IO = IOHandler.Create(_externalFunctions);
        if (externalFunctions is not null) _externalFunctions.AddRange(externalFunctions);
        Processor = new BytecodeProcessor(program, memory, _externalFunctions.ToFrozenDictionary(), settings);
    }

    Dictionary<int, IExternalFunction> GenerateExternalFunctions()
    {
        Dictionary<int, IExternalFunction> externalFunctions = new();

        externalFunctions.AddExternalFunction("sleep", (int t) => CurrentSleepFinishAt = DateTime.UtcNow.TimeOfDay.TotalSeconds + t);

        AddStaticExternalFunctions(externalFunctions);

        return externalFunctions;
    }

    public static Dictionary<int, IExternalFunction> GetExternalFunctions()
    {
        Dictionary<int, IExternalFunction> externalFunctions = new();

        AddRuntimeExternalFunctions(externalFunctions);

        AddStaticExternalFunctions(externalFunctions);

        return externalFunctions;
    }

    static void AddRuntimeExternalFunctions(Dictionary<int, IExternalFunction> externalFunctions)
    {
        externalFunctions.AddExternalFunction(ExternalFunctionNames.StdIn, static () => '\0');
        externalFunctions.AddExternalFunction(ExternalFunctionNames.StdOut, static (char @char) => { });
        externalFunctions.AddExternalFunction("console-set", static (char @char, int x, int y) => { });
        externalFunctions.AddExternalFunction("console-clear", static () => { });
        externalFunctions.AddExternalFunction("sleep", static (int t) => { });
    }

    static void AddStaticExternalFunctions(Dictionary<int, IExternalFunction> externalFunctions)
    {
        externalFunctions.AddExternalFunction("utc-time", static () => (int)DateTime.UtcNow.TimeOfDay.TotalMilliseconds);
        externalFunctions.AddExternalFunction("local-time", static () => (int)DateTime.Now.TimeOfDay.TotalMilliseconds);
        externalFunctions.AddExternalFunction("utc-date-day", static () => (int)DateTime.UtcNow.DayOfYear);
        externalFunctions.AddExternalFunction("local-date-day", static () => (int)DateTime.Now.DayOfYear);
        externalFunctions.AddExternalFunction("utc-date-year", static () => (int)DateTime.UtcNow.Year);
        externalFunctions.AddExternalFunction("local-date-year", static () => (int)DateTime.Now.Year);
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
