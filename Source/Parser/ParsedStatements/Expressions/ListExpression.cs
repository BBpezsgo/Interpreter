namespace LanguageCore.Parser.Statements;

public class ListExpression : Expression
{
    public ImmutableArray<Expression> Values { get; }
    public TokenPair Brackets { get; }

    public override Position Position => new(Brackets);

    public ListExpression(ImmutableArray<Expression> values, TokenPair brackets, Uri file) : base(file)
    {
        Brackets = brackets;
        Values = values;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(SurroundingBrackets?.Start);
        result.Append(Brackets.Start);

        if (Values.Length == 0)
        {
            result.Append(' ');
        }
        else
        {
            for (int i = 0; i < Values.Length; i++)
            {
                if (i > 0)
                { result.Append(", "); }
                if (result.Length >= Stringify.CozyLength)
                { result.Append("..."); break; }

                result.Append(Values[i]);
            }
        }
        result.Append(Brackets.End);
        result.Append(SurroundingBrackets?.End);

        if (Semicolon != null) result.Append(Semicolon);

        return result.ToString();
    }
}
