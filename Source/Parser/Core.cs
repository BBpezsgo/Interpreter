using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public static class ExportableExtensions
{
    public static bool CanUse(this IExportable self, Uri? sourceFile)
    {
        if (self.IsExported) return true;
        if (sourceFile == null) return true;
        if (sourceFile == self.File) return true;
        return false;
    }
}

public interface IInFile
{
    public Uri File { get; }
}

public interface IExportable : IInFile
{
    public bool IsExported { get; }
}

public interface IHaveType
{
    public TypeInstance Type { get; }
}

public interface IReferenceableTo<TReference> : IInFile, IReferenceableTo
{
    public new TReference? Reference { get; set; }
    object? IReferenceableTo.Reference
    {
        get => Reference;
        set => Reference = (TReference?)value;
    }
}

public interface IReferenceableTo : IInFile
{
    public object? Reference { get; set; }
}

public enum LiteralType
{
    Integer,
    Float,
    String,
    Char,
}

public readonly struct ParserResult
{
    public readonly ImmutableArray<LanguageError> Errors;

    public readonly ImmutableArray<FunctionDefinition> Functions;
    public readonly ImmutableArray<FunctionDefinition> Operators;
    public readonly ImmutableArray<StructDefinition> Structs;

    public readonly ImmutableArray<UsingDefinition> Usings;

    public readonly ImmutableArray<Statement.Statement> TopLevelStatements;

    public readonly ImmutableArray<Token> OriginalTokens;
    public readonly ImmutableArray<Token> Tokens;

    public bool IsEmpty { get; private init; }

    public static ParserResult Empty => new(
        Enumerable.Empty<LanguageError>(),
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<StructDefinition>(),
        Enumerable.Empty<UsingDefinition>(),
        Enumerable.Empty<Statement.Statement>(),
        Enumerable.Empty<Token>(),
        Enumerable.Empty<Token>())
    { IsEmpty = true };

    public ParserResult(
        IEnumerable<LanguageError> errors,
        IEnumerable<FunctionDefinition> functions,
        IEnumerable<FunctionDefinition> operators,
        IEnumerable<StructDefinition> structs,
        IEnumerable<UsingDefinition> usings,
        IEnumerable<Statement.Statement> topLevelStatements,
        IEnumerable<Token> originalTokens,
        IEnumerable<Token> tokens)
    {
        Errors = errors.ToImmutableArray();

        Functions = functions.ToImmutableArray();
        Operators = operators.ToImmutableArray();
        Structs = structs.ToImmutableArray();
        Usings = usings.ToImmutableArray();
        TopLevelStatements = topLevelStatements.ToImmutableArray();
        OriginalTokens = originalTokens.ToImmutableArray();
        Tokens = tokens.ToImmutableArray();

        IsEmpty = false;
    }

    public IEnumerable<Statement.Statement> GetStatementsRecursively()
    {
        for (int i = 0; i < TopLevelStatements.Length; i++)
        {
            foreach (Statement.Statement statement in TopLevelStatements[i].GetStatementsRecursively(true))
            { yield return statement; }
        }

        for (int i = 0; i < Functions.Length; i++)
        {
            FunctionDefinition function = Functions[i];

            if (function.Block == null)
            { continue; }

            foreach (Statement.Statement statement in function.Block.GetStatementsRecursively(true))
            { yield return statement; }
        }

        for (int i = 0; i < Operators.Length; i++)
        {
            FunctionDefinition @operator = Operators[i];

            if (@operator.Block == null)
            { continue; }

            foreach (Statement.Statement statement in @operator.Block.GetStatementsRecursively(true))
            { yield return statement; }
        }

        for (int i = 0; i < Structs.Length; i++)
        {
            StructDefinition structs = Structs[i];

            foreach (GeneralFunctionDefinition method in structs.GeneralFunctions)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement.Statement statement in method.Block.GetStatementsRecursively(true))
                { yield return statement; }
            }

            foreach (FunctionDefinition method in structs.Functions)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement.Statement statement in method.Block.GetStatementsRecursively(true))
                { yield return statement; }
            }

            foreach (FunctionDefinition method in structs.Operators)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement.Statement statement in method.Block.GetStatementsRecursively(true))
                { yield return statement; }
            }

            foreach (ConstructorDefinition constructor in structs.Constructors)
            {
                if (constructor.Block == null)
                { continue; }

                foreach (Statement.Statement statement in constructor.Block.GetStatementsRecursively(true))
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
