namespace LanguageCore.Parser.Statements;

public class Block : Statement
{
    public ImmutableArray<Statement> Statements { get; }
    public TokenPair Brackets { get; }

    public override Position Position => new(Brackets);

    public Block(ImmutableArray<Statement> statements, TokenPair brackets, Uri file) : base(file)
    {
        Statements = statements;
        Brackets = brackets;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Brackets.Start);

        if (Statements.Length == 0)
        { result.Append(' '); }
        else if (Statements.Length == 1)
        {
            result.Append(' ');
            result.Append(Statements[0]);
            result.Append(' ');
        }
        else
        { result.Append("..."); }

        result.Append(Brackets.End);
        result.Append(Semicolon);

        return result.ToString();
    }

    public static Block CreateIfNotBlock(Statement statement)
    {
        if (statement is Block block) return block;
        return new Block(
            ImmutableArray.Create(statement),
            TokenPair.CreateAnonymous(statement.Position, "{", "}"),
            statement.File
        );
    }
}
