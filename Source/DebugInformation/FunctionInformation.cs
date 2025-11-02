using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct FunctionInformation
{
    public bool IsValid;
    public Parser.FunctionThingDefinition? Function;
    public ImmutableDictionary<string, GeneralType>? TypeArguments;
    public MutableRange<int> Instructions;

    public readonly Position SourcePosition => Function?.Identifier.Position ?? default;
    public readonly string? Identifier => Function?.Identifier.Content;
    public readonly Uri? File => Function?.File;
    public readonly string? ReadableIdentifier => Function?.ToReadable();

    public readonly bool Contains(int instruction) =>
        Instructions.Start <= instruction &&
        Instructions.End > instruction;

    public override readonly string? ToString()
    {
        if (!IsValid) return null;

        StringBuilder result = new();

        result.Append(ReadableIdentifier);

        result.Append(LanguageException.Format(ReadableIdentifier, SourcePosition, File));

        return result.ToString();
    }

    readonly string GetDebuggerDisplay()
    {
        if (!IsValid) return "<unknown>";
        StringBuilder result = new();

        result.Append(Instructions.ToString());

        result.Append(ReadableIdentifier);

        return result.ToString();
    }
}
