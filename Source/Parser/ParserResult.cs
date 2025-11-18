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

    public bool IsNotEmpty { get; private init; }

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

    public IEnumerable<Statement> GetStatementsRecursively()
    {
        foreach (Statement v in TopLevelStatements.IsDefault ? ImmutableArray<Statement>.Empty : TopLevelStatements)
        {
            foreach (Statement statement in v.GetStatementsRecursively(StatementWalkFlags.IncludeThis))
            { yield return statement; }
        }

        foreach (FunctionDefinition function in Functions.IsDefault ? ImmutableArray<FunctionDefinition>.Empty : Functions)
        {
            if (function.Block == null)
            { continue; }

            foreach (Statement statement in function.Block.GetStatementsRecursively(StatementWalkFlags.IncludeThis))
            { yield return statement; }
        }

        foreach (FunctionDefinition @operator in Operators.IsDefault ? ImmutableArray<FunctionDefinition>.Empty : Operators)
        {
            if (@operator.Block == null)
            { continue; }

            foreach (Statement statement in @operator.Block.GetStatementsRecursively(StatementWalkFlags.IncludeThis))
            { yield return statement; }
        }

        foreach (StructDefinition structs in Structs.IsDefault ? ImmutableArray<StructDefinition>.Empty : Structs)
        {
            foreach (GeneralFunctionDefinition method in structs.GeneralFunctions)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement statement in method.Block.GetStatementsRecursively(StatementWalkFlags.IncludeThis))
                { yield return statement; }
            }

            foreach (FunctionDefinition method in structs.Functions)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement statement in method.Block.GetStatementsRecursively(StatementWalkFlags.IncludeThis))
                { yield return statement; }
            }

            foreach (FunctionDefinition method in structs.Operators)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement statement in method.Block.GetStatementsRecursively(StatementWalkFlags.IncludeThis))
                { yield return statement; }
            }

            foreach (ConstructorDefinition constructor in structs.Constructors)
            {
                if (constructor.Block == null)
                { continue; }

                foreach (Statement statement in constructor.Block.GetStatementsRecursively(StatementWalkFlags.IncludeThis))
                { yield return statement; }
            }
        }
    }

    public bool GetFieldAt(Uri file, SinglePosition position, [NotNullWhen(true)] out FieldDefinition? result)
    {
        foreach (StructDefinition @struct in Structs.IsDefault ? ImmutableArray<StructDefinition>.Empty : Structs)
        {
            if (@struct.File != file) continue;

            foreach (FieldDefinition field in @struct.Fields)
            {
                if (field.Identifier.Position.Range.Contains(position))
                {
                    result = field;
                    return true;
                }
            }
        }

        result = null;
        return false;
    }
}
