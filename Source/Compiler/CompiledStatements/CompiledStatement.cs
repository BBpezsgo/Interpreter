using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public abstract class CompiledStatement : ILocated, IInFile, IPositioned
{
    public required Location Location { get; init; }

    Uri IInFile.File => Location.File;
    Position IPositioned.Position => Location.Position;

    protected const int Identation = 4;
    protected const int CozyLength = 30;

    public abstract string Stringify(int depth = 0);
    public abstract override string ToString();
}

public class EmptyStatement : CompiledStatement
{
    public override string Stringify(int depth = 0) => "";
    public override string ToString() => $";";
}

public abstract class CompiledStatementWithValue : CompiledStatement
{
    public required bool SaveValue { get; set; } = true;
    public virtual required GeneralType Type { get; init; }
}

public class CompiledStatementWithValueThatActuallyDoesntHaveValue : CompiledStatementWithValue
{
    public required CompiledStatement Statement { get; init; }

    public override string Stringify(int depth = 0) => Statement.Stringify(depth);
    public override string ToString() => Statement.ToString();
}

public class CompiledBlock : CompiledStatement
{
    public required ImmutableArray<CompiledStatement> Statements { get; init; }

    public static CompiledBlock CreateIfNot(CompiledStatement statement) =>
        statement is CompiledBlock block
        ? block
        : new CompiledBlock()
        {
            Location = statement.Location,
            Statements = ImmutableArray.Create(statement),
        };

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();
        res.AppendLine();
        res.Append(' ', depth * Identation);
        res.AppendLine("{");

        foreach (CompiledStatement statement in Statements)
        {
            if (statement is EmptyStatement) continue;
            res.Append(' ', (depth + 1) * Identation);
            res.Append(statement.Stringify(depth + 1));
            res.Append(';');
            res.AppendLine();
        }

        res.Append(' ', depth * Identation);
        res.Append('}');
        return res.ToString();
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append('{');

        switch (Statements.Length)
        {
            case 0:
                result.Append(' ');
                break;
            case 1:
                result.Append(' ');
                result.Append(Statements[0]);
                result.Append(' ');
                break;
            default:
                result.Append("...");
                break;
        }

        result.Append('}');

        return result.ToString();
    }
}

public abstract class CompiledBranch : CompiledStatement
{
}

public class CompiledIf : CompiledBranch
{
    public required CompiledStatementWithValue Condition { get; init; }
    public required CompiledStatement Body { get; init; }
    public required CompiledBranch? Next { get; init; }

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();

        res.Append($"if ({Condition.Stringify(depth + 1)})");
        res.Append(' ');
        res.Append(Body.Stringify(depth));

        if (Next is CompiledElse _else)
        {
            res.AppendLine();
            res.Append(' ', depth * Identation);
            res.Append($"else");
            res.Append(' ');
            res.Append(_else.Body.Stringify(depth));
        }
        else if (Next is CompiledIf _elseIf)
        {
            res.AppendLine();
            res.Append(' ', depth * Identation);
            res.Append($"elseif {_elseIf.Condition.Stringify(depth + 1)}");
            res.Append(' ');
            res.Append(_elseIf.Body.Stringify(depth));
        }
        else if (Next is not null)
        {
            res.AppendLine();
            res.Append(' ', depth * Identation);
            res.Append($"else");
            res.Append(' ');
            res.Append(Next.Stringify(depth));
        }

        return res.ToString();
    }

    public override string ToString()
    {
        StringBuilder res = new();

        res.Append($"if ({Condition.ToString()})");
        res.Append(' ');
        res.Append(Body.ToString());

        if (Next is not null)
        {
            res.Append("...");
        }

        return res.ToString();
    }
}

public class CompiledElse : CompiledBranch
{
    public required CompiledStatement Body { get; init; }

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();

        res.AppendLine();
        res.Append(' ', depth * Identation);
        res.Append($"else");
        res.Append(' ');
        res.Append(Body.Stringify(depth + 1));

        return res.ToString();
    }

    public override string ToString()
    {
        StringBuilder res = new();

        res.Append($"else ");
        res.Append(Body.ToString());

        return res.ToString();
    }
}

public class CompiledLiteralList : CompiledStatementWithValue
{
    public required ImmutableArray<CompiledStatementWithValue> Values { get; init; }

