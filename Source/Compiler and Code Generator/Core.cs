using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public abstract class Address
{
    public static Address operator -(Address a, int b)
        => a + -b;
    public static Address operator +(Address a, int b)
    {
        if (a is AddressOffset addressOffset)
        {
            return new AddressOffset(addressOffset.Base, addressOffset.Offset + b);
        }
        else if (a is AddressAbsolute addressAbsolute)
        {
            return new AddressAbsolute(addressAbsolute.Value + b);
        }
        else
        {
            return new AddressOffset(a, b);
        }
    }
}

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

    [ExcludeFromCodeCoverage]
    public override string ToString() => Offset switch
    {
        > 0 => $"{Base} + {Offset}",
        < 0 => $"{Base} - {-Offset}",
        _ => $"{Base} + 0"
    };
}

public class AddressPointer : Address
{
    public Address PointerAddress { get; }

    public AddressPointer(Address pointerAddress)
    {
        PointerAddress = pointerAddress;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"*[{PointerAddress}]";
}

public class AddressRegisterPointer : Address
{
    public Register Register { get; }

    public AddressRegisterPointer(Register register)
    {
        Register = register;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"{Register}";
}

public class AddressRuntimePointer : Address
{
    public StatementWithValue PointerValue { get; }

    public AddressRuntimePointer(StatementWithValue pointerValue)
    {
        PointerValue = pointerValue;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"*[{PointerValue}]";
}

public class AddressRuntimeIndex : Address
{
    public Address Base { get; }
    public StatementWithValue IndexValue { get; }
    public int ElementSize { get; }

    public AddressRuntimeIndex(Address @base, StatementWithValue indexValue, int elementSize)
    {
        Base = @base;
        IndexValue = indexValue;
        ElementSize = elementSize;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"({Base} + {IndexValue} * {ElementSize})";
}

public class AddressAbsolute : Address
{
    public int Value { get; }

    public AddressAbsolute(int value)
    {
        Value = value;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => Value.ToString();
}

readonly struct UndefinedOffset<TFunction>
{
    public int InstructionIndex { get; }
    public bool IsAbsoluteAddress { get; }

    public Location CallerLocation { get; }

    public TFunction Called { get; }

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, ILocated caller, TFunction called)
        : this(callInstructionIndex, isAbsoluteAddress, caller.Location, called)
    { }

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, Location callerLocation, TFunction called)
    {
        InstructionIndex = callInstructionIndex;
        IsAbsoluteAddress = isAbsoluteAddress;

        CallerLocation = callerLocation;
        Called = called;
    }
}

public static class ReferenceExtensions
{
    public static void AddReference<TSource>(this List<Reference<TSource>> references, TSource source, Uri sourceFile)
        => references.Add(new Reference<TSource>(source, sourceFile));

    public static void AddReference<TSource>(this List<Reference<TSource>> references, TSource source)
        where TSource : IInFile
        => references.Add(new Reference<TSource>(source, source.File));
}

public readonly struct Reference
{
    public Uri SourceFile { get; }

    public Reference(Uri sourceFile)
    {
        SourceFile = sourceFile;
    }
}

public readonly struct Reference<TSource>
{
    public TSource Source { get; }
    public Uri SourceFile { get; }

    public Reference(TSource source, Uri sourceFile)
    {
        Source = source;
        SourceFile = sourceFile;
    }

    public static implicit operator Reference(Reference<TSource> v) => new(v.SourceFile);
}

public interface IHaveInstructionOffset
{
    public int InstructionOffset { get; set; }
}

public interface ICompiledFunction :
    IHaveCompiledType,
    IInFile
{
    public bool ReturnSomething { get; }
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

public interface IHaveCompiledType
{
    public GeneralType Type { get; }
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

public interface IDefinition { }

public interface IDefinition<TOther> : IDefinition
{
    public bool DefinitionEquals(TOther other);
}

public interface IIdentifiable<TIdentifier>
{
    public TIdentifier Identifier { get; }
}

public interface IHaveAttributes
{
    public ImmutableArray<AttributeUsage> Attributes { get; }
}
