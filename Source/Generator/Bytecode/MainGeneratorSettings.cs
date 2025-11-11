using LanguageCore.IL.Generator;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

[Flags]
public enum OptimizationSettings : uint
{
    None = 0,
    All = uint.MaxValue,
    BytecodeLevel = 1,
    BinaryOperatorFetchSkip = 2,
    IndexerFetchSkip = 4,
    TrimReturnBreak = 8,
}

[ExcludeFromCodeCoverage]
public struct MainGeneratorSettings
{
    public bool GenerateComments;
    public OptimizationSettings Optimizations;
    public bool CheckNullPointers;
    public int PointerSize;
    public int StackSize;
    public bool CleanupGlobalVaraibles;
    public ILGeneratorSettings? ILGeneratorSettings;

    public MainGeneratorSettings(MainGeneratorSettings other)
    {
        GenerateComments = other.GenerateComments;
        Optimizations = other.Optimizations;
        CheckNullPointers = other.CheckNullPointers;
        PointerSize = other.PointerSize;
        StackSize = other.StackSize;
        CleanupGlobalVaraibles = other.CleanupGlobalVaraibles;
        ILGeneratorSettings = other.ILGeneratorSettings;
    }

    public static MainGeneratorSettings Default => new()
    {
        GenerateComments = true,
        Optimizations = OptimizationSettings.All,
        CheckNullPointers = true,
        PointerSize = 4,
        StackSize = BytecodeInterpreterSettings.Default.StackSize,
        CleanupGlobalVaraibles = true,
        ILGeneratorSettings = null,
    };
}
