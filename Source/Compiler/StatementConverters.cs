using LanguageCore.Compiler;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Parser;

public static class StatementConverters
{
    public static bool ToFunctionCall(this AnyCall anyCall, [NotNullWhen(true)] out FunctionCall? functionCall)
    {
        functionCall = null;

        if (anyCall.PrevStatement is null)
        { return false; }

        if (anyCall.PrevStatement is Identifier functionIdentifier)
        {
            functionCall = new FunctionCall(null, functionIdentifier, anyCall.Arguments, anyCall.Brackets, anyCall.File)
            {
                Semicolon = anyCall.Semicolon,
                SaveValue = anyCall.SaveValue,
                SurroundingBracelet = anyCall.SurroundingBracelet,
                CompiledType = anyCall.CompiledType,
                PredictedValue = anyCall.PredictedValue,
                Reference = anyCall.Reference,
            };
            return true;
        }

        if (anyCall.PrevStatement is Field field)
        {
            functionCall = new FunctionCall(field.PrevStatement, field.Identifier, anyCall.Arguments, anyCall.Brackets, anyCall.File)
            {
                Semicolon = anyCall.Semicolon,
                SaveValue = anyCall.SaveValue,
                SurroundingBracelet = anyCall.SurroundingBracelet,
                CompiledType = anyCall.CompiledType,
                PredictedValue = anyCall.PredictedValue,
                Reference = anyCall.Reference,
            };
            return true;
        }

        return false;
    }

    static LinkedIfThing? ToLinks(this IfContainer ifContainer, int i)
    {
        if (i >= ifContainer.Branches.Length)
        { return null; }

        if (ifContainer.Branches[i] is ElseIfBranch elseIfBranch)
        {
            return new LinkedIf(
                elseIfBranch.Keyword,
                elseIfBranch.Condition,
                elseIfBranch.Block,
                elseIfBranch.File)
            {
                NextLink = ifContainer.ToLinks(i + 1),
            };
        }

        if (ifContainer.Branches[i] is ElseBranch elseBranch)
        {
            return new LinkedElse(
                elseBranch.Keyword,
                elseBranch.Block,
                elseBranch.File);
        }

        throw new NotImplementedException();
    }

    public static LinkedIf ToLinks(this IfContainer ifContainer)
    {
        if (ifContainer.Branches.Length == 0) throw new InternalExceptionWithoutContext();
        if (ifContainer.Branches[0] is not IfBranch ifBranch) throw new InternalExceptionWithoutContext();
        return new LinkedIf(
            ifBranch.Keyword,
            ifBranch.Condition,
            ifBranch.Block,
            ifBranch.File)
        {
            NextLink = ifContainer.ToLinks(1),
        };
    }

    public static NewInstance ToInstantiation(this ConstructorCall constructorCall) => new(constructorCall.Keyword, constructorCall.Type, constructorCall.File)
    {
        CompiledType = constructorCall.CompiledType,
        SaveValue = true,
        Semicolon = constructorCall.Semicolon,
    };

    public static CompiledVariableDeclaration ToVariable(this ParameterDefinition parameterDefinition, GeneralType type, CompiledPassedArgument? initialValue = null)
        => new()
        {
            Identifier = parameterDefinition.Identifier.Content,
            Type = type,
            Cleanup = new CompiledCleanup()
            {
                Location = parameterDefinition.Location,
                TrashType = type,
            },
            InitialValue = initialValue,
            Location = parameterDefinition.Location,
            IsGlobal = false,
        };
}
