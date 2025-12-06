using LanguageCore.Compiler;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Parser;

public static class StatementConverters
{
    public static bool ToFunctionCall(this AnyCallExpression anyCall, [NotNullWhen(true)] out FunctionCallExpression? functionCall)
    {
        functionCall = null;

        if (anyCall.Expression is null)
        { return false; }

        if (anyCall.Expression is IdentifierExpression functionIdentifier)
        {
            functionCall = new FunctionCallExpression(null, functionIdentifier, anyCall.Arguments, anyCall.Brackets, anyCall.File)
            {
                Semicolon = anyCall.Semicolon,
                SaveValue = anyCall.SaveValue,
                SurroundingBrackets = anyCall.SurroundingBrackets,
                CompiledType = anyCall.CompiledType,
                PredictedValue = anyCall.PredictedValue,
                Reference = anyCall.Reference,
            };
            return true;
        }

        if (anyCall.Expression is FieldExpression field)
        {
            functionCall = new FunctionCallExpression(ArgumentExpression.Wrap(field.Object), field.Identifier, anyCall.Arguments, anyCall.Brackets, anyCall.File)
            {
                Semicolon = anyCall.Semicolon,
                SaveValue = anyCall.SaveValue,
                SurroundingBrackets = anyCall.SurroundingBrackets,
                CompiledType = anyCall.CompiledType,
                PredictedValue = anyCall.PredictedValue,
                Reference = anyCall.Reference,
            };
            return true;
        }

        return false;
    }

    static LinkedBranch? ToLinks(this IfContainer ifContainer, int i)
    {
        if (i >= ifContainer.Branches.Length)
        { return null; }

        if (ifContainer.Branches[i] is ElseIfBranchStatement elseIfBranch)
        {
            return new LinkedIf(
                elseIfBranch.Keyword,
                elseIfBranch.Condition,
                elseIfBranch.Body,
                elseIfBranch.File)
            {
                NextLink = ifContainer.ToLinks(i + 1),
            };
        }

        if (ifContainer.Branches[i] is ElseBranchStatement elseBranch)
        {
            return new LinkedElse(
                elseBranch.Keyword,
                elseBranch.Body,
                elseBranch.File);
        }

        throw new NotImplementedException();
    }

    public static LinkedIf ToLinks(this IfContainer ifContainer)
    {
        if (ifContainer.Branches.Length == 0) throw new InternalExceptionWithoutContext();
        if (ifContainer.Branches[0] is not IfBranchStatement ifBranch) throw new InternalExceptionWithoutContext();
        return new LinkedIf(
            ifBranch.Keyword,
            ifBranch.Condition,
            ifBranch.Body,
            ifBranch.File)
        {
            NextLink = ifContainer.ToLinks(1),
        };
    }

    public static NewInstanceExpression ToInstantiation(this ConstructorCallExpression constructorCall) => new(constructorCall.Keyword, constructorCall.Type, constructorCall.File)
    {
        CompiledType = constructorCall.CompiledType,
        SaveValue = true,
        Semicolon = constructorCall.Semicolon,
    };

    public static CompiledVariableDefinition ToVariable(this ParameterDefinition parameterDefinition, GeneralType type, CompiledArgument? initialValue = null)
        => new()
        {
            TypeExpression = CompiledTypeExpression.CreateAnonymous(type, parameterDefinition.Type.Location),
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
