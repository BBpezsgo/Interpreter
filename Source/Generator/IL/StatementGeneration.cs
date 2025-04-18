﻿using System.Reflection;
using System.Reflection.Emit;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.IL.Generator;

public partial class CodeGeneratorForIL : CodeGenerator
{
    readonly Dictionary<CompiledVariableDeclaration, LocalBuilder> LocalBuilders = new();
    readonly Stack<Label> LoopLabels = new();
    readonly Dictionary<ICompiledFunctionDefinition, DynamicMethod> FunctionBuilders = new();
    readonly HashSet<ICompiledFunctionDefinition> EmittedFunctions = new();
    readonly ModuleBuilder Module;

    void GenerateDeallocator(CompiledCleanup cleanup, ILGenerator il, ref bool successful)
    {
        if (cleanup.Deallocator is null) return;

        /*

        if (cleanup.Deallocator.ExternalFunctionName is not null)
        {
            Diagnostics.Add(Diagnostic.Critical($"External deallocator not supported", cleanup));
            successful = false;
            return;
        }

        if (cleanup.Deallocator.ReturnSomething)
        {
            Diagnostics.Add(Diagnostic.Critical($"Deallocator should not return anything", cleanup));
            successful = false;
            return;
        }

        if (!EmitFunction(cleanup.Deallocator, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to emit function \"{cleanup.Deallocator}\"", cleanup));
            successful = false;
            return;
        }

        if (function.GetParameters().Length != 1)
        {
            Diagnostics.Add(Diagnostic.Internal($"Invalid deallocator", cleanup));
            successful = false;
            return;
        }

        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Call, function);
        */
    }

    void GenerateDestructor(CompiledCleanup cleanup, ILGenerator il, ref bool successful)
    {
        GeneralType deallocateableType = cleanup.TrashType;

        if (cleanup.TrashType.Is<PointerType>())
        {
            if (cleanup.Destructor is null)
            {
                GenerateDeallocator(cleanup, il, ref successful);
                return;
            }
        }
        else
        {
            if (cleanup.Destructor is null)
            { return; }
        }

        if (!EmitFunction(cleanup.Destructor, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to emit function \"{cleanup.Destructor}\"", cleanup));
            successful = false;
            return;
        }