    public override string Stringify(int depth = 0) => $"[{string.Join(", ", Values.Select(v => v.Stringify(depth + 1)))}]";

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append('[');

        if (Values.Length == 0)
        {
            result.Append(' ');
        }
        else
        {
            for (int i = 0; i < Values.Length; i++)
            {
                if (i > 0)
                { result.Append(", "); }
                if (result.Length >= CozyLength)
                { result.Append("..."); break; }

                result.Append(Values[i]);
            }
        }
        result.Append(']');

        return result.ToString();
    }
}

public class CompiledVariableDeclaration : CompiledStatement
{
    public required GeneralType Type { get; init; }
    public required string Identifier { get; init; }
    public required CompiledStatementWithValue? InitialValue { get; init; }
    public required CompiledCleanup Cleanup { get; init; }
    public required bool IsGlobal { get; init; }
    public HashSet<CompiledVariableSetter> Setters { get; } = new();
    public HashSet<CompiledVariableGetter> Getters { get; } = new();

    public override string Stringify(int depth = 0)
        =>
        InitialValue is null
        ? $"{Type} {Identifier}"
        : $"{Type} {Identifier} = {InitialValue.Stringify(depth + 1)}";

    public override string ToString()
        =>
        InitialValue is null
        ? $"{Type} {Identifier}"
        : $"{Type} {Identifier} = {InitialValue}";
}

public class CompiledRuntimeCall : CompiledStatementWithValue
{
    public required CompiledStatementWithValue Function { get; init; }
    public required ImmutableArray<CompiledPassedArgument> Arguments { get; init; }

    public override string Stringify(int depth = 0) => $"{Function.Stringify(depth + 1)}({string.Join(", ", Arguments.Select(v => v.Stringify(depth + 1)))})";

    public override string ToString() => $"{Function}({string.Join(", ", Arguments.Select(v => v.ToString()))})";
}

public class CompiledFunctionCall : CompiledStatementWithValue
{
    public required ICompiledFunctionDefinition Function { get; init; }
    public required ImmutableArray<CompiledPassedArgument> Arguments { get; init; }

    public override string Stringify(int depth = 0) => $"{Function switch
    {
        CompiledFunctionDefinition v => v.Identifier.Content,
        CompiledOperatorDefinition v => v.Identifier.Content,
        CompiledGeneralFunctionDefinition v => v.Identifier.Content,
        CompiledConstructorDefinition v => v.Type.ToString(),
        _ => throw new UnreachableException(),
    }}({string.Join(", ", Arguments.Select(v => v.Stringify(depth + 1)))})";

    public override string ToString() => $"{Function switch
    {
        CompiledFunctionDefinition v => v.Identifier.Content,
        CompiledOperatorDefinition v => v.Identifier.Content,
        CompiledGeneralFunctionDefinition v => v.Identifier.Content,
        CompiledConstructorDefinition v => v.Type.ToString(),
        _ => throw new UnreachableException(),
    }}({string.Join(", ", Arguments.Select(v => v.ToString()))})";
}

public class CompiledExternalFunctionCall : CompiledStatementWithValue
{
    public required IExternalFunction Function { get; init; }
    public required ICompiledFunctionDefinition Declaration { get; init; }
    public required ImmutableArray<CompiledPassedArgument> Arguments { get; init; }

    unsafe string FunctionToString() => Function switch
    {
        CompiledFunctionDefinition v => v.Identifier.Content,
        CompiledOperatorDefinition v => v.Identifier.Content,
        CompiledGeneralFunctionDefinition v => v.Identifier.Content,
        CompiledConstructorDefinition v => v.Type.ToString(),
        ExternalFunctionSync v => v.UnmarshaledCallback.Method.Name ?? v.Name ?? v.Id.ToString(),
        ExternalFunctionAsync v => v.Name ?? v.Id.ToString(),
        ExternalFunctionManaged v => v.Method.ToString() ?? v.Name ?? v.Id.ToString(),
#if UNITY_BURST
        ExternalFunctionScopedSync v => ((nint)v.Callback).ToString() ?? v.Id.ToString(),
#else
        ExternalFunctionScopedSync v => v.Callback.Method.Name ?? v.Id.ToString(),
#endif
        ExternalFunctionStub v => v.Name ?? v.Id.ToString(),
        _ => throw new NotImplementedException(),
    };

