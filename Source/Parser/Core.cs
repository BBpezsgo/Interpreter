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
    public Uri? FilePath { get; set; }
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

    public readonly FunctionDefinition[] Functions;
    public readonly FunctionDefinition[] Operators;
    public readonly MacroDefinition[] Macros;
    public readonly StructDefinition[] Structs;
    public readonly UsingDefinition[] Usings;
    public readonly Statement.CompileTag[] Hashes;
    public readonly List<UsingAnalysis> UsingsAnalytics;
    public readonly Statement.Statement[] TopLevelStatements;
    public readonly EnumDefinition[] Enums;
    public readonly ImmutableArray<Token> Tokens;

    public static ParserResult Empty => new(
        Enumerable.Empty<Error>(),
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<MacroDefinition>(),
        Enumerable.Empty<StructDefinition>(),
        Enumerable.Empty<UsingDefinition>(),
        Enumerable.Empty<Statement.CompileTag>(),
        Enumerable.Empty<Statement.Statement>(),
        Enumerable.Empty<EnumDefinition>(),
        Enumerable.Empty<Token>());

    public ParserResult(IEnumerable<Error> errors, IEnumerable<FunctionDefinition> functions, IEnumerable<FunctionDefinition> operators, IEnumerable<MacroDefinition> macros, IEnumerable<StructDefinition> structs, IEnumerable<UsingDefinition> usings, IEnumerable<Statement.CompileTag> hashes, IEnumerable<Statement.Statement> topLevelStatements, IEnumerable<EnumDefinition> enums, IEnumerable<Token> tokens)
    {
        Errors = errors.ToArray();

        Functions = functions.ToArray();
        Operators = operators.ToArray();
        Macros = macros.ToArray();
        Structs = structs.ToArray();
        Usings = usings.ToArray();
        UsingsAnalytics = new();
        Hashes = hashes.ToArray();
        TopLevelStatements = topLevelStatements.ToArray();
        Enums = enums.ToArray();
        Tokens = tokens.ToImmutableArray();
    }

    public void SetFile(Uri path)
    {
        for (int i = 0; i < Functions.Length; i++)
        { Functions[i].FilePath = path; }

        for (int i = 0; i < Enums.Length; i++)
        { Enums[i].FilePath = path; }

        for (int i = 0; i < Macros.Length; i++)
        { Macros[i].FilePath = path; }

        for (int i = 0; i < Structs.Length; i++)
        {
            Structs[i].FilePath = path;

            foreach (FunctionDefinition method in Structs[i].Methods)
            { method.FilePath = path; }
        }

        for (int i = 0; i < Hashes.Length; i++)
        { Hashes[i].FilePath = path; }

        foreach (IInFile item in StatementExtensions.GetStatements<IInFile>(this))
        {
            item.FilePath = path;
        }

        foreach (IReferenceableTo item in StatementExtensions.GetStatements<IReferenceableTo>(this))
        {
            item.OriginalFile = path;
        }
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

        for (int i = 0; i < Macros.Length; i++)
        {
            MacroDefinition macro = Macros[i];

            if (macro.Block == null)
            { continue; }

            foreach (Statement.Statement statement in macro.Block.GetStatementsRecursively(true))
            { yield return statement; }
        }

        for (int i = 0; i < Structs.Length; i++)
        {
            StructDefinition structs = Structs[i];

            foreach (GeneralFunctionDefinition method in structs.GeneralMethods)
            {
                if (method.Block == null)
                { continue; }

                foreach (Statement.Statement statement in method.Block.GetStatementsRecursively(true))
                { yield return statement; }
            }

            foreach (FunctionDefinition method in structs.Methods)
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
