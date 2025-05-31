using LanguageCore.Parser.Statements;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

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
    public readonly bool IsInteractive;

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
            for (int i = 0; i < function.Parameters.Count; i++)
            {
                if (i > 0) res.Append(", ");
                res.Append(function.ParameterTypes[i].ToString());
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
            if (statement is EmptyStatement) continue;
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
        Enumerable.Empty<ParsedFile>(),
        Enumerable.Empty<CompiledFunctionDefinition>(),
        Enumerable.Empty<CompiledGeneralFunctionDefinition>(),
        Enumerable.Empty<CompiledOperatorDefinition>(),
        Enumerable.Empty<CompiledConstructorDefinition>(),
        Enumerable.Empty<CompiledAlias>(),
        Enumerable.Empty<IExternalFunction>(),
        Enumerable.Empty<CompiledStruct>(),
        Enumerable.Empty<(ImmutableArray<Statement>, Uri)>(),
        file,
        false,
        ImmutableArray<CompiledStatement>.Empty,
        ImmutableArray<CompiledFunction>.Empty);

    public CompilerResult(
        IEnumerable<ParsedFile> tokens,
        IEnumerable<CompiledFunctionDefinition> functions,
        IEnumerable<CompiledGeneralFunctionDefinition> generalFunctions,
        IEnumerable<CompiledOperatorDefinition> operators,
        IEnumerable<CompiledConstructorDefinition> constructors,
        IEnumerable<CompiledAlias> aliases,
        IEnumerable<IExternalFunction> externalFunctions,
        IEnumerable<CompiledStruct> structs,
        IEnumerable<(ImmutableArray<Statement> Statements, Uri File)> topLevelStatements,
        Uri file,
        bool isInteractive,
        ImmutableArray<CompiledStatement> compiledStatements,
        ImmutableArray<CompiledFunction> functions2)
    {
        RawTokens = tokens.ToImmutableArray();
        FunctionDefinitions = functions.ToImmutableArray();
        GeneralFunctionDefinitions = generalFunctions.ToImmutableArray();
        OperatorDefinitions = operators.ToImmutableArray();
        ConstructorDefinitions = constructors.ToImmutableArray();
        Aliases = aliases.ToImmutableArray();
        ExternalFunctions = externalFunctions.ToImmutableArray();
        Structs = structs.ToImmutableArray();
        RawStatements = topLevelStatements.ToImmutableArray();
        File = file;
        IsInteractive = isInteractive;
        Statements = compiledStatements;
        Functions = functions2;
    }

    public static bool GetThingAt<TThing, TIdentifier>(IEnumerable<TThing> things, Uri file, SinglePosition position, [NotNullWhen(true)] out TThing? result)
        where TThing : IInFile, IIdentifiable<TIdentifier>
        where TIdentifier : IPositioned
    {
        foreach (TThing? thing in things)
        {
            if (thing.File != file)
            { continue; }

            if (!thing.Identifier.Position.Range.Contains(position))
            { continue; }

            result = thing;
            return true;
        }

        result = default;
        return false;
    }

    public bool GetFunctionAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledFunctionDefinition? result)
        => GetThingAt<CompiledFunctionDefinition, Token>(FunctionDefinitions, file, position, out result);

    public bool GetGeneralFunctionAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledGeneralFunctionDefinition? result)
        => GetThingAt<CompiledGeneralFunctionDefinition, Token>(GeneralFunctionDefinitions, file, position, out result);

    public bool GetOperatorAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledOperatorDefinition? result)
        => GetThingAt<CompiledOperatorDefinition, Token>(OperatorDefinitions, file, position, out result);

    public bool GetStructAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledStruct? result)
        => GetThingAt<CompiledStruct, Token>(Structs, file, position, out result);

    public bool GetFieldAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledField? result)
    {
        foreach (CompiledStruct @struct in Structs)
        {
            if (@struct.File != file) continue;

            foreach (CompiledField field in @struct.Fields)
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