    public override string Stringify(int depth = 0) => $"{FunctionToString()}({string.Join(", ", Arguments.Select(v => v.Stringify(depth + 1)))})";

    public override string ToString() => $"{FunctionToString()}({string.Join(", ", Arguments.Select(v => v.ToString()))})";
}

public class CompiledSizeof : CompiledStatementWithValue
{
    public required GeneralType Of { get; init; }

    public override string Stringify(int depth = 0) => $"sizeof({Of})";
    public override string ToString() => $"sizeof({Of})";
}

public class CompiledCrash : CompiledStatement
{
    public required CompiledStatementWithValue Value { get; init; }

    public override string Stringify(int depth = 0) => $"crash {Value.Stringify(depth + 1)}";
    public override string ToString() => $"crash {Value}";
}

public class CompiledDelete : CompiledStatement
{
    public required CompiledStatementWithValue Value { get; init; }
    public required CompiledCleanup Cleanup { get; init; }

    public override string Stringify(int depth = 0) => $"delete {Value.Stringify(depth + 1)}";
    public override string ToString() => $"delete {Value}";
}

public class CompiledReturn : CompiledStatement
{
    public required CompiledStatementWithValue? Value { get; init; }

    public override string Stringify(int depth = 0)
        => Value is null
        ? $"return"
        : $"return {Value.Stringify(depth + 1)}";

    public override string ToString()
        => Value is null
        ? $"return"
        : $"return {Value}";
}

public class CompiledBreak : CompiledStatement
{
    public override string Stringify(int depth = 0) => $"break";
    public override string ToString() => $"break";
}

public class CompiledCleanup : CompiledStatement
{
    public CompiledGeneralFunctionDefinition? Destructor { get; init; }
    public CompiledFunctionDefinition? Deallocator { get; init; }
    public required GeneralType TrashType { get; init; }

    public override string Stringify(int depth = 0) => "";
    public override string ToString() => "::cleanup::";
}

public class CompiledPassedArgument : CompiledStatementWithValue
{
    public required CompiledStatementWithValue Value { get; init; }
    public required CompiledCleanup Cleanup { get; init; }

    public override string Stringify(int depth = 0) => Value.Stringify(depth + 1);
    public override string ToString() => Value.ToString();

    public static CompiledPassedArgument Wrap(CompiledStatementWithValue value) => new()
    {
        Value = value,
        Location = value.Location,
        Cleanup = new CompiledCleanup()
        {
            Location = value.Location,
            TrashType = value.Type,
        },
        Type = value.Type,
        SaveValue = value.SaveValue,
    };
}

public class CompiledGoto : CompiledStatement
{
    public required CompiledStatementWithValue Value { get; init; }

    public override string Stringify(int depth = 0) => $"goto {Value.Stringify(depth + 1)}";
    public override string ToString() => $"goto {Value}";
}

public class CompiledBinaryOperatorCall : CompiledStatementWithValue
{
    #region Operators

    public const string BitshiftLeft = "<<";
    public const string BitshiftRight = ">>";
    public const string Addition = "+";
    public const string Subtraction = "-";
    public const string Multiplication = "*";
    public const string Division = "/";
    public const string Modulo = "%";
    public const string BitwiseAND = "&";
    public const string BitwiseOR = "|";
    public const string BitwiseXOR = "^";
    public const string CompLT = "<";
    public const string CompGT = ">";
    public const string CompGEQ = ">=";
    public const string CompLEQ = "<=";
    public const string CompNEQ = "!=";
    public const string CompEQ = "==";
    public const string LogicalAND = "&&";
    public const string LogicalOR = "||";

    #endregion

    public required string Operator { get; init; }
    public required CompiledStatementWithValue Left { get; init; }
    public required CompiledStatementWithValue Right { get; init; }

    public override string Stringify(int depth = 0) => $"({Left.Stringify(depth + 1)} {Operator} {Right.Stringify(depth + 1)})";

