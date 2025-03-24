using LanguageCore.Compiler;

namespace LanguageCore;

public readonly struct SourceCodeManagerResult
{
    public required ImmutableArray<ParsedFile> ParsedFiles { get; init; }
    public required Uri? ResolvedEntry { get; init; }
}
