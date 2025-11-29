namespace LanguageCore.Compiler;

public class CompiledList : CompiledExpression
{
    public required ImmutableArray<CompiledExpression> Values { get; init; }

    public override string Stringify(int depth = 0) => $"[{string.Join(", ", Values.Select(v => v.Stringify(depth + 1)))}]";

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append('[');

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
                if (result.Length >= CozyLength)
                { result.Append("..."); break; }

                result.Append(Values[i]);
            }
        }
        result.Append(']');

        return result.ToString();
    }
}
