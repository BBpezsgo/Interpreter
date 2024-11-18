using LanguageCore.Compiler;
using LanguageCore.Parser;

namespace LanguageCore.Brainfuck.Generator;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class BrainfuckVariable :
    CompiledVariable,
    IIdentifiable<string>,
    IHaveCompiledType,
    IInFile
{
    public string Name => Identifier.Content;
    public readonly int Address;
    public readonly bool IsReference;

    public readonly bool HaveToClean;
    public readonly bool DeallocateOnClean;

    public readonly int Size;

    public bool IsDiscarded;

    string IIdentifiable<string>.Identifier => Name;
    GeneralType IHaveCompiledType.Type => Type;
    Uri IInFile.File => File;

    public BrainfuckVariable(int address, bool isReference, bool haveToClean, bool deallocateOnClean, GeneralType type, int size, Parser.Statement.VariableDeclaration declaration)
        : base(address, type, declaration)
    {
        Address = address;
        IsReference = isReference;

        HaveToClean = haveToClean;
        DeallocateOnClean = deallocateOnClean;

        IsDiscarded = false;
        Size = size;
        IsInitialized = false;
    }

    [ExcludeFromCodeCoverage]
    string GetDebuggerDisplay() => $"{Type} {Name} ({Size} bytes at {Address})";
}
