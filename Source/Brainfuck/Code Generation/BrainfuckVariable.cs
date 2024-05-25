namespace LanguageCore.Brainfuck.Generator;

using Compiler;
using Parser;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class BrainfuckVariable :
    IIdentifiable<string>,
    IHaveCompiledType,
    IInFile
{
    public readonly string Name;
    public readonly int Address;
    public readonly Uri File;

    public readonly bool HaveToClean;
    public readonly bool DeallocateOnClean;

    public readonly GeneralType Type;
    public readonly int Size;

    public bool IsDiscarded;
    public bool IsInitialized;

    string IIdentifiable<string>.Identifier => Name;
    GeneralType IHaveCompiledType.Type => Type;
    Uri IInFile.File => File;

    public BrainfuckVariable(string name, Uri file, int address, bool haveToClean, bool deallocateOnClean, GeneralType type)
        : this(name, file, address, haveToClean, deallocateOnClean, type, type.Size) { }
    public BrainfuckVariable(string name, Uri file, int address, bool haveToClean, bool deallocateOnClean, GeneralType type, int size)
    {
        Name = name;
        Address = address;
        File = file;

        HaveToClean = haveToClean;
        DeallocateOnClean = deallocateOnClean;

        Type = type;
        IsDiscarded = false;
        Size = size;
        IsInitialized = false;
    }

    string GetDebuggerDisplay() => $"{Type} {Name} ({Type.Size} bytes at {Address})";
}
