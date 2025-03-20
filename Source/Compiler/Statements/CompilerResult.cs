using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public readonly struct CompilerResult
{
    public readonly ImmutableArray<CompiledFunction> Functions;
    public readonly ImmutableArray<CompiledGeneralFunction> GeneralFunctions;
    public readonly ImmutableArray<CompiledOperator> Operators;
    public readonly ImmutableArray<CompiledConstructor> Constructors;
    public readonly ImmutableArray<CompiledAlias> Aliases;

    public readonly ImmutableArray<ParsedFile> Raw;

    public readonly ImmutableArray<IExternalFunction> ExternalFunctions;

    public readonly ImmutableArray<CompiledStruct> Structs;

    public readonly ImmutableArray<(ImmutableArray<Statement> Statements, Uri File)> TopLevelStatements;

    public readonly Uri File;

    public readonly bool IsInteractive;

    public readonly ImmutableArray<CompiledStatement> CompiledStatements;
    public readonly ImmutableArray<CompiledFunction2> Functions2;

    public readonly string Stringify()
    {
        StringBuilder res = new();

        foreach ((ICompiledFunction function, CompiledBlock body) in Functions2)
        {
            res.Append(function.Type.ToString());
            res.Append(' ');
            res.Append(function switch
            {
                CompiledFunction v => v.Identifier.Content,
                CompiledOperator v => v.Identifier.Content,
                CompiledGeneralFunction v => v.Identifier.Content,
                CompiledConstructor v => v.Type.ToString(),
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
        }

        res.AppendLine("// Top level statements");
        foreach (CompiledStatement statement in CompiledStatements)
        {
            if (statement is EmptyStatement) continue;
            res.Append(statement.Stringify(0));
            res.Append(';');
            res.AppendLine();
        }

        return res.ToString();
    }

    public readonly IEnumerable<Uri> Files
    {
        get
        {
            HashSet<Uri> alreadyExists = new();

            foreach (CompiledFunction function in Functions)
            {
                Uri file = function.File;
                if (!alreadyExists.Add(file))
                { yield return file; }
            }

            foreach (CompiledGeneralFunction generalFunction in GeneralFunctions)
            {
                Uri file = generalFunction.File;
                if (!alreadyExists.Add(file))
                { yield return file; }
            }

            foreach (CompiledOperator @operator in Operators)
            {
                Uri file = @operator.File;
                if (!alreadyExists.Add(file))
                { yield return file; }
            }

            foreach (CompiledStruct @struct in Structs)
            {
                Uri file = @struct.File;
                if (!alreadyExists.Add(file))
                { yield return file; }
            }
        }
    }

    public readonly IEnumerable<Statement> Statements
    {
        get
        {
            foreach ((ImmutableArray<Statement> topLevelStatements, _) in TopLevelStatements)
            {
                foreach (Statement statement in topLevelStatements)
                { yield return statement; }
            }

            foreach (CompiledFunction function in Functions)
            {
                if (function.Block != null) yield return function.Block;
            }

            foreach (CompiledGeneralFunction function in GeneralFunctions)
            {
                if (function.Block != null) yield return function.Block;
            }

            foreach (CompiledOperator @operator in Operators)
            {
                if (@operator.Block != null) yield return @operator.Block;
            }
        }
    }

    public readonly IEnumerable<Statement> StatementsIn(Uri file)
    {
        foreach ((ImmutableArray<Statement> topLevelStatements, Uri _file) in TopLevelStatements)
        {
            if (file != _file) continue;
            foreach (Statement statement in topLevelStatements)
            { yield return statement; }
        }

        foreach (CompiledFunction function in Functions)
        {
            if (file != function.File) continue;
            if (function.Block != null) yield return function.Block;
        }

        foreach (CompiledGeneralFunction function in GeneralFunctions)
        {
            if (file != function.File) continue;
            if (function.Block != null) yield return function.Block;
        }

        foreach (CompiledOperator @operator in Operators)
        {
            if (file != @operator.File) continue;
            if (@operator.Block != null) yield return @operator.Block;
        }
    }

    public static CompilerResult MakeEmpty(Uri file) => new(
        Enumerable.Empty<ParsedFile>(),
        Enumerable.Empty<CompiledFunction>(),
        Enumerable.Empty<CompiledGeneralFunction>(),
        Enumerable.Empty<CompiledOperator>(),
        Enumerable.Empty<CompiledConstructor>(),
        Enumerable.Empty<CompiledAlias>(),
        Enumerable.Empty<IExternalFunction>(),
        Enumerable.Empty<CompiledStruct>(),
        Enumerable.Empty<(ImmutableArray<Statement>, Uri)>(),
        file,
        false,
        ImmutableArray<CompiledStatement>.Empty,
        ImmutableArray<CompiledFunction2>.Empty);

    public CompilerResult(
        IEnumerable<ParsedFile> tokens,
        IEnumerable<CompiledFunction> functions,
        IEnumerable<CompiledGeneralFunction> generalFunctions,
        IEnumerable<CompiledOperator> operators,
        IEnumerable<CompiledConstructor> constructors,
        IEnumerable<CompiledAlias> aliases,
        IEnumerable<IExternalFunction> externalFunctions,
        IEnumerable<CompiledStruct> structs,
        IEnumerable<(ImmutableArray<Statement> Statements, Uri File)> topLevelStatements,
        Uri file,
        bool isInteractive,
        ImmutableArray<CompiledStatement> compiledStatements,
        ImmutableArray<CompiledFunction2> functions2)
    {
        Raw = tokens.ToImmutableArray();
        Functions = functions.ToImmutableArray();
        GeneralFunctions = generalFunctions.ToImmutableArray();
        Operators = operators.ToImmutableArray();
        Constructors = constructors.ToImmutableArray();
        Aliases = aliases.ToImmutableArray();
        ExternalFunctions = externalFunctions.ToImmutableArray();
        Structs = structs.ToImmutableArray();
        TopLevelStatements = topLevelStatements.ToImmutableArray();
        File = file;
        IsInteractive = isInteractive;
        CompiledStatements = compiledStatements;
        Functions2 = functions2;
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

    public bool GetFunctionAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledFunction? result)
        => GetThingAt<CompiledFunction, Token>(Functions, file, position, out result);

    public bool GetGeneralFunctionAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledGeneralFunction? result)
        => GetThingAt<CompiledGeneralFunction, Token>(GeneralFunctions, file, position, out result);

    public bool GetOperatorAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledOperator? result)
        => GetThingAt<CompiledOperator, Token>(Operators, file, position, out result);

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
