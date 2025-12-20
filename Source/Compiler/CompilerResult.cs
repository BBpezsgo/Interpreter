using LanguageCore.Parser.Statements;
using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public readonly struct CompilerResult
{
    public readonly ImmutableArray<CompiledFunctionDefinition> FunctionDefinitions;
    public readonly ImmutableArray<CompiledGeneralFunctionDefinition> GeneralFunctionDefinitions;
    public readonly ImmutableArray<CompiledOperatorDefinition> OperatorDefinitions;
    public readonly ImmutableArray<CompiledConstructorDefinition> ConstructorDefinitions;

    public readonly ImmutableArray<CompiledAlias> Aliases;
    public readonly ImmutableArray<CompiledStruct> Structs;

    public readonly ImmutableArray<ParsedFile> RawTokens;
    public readonly ImmutableArray<(ImmutableArray<Statement> Statements, Uri File)> RawStatements;

    public readonly ImmutableArray<IExternalFunction> ExternalFunctions;
    public readonly Uri File;
    public readonly bool IsExpression;

    public readonly ImmutableArray<CompiledStatement> Statements;
    public readonly ImmutableArray<CompiledFunction> Functions;

    public readonly string Stringify()
    {
        StringBuilder res = new();

        foreach ((ICompiledFunctionDefinition function, CompiledBlock body) in Functions)
        {
            res.Append(function.Type.ToString());
            res.Append(' ');
            res.Append(function switch
            {
                CompiledFunctionDefinition v => v.Identifier.Content,
                CompiledOperatorDefinition v => v.Identifier.Content,
                CompiledGeneralFunctionDefinition v => v.Identifier.Content,
                CompiledConstructorDefinition v => v.Type.ToString(),
                _ => "???",
            });
            res.Append('(');
            for (int i = 0; i < function.Parameters.Length; i++)
            {
                if (i > 0) res.Append(", ");
                res.Append(function.Parameters[i].Type.ToString());
                res.Append(' ');
                res.Append(function.Parameters[i].Identifier.Content);
            }
            res.Append(')');
            res.Append(body.Stringify(0));
            res.AppendLine();
            res.AppendLine();
        }

        res.AppendLine("// Top level statements");
        foreach (CompiledStatement statement in Statements)
        {
            if (statement is CompiledEmptyStatement) continue;
            res.Append(statement.Stringify(0));
            res.Append(';');
            res.AppendLine();
        }

        return res.ToString();
    }

    public readonly IEnumerable<Statement> StatementsIn(Uri file)
    {
        foreach ((ImmutableArray<Statement> topLevelStatements, Uri _file) in RawStatements)
        {
            if (file != _file) continue;
            foreach (Statement statement in topLevelStatements)
            { yield return statement; }
        }

        foreach (CompiledFunctionDefinition function in FunctionDefinitions)
        {
            if (file != function.File) continue;
            if (function.Block != null) yield return function.Block;
        }

        foreach (CompiledGeneralFunctionDefinition function in GeneralFunctionDefinitions)
        {
            if (file != function.File) continue;
            if (function.Block != null) yield return function.Block;
        }

        foreach (CompiledOperatorDefinition @operator in OperatorDefinitions)
        {
            if (file != @operator.File) continue;
            if (@operator.Block != null) yield return @operator.Block;
        }
    }

    public static CompilerResult MakeEmpty(Uri file) => new(
        ImmutableArray<ParsedFile>.Empty,
        ImmutableArray<CompiledFunctionDefinition>.Empty,
        ImmutableArray<CompiledGeneralFunctionDefinition>.Empty,
        ImmutableArray<CompiledOperatorDefinition>.Empty,
        ImmutableArray<CompiledConstructorDefinition>.Empty,
        ImmutableArray<CompiledAlias>.Empty,
        ImmutableArray<IExternalFunction>.Empty,
        ImmutableArray<CompiledStruct>.Empty,
        ImmutableArray<(ImmutableArray<Statement>, Uri)>.Empty,
        file,
        false,
        ImmutableArray<CompiledStatement>.Empty,
        ImmutableArray<CompiledFunction>.Empty);

    public CompilerResult(
        ImmutableArray<ParsedFile> tokens,
        ImmutableArray<CompiledFunctionDefinition> functions,
        ImmutableArray<CompiledGeneralFunctionDefinition> generalFunctions,
        ImmutableArray<CompiledOperatorDefinition> operators,
        ImmutableArray<CompiledConstructorDefinition> constructors,
        ImmutableArray<CompiledAlias> aliases,
        ImmutableArray<IExternalFunction> externalFunctions,
        ImmutableArray<CompiledStruct> structs,
        ImmutableArray<(ImmutableArray<Statement> Statements, Uri File)> topLevelStatements,
        Uri file,
        bool isExpression,
        ImmutableArray<CompiledStatement> compiledStatements,
        ImmutableArray<CompiledFunction> functions2)
    {
        RawTokens = tokens;
        FunctionDefinitions = functions;
        GeneralFunctionDefinitions = generalFunctions;
        OperatorDefinitions = operators;
        ConstructorDefinitions = constructors;
        Aliases = aliases;
        ExternalFunctions = externalFunctions;
        Structs = structs;
        RawStatements = topLevelStatements;
        File = file;
        IsExpression = isExpression;
        Statements = compiledStatements;
        Functions = functions2;
    }
}