        if (function.GetParameters().Length != 1)
        {
            Diagnostics.Add(Diagnostic.Internal($"Invalid destructor", cleanup));
            successful = false;
            return;
        }

        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Call, function);

        if (deallocateableType.Is<PointerType>())
        {
            GenerateDeallocator(cleanup, il, ref successful);
        }
    }

    void EmitStatement(CompiledEvaluatedValue statement, ILGenerator il, ref bool successful)
    {
        switch (statement.Value.Type)
        {
            case RuntimeType.U8:
                il.Emit(OpCodes.Ldc_I4_S, statement.Value.U8);
                return;
            case RuntimeType.I8:
                il.Emit(OpCodes.Ldc_I4_S, statement.Value.U8);
                return;
            case RuntimeType.U16:
                if (statement.Value.I16 is >= byte.MinValue and <= byte.MaxValue)
                {
                    il.Emit(OpCodes.Ldc_I4_S, statement.Value.I16);
                }
                else
                {
                    il.Emit(OpCodes.Ldc_I4, statement.Value.I16);
                }
                return;
            case RuntimeType.I16:
                if (statement.Value.I16 is >= byte.MinValue and <= byte.MaxValue)
                {
                    il.Emit(OpCodes.Ldc_I4_S, statement.Value.I16);
                }
                else
                {
                    il.Emit(OpCodes.Ldc_I4, statement.Value.I16);
                }
                return;
            case RuntimeType.I32:
                switch (statement.Value.I32)
                {
                    case -1: il.Emit(OpCodes.Ldc_I4_M1); break;
                    case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                    case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                    case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                    case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                    case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                    case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                    case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                    case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                    case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                    default: il.Emit(OpCodes.Ldc_I4, statement.Value.I32); break;
                }
                return;
            case RuntimeType.U32:
                switch (statement.Value.U32)
                {
                    case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                    case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                    case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                    case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                    case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                    case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                    case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                    case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                    case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                    default: il.Emit(OpCodes.Ldc_I4, statement.Value.U32); break;
                }
                return;
            case RuntimeType.F32:
                il.Emit(OpCodes.Ldc_R4, statement.Value.F32);
                return;
            case RuntimeType.Null:
                Diagnostics.Add(Diagnostic.Internal($"Value has type of null", statement));
                return;
            default:
                throw new UnreachableException();
        }
    }
    void EmitStatement(CompiledReturn statement, ILGenerator il, ref bool successful)
    {
        if (statement.Value is not null)
        {
            EmitStatement(statement.Value, il, ref successful);
        }
        il.Emit(OpCodes.Ret);
    }
    void EmitStatement(CompiledBinaryOperatorCall statement, ILGenerator il, ref bool successful)
    {
        switch (statement.Operator)
        {
            case "+":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Add);
                return;
            }
            case "-":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Sub);
                return;
            }
            case "*":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Mul);
                return;
            }
            case "/":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Div);
                return;
            }
            case "%":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Rem);
                return;
            }
            case "&":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.And);
                return;
            }
            case "|":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Or);
                return;
            }
            case "^":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Xor);
                return;
            }
            case "<<":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Shl);
                return;
            }
            case ">>":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Shr);
                return;
            }
            case "<":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Blt, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case ">":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Bgt, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case "<=":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Ble, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case ">=":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Bge, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case "==":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Beq, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
            case "!=":
            {
                EmitStatement(statement.Left, il, ref successful);
                EmitStatement(statement.Right, il, ref successful);

                Label labelFalse = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                il.Emit(OpCodes.Beq, labelFalse);

                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelFalse);
                il.Emit(OpCodes.Ldc_I4_0);

                il.MarkLabel(labelEnd);

                return;
            }
            case "&&":
            {
                Label labelFalse = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                EmitStatement(statement.Left, il, ref successful);
                il.Emit(OpCodes.Brfalse, labelFalse);

                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Brfalse, labelFalse);

                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelFalse);
                il.Emit(OpCodes.Ldc_I4_0);

                il.MarkLabel(labelEnd);

                return;
            }
            case "||":
            {
                Label labelTrue = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                EmitStatement(statement.Left, il, ref successful);
                il.Emit(OpCodes.Brtrue, labelTrue);

                EmitStatement(statement.Right, il, ref successful);
                il.Emit(OpCodes.Brtrue, labelTrue);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelTrue);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }
        }

        Diagnostics.Add(Diagnostic.Critical($"Unimplemented binary operator {statement.Operator}", statement));
        successful = false;
        return;
    }
    void EmitStatement(CompiledUnaryOperatorCall statement, ILGenerator il, ref bool successful)
    {
        switch (statement.Operator)
        {
            case "!":
            {
                Label labelFalse = il.DefineLabel();
                Label labelEnd = il.DefineLabel();

                EmitStatement(statement.Left, il, ref successful);
                il.Emit(OpCodes.Brfalse, labelFalse);

                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Br, labelEnd);

                il.MarkLabel(labelFalse);
                il.Emit(OpCodes.Ldc_I4_1);

                il.MarkLabel(labelEnd);

                return;
            }

            case "~":
            {
                EmitStatement(statement.Left, il, ref successful);
                il.Emit(OpCodes.Not);

                return;
            }
        }

        Diagnostics.Add(Diagnostic.Critical($"Unimplemented unary operator {statement.Operator}", statement));
        successful = false;
        return;
    }
    void EmitStatement(CompiledVariableDeclaration statement, ILGenerator il, ref bool successful)
    {
        if (LocalBuilders.ContainsKey(statement)) return;
        if (!ToType(statement.Type, out Type? type, out PossibleDiagnostic? typeError))
        {
            Diagnostics.Add(typeError.ToError(statement));
            successful = false;
            return;
        }

        LocalBuilder local = LocalBuilders[statement] = il.DeclareLocal(type);
        if (statement.InitialValue is not null)
        {
            if (statement.InitialValue is CompiledConstructorCall constructorCall)
            {
                EmitStatement(constructorCall, il, ref successful, local);
            }
            else if (statement.InitialValue is CompiledStackAllocation compiledStackAllocation)
            {
                EmitStatement(compiledStackAllocation, il, ref successful, local);
            }
            else
            {
                EmitStatement(statement.InitialValue, il, ref successful);
                il.Emit(OpCodes.Stloc, local.LocalIndex);
            }
        }
    }
    void EmitStatement(CompiledVariableGetter statement, ILGenerator il, ref bool successful)
    {
        if (!LocalBuilders.TryGetValue(statement.Variable, out LocalBuilder? local))
        {
            if (statement.Variable.IsGlobal)
            { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement)); }
            else
            {
                if (successful)
                { Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" not compiled", statement)); }
                else
                { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{statement.Variable.Identifier}\" not compiled", statement)); }
            }
            successful = false;
            return;
        }
        il.Emit(OpCodes.Ldloc, local.LocalIndex);
    }
    void EmitStatement(CompiledParameterGetter statement, ILGenerator il, ref bool successful)
    {
        il.Emit(OpCodes.Ldarg, statement.Variable.Index);
    }
    void EmitStatement(CompiledFieldGetter statement, ILGenerator il, ref bool successful)
    {
        CompiledStatementWithValue _object = statement.Object;

        if (!_object.Type.Is(out PointerType? objectType))
        {
            _object = new CompiledAddressGetter()
            {
                Of = _object,
                Type = objectType = new PointerType(_object.Type),
                Location = _object.Location,
                SaveValue = true,
            };
        }

        EmitStatement(_object, il, ref successful);

        while (objectType.To.Is(out PointerType? indirectPointer))
        {
            il.Emit(OpCodes.Ldind_Ref);
            objectType = indirectPointer;
        }

        if (objectType.To.Is(out StructType? structType))
        {
            if (!ToType(structType, out Type? type, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(statement));
                successful = false;
                return;
            }

            FieldInfo? field = type.GetField(statement.Field.Identifier.Content);
            if (field is null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Field \"{statement.Field.Identifier.Content}\" not found in type {type}", _object));
                successful = false;
                return;
            }
            il.Emit(OpCodes.Ldfld, field);
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a struct", statement.Object));
            successful = false;
            return;
        }
    }
    void EmitStatement(CompiledFunctionCall statement, ILGenerator il, ref bool successful)
    {
        if (statement.Function is CompiledFunctionDefinition compiledFunction &&
            compiledFunction.BuiltinFunctionName == "free")
        {
            return;
        }

        if (!EmitFunction(statement.Function, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to emit function \"{statement.Function}\"", statement));
            successful = false;
            return;
        }

        for (int i = 0; i < statement.Arguments.Length; i++)
        {
            EmitStatement(statement.Arguments[i].Value, il, ref successful);
        }

        il.Emit(OpCodes.Call, function);

        if (!statement.SaveValue && function.ReturnType != typeof(void))
        {
            il.Emit(OpCodes.Pop);
        }
    }
    void EmitStatement(CompiledExternalFunctionCall statement, ILGenerator il, ref bool successful)
    {
        switch (statement.Function)
        {
            case ExternalFunctionSync f:
            {
                if (f.UnmarshaledCallback.Method.IsStatic)
                {
                    foreach (CompiledPassedArgument item in statement.Arguments)
                    {
                        EmitStatement(item.Value, il, ref successful);
                    }
                    il.Emit(OpCodes.Call, f.UnmarshaledCallback.GetMethodInfo());

                    return;
                }

                Diagnostics.Add(Diagnostic.CriticalNoBreak($"Non-static external functions not supported", statement));
                successful = false;
                return;
            }

            default:
                Diagnostics.Add(Diagnostic.CriticalNoBreak($"{statement.Function.GetType()} external functions not supported", statement));
                successful = false;
                return;
        }
    }
    void EmitStatement(CompiledVariableSetter statement, ILGenerator il, ref bool successful)
    {
        EmitStatement(statement.Value, il, ref successful);
        if (!LocalBuilders.TryGetValue(statement.Variable, out LocalBuilder? local))
        {
            if (statement.Variable.IsGlobal)
            { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement)); }
            else
            {
                if (successful)
                { Diagnostics.Add(Diagnostic.Critical($"Variable \"{statement.Variable.Identifier}\" not compiled", statement)); }
                else
                { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{statement.Variable.Identifier}\" not compiled", statement)); }
            }
            successful = false;
            return;
        }
        il.Emit(OpCodes.Stloc, local.LocalIndex);
    }
    void EmitStatement(CompiledParameterSetter statement, ILGenerator il, ref bool successful)
    {
        EmitStatement(statement.Value, il, ref successful);
        il.Emit(OpCodes.Starg, statement.Variable.Index);
    }
    void EmitStatement(CompiledFieldSetter statement, ILGenerator il, ref bool successful)
    {
        CompiledStatementWithValue _object = statement.Object;

        if (!_object.Type.Is(out PointerType? objectType))
        {
            _object = new CompiledAddressGetter()
            {
                Of = _object,
                Type = objectType = new PointerType(_object.Type),
                Location = _object.Location,
                SaveValue = true,
            };
        }

        EmitStatement(_object, il, ref successful);

        while (objectType.To.Is(out PointerType? indirectPointer))
        {
            il.Emit(OpCodes.Ldind_Ref);
            objectType = indirectPointer;
        }

        if (objectType.To.Is(out StructType? structType))
        {
            if (!ToType(structType, out Type? type, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(statement));
                successful = false;
                return;
            }

            FieldInfo? field = type.GetField(statement.Field.Identifier.Content);
            if (field is null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Field \"{statement.Field.Identifier.Content}\" not found in type {type}", _object));
                successful = false;
                return;
            }

            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Stfld, field);
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a struct", statement.Object));
            successful = false;
        }
    }
    void EmitStatement(CompiledIndirectSetter statement, ILGenerator il, ref bool successful)
    {
        if (!Settings.AllowPointers)
        {
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Pointers are banned by the generator settings", statement));
            successful = false;
            return;
        }

        switch (statement.AddressValue)
        {
            case CompiledVariableGetter:
            case CompiledParameterGetter:
            case CompiledFieldGetter:
                break;
            default:
                Debugger.Break();
                Diagnostics.Add(Diagnostic.CriticalNoBreak($"Unsafe!!!", statement));
                successful = false;
                return;
        }

        EmitStatement(statement.AddressValue, il, ref successful);
        EmitStatement(statement.Value, il, ref successful);
        if (!statement.AddressValue.Type.Is(out PointerType? pointer))
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a pointer", statement.AddressValue));
            successful = false;
            return;
        }

        if (!StoreIndirect(pointer.To, il, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(statement));
            successful = false;
            return;
        }
    }
    void EmitStatement(CompiledPointer statement, ILGenerator il, ref bool successful)
    {
        if (!Settings.AllowPointers)
        {
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Pointers are banned by the generator settings", statement));
            successful = false;
            return;
        }

        switch (statement.To)
        {
            case CompiledVariableGetter:
            case CompiledParameterGetter:
            case CompiledFieldGetter:
                break;
            default:
                Debugger.Break();
                Diagnostics.Add(Diagnostic.CriticalNoBreak($"Unsafe!!!", statement));
                successful = false;
                return;
        }

        EmitStatement(statement.To, il, ref successful);

        if (!statement.To.Type.Is(out PointerType? pointer))
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a pointer", statement.To));
            successful = false;
            return;
        }

        if (!LoadIndirect(pointer.To, il, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(statement.To));
            successful = false;
            return;
        }
    }
    void EmitStatement(CompiledWhileLoop statement, ILGenerator il, ref bool successful)
    {
        Label loopStart = il.DefineLabel();
        Label loopEnd = il.DefineLabel();

        LoopLabels.Push(loopEnd);

        il.MarkLabel(loopStart);
        EmitStatement(statement.Condition, il, ref successful);
        il.Emit(OpCodes.Brfalse, loopEnd);

        EmitStatement(statement.Body, il, ref successful);

        il.Emit(OpCodes.Br, loopStart);
        il.MarkLabel(loopEnd);

        if (LoopLabels.Pop() != loopEnd)
        {
            Diagnostics.Add(Diagnostic.Internal($"Something went wrong ...", statement));
            successful = false;
        }
    }
    void EmitStatement(CompiledForLoop statement, ILGenerator il, ref bool successful)
    {
        EmitStatement(statement.VariableDeclaration, il, ref successful);

        Label loopStart = il.DefineLabel();
        Label loopEnd = il.DefineLabel();

        LoopLabels.Push(loopEnd);

        il.MarkLabel(loopStart);
        EmitStatement(statement.Condition, il, ref successful);
        il.Emit(OpCodes.Brfalse, loopEnd);

        EmitStatement(statement.Body, il, ref successful);
        EmitStatement(statement.Expression, il, ref successful);
        il.Emit(OpCodes.Br, loopStart);

        il.MarkLabel(loopEnd);

        if (LoopLabels.Pop() != loopEnd)
        {
            Diagnostics.Add(Diagnostic.Internal($"Something went wrong ...", statement));
            successful = false;
        }
    }
    void EmitStatement(CompiledIf statement, ILGenerator il, ref bool successful)
    {
        Label labelEnd = il.DefineLabel();

        CompiledBranch? current = statement;
        while (current is not null)
        {
            switch (current)
            {
                case CompiledIf _if:
                    Label labelNext = il.DefineLabel();

                    EmitStatement(_if.Condition, il, ref successful);
                    il.Emit(OpCodes.Brfalse, labelNext);

                    EmitStatement(_if.Body, il, ref successful);
                    il.Emit(OpCodes.Br, labelEnd);

                    il.MarkLabel(labelNext);

                    current = _if.Next;
                    break;

                case CompiledElse _else:
                    EmitStatement(_else.Body, il, ref successful);

                    current = null;
                    break;

                default:
                    throw new UnreachableException();
            }
        }

        il.MarkLabel(labelEnd);
    }
    void EmitStatement(CompiledBreak statement, ILGenerator il, ref bool successful)
    {
        if (LoopLabels.Count == 0)
        {
            Diagnostics.Add(Diagnostic.Critical($"You can only break in a loop", statement));
            successful = false;
            return;
        }

        il.Emit(OpCodes.Br, LoopLabels.Last);
    }
    void EmitStatement(CompiledBlock statement, ILGenerator il, ref bool successful)
    {
        foreach (CompiledStatement v in statement.Statements)
        {
            EmitStatement(v, il, ref successful);
        }
    }
    void EmitStatement(CompiledAddressGetter statement, ILGenerator il, ref bool successful)
    {
        if (!Settings.AllowPointers)
        {
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Pointers are banned by the generator settings", statement));
            successful = false;
            return;
        }

        switch (statement.Of)
        {
            case CompiledVariableGetter v:
                if (!LocalBuilders.TryGetValue(v.Variable, out LocalBuilder? local))
                {
                    if (v.Variable.IsGlobal)
                    { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Global variables not supported", statement)); }
                    else
                    {
                        if (successful)
                        { Diagnostics.Add(Diagnostic.Critical($"Variable \"{v.Variable.Identifier}\" not compiled", statement)); }
                        else
                        { Diagnostics.Add(Diagnostic.CriticalNoBreak($"Variable \"{v.Variable.Identifier}\" not compiled", statement)); }
                    }

                    successful = false;
                    return;
                }
                il.Emit(OpCodes.Ldloca_S, local);
                break;
            case CompiledParameterGetter v:
                il.Emit(OpCodes.Ldarga_S, v.Variable.Index);
                break;
            case CompiledFieldGetter v:
                CompiledStatementWithValue _object = v.Object;

                if (!_object.Type.Is(out PointerType? objectType))
                {
                    _object = new CompiledAddressGetter()
                    {
                        Of = _object,
                        Type = objectType = new PointerType(_object.Type),
                        Location = _object.Location,
                        SaveValue = true,
                    };
                }

                EmitStatement(_object, il, ref successful);

                while (objectType.To.Is(out PointerType? indirectPointer))
                {
                    il.Emit(OpCodes.Ldind_Ref);
                    objectType = indirectPointer;
                }

                if (objectType.To.Is(out StructType? structType))
                {
                    if (!ToType(structType, out Type? type, out PossibleDiagnostic? typeError))
                    {
                        Diagnostics.Add(typeError.ToError(statement));
                        successful = false;
                        return;
                    }

                    FieldInfo? field = type.GetField(v.Field.Identifier.Content);
                    if (field is null)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Field \"{v.Field.Identifier.Content}\" not found in type {type}", _object));
                        successful = false;
                        return;
                    }
                    il.Emit(OpCodes.Ldflda, field);
                }
                else
                {
                    Diagnostics.Add(Diagnostic.Critical($"This should be a struct", v.Object));
                    successful = false;
                }
                break;
            default:
                Debugger.Break();
                Diagnostics.Add(Diagnostic.CriticalNoBreak($"Can't get the address of \"{statement.Of}\" ({statement.Of.GetType().Name})", statement.Of));
                successful = false;
                break;
        }
    }
    void EmitStatement(CompiledHeapAllocation statement, ILGenerator il, ref bool successful)
    {
        if (!Settings.AllowHeap)
        {
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Heap is banned by the generator settings", statement));
            successful = false;
            return;
        }

        if (!statement.Type.Is(out PointerType? pointerType))
        {
            Diagnostics.Add(Diagnostic.Internal("What", statement));
            successful = false;
            return;
        }

        switch (pointerType.To.FinalValue)
        {
            case ArrayType arrayType:
            {
                if (!ToType(arrayType.Of, out Type? type, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(statement));
                    successful = false;
                    return;
                }

                if (arrayType.Length is null)
                {
                    Diagnostics.Add(Diagnostic.Internal($"The array's length must be specified", statement));
                    successful = false;
                    return;
                }

                if (!arrayType.Length.Type.SameAs(BuiltinType.I32))
                {
                    Diagnostics.Add(Diagnostic.Internal($"The array's length must be an i32 and not {arrayType.Length.Type}", statement));
                    successful = false;
                    return;
                }

                EmitStatement(arrayType.Length, il, ref successful);
                il.Emit(OpCodes.Newarr, type);
                return;
            }

            case StructType structType:
            {
                if (!ToType(structType, out Type? type, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(statement));
                    successful = false;
                    return;
                }

                ConstructorInfo? constructor = type.GetConstructor(Array.Empty<Type>());

                if (constructor is null)
                {
                    Diagnostics.Add(Diagnostic.Internal($"Type \"{type}\" doesn't have a parameterless constructor", statement));
                    successful = false;
                    return;
                }

                il.Emit(OpCodes.Newobj, constructor);
                il.Emit(OpCodes.Box, type);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Initobj, type);
                return;
            }

            case BuiltinType builtinType:
            {
                if (!EmitDefaultValue(builtinType, il, out PossibleDiagnostic? defaultValueError))
                {
                    Diagnostics.Add(defaultValueError.ToError(statement));
                    successful = false;
                    return;
                }

                if (!ToType(builtinType, out Type? type, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(statement));
                    successful = false;
                    return;
                }

                il.Emit(OpCodes.Box, type);
                return;
            }

            default:
                throw new NotImplementedException();

        }
    }
    void EmitStatement(CompiledFakeTypeCast statement, ILGenerator il, ref bool successful)
    {
        if (statement.Type.Is(out PointerType? resultPointerType) &&
            statement.Value is CompiledFunctionCall allocatorCaller &&
            allocatorCaller.Function is CompiledFunctionDefinition allocatorCallee &&
            allocatorCallee.BuiltinFunctionName == "alloc" &&
            allocatorCaller.Arguments.Length == 1)
        {
            if (allocatorCaller.Arguments[0].Value is CompiledSizeof sizeofArgument)
            {
                if (sizeofArgument.Of.Is(out ArrayType? sizeofArrayType))
                {
                    if (!resultPointerType.To.Is(out ArrayType? resultArrayType) ||
                        !resultArrayType.Of.SameAs(sizeofArrayType.Of))
                    {
                        Diagnostics.Add(Diagnostic.Internal($"Invalid heap allocation types", sizeofArgument));
                        successful = false;
                        return;
                    }

                    EmitStatement(new CompiledHeapAllocation()
                    {
                        Allocator = (CompiledFunctionDefinition)allocatorCaller.Function,
                        Location = statement.Location,
                        SaveValue = statement.SaveValue,
                        Type = resultPointerType,
                    }, il, ref successful);
                }
                else
                {
                    if (!resultPointerType.To.SameAs(sizeofArgument.Of))
                    {
                        Diagnostics.Add(Diagnostic.Internal($"Invalid heap allocation types", sizeofArgument));
                        successful = false;
                        return;
                    }

                    EmitStatement(new CompiledHeapAllocation()
                    {
                        Allocator = (CompiledFunctionDefinition)allocatorCaller.Function,
                        Location = statement.Location,
                        SaveValue = statement.SaveValue,
                        Type = resultPointerType,
                    }, il, ref successful);
                }
                return;
            }
            else if (allocatorCaller.Arguments[0].Value is CompiledEvaluatedValue valueArgument)
            {
                if (!FindSize(resultPointerType.To, out int size, out PossibleDiagnostic? sizeError))
                {
                    Diagnostics.Add(sizeError.ToError(statement));
                    successful = false;
                    return;
                }

                if (!valueArgument.Type.SameAs(BuiltinType.I32))
                {
                    Diagnostics.Add(Diagnostic.Internal($"Invalid heap allocation sizes", statement));
                    successful = false;
                    return;
                }

                if (valueArgument.Value.I32 != size)
                {
                    Diagnostics.Add(Diagnostic.Internal($"Invalid heap allocation sizes", statement));
                    successful = false;
                    return;
                }

                EmitStatement(new CompiledHeapAllocation()
                {
                    Allocator = (CompiledFunctionDefinition)allocatorCaller.Function,
                    Location = statement.Location,
                    SaveValue = statement.SaveValue,
                    Type = resultPointerType,
                }, il, ref successful);
                return;
            }
        }

        if (statement.Type.SameAs(BuiltinType.I32) &&
            statement.Value.Type.SameAs(BuiltinType.F32))
        {
            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Call, ((Func<float, int>)BitConverter.SingleToInt32Bits).Method);
            if (!statement.SaveValue) il.Emit(OpCodes.Pop);
            return;
        }

