using LanguageCore.Compiler;
using LanguageCore.Parser;

namespace LanguageCore.Brainfuck.Generator;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class BrainfuckVariable :
    IIdentifiable<string>,
    IHaveCompiledType,
    IInFile
{
    public readonly string Name;
    public readonly int Address;
    public readonly Uri File;
    /// <summary>
    /// The address is already the value of the variable.
    /// This is used for pointer dereferencing to like references.
    /// </summary>
    public readonly bool IsReference;

    public readonly bool HaveToClean;
    public readonly bool DeallocateOnClean;

    public readonly GeneralType Type;
    public readonly int Size;

    public bool IsDiscarded;
    public bool IsInitialized;

    string IIdentifiable<string>.Identifier => Name;
    GeneralType IHaveCompiledType.Type => Type;
    Uri IInFile.File => File;

    public BrainfuckVariable(string name, Uri file, int address, bool isReference, bool haveToClean, bool deallocateOnClean, GeneralType type, int size)
    {
        Name = name;
        Address = address;
        File = file;
        IsReference = isReference;

        HaveToClean = haveToClean;
        DeallocateOnClean = deallocateOnClean;

        Type = type;
        IsDiscarded = false;
        Size = size;
        IsInitialized = false;
    }

    [ExcludeFromCodeCoverage]
    string GetDebuggerDisplay() => $"{Type} {Name} ({Size} bytes at {Address})";
}
