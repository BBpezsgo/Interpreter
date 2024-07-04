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
    public readonly BitWidth DataSize;

    public ValueAddress(int address, AddressingMode addressingMode)
    {
        Address = address;
        AddressingMode = addressingMode;
        IsReference = false;
    }

    public ValueAddress(int address, AddressingMode addressingMode, bool isReference)
    {
        Address = address;
        AddressingMode = addressingMode;
        IsReference = isReference;
    }

    public ValueAddress(CompiledVariable variable)
    {
        Address = variable.MemoryAddress;
        AddressingMode = AddressingMode.PointerBP;
        IsReference = false;
    }

    public static ValueAddress operator +(ValueAddress address, int offset) => new(address.Address + offset, address.AddressingMode, address.IsReference);
    public static ValueAddress operator -(ValueAddress address, int offset) => new(address.Address - offset, address.AddressingMode, address.IsReference);

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append('*');
        result.Append('[');

        switch (AddressingMode)
        {
            case AddressingMode.Pointer:
                result.Append(Address);
                break;
            case AddressingMode.PointerBP:
                result.Append("BP");
                if (Address > 0)
                {
                    result.Append('+');
                    result.Append(Address);
                }
                else
                {
                    result.Append(Address);
                }
                break;
            case AddressingMode.PointerSP:
                result.Append("SP+");
                if (Address > 0)
                {
                    result.Append('+');
                    result.Append(Address);
                }
                else
                {
                    result.Append(Address);
                }
                break;
            default:
                throw new UnreachableException();
        }

        result.Append(']');

        return result.ToString();
    }
    string GetDebuggerDisplay() => ToString();

    public ValueAddress ToUnreferenced() => new(Address, AddressingMode);
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

public interface ICompiledFunction :
    IHaveCompiledType,
    IInFile
{
    public bool ReturnSomething => Type != BasicType.Void;
    public Block? Block { get; }
    public IReadOnlyList<ParameterDefinition> Parameters { get; }
    public IReadOnlyList<GeneralType> ParameterTypes { get; }
}

public interface ITemplateable<TSelf> where TSelf : notnull
{
    public bool IsTemplate { get; }
    public TSelf InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters);
}

public interface IReferenceable
{
    public IEnumerable<Reference> References { get; }
}

public interface IReferenceable<TBy> : IReferenceable
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

public interface IIdentifiable<TIdentifier>
{
    public TIdentifier Identifier { get; }
}
