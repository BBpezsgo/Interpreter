using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public struct GeneratorStatistics
{
    public int Optimizations;
    public int Precomputations;
    public int FunctionEvaluations;
    public int InstructionLevelOptimizations;
    public int InlinedFunctions;
}

public struct BBLangGeneratorResult
{
    public ImmutableArray<Instruction> Code;
    public BytecodeEmitter CodeEmitter;
    public DebugInformation? DebugInfo;
    public GeneratorStatistics Statistics;
    public ImmutableArray<CompiledFunctionDefinition> CompiledFunctions;
    public ImmutableArray<CompiledOperatorDefinition> CompiledOperators;
    public ImmutableArray<CompiledGeneralFunctionDefinition> CompiledGeneralFunctions;
    public ImmutableArray<CompiledConstructorDefinition> CompiledConstructors;
    public FrozenDictionary<string, ExposedFunction> ExposedFunctions;
    public ImmutableArray<ExternalFunctionScopedSync> GeneratedUnmanagedFunctions;
    public ImmutableArray<ExternalFunctionScopedSyncCallback> GeneratedUnmanagedFunctionReferences;
    public ImmutableArray<string> ILGeneratorBuilders;
}
