namespace LanguageCore.BBLang.Generator;

using Runtime;

public struct BBLangGeneratorResult
{
    public ImmutableArray<Instruction> Code;
    public DebugInformation? DebugInfo;
}
