namespace LanguageCore;

using Runtime;

[ExcludeFromCodeCoverage]
public struct ProgramArguments
{
    [MemberNotNullWhen(false, nameof(File))]
    public readonly bool IsEmpty => File is null;

    public Uri? File;

    public Compiler.CompilerSettings CompilerSettings;
    public BBLang.Generator.MainGeneratorSettings MainGeneratorSettings;
    public Brainfuck.Generator.BrainfuckGeneratorSettings BrainfuckGeneratorSettings;
    public BytecodeInterpreterSettings BytecodeInterpreterSettings;

    public bool ThrowErrors;

    public LogType LogFlags;

    public ProgramRunType RunType;

    public bool ConsoleGUI;

    public bool DoNotPause;
    public bool ShowProgress;

    public string? OutputFile;

    public PrintFlags PrintFlags;

    public static ProgramArguments Default => new()
    {
        ThrowErrors = false,
        LogFlags = LogType.Debug | LogType.Normal | LogType.Warning | LogType.Error,
        RunType = ProgramRunType.Normal,
        CompilerSettings = Compiler.CompilerSettings.Default,
        MainGeneratorSettings = BBLang.Generator.MainGeneratorSettings.Default,
        BrainfuckGeneratorSettings = Brainfuck.Generator.BrainfuckGeneratorSettings.Default,
        BytecodeInterpreterSettings = BytecodeInterpreterSettings.Default,
        ConsoleGUI = false,
        DoNotPause = false,
        File = null,
        ShowProgress = false,
        OutputFile = null,
        PrintFlags = PrintFlags.None,
    };
}

public enum ProgramRunType
{
    Normal,
    Brainfuck,
    ASM,
}

[Flags]
public enum PrintFlags
{
    None = 0x00,
    Final = 0x01,
    Commented = 0x02,
    Simplified = 0x04,
    Heap = 0x08,
}

[ExcludeFromCodeCoverage]
public static class ArgumentNormalizer
{
    enum NormalizerState
    {
        None,
        String,
    }

    public static string[] NormalizeArgs(params string[] args) => NormalizeArgs(string.Join(' ', args));
    public static string[] NormalizeArgs(string args)
    {
        List<string> result = new();

        NormalizerState state = NormalizerState.None;
        StringBuilder currentArg = new();

        void FinishArgument()
        {
            state = NormalizerState.None;

            if (currentArg.Length == 0) return;

            result.Add(currentArg.ToString());
            currentArg.Clear();
        }

        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];

            switch (c)
            {
                case '"':
                    if (state == NormalizerState.String)
                    {
                        FinishArgument();
                        break;
                    }
                    state = NormalizerState.String;
                    break;
                case '\t':
                case ' ':
                    if (state == NormalizerState.String)
                    {
                        currentArg.Append(c);
                        break;
                    }
                    FinishArgument();
                    break;
                default:
                    currentArg.Append(c);
                    break;
            }
        }

        FinishArgument();

        return result.ToArray();
    }
}

[ExcludeFromCodeCoverage]
public static class ArgumentParser
{
    class ArgumentsSource
    {
        readonly ImmutableArray<string> Arguments;
        int Index;

        public bool Has => Index < Arguments.Length - 1;
        public object Current => Arguments[Index];

        public ArgumentsSource(IEnumerable<string> args)
        {
            Arguments = args.ToImmutableArray();
            Index = 0;
        }

        public bool TryConsume([NotNullWhen(true)] out string? result, params string[] name)
        {
            result = null;

            if (Index >= Arguments.Length - 1)
            { return false; }

            for (int j = 0; j < name.Length; j++)
            {
                if (Arguments[Index] == name[j])
                {
                    result = Arguments[Index];
                    Index++;
                    return true;
                }
            }

            return false;
        }

        public bool TryConsume(out int result)
        {
            result = default;

            if (Index >= Arguments.Length - 1)
            { return false; }

            if (!int.TryParse(Arguments[Index], out int @int))
            { return false; }

            Index++;
            result = @int;
            return true;
        }

        public bool TryConsume([NotNullWhen(true)] out string? result)
        {
            result = default;

            if (Index >= Arguments.Length - 1)
            { return false; }

            result = Arguments[Index];
            Index++;
            return true;
        }

        public IEnumerable<string> TryConsumeAll(params string[] words)
        {
            while (true)
            {
                if (TryConsume(out string? result, words))
                { yield return result; }
                else
                { break; }
            }
        }
    }

