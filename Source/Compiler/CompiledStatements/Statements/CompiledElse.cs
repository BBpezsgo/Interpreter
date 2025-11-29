namespace LanguageCore.Compiler;

public class CompiledElse : CompiledBranch
{
    public required CompiledStatement Body { get; init; }

    public override string Stringify(int depth = 0)
    {
        StringBuilder res = new();

        res.AppendLine();
        res.Append(' ', depth * Identation);
        res.Append($"else");
        res.Append(' ');
        res.Append(Body.Stringify(depth + 1));

        return res.ToString();
    }

    public override string ToString()
    {
        StringBuilder res = new();

        res.Append($"else ");
        res.Append(Body.ToString());

        return res.ToString();
    }
}
