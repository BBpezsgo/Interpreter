namespace LanguageCore.Compiler;

public class CompiledIf : CompiledBranch
{
    public required CompiledExpression Condition { get; init; }
    public required CompiledStatement Body { get; init; }
    public required CompiledBranch? Next { get; init; }

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();

        res.Append($"if ({Condition.Stringify(depth + 1)})");
        res.Append(' ');
        res.Append(Body.Stringify(depth));

        if (Next is CompiledElse _else)
        {
            res.AppendLine();
            res.Append(' ', depth * Identation);
            res.Append($"else");
            res.Append(' ');
            res.Append(_else.Body.Stringify(depth));
        }
        else if (Next is CompiledIf _elseIf)
        {
            res.AppendLine();
            res.Append(' ', depth * Identation);
            res.Append($"elseif {_elseIf.Condition.Stringify(depth + 1)}");
            res.Append(' ');
            res.Append(_elseIf.Body.Stringify(depth));
        }
        else if (Next is not null)
        {
            res.AppendLine();
            res.Append(' ', depth * Identation);
            res.Append($"else");
            res.Append(' ');
            res.Append(Next.Stringify(depth));
        }

        return res.ToString();
    }

    public override string ToString()
    {
        StringBuilder res = new();

        res.Append($"if ({Condition.ToString()})");
        res.Append(' ');
        res.Append(Body.ToString());

        if (Next is not null)
        {
            res.Append("...");
        }

        return res.ToString();
    }
}
