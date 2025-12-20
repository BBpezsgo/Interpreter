using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public static partial class StatementWalkerFilter
{
    public static bool FrameOnlyFilter(Statement statement) => statement is not LambdaExpression;
    public static bool All(Statement _) => true;
}

public static partial class StatementWalker
{
    public static IEnumerable<Statement> Visit(Statement? statement) => Visit(statement, StatementWalkerFilter.All);
    public static IEnumerable<Statement> Visit(Statement? statement, Func<Statement, bool> callback)
    {
        if (statement is null) yield break;
        if (!callback(statement)) yield break;
        yield return statement;

        switch (statement)
        {
            case AnyCallExpression v:
                foreach (Statement w in Visit(v.Expression, callback)) yield return w;
                foreach (Statement w in Visit(v.Arguments, callback)) yield return w;
                break;
            case ArgumentExpression v:
                foreach (Statement w in Visit(v.Value, callback)) yield return w;
                break;
            case BinaryOperatorCallExpression v:
                foreach (Statement w in Visit(v.Left, callback)) yield return w;
                foreach (Statement w in Visit(v.Right, callback)) yield return w;
                break;
            case ConstructorCallExpression v:
                foreach (Statement w in Visit(v.Type, callback)) yield return w;
                foreach (Statement w in Visit(v.Arguments, callback)) yield return w;
                break;
            case DereferenceExpression v:
                foreach (Statement w in Visit(v.Expression, callback)) yield return w;
                break;
            case FieldExpression v:
                foreach (Statement w in Visit(v.Object, callback)) yield return w;
                foreach (Statement w in Visit(v.Identifier, callback)) yield return w;
                break;
            case FunctionCallExpression v:
                if (v.Object is not null) foreach (Statement w in Visit(v.Object, callback)) yield return w;
                foreach (Statement w in Visit(v.Arguments, callback)) yield return w;
                foreach (Statement w in Visit(v.Identifier, callback)) yield return w;
                break;
            case GetReferenceExpression v:
                foreach (Statement w in Visit(v.Expression, callback)) yield return w;
                break;
            case IdentifierExpression v:
                break;
            case IndexCallExpression v:
                foreach (Statement w in Visit(v.Object, callback)) yield return w;
                foreach (Statement w in Visit(v.Index, callback)) yield return w;
                break;
            case LambdaExpression v:
                //foreach (var item in v.Parameters.Parameters)
                //{
                //    Visit(item.Type, callback);
                //}
                foreach (Statement w in Visit(v.Body, callback)) yield return w;
                break;
            case ListExpression v:
                foreach (Statement w in Visit(v.Values, callback)) yield return w;
                break;
            case LiteralExpression v:
                break;
            case ManagedTypeCastExpression v:
                foreach (Statement w in Visit(v.Expression, callback)) yield return w;
                foreach (Statement w in Visit(v.Type, callback)) yield return w;
                break;
            case NewInstanceExpression v:
                foreach (Statement w in Visit(v.Type, callback)) yield return w;
                break;
            case ReinterpretExpression v:
                foreach (Statement w in Visit(v.PrevStatement, callback)) yield return w;
                foreach (Statement w in Visit(v.Type, callback)) yield return w;
                break;
            case UnaryOperatorCallExpression v:
                foreach (Statement w in Visit(v.Expression, callback)) yield return w;
                break;
            case CompiledVariableConstant v:
                foreach (Statement w in Visit(v.InitialValue, callback)) yield return w;
                break;
            case ShortOperatorCall v:
                foreach (Statement w in Visit(v.Expression, callback)) yield return w;
                break;
            case Block v:
                foreach (Statement w in Visit(v.Statements, callback)) yield return w;
                break;
            case CompoundAssignmentStatement v:
                foreach (Statement w in Visit(v.Left, callback)) yield return w;
                foreach (Statement w in Visit(v.Right, callback)) yield return w;
                break;
            case ElseBranchStatement v:
                foreach (Statement w in Visit(v.Body, callback)) yield return w;
                break;
            case ElseIfBranchStatement v:
                foreach (Statement w in Visit(v.Condition, callback)) yield return w;
                foreach (Statement w in Visit(v.Body, callback)) yield return w;
                break;
            case ForLoopStatement v:
                foreach (Statement w in Visit(v.Initialization, callback)) yield return w;
                foreach (Statement w in Visit(v.Condition, callback)) yield return w;
                foreach (Statement w in Visit(v.Step, callback)) yield return w;
                foreach (Statement w in Visit(v.Block, callback)) yield return w;
                break;
            case IfBranchStatement v:
                foreach (Statement w in Visit(v.Condition, callback)) yield return w;
                foreach (Statement w in Visit(v.Body, callback)) yield return w;
                break;
            case IfContainer v:
                foreach (BranchStatementBase item in v.Branches) foreach (Statement w in Visit(item, callback)) yield return w;
                break;
            case InstructionLabelDeclaration v:
                foreach (Statement w in Visit(v.Identifier, callback)) yield return w;
                break;
            case KeywordCallStatement v:
                foreach (Statement w in Visit(v.Identifier, callback)) yield return w;
                foreach (Statement w in Visit(v.Arguments, callback)) yield return w;
                break;
            case LinkedElse v:
                foreach (Statement w in Visit(v.Body, callback)) yield return w;
                break;
            case LinkedIf v:
                foreach (Statement w in Visit(v.Condition, callback)) yield return w;
                foreach (Statement w in Visit(v.Body, callback)) yield return w;
                foreach (Statement w in Visit(v.NextLink, callback)) yield return w;
                break;
            case SimpleAssignmentStatement v:
                foreach (Statement w in Visit(v.Target, callback)) yield return w;
                foreach (Statement w in Visit(v.Value, callback)) yield return w;
                break;
            case VariableDefinition v:
                foreach (Statement w in Visit(v.Type, callback)) yield return w;
                foreach (Statement w in Visit(v.Identifier, callback)) yield return w;
                foreach (Statement w in Visit(v.InitialValue, callback)) yield return w;
                break;
            case WhileLoopStatement v:
                foreach (Statement w in Visit(v.Condition, callback)) yield return w;
                foreach (Statement w in Visit(v.Body, callback)) yield return w;
                break;
            default: throw new NotImplementedException(statement.GetType().Name);
        }
    }
    static IEnumerable<Statement> Visit(IEnumerable<Statement> statements, Func<Statement, bool> callback)
    {
        foreach (Statement statement in statements)
        {
            foreach (Statement v in Visit(statement, callback)) yield return v;
        }
    }
    static IEnumerable<Statement> Visit(TypeInstance? type, Func<Statement, bool> callback)
    {
        if (type is null) yield break;

        switch (type)
        {
            case TypeInstanceFunction v:
                foreach (Statement w in Visit(v.FunctionReturnType, callback)) yield return w;
                foreach (TypeInstance item in v.FunctionParameterTypes) foreach (Statement w in Visit(item, callback)) yield return w;
                break;
            case TypeInstancePointer v:
                foreach (Statement w in Visit(v.To, callback)) yield return w;
                break;
            case TypeInstanceSimple v:
                break;
            case TypeInstanceStackArray v:
                foreach (Statement w in Visit(v.StackArrayOf, callback)) yield return w;
                foreach (Statement w in Visit(v.StackArraySize, callback)) yield return w;
                break;
            default: throw new UnreachableException(type.GetType().Name);
        }
    }
}
