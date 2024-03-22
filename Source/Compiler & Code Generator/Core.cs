namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;
using Runtime;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct ValueAddress
{
    public readonly int Address;
    public readonly AddressingMode AddressingMode;
    public readonly bool IsReference;
    public readonly bool InHeap;

    public ValueAddress(int address, AddressingMode addressingMode, bool isReference = false, bool inHeap = false)
    {
        Address = address;
        AddressingMode = addressingMode;
        IsReference = isReference;
        InHeap = inHeap;
    }

    public ValueAddress(CompiledVariable variable)
    {
        Address = variable.MemoryAddress;
        AddressingMode = AddressingMode.BasePointerRelative;
        IsReference = false;
        InHeap = false;
    }

    public ValueAddress(CompiledParameter parameter, int address)
    {
        Address = address;
        AddressingMode = AddressingMode.BasePointerRelative;
        IsReference = parameter.IsRef;
        InHeap = false;
    }

    public static ValueAddress operator +(ValueAddress address, int offset) => new(address.Address + offset, address.AddressingMode, address.IsReference, address.InHeap);

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append('(');
        result.Append(Address);

        switch (AddressingMode)
        {
            case AddressingMode.Absolute:
                result.Append(" (ABS)");
                break;
            case AddressingMode.Runtime:
                result.Append(" (RNT)");
                break;
            case AddressingMode.BasePointerRelative:
                result.Append(" (BPR)");
                break;
            case AddressingMode.StackRelative:
                result.Append(" (SR)");
                break;
            default:
                break;
        }

        if (IsReference)
        { result.Append(" | IsRef"); }
        if (InHeap)
        { result.Append(" | InHeap"); }
        result.Append(')');
        return result.ToString();
    }
    string GetDebuggerDisplay() => ToString();
}

readonly struct UndefinedOffset<TFunction>
{
    public int InstructionIndex { get; }
    public bool IsAbsoluteAddress { get; }

    public Position CallerPosition { get; }
    public TFunction Called { get; }

    public Uri? CurrentFile { get; }

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, IPositioned? caller, TFunction called, Uri? file)
        : this(callInstructionIndex, isAbsoluteAddress, caller?.Position, called, file)
    { }

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, Position? callerPosition, TFunction called, Uri? file)
        : this(callInstructionIndex, isAbsoluteAddress, callerPosition ?? Position.UnknownPosition, called, file)
    { }

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, Position callerPosition, TFunction called, Uri? file)
    {
        InstructionIndex = callInstructionIndex;
        IsAbsoluteAddress = isAbsoluteAddress;

        CallerPosition = callerPosition;
        Called = called;

        CurrentFile = file;
    }
}

public readonly struct Reference
{
    public Uri? SourceFile { get; }
    public ISameCheck? SourceContext { get; }

    public Reference(Uri? sourceFile = null, ISameCheck? sourceContext = null)
    {
        SourceFile = sourceFile;
        SourceContext = sourceContext;
    }

    public static implicit operator Reference(ValueTuple<Uri?, ISameCheck?> v) => new(v.Item1, v.Item2);
}

public readonly struct Reference<TSource>
{
    public TSource Source { get; }
    public Uri? SourceFile { get; }
    public ISameCheck? SourceContext { get; }

    public Reference(TSource source, Reference reference)
    {
        Source = source;
        SourceFile = reference.SourceFile;
        SourceContext = reference.SourceContext;
    }

    public Reference(TSource source, Uri? sourceFile = null, ISameCheck? sourceContext = null)
    {
        Source = source;
        SourceFile = sourceFile;
        SourceContext = sourceContext;
    }

    public static implicit operator Reference<TSource>(ValueTuple<TSource, Uri?, ISameCheck?> v) => new(v.Item1, v.Item2, v.Item3);
    public static implicit operator Reference(Reference<TSource> v) => new(v.SourceFile, v.SourceContext);
}

public interface IHaveInstructionOffset
{
    public int InstructionOffset { get; set; }
}

public interface ICompiledFunction
{
    public GeneralType Type { get; }
    public bool ReturnSomething => Type != BasicType.Void;
    public Block? Block { get; }
    public IReadOnlyList<ParameterDefinition> Parameters { get; }
    public IReadOnlyList<GeneralType> ParameterTypes { get; }
}

public interface ITemplateable<TSelf> where TSelf : notnull
{
    public TSelf InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters);
}

public interface IReferenceable
{
    public IEnumerable<Reference> References { get; }
}

public interface IReferenceable<TBy> : IReferenceable
    where TBy : notnull
{
    public new List<Reference<TBy>> References { get; }
    IEnumerable<Reference> IReferenceable.References => References.Select(v => (Reference)v);
}

public interface IHaveCompiledType : IProbablyHaveCompiledType
{
    public new GeneralType Type { get; }
    GeneralType? IProbablyHaveCompiledType.Type => Type;
}

public interface IProbablyHaveCompiledType
{
    public GeneralType? Type { get; }
}

public interface IInContext<TContext>
{
    public TContext Context { get; }
}

public enum Protection
{
    Private,
    Public,
}

public interface ISameCheck
{
    public bool IsSame(object? other);
}

public interface ISameCheck<TOther> : ISameCheck
{
    public bool IsSame(TOther other);

    bool ISameCheck.IsSame(object? other) => other is TOther _other && IsSame(_other);
}