    public override string ToString()
    {
        StringBuilder result = new();

        if (Left.ToString().Length < CozyLength)
        { result.Append(Left); }
        else
        { result.Append("..."); }

        result.Append(' ');
        result.Append(Operator);
        result.Append(' ');

        if (Right.ToString().Length < CozyLength)
        { result.Append(Right); }
        else
        { result.Append("..."); }

        return result.ToString();
    }
}

public class CompiledUnaryOperatorCall : CompiledStatementWithValue
{
    #region Operators

    public const string LogicalNOT = "!";
    public const string BinaryNOT = "~";
    public const string UnaryMinus = "-";
    public const string UnaryPlus = "+";

    #endregion

    public required string Operator { get; init; }
    public required CompiledStatementWithValue Left { get; init; }

    public override string Stringify(int depth = 0) => $"({Operator}{Left.Stringify(depth + 1)})";

    public override string ToString() => $"{Operator}{Left}";
}

public class CompiledEvaluatedValue : CompiledStatementWithValue
{
    public required CompiledValue Value { get; init; }

    public override string Stringify(int depth = 0)
    {
        return Value.Type switch
        {
            RuntimeType.U8 => Value.U8.ToString(),
            RuntimeType.I8 => Value.I8.ToString(),
            RuntimeType.U16 => $"'{((char)Value.U16).Escape()}'",
            RuntimeType.I16 => Value.I16.ToString(),
            RuntimeType.U32 => Value.U32.ToString(),
            RuntimeType.I32 => Value.I32.ToString(),
            RuntimeType.F32 => Value.F32.ToString(),
            RuntimeType.Null => "null",
            _ => Value.ToString(),

        };
    }

    public override string ToString()
    {
        return Value.Type switch
        {
            RuntimeType.U8 => Value.U8.ToString(),
            RuntimeType.I8 => Value.I8.ToString(),
            RuntimeType.U16 => $"'{((char)Value.U16).Escape()}'",
            RuntimeType.I16 => Value.I16.ToString(),
            RuntimeType.U32 => Value.U32.ToString(),
            RuntimeType.I32 => Value.I32.ToString(),
            RuntimeType.F32 => Value.F32.ToString(),
            RuntimeType.Null => "null",
            _ => Value.ToString(),

        };
    }

    public static CompiledEvaluatedValue Create(CompiledValue value, CompiledStatementWithValue statement) => new()
    {
        Value = value,
        Location = statement.Location,
        SaveValue = statement.SaveValue,
        Type = statement.Type,
    };
}

public class CompiledInstructionLabelDeclaration : CompiledStatement
{
    public static readonly FunctionType Type = new(BuiltinType.Void, Enumerable.Empty<GeneralType>());
    public required string Identifier { get; init; }
    public HashSet<InstructionLabelAddressGetter> Getters { get; } = new();

    public override string Stringify(int depth = 0) => $"{Identifier}:";
    public override string ToString() => $"{Identifier}:";
}

public class CompiledAddressGetter : CompiledStatementWithValue
{
    public required CompiledStatementWithValue Of { get; init; }

    public override string Stringify(int depth = 0) => $"&{Of.Stringify(depth + 1)}";
    public override string ToString() => $"&{Of}";
}

public class CompiledPointer : CompiledStatementWithValue
{
    public required CompiledStatementWithValue To { get; init; }

    public override string Stringify(int depth = 0) => $"*{To.Stringify(depth + 1)}";
    public override string ToString() => $"*{To}";
}

public class CompiledWhileLoop : CompiledStatement
{
    public required CompiledStatementWithValue Condition { get; init; }
    public required CompiledStatement Body { get; init; }

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();

        res.Append($"while ({Condition.Stringify(depth + 1)})");
        res.Append(' ');
        res.Append(Body.Stringify(depth));

        return res.ToString();
    }

    public override string ToString() => $"while ({Condition}) {Body}";
}

public class CompiledForLoop : CompiledStatement
{
    public required CompiledStatement? VariableDeclaration { get; init; }
    public required CompiledStatementWithValue Condition { get; init; }
    public required CompiledStatement Expression { get; init; }
    public required CompiledStatement Body { get; init; }

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();

        res.Append($"for ({VariableDeclaration?.Stringify(depth + 1)}; {Condition.Stringify(depth + 1)}; {Expression.Stringify(depth + 1)})");
        res.Append(' ');
        res.Append(Body.Stringify(depth));

