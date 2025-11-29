namespace LanguageCore.Compiler;

public abstract class CompiledStatement : ILocated, IInFile, IPositioned
{
    public required Location Location { get; init; }

    Uri IInFile.File => Location.File;
    Position IPositioned.Position => Location.Position;

    protected const int Identation = 2;
    protected const int CozyLength = 30;

    public abstract string Stringify(int depth = 0);
    public abstract override string ToString();
}
