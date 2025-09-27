using LanguageCore.Parser;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public class CompiledGeneratorContext
{
    public required CompiledParameter ThisParameter { get; init; }
    public required CompiledParameter ResultParameter { get; init; }
    public required GeneralType ResultType { get; init; }
    public required CompiledGeneratorState State { get; init; }
}

public class CompiledGeneratorState
{
    public CompiledStruct Struct { get; }
    public CompiledField StateField { get; }

    public bool GetVariable(string variableName, [NotNullWhen(true)] out CompiledField? compiledVariable, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        foreach (CompiledField field in Struct.Fields.Skip(1))
        {
            if (field.Identifier.Content == variableName)
            {
                compiledVariable = field;
                error = null;
                return true;
            }
        }

        error = new PossibleDiagnostic($"Variable \"{variableName}\" not found");
        compiledVariable = null;
        return false;
    }

    public CompiledField AddVariable(string variableName, GeneralType type)
    {
        if (GetVariable(variableName, out CompiledField? field, out _)) return field;
        CompiledField[] newFields = new CompiledField[Struct.Fields.Length + 1];
        Struct.Fields.CopyTo(newFields);
        newFields[^1] = new CompiledField(
            type,
            Struct,
            new FieldDefinition(
                Token.CreateAnonymous(variableName),
                null!, // FIXME
                Enumerable.Empty<Token>(),
                Array.Empty<AttributeUsage>()
            )
        );
        Struct.SetFields(newFields);
        return newFields[^1];
    }

    public CompiledGeneratorState()
    {
        // FIXME
        Uri file = null!;
        Struct = new CompiledStruct(
            new CompiledField[]
            {
                StateField = new(
                    BuiltinType.I32,
                    null!, // its ok
                    new FieldDefinition(
                        Token.CreateAnonymous("_state"),
                        new TypeInstanceSimple(Token.CreateAnonymous(TypeKeywords.I32), file),
                        Enumerable.Empty<Token>(),
                        Array.Empty<AttributeUsage>()
                    )
                )
            },
            new StructDefinition(
                Token.Empty,
                Token.Empty,
                Token.Empty,
                Enumerable.Empty<AttributeUsage>(),
                Enumerable.Empty<Token>(),
                Enumerable.Empty<FieldDefinition>(),
                Enumerable.Empty<FunctionDefinition>(),
                Enumerable.Empty<GeneralFunctionDefinition>(),
                Enumerable.Empty<FunctionDefinition>(),
                Enumerable.Empty<ConstructorDefinition>(),
                file
            )
        );
    }
}
