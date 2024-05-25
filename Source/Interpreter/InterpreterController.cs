﻿namespace LanguageCore.Runtime;

using Compiler;

public readonly struct TimeSleep : IEquatable<TimeSleep>
{
    readonly double Timeout;
    readonly double Started;

    public TimeSleep(double timeout)
    {
        Timeout = timeout;
        Started = DateTime.UtcNow.TimeOfDay.TotalSeconds;
    }

    public bool Tick()
    {
        double now = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        return now - Started < Timeout;
    }

    public override bool Equals(object? obj) => obj is TimeSleep other && Equals(other);
    public bool Equals(TimeSleep other) => Timeout == other.Timeout && Started == other.Started;
    public override int GetHashCode() => HashCode.Combine(Timeout, Started);
    public static bool operator ==(TimeSleep left, TimeSleep right) => left.Equals(right);
    public static bool operator !=(TimeSleep left, TimeSleep right) => !left.Equals(right);
}

public class Interpreter
{
    public delegate void OnOutputEventHandler(Interpreter sender, string message, LogType logType);
    public delegate void OnStdErrorEventHandler(Interpreter sender, char data);
    public delegate void OnStdOutEventHandler(Interpreter sender, char data);
    public delegate void OnInputEventHandler(Interpreter sender);
    public delegate void OnExecutedEventHandler(Interpreter sender);

    public event OnOutputEventHandler? OnOutput;
    public event OnStdOutEventHandler? OnStdOut;
    public event OnStdErrorEventHandler? OnStdError;
    /// <summary>
    /// Will be invoked when the code needs input<br/>
    /// Call <see cref="OnInput(char)"/> after this invoked
    /// </summary>
    public event OnInputEventHandler? OnNeedInput;

    public readonly DebugInformation? DebugInformation;
    public Instruction? NextInstruction
    {
        get
        {
            if (BytecodeInterpreter.Registers.CodePointer < 0 || BytecodeInterpreter.Registers.CodePointer >= BytecodeInterpreter.Code.Length) return null;
            return BytecodeInterpreter.Code[BytecodeInterpreter.Registers.CodePointer];
        }
    }

    public readonly BytecodeProcessor BytecodeInterpreter;

    protected bool IsPaused;
    ExternalFunctionManaged? ReturnValueConsumer;

    readonly bool ThrowExceptions;

    TimeSleep CurrentSleep;

    public Interpreter(bool throwExceptions, BytecodeInterpreterSettings settings, ImmutableArray<Instruction> program, DebugInformation? debugInformation)
    {
        ThrowExceptions = throwExceptions;
        DebugInformation = debugInformation;

        BytecodeInterpreter = new BytecodeProcessor(program, GenerateExternalFunctions().ToFrozenDictionary(), settings);
    }

    /// <summary>
    /// Provides input to the interpreter<br/>
    /// <lv>WARNING:</lv> Call it only after <see cref="OnNeedInput"/> invoked!
    /// </summary>
    /// <param name="key">
    /// The input value
    /// </param>
    public void OnInput(char key)
    {
        if (ReturnValueConsumer != null)
        {
            ReturnValueConsumer.OnReturn?.Invoke(new RuntimeValue(key));
            ReturnValueConsumer = null;
        }

        IsPaused = false;
    }

    Dictionary<int, ExternalFunctionBase> GenerateExternalFunctions()
    {
        Dictionary<int, ExternalFunctionBase> externalFunctions = new();

        #region Console

        externalFunctions.AddManagedExternalFunction(ExternalFunctionNames.StdIn, ImmutableArray<RuntimeType>.Empty, (ImmutableArray<RuntimeValue> parameters, ExternalFunctionManaged function) =>
        {
            this.IsPaused = true;
            this.ReturnValueConsumer = function;
            if (this.OnNeedInput == null)
            {
                this.OnOutput?.Invoke(this, $"Event {OnNeedInput} does not have listeners", LogType.Warning);
                this.OnInput('\0');
            }
            else
            {
                this.OnNeedInput?.Invoke(this);
            }
        });

        externalFunctions.AddExternalFunction(ExternalFunctionNames.StdOut, (char @char) => OnStdOut?.Invoke(this, @char));

        externalFunctions.AddExternalFunction("console-set",
            (char @char, int x, int y) =>
            {
                if (x < 0 || y < 0) return;
                (int lx, int ly) = Console.GetCursorPosition();
                Console.SetCursorPosition(x, y);
                Console.Write(@char);
                Console.SetCursorPosition(lx, ly);
            });

        externalFunctions.AddExternalFunction("console-clear", Console.Clear);

        externalFunctions.AddExternalFunction("stderr", (char @char) => OnStdError?.Invoke(this, @char));

        externalFunctions.AddExternalFunction("sleep", (int t) => CurrentSleep = new TimeSleep(t));

        #endregion

        AddStaticExternalFunctions(externalFunctions);

        return externalFunctions;
    }

