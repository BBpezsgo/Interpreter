using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public abstract class Statement : IPositioned, IInFile, ILocated
{
    /// <summary>
    /// Set by the <see cref="Parser"/>
    /// </summary>
    public Token? Semicolon { get; internal set; }
    public abstract Position Position { get; }
    public Uri File { get; }

    public Location Location => new(Position, File);

    protected Statement(Uri file)
    {
        Semicolon = null;
        File = file;
    }

    protected Statement(Statement other)
    {
        Semicolon = other.Semicolon;
        File = other.File;
    }

    public override string ToString()
        => $"{GetType().Name}{Semicolon}";
}
