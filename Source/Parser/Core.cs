using System;
using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.Parser;

public enum LiteralType
{
    Integer,
    Float,
    Boolean,
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

    public static ParserResult Empty => new(
        new List<Error>(),
        new List<FunctionDefinition>(),
        new List<FunctionDefinition>(),
        new List<MacroDefinition>(),
        new List<StructDefinition>(),
        new List<UsingDefinition>(),
        new List<Statement.CompileTag>(),
        new List<Statement.Statement>(),
        new List<EnumDefinition>());

    public ParserResult(IEnumerable<Error> errors, IEnumerable<FunctionDefinition> functions, IEnumerable<FunctionDefinition> operators, IEnumerable<MacroDefinition> macros, IEnumerable<StructDefinition> structs, IEnumerable<UsingDefinition> usings, IEnumerable<Statement.CompileTag> hashes, IEnumerable<Statement.Statement> topLevelStatements, IEnumerable<EnumDefinition> enums)
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
    }

    public void SetFile(Uri path)
    {
        for (int i = 0; i < this.Functions.Length; i++)
        { this.Functions[i].FilePath = path; }

        for (int i = 0; i < this.Enums.Length; i++)
        { this.Enums[i].FilePath = path; }

        for (int i = 0; i < this.Macros.Length; i++)
        { this.Macros[i].FilePath = path; }

        for (int i = 0; i < this.Structs.Length; i++)
        {
            this.Structs[i].FilePath = path;

            foreach (FunctionDefinition method in this.Structs[i].Methods)
            { method.FilePath = path; }
        }

        for (int i = 0; i < this.Hashes.Length; i++)
        { this.Hashes[i].FilePath = path; }

        foreach (IInFile item in Statement.StatementExtensions.GetStatements<IInFile>(this))
        {
            item.FilePath = path;
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

public readonly struct ParserResultHeader
{
    public readonly List<UsingDefinition> Usings;
    public readonly Statement.CompileTag[] Hashes;
    public readonly List<UsingAnalysis> UsingsAnalytics;

    public ParserResultHeader(List<UsingDefinition> usings, IEnumerable<Statement.CompileTag> hashes)
    {
        Usings = usings;
        UsingsAnalytics = new();
        Hashes = hashes.ToArray();
    }
}
