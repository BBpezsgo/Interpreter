﻿using LanguageCore.Compiler;
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
    public DebugInformation? DebugInfo;
    public GeneratorStatistics Statistics;
    public ImmutableArray<CompiledFunction> CompiledFunctions;
    public ImmutableArray<CompiledOperator> CompiledOperators;
    public ImmutableArray<CompiledGeneralFunction> CompiledGeneralFunctions;
    public ImmutableArray<CompiledConstructor> CompiledConstructors;
    public FrozenDictionary<string, ExposedFunction> ExposedFunctions;
}