#if !NETSTANDARD
        if (statement.Type.SameAs(BuiltinType.U32) &&
            statement.Value.Type.SameAs(BuiltinType.F32))
        {
            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Call, ((Func<float, uint>)BitConverter.SingleToUInt32Bits).Method);
            if (!statement.SaveValue) il.Emit(OpCodes.Pop);
            return;
        }
#endif

        if (statement.Type.SameAs(BuiltinType.F32) &&
            statement.Value.Type.SameAs(BuiltinType.I32))
        {
            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Call, ((Func<int, float>)BitConverter.Int32BitsToSingle).Method);
            if (!statement.SaveValue) il.Emit(OpCodes.Pop);
            return;
        }

        EmitStatement(statement.Value, il, ref successful);
        Diagnostics.Add(Diagnostic.Internal($"Fake type casts are unsafe (tried to cast {statement.Value.Type} to {statement.Type})", statement));
        successful = false;
    }
    void EmitStatement(CompiledTypeCast statement, ILGenerator il, ref bool successful)
    {
        EmitStatement(statement.Value, il, ref successful);
        switch (statement.Type.FinalValue)
        {
            case BuiltinType v:
                switch (v.Type)
                {
                    case BasicType.U8: il.Emit(OpCodes.Conv_U1); break;
                    case BasicType.I8: il.Emit(OpCodes.Conv_I1); break;
                    case BasicType.U16: il.Emit(OpCodes.Conv_U2); break;
                    case BasicType.I16: il.Emit(OpCodes.Conv_I2); break;
                    case BasicType.U32: il.Emit(OpCodes.Conv_U4); break;
                    case BasicType.I32: il.Emit(OpCodes.Conv_I4); break;
                    case BasicType.U64: il.Emit(OpCodes.Conv_U8); break;
                    case BasicType.I64: il.Emit(OpCodes.Conv_I8); break;
                    case BasicType.F32: il.Emit(OpCodes.Conv_R4); break;
                    default:
                        successful = false;
                        Diagnostics.Add(Diagnostic.Critical($"Invalid casting type {v.Type}", statement));
                        break;
                }
                return;
            default:
                successful = false;
                Diagnostics.Add(Diagnostic.Critical($"Unimplemented casting type {statement.Type.FinalValue}", statement));
                break;
        }
    }
    void EmitStatement(CompiledCrash statement, ILGenerator il, ref bool successful)
    {
        if (!Settings.AllowCrash)
        {
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Crashing is banned by the generator settings", statement));
            successful = false;
            return;
        }

        switch (statement.Value)
        {
            case CompiledStringInstance compiledStringInstance:
                il.Emit(OpCodes.Ldstr, compiledStringInstance.Value);
                il.Emit(OpCodes.Newobj, typeof(RuntimeException).GetConstructor(new Type[] { typeof(string) })!);
                il.Emit(OpCodes.Throw);
                break;
            case CompiledStackStringInstance stackStringInstance:
                il.Emit(OpCodes.Ldstr, stackStringInstance.Value);
                il.Emit(OpCodes.Newobj, typeof(RuntimeException).GetConstructor(new Type[] { typeof(string) })!);
                il.Emit(OpCodes.Throw);
                break;
            default:
                Diagnostics.Add(Diagnostic.Internal($"Unimplemented value for crash reason", statement.Value));
                successful = false;
                break;
        }
    }
    void EmitStatement(CompiledSizeof statement, ILGenerator il, ref bool successful)
    {
        if (!FindSize(statement.Of, out int size, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(statement));
            successful = false;
            return;
        }

        il.Emit(OpCodes.Ldc_I4, size);
    }
    void EmitStatement(CompiledDelete statement, ILGenerator il, ref bool successful)
    {
        EmitStatement(statement.Value, il, ref successful);
        GenerateDestructor(statement.Cleanup, il, ref successful);
        il.Emit(OpCodes.Pop);
    }
    void EmitStatement(CompiledConstructorCall statement, ILGenerator il, ref bool successful, LocalBuilder? destination = null)
    {
        if (!EmitFunction(statement.Function, out DynamicMethod? function))
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to emit function \"{statement.Function}\"", statement));
            successful = false;
            return;
        }

        if (destination is not null && statement.Object is CompiledStackAllocation stackAllocation)
        {
            EmitStatement(stackAllocation, il, ref successful, destination);

            il.Emit(OpCodes.Ldloca_S, destination.LocalIndex);
            for (int i = 0; i < statement.Arguments.Length; i++)
            {
                EmitStatement(statement.Arguments[i].Value, il, ref successful);
            }

            il.Emit(OpCodes.Call, function);

            if (!statement.SaveValue &&
                function.ReturnType != typeof(void))
            {
                il.Emit(OpCodes.Pop);
            }
        }
        else
        {
            if (!statement.Object.Type.Is<PointerType>())
            {
                Debugger.Break();
                Diagnostics.Add(Diagnostic.InternalNoBreak($"Only pointer constructors supported", statement));
                successful = false;
                return;
            }

            EmitStatement(statement.Object, il, ref successful);

            il.Emit(OpCodes.Dup);

            for (int i = 0; i < statement.Arguments.Length; i++)
            {
                EmitStatement(statement.Arguments[i].Value, il, ref successful);
            }

            il.Emit(OpCodes.Call, function);

            if (!statement.SaveValue &&
                function.ReturnType != typeof(void))
            {
                il.Emit(OpCodes.Pop);
            }
        }
    }
    void EmitStatement(CompiledStackAllocation statement, ILGenerator il, ref bool successful, LocalBuilder? destination = null)
    {
        switch (statement.Type)
        {
            case StructType v:
            {
                if (!ToType(v, out Type? type, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(statement));
                    successful = false;
                    return;
                }

                if (destination is not null)
                {
                    il.Emit(OpCodes.Ldloca_S, destination.LocalIndex);
                    il.Emit(OpCodes.Initobj, type);
                }
                else
                {
                    LocalBuilder local = il.DeclareLocal(type);
                    il.Emit(OpCodes.Ldloca_S, local.LocalIndex);
                    il.Emit(OpCodes.Initobj, type);
                    il.Emit(OpCodes.Ldloc, local.LocalIndex);
                }
                break;
            }
            default:
            {
                Diagnostics.Add(Diagnostic.Internal($"Only structs supported for stack allocations", statement));
                successful = false;
                break;
            }
        }
    }
    void EmitStatement(CompiledStringInstance statement, ILGenerator il, ref bool successful)
    {
        if (!Settings.AllowHeap)
        {
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Heap is banned by the generator settings", statement));
            successful = false;
            return;
        }

        if (statement.IsASCII)
        {
            Diagnostics.Add(Diagnostic.Internal($"ASCII strings not supported", statement));
            successful = false;
        }

        il.Emit(OpCodes.Ldstr, statement.Value + "\0");
    }
    void EmitStatement(CompiledIndexGetter statement, ILGenerator il, ref bool successful)
    {
        if (statement.Base.Type.Is(out PointerType? basePointerType) &&
            basePointerType.To.Is(out ArrayType? baseArrayType))
        {
            if (baseArrayType.Of.SameAs(BuiltinType.Char))
            {
                EmitStatement(statement.Base, il, ref successful);
                EmitStatement(statement.Index, il, ref successful);
                il.Emit(OpCodes.Callvirt, typeof(string).GetMethod("get_Chars", new Type[] { typeof(int) })!);

                // Diagnostics.Add(Diagnostic.Internal($"I will have to revisit this ...", statement));
                return;
            }

            if (!ToType(baseArrayType.Of, out Type? elementType, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(statement.Base));
                successful = false;
                return;
            }

            EmitStatement(statement.Base, il, ref successful);
            EmitStatement(statement.Index, il, ref successful);
            il.Emit(OpCodes.Ldelem, elementType);

            // Diagnostics.Add(Diagnostic.Internal($"I will have to revisit this ...", statement));
            return;
        }

        Diagnostics.Add(Diagnostic.Critical($"This should be an array", statement.Base));
        successful = false;
    }
    void EmitStatement(CompiledIndexSetter statement, ILGenerator il, ref bool successful)
    {
        if (statement.Base.Type.Is(out PointerType? basePointerType) &&
            basePointerType.To.Is(out ArrayType? baseArrayType))
        {
            if (!ToType(baseArrayType.Of, out Type? elementType, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(statement.Base));
                successful = false;
                return;
            }

            EmitStatement(statement.Base, il, ref successful);
            EmitStatement(statement.Index, il, ref successful);
            EmitStatement(statement.Value, il, ref successful);
            il.Emit(OpCodes.Stelem, elementType);
            return;
        }

        Debugger.Break();
        Diagnostics.Add(Diagnostic.CriticalNoBreak($"Unimplemented index setter {statement.Base.Type}[{statement.Index.Type}]", statement));
        successful = false;
    }
    void EmitStatement(FunctionAddressGetter statement, ILGenerator il, ref bool successful)
    {
        if (!Settings.AllowPointers)
        {
            Diagnostics.Add(Diagnostic.CriticalNoBreak($"Pointers are banned by the generator settings", statement));
            successful = false;
            return;
        }

        // var function = GetOrEmitFunctionSignature(statement.Function);
        // il.Emit(OpCodes.Ldftn, function);

        Diagnostics.Add(Diagnostic.CriticalNoBreak($"Function address getters not supported", statement));
        successful = false;
    }
    void EmitStatement(CompiledRuntimeCall statement, ILGenerator il, ref bool successful)
    {
        for (int i = 0; i < statement.Arguments.Length; i++)
        {
            EmitStatement(statement.Arguments[i].Value, il, ref successful);
        }

        EmitStatement(statement.Function, il, ref successful);

        il.Emit(OpCodes.Calli);

        if (!statement.SaveValue && !statement.Function.Type.SameAs(BuiltinType.Void))
        {
            il.Emit(OpCodes.Pop);
        }
    }
    void EmitStatement(CompiledStatement statement, ILGenerator il, ref bool successful)
    {
        switch (statement)
        {
            case EmptyStatement: break;
            case CompiledReturn v: EmitStatement(v, il, ref successful); break;
            case CompiledEvaluatedValue v: EmitStatement(v, il, ref successful); break;
            case CompiledBinaryOperatorCall v: EmitStatement(v, il, ref successful); break;
            case CompiledUnaryOperatorCall v: EmitStatement(v, il, ref successful); break;
            case CompiledVariableDeclaration v: EmitStatement(v, il, ref successful); break;
            case CompiledVariableGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledParameterGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledFieldGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledPointer v: EmitStatement(v, il, ref successful); break;
            case CompiledFunctionCall v: EmitStatement(v, il, ref successful); break;
            case CompiledExternalFunctionCall v: EmitStatement(v, il, ref successful); break;
            case CompiledVariableSetter v: EmitStatement(v, il, ref successful); break;
            case CompiledParameterSetter v: EmitStatement(v, il, ref successful); break;
            case CompiledFieldSetter v: EmitStatement(v, il, ref successful); break;
            case CompiledIndirectSetter v: EmitStatement(v, il, ref successful); break;
            case CompiledWhileLoop v: EmitStatement(v, il, ref successful); break;
            case CompiledBlock v: EmitStatement(v, il, ref successful); break;
            case CompiledForLoop v: EmitStatement(v, il, ref successful); break;
            case CompiledIf v: EmitStatement(v, il, ref successful); break;
            case CompiledBreak v: EmitStatement(v, il, ref successful); break;
            case CompiledAddressGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledFakeTypeCast v: EmitStatement(v, il, ref successful); break;
            case CompiledTypeCast v: EmitStatement(v, il, ref successful); break;
            case CompiledCrash v: EmitStatement(v, il, ref successful); break;
            case CompiledStatementWithValueThatActuallyDoesntHaveValue v: EmitStatement(v.Statement, il, ref successful); break;
            case CompiledSizeof v: EmitStatement(v, il, ref successful); break;
            case CompiledDelete v: EmitStatement(v, il, ref successful); break;
            case CompiledStackAllocation v: EmitStatement(v, il, ref successful); break;
            case CompiledConstructorCall v: EmitStatement(v, il, ref successful); break;
            case CompiledStringInstance v: EmitStatement(v, il, ref successful); break;
            case CompiledIndexGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledIndexSetter v: EmitStatement(v, il, ref successful); break;
            case FunctionAddressGetter v: EmitStatement(v, il, ref successful); break;
            case CompiledRuntimeCall v: EmitStatement(v, il, ref successful); break;
            default: throw new NotImplementedException(statement.GetType().Name);
        }
    }

    bool ToType(ImmutableArray<GeneralType> types, [NotNullWhen(true)] out Type[]? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        result = new Type[types.Length];
        error = null;

        for (int i = 0; i < types.Length; i++)
        {
            if (!ToType(types[i], out result[i]!, out error)) return false;
        }

        return true;
    }
    bool ToType(GeneralType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
    {
        AliasType v => ToType(v.Value, out result, out error),
        BuiltinType v => ToType(v, out result, out error),
        StructType v => ToType(v, out result, out error),
        PointerType v => ToType(v, out result, out error),
        ArrayType v => ToType(v, out result, out error),
        FunctionType v => ToType(v, out result, out error),
        _ => throw new UnreachableException(),
    };
    bool ToType(PointerType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (type.To.Is(out ArrayType? arrayType) &&
            arrayType.Of.SameAs(BuiltinType.Char))
        {
            result = typeof(string);
            return true;
        }

        result = typeof(nint);
        return true;
    }

    readonly List<(StructType Type, Type Value)> GeneratedTypes = new();
    string GenerateTypeId(GeneralType type)
    {
        StringBuilder res = new();
        GenerateTypeId(type, res);
        return res.ToString();
    }
    void GenerateTypeId(GeneralType type, StringBuilder builder)
    {
        switch (type.FinalValue)
        {
            case BuiltinType v:
                builder.Append(v.Type);
                return;
            case PointerType v:
                builder.Append($"{v.To}*");
                return;
            case FunctionType v:
                builder.Append($"{v.ReturnType}(");
                for (int i = 0; i < v.Parameters.Length; i++)
                {
                    if (i > 0) builder.Append(',');
                    GenerateTypeId(v.Parameters[i], builder);
                }
                builder.Append($")*");
                return;
            case ArrayType v:
                builder.Append($"{v.Of}[{v.ComputedLength?.ToString() ?? ""}]");
                return;
            case StructType v:
                string id = v.Struct.Identifier.Content;
                foreach ((_, GeneralType item) in v.TypeArguments)
                {
                    id += "`" + item;
                }

                if (Module.GetType(id) is not null)
                {
                    for (int i = 0; i < 64; i++)
                    {
                        string idCandidate = $"{id}_{i}";
                        if (Module.GetType(idCandidate) is not null) continue;
                        id = idCandidate;
                        goto good;
                    }

                    throw new InternalExceptionWithoutContext($"Failed to generate type id for type {type}");
                good:;
                }
                builder.Append(id);
                return;
        }
    }

    bool ToType(StructType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        result = GeneratedTypes.FirstOrDefault(v => v.Type.Equals(type) && Utils.SequenceEquals(v.Type.TypeArguments, type.TypeArguments, (a, b) => a.Equals(b))).Value;
        if (result is not null) return true;

        if (!type.AllGenericsDefined())
        {
            error = new PossibleDiagnostic($"Invalid template type", false);
            return false;
        }

        string id = GenerateTypeId(type);

        TypeBuilder builder = Module.DefineType(id, TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, typeof(ValueType));
        foreach (CompiledField field in type.Struct.Fields)
        {
            GeneralType fieldType = type.ReplaceType(field.Type, out error);
            if (error is not null) return false;
            if (!ToType(fieldType, out Type? fieldType1, out error)) return false;

            builder.DefineField(field.Identifier.Content, fieldType1, FieldAttributes.Public);
        }

        ConstructorBuilder constructor = builder.DefineConstructor(
            MethodAttributes.Public |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            Array.Empty<Type>());
        ConstructorInfo conObj = typeof(object).GetConstructor(Array.Empty<Type>())!;

        ILGenerator il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, conObj);
        il.Emit(OpCodes.Ret);

        result = builder.CreateType();
        GeneratedTypes.Add((type, result));
        return true;
    }
    bool ToType(BuiltinType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        result = type.Type switch
        {
            BasicType.Void => typeof(void),
            BasicType.U8 => typeof(byte),
            BasicType.I8 => typeof(sbyte),
            BasicType.U16 => typeof(ushort),
            BasicType.I16 => typeof(short),
            BasicType.U32 => typeof(uint),
            BasicType.I32 => typeof(int),
            BasicType.F32 => typeof(float),
            BasicType.U64 => typeof(ulong),
            BasicType.I64 => typeof(long),
            BasicType.Any => throw new NotImplementedException(),
            _ => throw new UnreachableException(),
        };
        return true;
    }
    bool ToType(ArrayType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = new PossibleDiagnostic($"Inline arrays not supported", false);
        result = null;
        return false;
    }
    bool ToType(FunctionType type, [NotNullWhen(true)] out Type? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        result = typeof(nint);
        return true;
    }

    Func<int> GenerateCodeForTopLevelStatements(ImmutableArray<CompiledStatement> statements, ref bool successful)
    {
        DynamicMethod method = new(
            "top_level_statements",
            typeof(int),
            Array.Empty<Type>(),
            Module);
        ILGenerator il = method.GetILGenerator();

        EmitFunctionBody(statements, BuiltinType.I32, il, ref successful);

        return (Func<int>)method.CreateDelegate(typeof(Func<int>));
    }

    void EmitFunctionBody(ImmutableArray<CompiledStatement> statements, GeneralType returnType, ILGenerator il, ref bool successful)
    {
        KeyValuePair<CompiledVariableDeclaration, LocalBuilder>[] savedLocals = LocalBuilders.ToArray();
        Label[] savedLoopLabels = LoopLabels.ToArray();

        LocalBuilders.Clear();
        LoopLabels.Clear();

        foreach (CompiledStatement statement in statements)
        {
            EmitStatement(statement, il, ref successful);
            if (statement is CompiledReturn) goto end;
        }

        if (!EmitDefaultValue(returnType, il, out PossibleDiagnostic? defaultValueError))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical(defaultValueError.Message));
            successful = false;
        }

        il.Emit(OpCodes.Ret);

    end:

        LocalBuilders.Clear();
        LoopLabels.Clear();

        LocalBuilders.AddRange(savedLocals);
        LoopLabels.AddRange(savedLoopLabels);
    }

    readonly Stack<ICompiledFunctionDefinition> EmittingFunctionsStack = new();
    bool EmitFunction(ICompiledFunctionDefinition function, [NotNullWhen(true)] out DynamicMethod? dynamicMethod)
    {
        dynamicMethod = null;

        if (EmittingFunctionsStack.Any(v => v == function))
        {
            return false;
        }

        using StackAuto<ICompiledFunctionDefinition> _ = EmittingFunctionsStack.PushAuto(function);
        if (EmittingFunctionsStack.Count > 10)
        {
            throw new StackOverflowException();
        }

        CompiledBlock? body = Functions.FirstOrDefault(v => v.Function == function)?.Body;

        if (body is null)
        {
            return false;
        }

        if (!EmitFunctionSignature(function, out dynamicMethod))
        {
            return false;
        }

        if (EmittedFunctions.Contains(function)) return true;

        ILGenerator il = dynamicMethod.GetILGenerator();

        bool successful = true;
        EmitFunctionBody(body.Statements, function is CompiledConstructorDefinition ? BuiltinType.Void : function.Type, il, ref successful);

        if (!successful)
        {
            FunctionBuilders.Remove(function);
            return false;
        }

        EmittedFunctions.Add(function);
        return true;
    }

    bool EmitFunctionSignature(ICompiledFunctionDefinition function, [NotNullWhen(true)] out DynamicMethod? dynamicMethod)
    {
        if (FunctionBuilders.TryGetValue(function, out dynamicMethod))
        {
            return true;
        }

        dynamicMethod = null;
        if (!ToType(function is CompiledConstructorDefinition ? BuiltinType.Void : function.Type, out Type? returnType, out PossibleDiagnostic? returnTypeError))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical(returnTypeError.Message));
            return false;
        }
        if (!ToType(function.ParameterTypes.ToImmutableArray(), out Type[]? parameterTypes, out PossibleDiagnostic? parameterTypesError))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical(parameterTypesError.Message));
            return false;
        }

        dynamicMethod = function switch
        {
            CompiledFunctionDefinition v => new DynamicMethod(
                v.Identifier.Content,
                returnType,
                parameterTypes,
                Module),
            CompiledOperatorDefinition v => new DynamicMethod(
                $"op_{v.Identifier.Content}",
                returnType,
                parameterTypes,
                Module),
            CompiledConstructorDefinition v => new DynamicMethod(
                $"ctor_{v.Identifier.Content}",
                returnType,
                parameterTypes,
                Module),
            CompiledGeneralFunctionDefinition v => new DynamicMethod(
                $"genr_{v.Identifier.Content}",
                returnType,
                parameterTypes,
                Module),
            _ => throw new UnreachableException(),
        };
        FunctionBuilders.Add(function, dynamicMethod);
        return true;
    }

    public static bool ScanForPointer(GeneralType type) => type switch
    {
        BuiltinType => false,
        PointerType => true,
        AliasType v => ScanForPointer(v.Value),
        StructType v => v.Struct.Fields.Any(field => ScanForPointer(GeneralType.InsertTypeParameters(field.Type, v.TypeArguments) ?? field.Type)),
        ArrayType v => ScanForPointer(v.Of),
        FunctionType => true,
        GenericType => true,
        _ => throw new UnreachableException(),
    };

    public unsafe bool GenerateImplMarshaled(CompiledFunction function, [NotNullWhen(true)] out ExternalFunctionScopedSyncCallback? marshaled)
    {
        marshaled = null;

        if (function.Function.ParameterTypes.Append(function.Function.Type).Any(ScanForPointer))
        {
            return false;
        }

        if (!EmitFunction(function.Function, out DynamicMethod? builder)) return false;
        if (!WrapWithMarshaling(builder, out DynamicMethod? marshaledBuilder)) return false;

        marshaled = (ExternalFunctionScopedSyncCallback)marshaledBuilder.CreateDelegate(typeof(ExternalFunctionScopedSyncCallback));

        return true;
    }

    static int GetManagedSize(Type type)
    {
        DynamicMethod method = new("GetManagedSizeImpl", typeof(uint), Array.Empty<Type>(), false);
        ILGenerator gen = method.GetILGenerator();
        gen.Emit(OpCodes.Sizeof, type);
        gen.Emit(OpCodes.Ret);
        Func<uint> func = (Func<uint>)method.CreateDelegate(typeof(Func<uint>));
        return checked((int)func());
    }

    readonly Dictionary<object, DynamicMethod> _marshalCache = new();
    bool WrapWithMarshaling(DynamicMethod method, [NotNullWhen(true)] out DynamicMethod? marshaled)
    {
        if (_marshalCache.TryGetValue(method, out marshaled)) return true;

        marshaled = new(
            $"marshal___{method.Name}",
            typeof(void),
            new Type[] { typeof(nint), typeof(nint), typeof(nint) },
            Module);

        (Type type, int size)[] parameters =
            method.GetParameters()
            .Select(v => (v.ParameterType, GetManagedSize(v.ParameterType)))
            .ToArray();
        int parametersSize = parameters.Aggregate(0, (a, b) => a + b.size);

        ILGenerator il = marshaled.GetILGenerator();
        if (method.ReturnType != typeof(void)) il.Emit(OpCodes.Ldarg_2);

        for (int i = 0; i < parameters.Length; i++)
        {
            (Type parameterType, int parameterSize) = parameters[i];

            parametersSize -= parameterSize;

            il.Emit(OpCodes.Ldarg_1);
            if (parametersSize > 0)
            {
                switch (parametersSize)
                {
                    case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                    case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                    case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                    case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                    case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                    case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                    case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                    case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                    case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                    default: il.Emit(OpCodes.Ldc_I4, parametersSize); break;
                }

                il.Emit(OpCodes.Conv_I);
                il.Emit(OpCodes.Add);
            }

            if (!LoadIndirect(parameterType, il, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(DiagnosticWithoutContext.Error(error1.Message));
                return false;
            }
        }

        il.Emit(OpCodes.Call, method);

        if (!StoreIndirect(method.ReturnType, il, out PossibleDiagnostic? error2))
        {
            Diagnostics.Add(DiagnosticWithoutContext.Error(error2.Message));
            return false;
        }

        il.Emit(OpCodes.Ret);

        _marshalCache.Add(method, marshaled);
        return true;
    }

    Func<int> GenerateImpl()
    {
        bool successful = true;

        Func<int> result = GenerateCodeForTopLevelStatements(TopLevelStatements, ref successful);

        if (!successful && !Diagnostics.HasErrors)
        {
            Diagnostics.Add(DiagnosticWithoutContext.Critical($"Failed to generate valid MSIL"));
        }

        return result;
    }

    public static Func<int> Generate(CompilerResult compilerResult, DiagnosticsCollection diagnostics, ILGeneratorSettings settings, ModuleBuilder? module = null)
        => new CodeGeneratorForIL(compilerResult, diagnostics, settings, module)
        .GenerateImpl();
}
