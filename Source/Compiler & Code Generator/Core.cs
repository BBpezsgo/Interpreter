namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;
using Runtime;

public abstract class Address;

public class AddressOffset : Address
{
    public Address Base { get; }
    public int Offset { get; }

    public AddressOffset(Register register, int offset)
    {
        Base = new AddressRegisterPointer(register);
        Offset = offset;
    }

    public AddressOffset(Address @base, int offset)
    {
        if (@base is AddressOffset baseAddressOffset)
        {
            Base = baseAddressOffset.Base;
            Offset = baseAddressOffset.Offset + offset;
        }
        else
        {
            Base = @base;
            Offset = offset;
        }
    }

    public override string ToString() => Offset switch
    {
        > 0 => $"{Base} + {Offset}",
        < 0 => $"{Base} - {-Offset}",
        _ => $"{Base}"
    };
}

public class AddressRuntimePointer : Address
{
    public Address PointerAddress { get; }

    public AddressRuntimePointer(Address pointerAddress)
    {
        PointerAddress = pointerAddress;
    }

    public override string ToString() => $"*[{PointerAddress}]";
}

public class AddressRegisterPointer : Address
{
    public Register Register { get; }

    public AddressRegisterPointer(Register register)
    {
        Register = register;
    }

    public override string ToString() => $"{Register}";
}

public readonly struct ValueAddress
{
    public readonly Address Address;
    public readonly BitWidth DataSize;

    public ValueAddress(Address address, BitWidth dataSize)
    {
        Address = address;
        DataSize = dataSize;
    }
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