    public static Dictionary<int, ExternalFunctionBase> GetExternalFunctions()
    {
        Dictionary<int, ExternalFunctionBase> externalFunctions = new();

        AddRuntimeExternalFunctions(externalFunctions);

        AddStaticExternalFunctions(externalFunctions);

        return externalFunctions;
    }

    static void AddRuntimeExternalFunctions(Dictionary<int, ExternalFunctionBase> externalFunctions)
    {
        externalFunctions.AddManagedExternalFunction(ExternalFunctionNames.StdIn, ImmutableArray<RuntimeType>.Empty, (ImmutableArray<RuntimeValue> parameters, ExternalFunctionManaged function) => { });
        externalFunctions.AddExternalFunction(ExternalFunctionNames.StdOut, (char @char) => { });
        externalFunctions.AddExternalFunction("console-set", (char @char, int x, int y) => { });
        externalFunctions.AddExternalFunction("console-clear", () => { });
        externalFunctions.AddExternalFunction("stderr", (char @char) => { });
        externalFunctions.AddExternalFunction("sleep", (int t) => { });
    }

    static void AddStaticExternalFunctions(Dictionary<int, ExternalFunctionBase> externalFunctions)
    {
        externalFunctions.AddExternalFunction("utc-time", () => (int)DateTime.UtcNow.TimeOfDay.TotalMilliseconds);
        externalFunctions.AddExternalFunction("local-time", () => (int)DateTime.Now.TimeOfDay.TotalMilliseconds);
        externalFunctions.AddExternalFunction("utc-date-day", () => (int)DateTime.Now.DayOfYear);
        externalFunctions.AddExternalFunction("local-date-day", () => (int)DateTime.Now.DayOfYear);
        externalFunctions.AddExternalFunction("utc-date-year", () => (int)DateTime.Now.Year);
        externalFunctions.AddExternalFunction("local-date-year", () => (int)DateTime.Now.Year);
    }

    /// <exception cref="UserException"/>
    /// <exception cref="RuntimeException"/>
    /// <exception cref="Exception"/>
    public void Update()
    {
        if (CurrentSleep != default)
        {
            if (CurrentSleep.Tick())
            { return; }
            else
            { CurrentSleep = default; }
        }

        if (IsPaused) return;

        try
        {
            BytecodeInterpreter.Tick();
        }
        catch (UserException error)
        {
            if (DebugInformation is not null) error.FeedDebugInfo(DebugInformation);

            OnOutput?.Invoke(this, $"User Exception: {error}", LogType.Error);

            if (ThrowExceptions) throw;
            else BytecodeInterpreter.Registers.CodePointer = BytecodeInterpreter.Code.Length;
        }
        catch (RuntimeException error)
        {
            if (DebugInformation is not null) error.FeedDebugInfo(DebugInformation);

            OnOutput?.Invoke(this, $"Runtime Exception: {error}", LogType.Error);

            if (ThrowExceptions) throw;
            else BytecodeInterpreter.Registers.CodePointer = BytecodeInterpreter.Code.Length;
        }
        catch (Exception error)
        {
            OnOutput?.Invoke(this, $"Internal Exception: {new RuntimeException(error.Message, error, BytecodeInterpreter.GetContext())}", LogType.Error);

            if (ThrowExceptions) throw;
            else BytecodeInterpreter.Registers.CodePointer = BytecodeInterpreter.Code.Length;
        }
    }
}
