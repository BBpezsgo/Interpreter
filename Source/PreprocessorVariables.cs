namespace LanguageCore;

public static class PreprocessorVariables
{
    public static readonly ImmutableHashSet<string> Normal = ImmutableHashSet.Create(
        "BYTECODE"
    );

    public static readonly ImmutableHashSet<string> IL = ImmutableHashSet.Create(
        "IL"
    );

    public static readonly ImmutableHashSet<string> Interactive = ImmutableHashSet.Create(
        "BYTECODE",
        "INTERACTIVE"
    );

    public static readonly ImmutableHashSet<string> Brainfuck = ImmutableHashSet.Create(
        "BRAINFUCK"
    );
}
