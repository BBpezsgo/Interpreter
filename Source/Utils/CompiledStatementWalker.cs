namespace LanguageCore.Compiler;

public static partial class StatementWalker
{
    public static void VisitWithFunctions(IReadOnlyCollection<CompiledFunction> functions, CompiledStatement statement, Func<CompiledStatement, bool> callback, Action<CompiledFunction> functionCallback)
    {
        void TryFunctionCallback(ICompiledFunctionDefinition? function)
        {
            if (function is null) return;
            CompiledFunction? f = functions.FirstOrDefault(w => w.Function == function);
            if (f is null) return;
            functionCallback(f);
        }

        Visit(statement, statement =>
        {
            switch (statement)
            {
                case CompiledCleanup v:
                    TryFunctionCallback(v.Deallocator);
                    TryFunctionCallback(v.Destructor);
                    break;
                case CompiledFunctionCall v:
                    TryFunctionCallback(v.Function);
                    break;
                case CompiledExternalFunctionCall v:
                    TryFunctionCallback(v.Declaration);
                    break;
                case CompiledHeapAllocation v:
                    TryFunctionCallback(v.Allocator);
                    break;
                case CompiledConstructorCall v:
                    TryFunctionCallback(v.Function);
                    break;
                case CompiledDesctructorCall v:
                    TryFunctionCallback(v.Function);
                    break;
                case CompiledFunctionReference v:
                    TryFunctionCallback((ICompiledFunctionDefinition)v.Function);
                    break;
            }
            callback(statement);
            return true;
        });
    }

