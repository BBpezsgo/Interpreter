using LanguageCore.Tokenizing;

namespace LanguageCore.Parser.Statements;

public class SimpleAssignmentStatement : AssignmentStatement
{
    public Token Operator { get; }
    public Expression Target { get; }
    public Expression Value { get; }

    public override Position Position => new(Operator, Target, Value);

    public SimpleAssignmentStatement(
        Token @operator,
        Expression target,
        Expression value,
        Uri file) : base(file)
    {
        Operator = @operator;
        Target = target;
        Value = value;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (result.Length + Target.ToString().Length > Stringify.CozyLength)
        {
            result.Append($"... {Operator} ...");
        }
        else
        {
            result.Append(Target);
            result.Append(' ');
            result.Append(Operator);
            result.Append(' ');
            if (result.Length + Value.ToString().Length > Stringify.CozyLength)
            { result.Append("..."); }
            else
            { result.Append(Value); }
        }

        result.Append(Semicolon);
        return result.ToString();
    }
    public override SimpleAssignmentStatement ToAssignment() => this;
}
