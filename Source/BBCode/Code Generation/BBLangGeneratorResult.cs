using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public struct BBLangGeneratorResult
{
    public ImmutableArray<Instruction> Code;
    public DebugInformation? DebugInfo;
}
