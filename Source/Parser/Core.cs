using LanguageCore.Parser.Statement;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public static class ExportableExtensions
{
    public static bool CanUse(this IExportable self, Uri? sourceFile)
    {
        if (self.IsExport) return true;
        if (sourceFile == null) return true;
        if (sourceFile == self.FilePath) return true;
        return false;
    }
}

public interface IInFile
{
    public Uri? FilePath { get; }
}

public interface IExportable : IInFile
{
    public bool IsExport { get; }
}

public interface IHaveType
{
    public TypeInstance Type { get; }
}

public enum LiteralType
{
    Integer,
    Float,
    String,
    Char,
}

public struct UsingAnalysis
{
    public string Path;
    public bool Found;
    public double ParseTime;
}

public readonly struct ParserResult
{
    public readonly Error[] Errors;

    public readonly ImmutableArray<FunctionDefinition> Functions;
    public readonly ImmutableArray<FunctionDefinition> Operators;
    public readonly ImmutableArray<StructDefinition> Structs;
    public readonly ImmutableArray<EnumDefinition> Enums;

    public readonly ImmutableArray<UsingDefinition> Usings;
    public readonly List<UsingAnalysis> UsingsAnalytics;
    public readonly ImmutableArray<CompileTag> Hashes;

    public readonly ImmutableArray<Statement.Statement> TopLevelStatements;

    public readonly ImmutableArray<Token> OriginalTokens;
    public readonly ImmutableArray<Token> Tokens;

    public bool IsEmpty { get; private init; }

    public static ParserResult Empty => new(
        Enumerable.Empty<Error>(),
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<StructDefinition>(),
        Enumerable.Empty<UsingDefinition>(),
        Enumerable.Empty<CompileTag>(),
        Enumerable.Empty<Statement.Statement>(),
        Enumerable.Empty<EnumDefinition>(),
        Enumerable.Empty<Token>(),
        Enumerable.Empty<Token>())
    { IsEmpty = true };

    public ParserResult(
        IEnumerable<Error> errors,
        IEnumerable<FunctionDefinition> functions,
        IEnumerable<FunctionDefinition> operators,
        IEnumerable<StructDefinition> structs,
        IEnumerable<UsingDefinition> usings,
        IEnumerable<Statement.CompileTag> hashes,
        IEnumerable<Statement.Statement> topLevelStatements,
        IEnumerable<EnumDefinition> enums,
        IEnumerable<Token> originalTokens,
        IEnumerable<Token> tokens)
    {
        Errors = errors.ToArray();

        Functions = functions.ToImmutableArray();
        Operators = operators.ToImmutableArray();
        Structs = structs.ToImmutableArray();
        Usings = usings.ToImmutableArray();
        UsingsAnalytics = new();
        Hashes = hashes.ToImmutableArray();
        TopLevelStatements = topLevelStatements.ToImmutableArray();
        Enums = enums.ToImmutableArray();
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
}
