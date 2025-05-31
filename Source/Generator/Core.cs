using LanguageCore.Parser;

namespace LanguageCore.Compiler;

readonly struct UndefinedOffset
{
    public int InstructionIndex { get; }
    public bool IsAbsoluteAddress { get; }

    public Location CallerLocation { get; }
    public IHaveInstructionOffset Called { get; }

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, ILocated caller, IHaveInstructionOffset called)
        : this(callInstructionIndex, isAbsoluteAddress, caller.Location, called)
    { }

    public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, Location callerLocation, IHaveInstructionOffset called)
    {
        InstructionIndex = callInstructionIndex;
        IsAbsoluteAddress = isAbsoluteAddress;

        CallerLocation = callerLocation;
        Called = called;
    }

    public void Apply(List<Runtime.PreparationInstruction> code)
    {
        int offset = IsAbsoluteAddress ? Called.InstructionOffset : Called.InstructionOffset - InstructionIndex;
        code[InstructionIndex].Operand1 = offset;
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
    int InstructionOffset { get; set; }
}

public interface ITemplateable<TSelf> where TSelf : notnull
{
    bool IsTemplate { get; }
    TSelf InstantiateTemplate(IReadOnlyDictionary<string, GeneralType> parameters);
}

public interface IReferenceable
{
    IEnumerable<Reference> References { get; }
}

public interface IReferenceable<TBy> : IReferenceable
{
    new List<Reference<TBy>> References { get; }
    IEnumerable<Reference> IReferenceable.References => References.Select(v => (Reference)v);
}

public interface IHaveCompiledType
{
    GeneralType Type { get; }
}

public interface IInContext<TContext>
{
    TContext Context { get; }
}

public enum Protection
{
    Private,
    Public,
}

public interface IDefinition { }

public interface IDefinition<TOther> : IDefinition
{
    bool DefinitionEquals(TOther other);
}

public interface IIdentifiable<TIdentifier>
{
    TIdentifier Identifier { get; }
}

public interface IHaveAttributes
{
    ImmutableArray<AttributeUsage> Attributes { get; }
}

public interface IExternalFunctionDefinition
{
    string? ExternalFunctionName { get; }
}

public interface IExposeable
{
    string? ExposedFunctionName { get; }
}
