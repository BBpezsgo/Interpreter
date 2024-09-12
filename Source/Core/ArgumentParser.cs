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

    static readonly OptionSpecification[] OptionSpecifications = new OptionSpecification[]
    {
        new()
        {
            LongName = "help",
            ShortName = 'h',
            Help = "Prints this",
        },
        new()
        {
            LongName = "brainfuck",
            ShortName = 'b',
            Help = "Compiles and executes the code with a brainfuck interpreter",
        },
        new()
        {
            LongName = "asm",
            Help = "Assembles and links the code with nasm and ld",
        },
        new()
        {
            LongName = "console-gui",
            ShortName = 'c',
            Help = "Launch the debugger screen (only avaliable on Windows)",
        },
        new()
        {
            LongName = "output",
            ShortName = 'o',
            Arguments = new (string Name, string Description)[]
            {
                ("filename", "Path to the file to write the output to"),
            },
            Help = "Writes the generated code to the specified file (this option only works for brainfuck)",
        },
        new()
        {
            LongName = "throw-errors",
            ShortName = 't',
            Help = "Whenever an exception occurs, the program crashes. This is useful when debugging the compiler.",
        },
        new()
        {
            LongName = "hide",
            ShortName = 'd',
            Arguments = new (string Name, string Description)[]
            {
                ("w;i;d", "The logging levels to hide (w: Warning, i: Information, d: Debug)")
            },
            Help = "Hides the specified log levels",
        },
        new()
        {
            LongName = "print",
            ShortName = 'p',
            Arguments = new (string Name, string Description)[]
            {
                ("i;m;p", "The information to print (i: Instructions, m: Memory, p: Compilation progress)")
            },
            Help = "Prints the specified informations",
        },
        new()
        {
            LongName = "basepath",
            Arguments = new (string Name, string Description)[]
            {
                ("directory", "Path to the directory")
            },
            Help = $"Sets the path where source files will be searched for \"{DeclarationKeywords.Using}\"",
        },
        new()
        {
            LongName = "dont-optimize",
            Help = "Disable all optimization",
        },
        new()
        {
            LongName = "no-debug-info",
            Help = "Do not generate any debug information (if you compiling into brainfuck, generating debug informations will take a lots of time)",
        },
        new()
        {
            LongName = "stack-size",
            Arguments = new (string Name, string Description)[]
            {
                ("size", "Size in bytes")
            },
            Help = "Specifies the stack size",
        },
        new()
        {
            LongName = "heap-size",
            Arguments = new (string Name, string Description)[]
            {
                ("size", "Size in bytes")
            },
            Help = "Specifies the HEAP size",
        },
        new()
        {
            LongName = "no-nullcheck",
            Help = "Do not generate null-checks when dereferencing a pointer",
        },
        new()
        {
            LongName = "no-pause",
            Help = "Do not pause when finished",
        },
    };

    static ProgramArguments? ParseInternal(string[] args)
    {
        (List<Option> options, List<string> arguments) = CommandLineParser.Parse(args, OptionSpecifications);

        ProgramArguments result = ProgramArguments.Default;

        foreach (Option option in options)
        {
            switch (option.Name)
            {
                case "help":
                    CommandLineParser.PrintHelp(OptionSpecifications, new (string Name, string Help)[]
                    {
                        ("file", "Path to the source file to compile and execute")
                    });
                    return null;
                case "brainfuck":
                    if (result.RunType != ProgramRunType.Normal)
                    { throw new ArgumentException($"Run type already defined ({result.RunType}), but you tried to set it to {ProgramRunType.Brainfuck}"); }

                    result.RunType = ProgramRunType.Brainfuck;
                    break;
                case "asm":
                    if (result.RunType != ProgramRunType.Normal)
                    { throw new ArgumentException($"Run type already defined ({result.RunType}), but you tried to set it to {ProgramRunType.ASM}"); }

                    result.RunType = ProgramRunType.ASM;
                    break;
                case "console-gui":
                    result.ConsoleGUI = true;
                    break;
                case "output":
                    result.OutputFile = option.Arguments[0];
                    break;
                case "throw-errors":
                    result.ThrowErrors = true;
                    break;
                case "hide":
                    foreach (char hide in option.Arguments[0])
                    {
                        result.LogFlags &= hide switch
                        {
                            'w' => ~LogType.Warning,
                            'i' => ~LogType.Normal,
                            'd' => ~LogType.Debug,
                            _ => throw new ArgumentException($"Unknown log flag '{hide}'"),
                        };
                    }
                    break;
                case "print":
                    foreach (char print in option.Arguments[0])
                    {
                        switch (print)
                        {
                            case 'i':
                                result.MainGeneratorSettings.PrintInstructions = true;
                                result.PrintFlags |= PrintFlags.Final;
                                break;
                            case 'm':
                                result.PrintFlags |= PrintFlags.Heap;
                                break;
                            case 'p':
                                result.ShowProgress = true;
                                result.BrainfuckGeneratorSettings.ShowProgress = true;
                                break;
                            default:
                                throw new ArgumentException($"Unknown print flag '{print}'");
                        }
                    }
                    break;
                case "basepath":
                    result.CompilerSettings.BasePath = option.Arguments[0];
                    break;
                case "dont-optimize":
                    result.BrainfuckGeneratorSettings.DontOptimize = true;
                    result.MainGeneratorSettings.DontOptimize = true;
                    break;
                case "no-debug-info":
                    result.BrainfuckGeneratorSettings.GenerateDebugInformation = false;
                    result.MainGeneratorSettings.GenerateComments = false;
                    result.MainGeneratorSettings.GenerateDebugInstructions = false;
                    break;
                case "stack-size":
                    result.BrainfuckGeneratorSettings.StackSize = int.Parse(option.Arguments[0]);
                    result.BytecodeInterpreterSettings.StackSize = int.Parse(option.Arguments[0]);
                    break;
                case "heap-size":
                    result.BrainfuckGeneratorSettings.HeapSize = int.Parse(option.Arguments[0]);
                    result.BytecodeInterpreterSettings.HeapSize = int.Parse(option.Arguments[0]);
                    break;
                case "no-nullcheck":
                    result.MainGeneratorSettings.CheckNullPointers = false;
                    break;
                case "no-pause":
                    result.DoNotPause = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option --\"{option.Name}\"");
            }
        }

        if (arguments.Count == 1)
        {
            Uri? file;
            if (System.IO.File.Exists(arguments[0]))
            { file = Utils.ToFileUri(arguments[0]); }
            else if (!Uri.TryCreate(arguments[0], UriKind.RelativeOrAbsolute, out file))
            { throw new ArgumentException($"Invalid uri \"{arguments[0]}\""); }
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