        return res.ToString();
    }

    public override string ToString() => $"for ({VariableDeclaration}; {Condition}; {Expression}) {Body}";
}

public class CompiledStackAllocation : CompiledStatementWithValue
{
    public override string Stringify(int depth = 0) => $"new {Type}";
    public override string ToString() => $"new {Type}";
}

public class CompiledHeapAllocation : CompiledStatementWithValue
{
    public required CompiledFunctionDefinition Allocator { get; init; }

    public override string Stringify(int depth = 0) => $"new {Type}";
    public override string ToString() => $"new {Type}";
}

public class CompiledConstructorCall : CompiledStatementWithValue
{
    public required CompiledConstructorDefinition Function { get; init; }
    public required CompiledStatementWithValue Object { get; init; }
    public required ImmutableArray<CompiledPassedArgument> Arguments { get; init; }

    public override string Stringify(int depth = 0) => $"new {Function.Type}({string.Join(", ", Arguments.Select(v => v.Stringify(depth + 1)))})";
    public override string ToString() => $"new {Function.Type}({string.Join(", ", Arguments.Select(v => v.ToString()))})";
}

public class CompiledDesctructorCall : CompiledStatementWithValue
{
    public required CompiledGeneralFunctionDefinition Function { get; init; }
    public required CompiledStatementWithValue Value { get; init; }

    public override string Stringify(int depth = 0) => $"{Function.Identifier}({Value.Stringify(depth + 1)})";
    public override string ToString() => $"{Function.Identifier}({Value})";
}

public class CompiledTypeCast : CompiledStatementWithValue
{
    public required CompiledStatementWithValue Value { get; init; }

    public override string Stringify(int depth = 0) => $"({Type}){Value.Stringify(depth + 1)}";

    public override string ToString() => $"({Type}){Value}";

    public static CompiledTypeCast Wrap(CompiledStatementWithValue value, GeneralType type) => new()
    {
        Value = value,
        Type = type,
        Location = value.Location,
        SaveValue = value.SaveValue,
    };
}

public class CompiledFakeTypeCast : CompiledStatementWithValue
{
    public required CompiledStatementWithValue Value { get; init; }

    public override string Stringify(int depth = 0) => $"{Value.Stringify(depth)}";
    public override string ToString() => $"{Value} as {Type}";
}

public class CompiledIndexGetter : CompiledStatementWithValue
{
    public required CompiledStatementWithValue Base { get; init; }
    public required CompiledStatementWithValue Index { get; init; }

    public override string Stringify(int depth = 0) => $"{Base.Stringify(depth + 1)}[{Index.Stringify(depth + 1)}]";
    public override string ToString() => $"{Base}[{Index}]";
}

public class CompiledVariableSetter : CompiledStatement
{
    public required CompiledVariableDeclaration Variable { get; init; }
    public required CompiledStatementWithValue Value { get; init; }
    public required bool IsCompoundAssignment { get; init; }

    public override string Stringify(int depth = 0) => $"{Variable.Identifier} = {Value.Stringify(depth + 1)}";
    public override string ToString() => $"{Variable.Identifier} = {Value}";
}

public class CompiledParameterSetter : CompiledStatement
{
    public required CompiledParameter Variable { get; init; }
    public required CompiledStatementWithValue Value { get; init; }
    public required bool IsCompoundAssignment { get; init; }

    public override string Stringify(int depth = 0) => $"{Variable.Identifier} = {Value.Stringify(depth + 1)}";
    public override string ToString() => $"{Variable.Identifier} = {Value}";
}

public class CompiledFieldSetter : CompiledStatement
{
    public required CompiledStatementWithValue Object { get; init; }
    public required CompiledField Field { get; init; }
    public required CompiledStatementWithValue Value { get; init; }
    public required GeneralType Type { get; init; }
    public required bool IsCompoundAssignment { get; init; }

    public override string Stringify(int depth = 0) => $"{Object.Stringify(depth + 1)}.{Field.Identifier} = {Value.Stringify(depth + 1)}";
    public override string ToString() => $"{Object}.{Field.Identifier} = {Value}";

    public CompiledFieldGetter ToGetter() => new()
    {
        Object = Object,
        Field = Field,
        Location = Location,
        SaveValue = true,
        Type = Value.Type,
    };
}