    static ProgramArguments ParseInternal(string[] args)
    {
        ProgramArguments result = ProgramArguments.Default;
        ArgumentsSource _args = new(args);
#pragma warning disable IDE0018 // Inline variable declaration
        string? arg;
#pragma warning restore IDE0018

        if (args.Length > 1)
        {
            while (_args.Has)
            {
                if (_args.TryConsume(out _, "--show-progress", "-sp"))
                {
                    result.ShowProgress = true;
                    continue;
                }

                if (_args.TryConsume(out _, "--no-nullcheck", "-nn"))
                {
                    result.MainGeneratorSettings.CheckNullPointers = false;
                    continue;
                }

                if (_args.TryConsume(out _, "--no-pause", "-np"))
                {
                    result.DoNotPause = true;
                    continue;
                }

                if (_args.TryConsume(out arg, "--heap-size", "-hs"))
                {
                    if (!_args.TryConsume(out int heapSize))
                    { throw new ArgumentException($"Expected number value after argument \"{arg}\""); }

                    result.BytecodeInterpreterSettings.HeapSize = heapSize;
                    result.BrainfuckGeneratorSettings.HeapSize = heapSize;

                    continue;
                }

                if (_args.TryConsume(out _, "--brainfuck", "-bf"))
                {
                    if (result.RunType != ProgramRunType.Normal)
                    { throw new ArgumentException($"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {ProgramRunType.Brainfuck}"); }

                    result.RunType = ProgramRunType.Brainfuck;
                    continue;
                }

                if (_args.TryConsume(out _, "--asm"))
                {
                    if (result.RunType != ProgramRunType.Normal)
                    { throw new ArgumentException($"The \"RunType\" is already defined ({result.RunType}), but you tried to set it to {ProgramRunType.ASM}"); }

                    result.RunType = ProgramRunType.ASM;
                    continue;
                }

                if (_args.TryConsume(out _, "--console-gui", "-cg"))
                {
                    result.ConsoleGUI = true;
                    continue;
                }

                if (_args.TryConsume(out arg, "--basepath", "-bp"))
                {
                    if (!_args.TryConsume(out result.CompilerSettings.BasePath))
                    { throw new ArgumentException($"Expected string value after argument \"{arg}\""); }

                    continue;
                }

                if (_args.TryConsume(out arg, "--output", "-o"))
                {
                    if (!_args.TryConsume(out result.OutputFile))
                    { throw new ArgumentException($"Expected string value after argument \"{arg}\""); }

                    continue;
                }

                if (_args.TryConsume(out _, "--throw-errors", "-te"))
                {
                    result.ThrowErrors = true;
                    continue;
                }

                if (_args.TryConsume(out _, "--hide-debug", "-hd"))
                {
                    result.LogFlags &= ~LogType.Debug;
                    continue;
                }

                if (_args.TryConsume(out _, "--hide-warning", "--hide-warnings", "-hw"))
                {
                    result.LogFlags &= ~LogType.Warning;
                    continue;
                }

                if (_args.TryConsume(out _, "--hide-info", "-hi"))
                {
                    result.LogFlags &= ~LogType.Normal;
                    continue;
                }

                if (_args.TryConsume(out _, "--dont-optimize", "-do"))
                {
                    result.MainGeneratorSettings.DontOptimize = true;
                    result.BrainfuckGeneratorSettings.DontOptimize = true;
                    continue;
                }

                if (_args.TryConsume(out _, "--no-debug-info", "-ndi"))
                {
                    result.MainGeneratorSettings.GenerateDebugInstructions = false;
                    result.BrainfuckGeneratorSettings.GenerateDebugInformation = false;
                    continue;
                }

                if (_args.TryConsume(out arg, "--stack-size", "-ss"))
                {
                    if (!_args.TryConsume(out int value))
                    { throw new ArgumentException($"Expected int value after argument \"{arg}\""); }

                    result.BytecodeInterpreterSettings.StackSize = value;
                    result.BrainfuckGeneratorSettings.StackSize = value;

                    continue;
                }

                if (_args.TryConsume(out _, "--print-heap", "-ph"))
                {
                    result.PrintFlags |= PrintFlags.Heap;
                    continue;
                }

                if (_args.TryConsume(out _, "--print-instructions", "-pi"))
                {
                    foreach (string flag in _args.TryConsumeAll(
                        "final", "commented", "simplified",
                        "f", "c", "s"))
                    {
                        switch (flag)
                        {
                            case "final":
                            case "f":
                                result.PrintFlags |= PrintFlags.Final;
                                break;
                            case "commented":
                            case "c":
                                result.PrintFlags |= PrintFlags.Commented;
                                break;
                            case "simplified":
                            case "s":
                                result.PrintFlags |= PrintFlags.Simplified;
                                break;
                        }
                    }

                    if (result.PrintFlags == PrintFlags.None)
                    { result.PrintFlags = PrintFlags.Final; }
                    result.MainGeneratorSettings.PrintInstructions = true;
                    continue;
                }

                throw new ArgumentException($"Unknown argument \"{_args.Current}\"");
            }
        }

        if (args.Length > 0)
        {
            Uri? file;
            if (System.IO.File.Exists(args[^1]))
            { file = Utils.ToFileUri(args[^1]); }
            else if (!Uri.TryCreate(args[^1], UriKind.RelativeOrAbsolute, out file))
            { throw new ArgumentException($"Invalid uri \"{args[^1]}\""); }
            result.File = file;
        }
        else
        {
            result.File = null;
        }

        return result;
    }

    public static bool Parse(out ProgramArguments settings, params string[] args)
    {
        ProgramArguments? _settings = ArgumentParser.Parse(args);
        if (_settings.HasValue)
        {
            settings = _settings.Value;
            return true;
        }
        else
        {
            settings = ProgramArguments.Default;
            return false;
        }
    }

    public static ProgramArguments? Parse(params string[] args)
    {
        if (args.Length == 0)
        { return ProgramArguments.Default; }

        try
        {
            return ParseInternal(args);
        }
        catch (ArgumentException error)
        {
            Output.LogError(error.Message);
            return null;
        }
    }
}
