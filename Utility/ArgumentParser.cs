using LanguageCore.Runtime;

namespace LanguageCore;


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


public static class ArgumentNormalizer
{
    enum NormalizerState
    {
        None,
        String,
    }
}
