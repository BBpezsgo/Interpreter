using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public readonly struct ParserResult
{
    public readonly ImmutableArray<FunctionDefinition> Functions;
    public readonly ImmutableArray<FunctionDefinition> Operators;
    public readonly ImmutableArray<StructDefinition> Structs;
    public readonly ImmutableArray<AliasDefinition> AliasDefinitions;
    public readonly ImmutableArray<UsingDefinition> Usings;
    public readonly ImmutableArray<Statement> TopLevelStatements;
    public readonly ImmutableArray<Token> OriginalTokens;
    public readonly ImmutableArray<Token> Tokens;

    public readonly bool IsNotEmpty;

    public static ParserResult Empty => new(
        ImmutableArray<FunctionDefinition>.Empty,
        ImmutableArray<FunctionDefinition>.Empty,
        ImmutableArray<StructDefinition>.Empty,
        ImmutableArray<UsingDefinition>.Empty,
        ImmutableArray<AliasDefinition>.Empty,
        ImmutableArray<Statement>.Empty,
        ImmutableArray<Token>.Empty,
        ImmutableArray<Token>.Empty);

    public ParserResult(
        ImmutableArray<FunctionDefinition> functions,
        ImmutableArray<FunctionDefinition> operators,
        ImmutableArray<StructDefinition> structs,
        ImmutableArray<UsingDefinition> usings,
        ImmutableArray<AliasDefinition> aliasDefinitions,
        ImmutableArray<Statement> topLevelStatements,
        ImmutableArray<Token> originalTokens,
        ImmutableArray<Token> tokens)
    {
        Functions = functions;
        Operators = operators;
        Structs = structs;
        AliasDefinitions = aliasDefinitions;
        Usings = usings;
        TopLevelStatements = topLevelStatements;
        OriginalTokens = originalTokens;
        Tokens = tokens;

        IsNotEmpty = true;
    }
}
