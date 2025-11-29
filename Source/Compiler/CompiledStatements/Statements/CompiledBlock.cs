namespace LanguageCore.Compiler;

public class CompiledBlock : CompiledStatement
{
    public required ImmutableArray<CompiledStatement> Statements { get; init; }

    public static CompiledBlock CreateIfNot(CompiledStatement statement) =>
        statement is CompiledBlock block
        ? block
        : new CompiledBlock()
        {
            Location = statement.Location,
            Statements = ImmutableArray.Create(statement),
        };

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();
        res.AppendLine();
        res.Append(' ', depth * Identation);
        res.AppendLine("{");

        foreach (CompiledStatement statement in Statements)
        {
            if (statement is CompiledEmptyStatement) continue;
            res.Append(' ', (depth + 1) * Identation);
            res.Append(statement.Stringify(depth + 1));
            res.Append(';');
            res.AppendLine();
        }

        res.Append(' ', depth * Identation);
        res.Append('}');
        return res.ToString();
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append('{');

        switch (Statements.Length)
        {
            case 0:
                result.Append(' ');
                break;
            case 1:
                result.Append(' ');
                result.Append(Statements[0]);
                result.Append(' ');
                break;
            default:
                result.Append("...");
                break;
        }

        result.Append('}');

        return result.ToString();
    }
}
