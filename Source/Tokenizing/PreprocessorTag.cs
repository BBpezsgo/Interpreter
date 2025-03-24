namespace LanguageCore.Tokenizing;

public class PreprocessorTag :
    IPositioned
{
    /// <summary>
    /// Set by the <see cref="Parser"/>
    /// </summary>
    public Token? Semicolon { get; internal set; }
    public Token Identifier { get; }
    public Token? Argument { get; }

    public Position Position => new(Identifier, Argument, Semicolon);

    public PreprocessorTag(Token identifier, Token? argument)
    {
        Identifier = identifier;
        Argument = argument;
    }

    public override string ToString() => $"{Identifier} {Argument}".Trim();
}
