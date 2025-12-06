using LanguageCore.Compiler;

namespace LanguageCore.Brainfuck.Generator;

public class BrainfuckVariable :
    IIdentifiable<string>,
    IHaveCompiledType,
    IInFile
{
    public readonly CompiledVariableDefinition Declaration;
    public readonly int Address;
    public readonly bool IsReference;
    public readonly bool HaveToClean;
    public readonly CompiledCleanup? Cleanup;
    public readonly int Size;
    public bool IsDiscarded;
    public bool IsInitialized;

    public string Identifier => Declaration.Identifier;
    public GeneralType Type => Declaration.Type;
    public Uri File => Declaration.Location.File;

    public BrainfuckVariable(int address, bool isReference, bool haveToClean, CompiledCleanup? cleanup, int size, CompiledVariableDefinition declaration)
    {
        Declaration = declaration;
        Address = address;
        IsReference = isReference;
        HaveToClean = haveToClean;
        Cleanup = cleanup;
        IsDiscarded = false;
        Size = size;
    }

    public BrainfuckVariable(int address, bool isReference, bool haveToClean, CompiledCleanup? cleanup, int size, BrainfuckVariable declaration)
    {
        Address = address;
        IsReference = isReference;
        HaveToClean = haveToClean;
        Cleanup = cleanup;
        IsDiscarded = false;
        Size = size;
        Declaration = declaration.Declaration;
    }
}
