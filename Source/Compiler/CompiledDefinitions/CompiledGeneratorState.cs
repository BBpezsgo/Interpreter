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
        CompiledField newField = new(
            type,
            Struct,
            new FieldDefinition(
                Token.CreateAnonymous(variableName),
                null!, // FIXME
                ImmutableArray<Token>.Empty,
                ImmutableArray<AttributeUsage>.Empty
            )
        );
        Struct.SetFields(Struct.Fields.Add(newField));
        return newField;
    }

    public CompiledGeneratorState()
    {
        // FIXME
        Uri file = null!;
        Struct = new CompiledStruct(
            ImmutableArray.Create<CompiledField>(
                StateField = new(
                    BuiltinType.I32,
                    null!, // its ok
                    new FieldDefinition(
                        Token.CreateAnonymous("_state"),
                        new TypeInstanceSimple(Token.CreateAnonymous(TypeKeywords.I32), file),
                        ImmutableArray<Token>.Empty,
                        ImmutableArray<AttributeUsage>.Empty
                    )
                )
            ),
            new StructDefinition(
                Token.Empty,
                Token.Empty,
                Token.Empty,
                ImmutableArray<AttributeUsage>.Empty,
                ImmutableArray<Token>.Empty,
                ImmutableArray<FieldDefinition>.Empty,
                ImmutableArray<FunctionDefinition>.Empty,
                ImmutableArray<GeneralFunctionDefinition>.Empty,
                ImmutableArray<FunctionDefinition>.Empty,
                ImmutableArray<ConstructorDefinition>.Empty,
                file
            )
        );
    }
}
