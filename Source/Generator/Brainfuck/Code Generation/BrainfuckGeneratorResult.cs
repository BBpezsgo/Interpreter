using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck.Generator;

public struct GeneratorStatistics
{
    public int Optimizations;
    public int Precomputations;
    public int FunctionEvaluations;
}

public struct BrainfuckGeneratorResult
{
    public string Code;
    public DebugInformation? DebugInfo;
    public GeneratorStatistics Statistics;
}