    static void Visit(IEnumerable<CompiledStatement> statement, Func<CompiledStatement, bool> callback)
    {
        foreach (CompiledStatement item in statement)
        {
            Visit(item, callback);
        }
    }
    public static void Visit(CompiledStatement statement, Func<CompiledStatement, bool> callback)
    {
        switch (statement)
        {
            case CompiledExpression v: Visit(v, callback); break;
            case CompiledEmptyStatement: break;
            case CompiledBlock v: Visit(v, callback); break;
            case CompiledIf v: Visit(v, callback); break;
            case CompiledElse v: Visit(v, callback); break;
            case CompiledVariableDefinition v: Visit(v, callback); break;
            case CompiledCrash v: Visit(v, callback); break;
            case CompiledDelete v: Visit(v, callback); break;
            case CompiledReturn v: Visit(v, callback); break;
            case CompiledBreak v: Visit(v, callback); break;
            case CompiledGoto v: Visit(v, callback); break;
            case CompiledLabelDeclaration v: Visit(v, callback); break;
            case CompiledWhileLoop v: Visit(v, callback); break;
            case CompiledForLoop v: Visit(v, callback); break;
            case CompiledSetter v: Visit(v, callback); break;
            case CompiledCleanup v: Visit(v, callback); break;
            default: throw new UnreachableException(statement.GetType().Name);
        }
    }
    static void Visit(CompiledCleanup statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledExpression statement, Func<CompiledStatement, bool> callback)
    {
        switch (statement)
        {
            case CompiledDummyExpression v: Visit(v.Statement, callback); break;
            case CompiledList v: Visit(v, callback); break;
            case CompiledRuntimeCall v: Visit(v, callback); break;
            case CompiledFunctionCall v: Visit(v, callback); break;
            case CompiledExternalFunctionCall v: Visit(v, callback); break;
            case CompiledSizeof v: Visit(v, callback); break;
            case CompiledArgument v: Visit(v, callback); break;
            case CompiledBinaryOperatorCall v: Visit(v, callback); break;
            case CompiledUnaryOperatorCall v: Visit(v, callback); break;
            case CompiledConstantValue v: Visit(v, callback); break;
            case CompiledGetReference v: Visit(v, callback); break;
            case CompiledDereference v: Visit(v, callback); break;
            case CompiledStackAllocation v: Visit(v, callback); break;
            case CompiledHeapAllocation v: Visit(v, callback); break;
            case CompiledConstructorCall v: Visit(v, callback); break;
            case CompiledDesctructorCall v: Visit(v, callback); break;
            case CompiledCast v: Visit(v, callback); break;
            case CompiledReinterpretation v: Visit(v, callback); break;
            case CompiledElementAccess v: Visit(v, callback); break;
            case CompiledVariableAccess v: Visit(v, callback); break;
            case CompiledExpressionVariableAccess v: Visit(v, callback); break;
            case CompiledParameterAccess v: Visit(v, callback); break;
            case CompiledFieldAccess v: Visit(v, callback); break;
            case CompiledRegisterAccess v: Visit(v, callback); break;
            case CompiledString v: Visit(v, callback); break;
            case CompiledStackString v: Visit(v, callback); break;
            case CompiledFunctionReference v: Visit(v, callback); break;
            case CompiledLabelReference v: Visit(v, callback); break;
            case CompiledCompilerVariableAccess v: Visit(v, callback); break;
            case CompiledLambda v: Visit(v, callback); break;
            default: throw new UnreachableException();
        }
    }
    static void Visit(CompiledLambda statement, Func<CompiledStatement, bool> callback)
    {
        if (statement.Allocator is not null) Visit(statement.Allocator, callback);
        if (!callback(statement)) return;
        Visit(statement.Block, callback);
    }
    static void Visit(CompiledTypeExpression statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        switch (statement)
        {
            case CompiledAliasTypeExpression v:
                Visit(v.Value, callback);
                break;
            case CompiledArrayTypeExpression v:
                Visit(v.Of, callback);
                if (v.Length is not null) Visit(v.Length, callback);
                break;
            case CompiledFunctionTypeExpression v:
                Visit(v.ReturnType, callback);
                foreach (CompiledTypeExpression i in v.Parameters) Visit(i, callback);
                break;
            case CompiledPointerTypeExpression v:
                Visit(v.To, callback);
                break;
            case CompiledStructTypeExpression v:
                foreach (KeyValuePair<string, CompiledTypeExpression> i in v.TypeArguments) Visit(i.Value, callback);
                break;
            case CompiledBuiltinTypeExpression:
            case CompiledGenericTypeExpression:
                break;
            default:
                throw new UnreachableException();
        }
    }
    static void Visit(CompiledBlock statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Statements, callback);
    }
    static void Visit(CompiledSetter statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Target, callback);
        Visit(statement.Value, callback);
    }
    static void Visit(CompiledIf statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Condition, callback);
        Visit(statement.Body, callback);
        if (statement.Next is not null) Visit(statement.Next, callback);
    }
    static void Visit(CompiledElse statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Body, callback);
    }
    static void Visit(CompiledVariableDefinition statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.TypeExpression, callback);
        if (statement.InitialValue is not null) Visit(statement.InitialValue, callback);
        if (statement.Cleanup is not null) Visit(statement.Cleanup, callback);
    }
    static void Visit(CompiledCrash statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Value, callback);
    }
    static void Visit(CompiledDelete statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Value, callback);
    }
    static void Visit(CompiledReturn statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        if (statement.Value is not null) Visit(statement.Value, callback);
    }
    static void Visit(CompiledBreak statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledGoto statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Value, callback);
    }
    static void Visit(CompiledLabelDeclaration statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledWhileLoop statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Condition, callback);
        Visit(statement.Body, callback);
    }
    static void Visit(CompiledForLoop statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        if (statement.Initialization is not null) Visit(statement.Initialization, callback);
        if (statement.Condition is not null) Visit(statement.Condition, callback);
        if (statement.Step is not null) Visit(statement.Step, callback);
        Visit(statement.Body, callback);
    }
    static void Visit(CompiledList statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Values, callback);
    }
    static void Visit(CompiledRuntimeCall statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Function, callback);
        Visit(statement.Arguments, callback);
    }
    static void Visit(CompiledFunctionCall statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Arguments, callback);
    }
    static void Visit(CompiledExternalFunctionCall statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Arguments, callback);
    }
    static void Visit(CompiledSizeof statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Of, callback);
    }
    static void Visit(CompiledArgument statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Value, callback);
        Visit(statement.Cleanup, callback);
    }
    static void Visit(CompiledBinaryOperatorCall statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Left, callback);
        Visit(statement.Right, callback);
    }
    static void Visit(CompiledUnaryOperatorCall statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Left, callback);
    }
    static void Visit(CompiledConstantValue statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledGetReference statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Of, callback);
    }
    static void Visit(CompiledDereference statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Address, callback);
    }
    static void Visit(CompiledStackAllocation statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.TypeExpression, callback);
    }
    static void Visit(CompiledHeapAllocation statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.TypeExpression, callback);
    }
    static void Visit(CompiledConstructorCall statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Object, callback);
        Visit(statement.Arguments, callback);
    }
    static void Visit(CompiledDesctructorCall statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Value, callback);
    }
    static void Visit(CompiledCast statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Value, callback);
        Visit(statement.TypeExpression, callback);
    }
    static void Visit(CompiledReinterpretation statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Value, callback);
        Visit(statement.TypeExpression, callback);
    }
    static void Visit(CompiledElementAccess statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Base, callback);
        Visit(statement.Index, callback);
    }
    static void Visit(CompiledExpressionVariableAccess statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledVariableAccess statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledParameterAccess statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledFieldAccess statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Object, callback);
    }
    static void Visit(CompiledRegisterAccess statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledString statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
        Visit(statement.Allocator, callback);
    }
    static void Visit(CompiledStackString statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledFunctionReference statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledLabelReference statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
    static void Visit(CompiledCompilerVariableAccess statement, Func<CompiledStatement, bool> callback)
    {
        if (!callback(statement)) return;
    }
}
