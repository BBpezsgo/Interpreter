namespace LanguageCore.Parser.Statements;

public abstract class AssignmentStatement : Statement
{
    protected AssignmentStatement(Uri file) : base(file)
    {

    }

    public abstract SimpleAssignmentStatement ToAssignment();
}
