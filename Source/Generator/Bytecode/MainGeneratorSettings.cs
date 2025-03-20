using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

[ExcludeFromCodeCoverage]
public struct MainGeneratorSettings
{
    public bool GenerateComments;
    public bool DontOptimize;
    public bool CheckNullPointers;
    public int PointerSize;
    public int StackSize;
    public bool CleanupGlobalVaraibles;

    public MainGeneratorSettings(MainGeneratorSettings other)
    {
        GenerateComments = other.GenerateComments;
        DontOptimize = other.DontOptimize;
        CheckNullPointers = other.CheckNullPointers;
        PointerSize = other.PointerSize;
        StackSize = other.StackSize;
        CleanupGlobalVaraibles = other.CleanupGlobalVaraibles;
    }

    public static MainGeneratorSettings Default => new()
    {
        GenerateComments = true,
        DontOptimize = false,
        CheckNullPointers = true,
        PointerSize = 4,
        StackSize = BytecodeInterpreterSettings.Default.StackSize,
        CleanupGlobalVaraibles = true,
    };
}
