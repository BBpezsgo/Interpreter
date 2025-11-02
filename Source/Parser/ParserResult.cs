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

    public bool IsEmpty { get; private init; }

    public static ParserResult Empty => new(
        ImmutableArray<FunctionDefinition>.Empty,
        ImmutableArray<FunctionDefinition>.Empty,
        ImmutableArray<StructDefinition>.Empty,
        ImmutableArray<UsingDefinition>.Empty,
        ImmutableArray<AliasDefinition>.Empty,
        ImmutableArray<Statement>.Empty,
        ImmutableArray<Token>.Empty,
        ImmutableArray<Token>.Empty)
    { IsEmpty = true };

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

        IsEmpty = false;
    }

    public IEnumerable<Statement> GetStatementsRecursively()
    {
        for (int i = 0; i < TopLevelStatements.Length; i++)
        {
            foreach (Statement statement in TopLevelStatements[i].GetStatementsRecursively(true))
            { yield return statement; }
        }

        for (int i = 0; i < Functions.Length; i++)
        {
            FunctionDefinition function = Functions[i];

            if (function.Block == null)
            { continue; }

            foreach (Statement statement in function.Block.GetStatementsRecursively(true))
            { yield return statement; }
        }

        for (int i = 0; i < Operators.Length; i++)
        {
            FunctionDefinition @operator = Operators[i];

            if (@operator.Block == null)
            { continue; }

            foreach (Statement statement in @operator.Block.GetStatementsRecursively(true))
            { yield return statement; }
        }

        for (int i = 0; i < Structs.Length; i++)
        {
            StructDefinition structs = Structs[i];

            foreach (GeneralFunctionDefinition method in structs.GeneralFunctions)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement statement in method.Block.GetStatementsRecursively(true))
                { yield return statement; }
            }

            foreach (FunctionDefinition method in structs.Functions)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement statement in method.Block.GetStatementsRecursively(true))
                { yield return statement; }
            }

            foreach (FunctionDefinition method in structs.Operators)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement statement in method.Block.GetStatementsRecursively(true))
                { yield return statement; }
            }

            foreach (ConstructorDefinition constructor in structs.Constructors)
            {
                if (constructor.Block == null)
                { continue; }

                foreach (Statement statement in constructor.Block.GetStatementsRecursively(true))
                { yield return statement; }
            }
        }
    }

    public bool GetFieldAt(Uri file, SinglePosition position, [NotNullWhen(true)] out FieldDefinition? result)
    {
        foreach (StructDefinition @struct in Structs)
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
