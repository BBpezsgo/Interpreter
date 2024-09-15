using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

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

readonly struct UndefinedOffset<TFunction>
{
    public int InstructionIndex { get; }
    public bool IsAbsoluteAddress { get; }

    public Position CallerPosition { get; }
    public Uri? CallerFile { get; }

    public TFunction Called { get; }

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, IPositioned? caller, TFunction called, Uri? file)
        : this(callInstructionIndex, isAbsoluteAddress, caller?.Position ?? Position.UnknownPosition, called, file)
    { }

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, Position callerPosition, TFunction called, Uri? file)
    {
        InstructionIndex = callInstructionIndex;
        IsAbsoluteAddress = isAbsoluteAddress;

        CallerPosition = callerPosition;
        Called = called;

        CallerFile = file;
    }
}

public static class ReferenceExtensions
{
    public static void Add(this List<Reference> references, Uri? sourceFile) => references.Add(new Reference(sourceFile));
    public static void Add<TSource>(this List<Reference<TSource>> references, TSource source, Uri? sourceFile = null) => references.Add(new Reference<TSource>(source, sourceFile));
}

public readonly struct Reference
{
    public Uri? SourceFile { get; }

    public Reference(Uri? sourceFile = null)
    {
        SourceFile = sourceFile;
    }
}

public readonly struct Reference<TSource>
{
    public TSource Source { get; }
    public Uri? SourceFile { get; }

    public Reference(TSource source, Uri? sourceFile = null)
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

public interface IDefinition;

public interface IDefinition<TOther> : IDefinition
{
    public bool DefinitionEquals(TOther other);
}

public interface IIdentifiable<TIdentifier>
{
    public TIdentifier Identifier { get; }
}
