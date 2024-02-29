namespace LanguageCore.Compiler;

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

    public ValueAddress(BBCode.Generator.CompiledParameter parameter, int address)
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
    public readonly int InstructionIndex;
    public readonly bool IsAbsoluteAddress;

    public readonly Statement? Caller;
    public readonly TFunction Called;

    public readonly Uri? CurrentFile;

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, Statement? caller, TFunction called, Uri? file)
    {
        InstructionIndex = callInstructionIndex;
        IsAbsoluteAddress = isAbsoluteAddress;

        Caller = caller;
        Called = called;

        CurrentFile = file;
    }
}

public readonly struct Reference
{
    public readonly Uri? SourceFile;
    public readonly ISameCheck? SourceContext;

    public Reference(Uri? sourceFile = null, ISameCheck? sourceContext = null)
    {
        SourceFile = sourceFile;
        SourceContext = sourceContext;
    }

    public static implicit operator Reference(ValueTuple<Uri?, ISameCheck?> v) => new(v.Item1, v.Item2);
}

public readonly struct Reference<TSource>
{
    public readonly TSource Source;
    public readonly Uri? SourceFile;
    public readonly ISameCheck? SourceContext;

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

    public Reference<TTarget> Cast<TTarget>(Func<TSource, TTarget> caster) => new(caster.Invoke(Source), SourceFile, SourceContext);
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

public interface IInContext<T>
{
    public T Context { get; }
}

public enum Protection
{
    Private,
    Public,
}

public interface ISameCheck
{
    public bool IsSame(ISameCheck? other);
}

public interface ISameCheck<T> : ISameCheck
{
    public bool IsSame(T other);

    bool ISameCheck.IsSame(ISameCheck? other) => IsSame(other);
}
