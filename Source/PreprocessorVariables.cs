namespace LanguageCore;

public static class PreprocessorVariables
{
    public static readonly ImmutableArray<string> Normal = ImmutableArray.Create(
        "BYTECODE"
        );

    public static readonly ImmutableArray<string> Interactive = ImmutableArray.Create(
        "BYTECODE",
        "INTERACTIVE"
        );

    public static readonly ImmutableArray<string> Brainfuck = ImmutableArray.Create(
        "BRAINFUCK"
        );
}