public class CompiledIndirectSetter : CompiledStatement
{
    public required CompiledStatementWithValue AddressValue { get; init; }
    public required CompiledStatementWithValue Value { get; init; }
    public required bool IsCompoundAssignment { get; init; }

    public override string Stringify(int depth = 0) => $"*{AddressValue.Stringify(depth + 1)} = {Value.Stringify(depth + 1)}";
    public override string ToString() => $"*{AddressValue} = {Value}";
}

public class CompiledIndexSetter : CompiledStatement
{
    public required CompiledStatementWithValue Base { get; init; }
    public required CompiledStatementWithValue Index { get; init; }
    public required CompiledStatementWithValue Value { get; init; }
    public required bool IsCompoundAssignment { get; init; }

    public override string Stringify(int depth = 0) => $"{Base.Stringify(depth + 1)}[{Index.Stringify(depth + 1)}] = {Value.Stringify(depth + 1)}";
    public override string ToString() => $"{Base}[{Index}] = {Value}";

    public CompiledIndexGetter ToGetter() => new()
    {
        Base = Base,
        Index = Index,
        Location = Location,
        SaveValue = true,
        Type = Value.Type,
    };
}

public class RegisterSetter : CompiledStatement
{
    public required Register Register { get; init; }
    public required CompiledStatementWithValue Value { get; init; }
    public required bool IsCompoundAssignment { get; init; }

    public override string Stringify(int depth = 0) => $"{Register} = {Value.Stringify(depth + 1)}";
    public override string ToString() => $"{Register} = {Value}";
}

public class CompiledVariableGetter : CompiledStatementWithValue
{
    public required CompiledVariableDeclaration Variable { get; init; }

    public override string Stringify(int depth = 0) => $"{Variable.Identifier}";
    public override string ToString() => $"{Variable.Identifier}";
}

public class CompiledParameterGetter : CompiledStatementWithValue
{
    public required CompiledParameter Variable { get; init; }

    public override string Stringify(int depth = 0) => $"{Variable.Identifier}";
    public override string ToString() => $"{Variable.Identifier}";
}

public class CompiledFieldGetter : CompiledStatementWithValue
{
    public required CompiledStatementWithValue Object { get; init; }
    public required CompiledField Field { get; init; }

    public override string Stringify(int depth = 0) => $"{Object.Stringify(depth + 1)}.{Field.Identifier}";
    public override string ToString() => $"{Object}.{Field.Identifier}";
}

public class RegisterGetter : CompiledStatementWithValue
{
    public required Register Register { get; init; }

    public override string Stringify(int depth = 0) => $"{Register}";
    public override string ToString() => $"{Register}";
}

public class CompiledStringInstance : CompiledStatementWithValue
{
    public required string Value { get; init; }
    public required bool IsASCII { get; init; }
    public required CompiledStatementWithValue Allocator { get; init; }

    public override string Stringify(int depth = 0) => $"\"{Value}\"";
    public override string ToString() => $"\"{Value}\"";
}

public class CompiledStackStringInstance : CompiledStatementWithValue
{
    public required string Value { get; init; }
    public required bool IsNullTerminated { get; init; }
    public required bool IsASCII { get; init; }

    public int Length => IsNullTerminated ? Value.Length + 1 : Value.Length;

    public override string Stringify(int depth = 0) => $"\"{Value}\"";
    public override string ToString() => $"\"{Value}\"";
}

public class FunctionAddressGetter : CompiledStatementWithValue
{
    public required IHaveInstructionOffset Function { get; init; }

    public override string Stringify(int depth = 0) => $"&{Function switch
    {
        CompiledFunctionDefinition v => v.Identifier,
        _ => Function.ToString(),
    }}";
    public override string ToString() => $"&{Function switch
    {
        CompiledFunctionDefinition v => v.Identifier,
        _ => Function.ToString(),
    }}";
}

public class InstructionLabelAddressGetter : CompiledStatementWithValue
{
    public required CompiledInstructionLabelDeclaration InstructionLabel { get; init; }

    public override string Stringify(int depth = 0) => $"&{InstructionLabel.Identifier}";
    public override string ToString() => $"&{InstructionLabel.Identifier}";
}
