using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public class CompiledExternalFunctionCall : CompiledExpression
{
    public required IExternalFunction Function { get; init; }
    public required ICompiledFunctionDefinition Declaration { get; init; }
    public required ImmutableArray<CompiledArgument> Arguments { get; init; }

#if UNITY_BURST
    unsafe
#endif
    string FunctionToString() => Function switch
    {
        CompiledFunctionDefinition v => v.Identifier.Content,
        CompiledOperatorDefinition v => v.Identifier.Content,
        CompiledGeneralFunctionDefinition v => v.Identifier.Content,
        CompiledConstructorDefinition v => v.Type.ToString(),
        ExternalFunctionSync v => v.UnmarshaledCallback.Method.Name ?? v.Name ?? v.Id.ToString(),
        ExternalFunctionAsync v => v.Name ?? v.Id.ToString(),
        ExternalFunctionManaged v => v.Method.ToString() ?? v.Name ?? v.Id.ToString(),
#if UNITY_BURST
        ExternalFunctionScopedSync v => ((nint)v.Callback).ToString() ?? v.Id.ToString(),
#else
        ExternalFunctionScopedSync v => v.Callback.Method.Name ?? v.Id.ToString(),
#endif
        ExternalFunctionStub v => v.Name ?? v.Id.ToString(),
        _ => throw new NotImplementedException(),
    };

    public override string Stringify(int depth = 0) => $"{FunctionToString()}({string.Join(", ", Arguments.Select(v => v.Stringify(depth + 1)))})";

    public override string ToString() => $"{FunctionToString()}({string.Join(", ", Arguments.Select(v => v.ToString()))})";
}
