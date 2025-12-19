using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public struct FunctionInformation
{
    public bool IsValid;
    public bool IsTopLevelStub;
    public ICompiledFunctionDefinition? Function;
    public ImmutableDictionary<string, GeneralType>? TypeArguments;
    public MutableRange<int> Instructions;

    public readonly Position SourcePosition => (Function as CompiledFunctionDefinition)?.Identifier.Position ?? default;
    public readonly Uri? File => Function?.File;

    public readonly string? ReadableIdentifier()
    {
        if (!IsValid) return null;
        if (IsTopLevelStub) return "<top level statements>";
        if (Function is CompiledLambda) return "<lambda>";
        string? functionName = null;

        if (Function is CompiledFunctionDefinition f) return f.ToReadable(TypeArguments);
        functionName ??= Function?.ToReadable();

        functionName ??= "<unknown function>";

        return functionName;
    }

    public readonly bool Contains(int instruction) =>
        Instructions.Start <= instruction &&
        Instructions.End > instruction;

    public override readonly string? ToString()
    {
        if (!IsValid) return null;

        StringBuilder result = new();

        result.Append(ReadableIdentifier());

        result.Append(LanguageException.Format(ReadableIdentifier(), SourcePosition, File));

        return result.ToString();
    }
}
