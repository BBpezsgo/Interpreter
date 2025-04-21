using System.Diagnostics.SymbolStore;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace LanguageCore.IL.Generator;

public class ILProxy
{
    readonly ModuleBuilder? Module;
    readonly ILGenerator IL;
    readonly StringBuilder? UsingsBuilder;
    readonly StringBuilder? LocalsBuilder;
    readonly StringBuilder? InstructionsBuilder;
    readonly Dictionary<Uri, ISymbolDocumentWriter> SymbolDocumentWriters;

    public ILProxy(ILGenerator il, ModuleBuilder? module, bool generateText)
    {
        IL = il;
        UsingsBuilder = generateText ? new() : null;
        LocalsBuilder = generateText ? new() : null;
        InstructionsBuilder = generateText ? new() : null;
        SymbolDocumentWriters = new();
        Module = module;
    }

    public void MarkSequencePoint(ILocated location)
    {
        return;

        if (Module is null) throw new InvalidOperationException($"This IL generator isn't in a module");

        if (!SymbolDocumentWriters.TryGetValue(location.Location.File, out ISymbolDocumentWriter? symbolDocumentWriter))
        {
            SymbolDocumentWriters.Add(location.Location.File,
                symbolDocumentWriter = Module.DefineDocument(location.Location.File.ToString(),
                    SymDocumentType.Text,
                    SymLanguageType.ILAssembly,
                    SymLanguageVendor.Microsoft
                )
            );
        }

        MarkSequencePoint(
            symbolDocumentWriter,
            location.Location.Position.Range.Start.Line,
            location.Location.Position.Range.Start.Character,
            location.Location.Position.Range.End.Line,
            location.Location.Position.Range.End.Character);
    }

    public LocalBuilder DeclareLocal(Type localType)
    {
        LocalBuilder v = IL.DeclareLocal(localType);
        LocalsBuilder?.AppendLine($"[{v.LocalIndex}] {v.LocalType}");
        return v;
    }

    public LocalBuilder DeclareLocal(Type localType, bool pinned)
    {
        LocalBuilder v = IL.DeclareLocal(localType, pinned);
        LocalsBuilder?.AppendLine($"[{v.LocalIndex}] {v.LocalType}");
        return v;
    }

    public Label DefineLabel() => IL.DefineLabel();

    public void Emit(OpCode opcode, string str)
    {
        IL.Emit(opcode, str);
        InstructionsBuilder?.AppendLine($"{opcode} \"{str.Escape()}\"");
    }

    public void Emit(OpCode opcode, float arg)
    {
        IL.Emit(opcode, arg);
        InstructionsBuilder?.AppendLine($"{opcode} {arg}");
    }

    public void Emit(OpCode opcode, sbyte arg)
    {
        IL.Emit(opcode, arg);
        InstructionsBuilder?.AppendLine($"{opcode} {arg}");
    }

    public void Emit(OpCode opcode, MethodInfo meth)
    {
        IL.Emit(opcode, meth);
        InstructionsBuilder?.AppendLine($"{opcode} {meth}");
    }

    public void Emit(OpCode opcode, FieldInfo field)
    {
        IL.Emit(opcode, field);
        InstructionsBuilder?.AppendLine($"{opcode} {field}");
    }

    public void Emit(OpCode opcode, Type cls)
    {
        IL.Emit(opcode, cls);
        InstructionsBuilder?.AppendLine($"{opcode} {cls}");
    }

    public void Emit(OpCode opcode, Label[] labels)
    {
        IL.Emit(opcode, labels);
        InstructionsBuilder?.AppendLine($"{opcode} {string.Join(' ', labels.Select(v => $"{v}"))}");
    }

    public void Emit(OpCode opcode, SignatureHelper signature)
    {
        IL.Emit(opcode, signature);
        InstructionsBuilder?.AppendLine($"{opcode} {signature}");
    }

    public void Emit(OpCode opcode, LocalBuilder local)
    {
        IL.Emit(opcode, local);
        InstructionsBuilder?.AppendLine($"{opcode} {local}");
    }

    public void Emit(OpCode opcode, ConstructorInfo con)
    {
        IL.Emit(opcode, con);
        InstructionsBuilder?.AppendLine($"{opcode} {con}");
    }

    public void Emit(OpCode opcode, long arg)
    {
        IL.Emit(opcode, arg);
        InstructionsBuilder?.AppendLine($"{opcode} {arg}");
    }

    public void Emit(OpCode opcode, int arg)
    {
        IL.Emit(opcode, arg);
        InstructionsBuilder?.AppendLine($"{opcode} {arg}");
    }

    public void Emit(OpCode opcode, short arg)
    {
        IL.Emit(opcode, arg);
        InstructionsBuilder?.AppendLine($"{opcode} {arg}");
    }

    public void Emit(OpCode opcode, double arg)
    {
        IL.Emit(opcode, arg);
        InstructionsBuilder?.AppendLine($"{opcode} {arg}");
    }

    public void Emit(OpCode opcode, byte arg)
    {
        IL.Emit(opcode, arg);
        InstructionsBuilder?.AppendLine($"{opcode} {arg}");
    }

    public void Emit(OpCode opcode)
    {
        IL.Emit(opcode);
        InstructionsBuilder?.AppendLine($"{opcode}");
    }

    public void Emit(OpCode opcode, Label label)
    {
        IL.Emit(opcode, label);
        InstructionsBuilder?.AppendLine($"{opcode} {label}");
    }

    public void EmitCall(OpCode opcode, MethodInfo methodInfo, Type[]? optionalParameterTypes)
    {
        IL.EmitCall(opcode, methodInfo, optionalParameterTypes);
        InstructionsBuilder?.AppendLine($"{opcode} {methodInfo} ({string.Join(", ", optionalParameterTypes?.Select(v => v.ToString()) ?? Enumerable.Empty<string>())})");
    }

    public void EmitCalli(OpCode opcode, CallingConvention unmanagedCallConv, Type? returnType, Type[]? parameterTypes)
    {
        IL.EmitCalli(opcode, unmanagedCallConv, returnType, parameterTypes);
        InstructionsBuilder?.AppendLine($"{opcode} {unmanagedCallConv} ({returnType} {string.Join(", ", parameterTypes?.Select(v => v.ToString()) ?? Enumerable.Empty<string>())})");
    }

    public void EmitCalli(OpCode opcode, CallingConventions callingConvention, Type? returnType, Type[]? parameterTypes, Type[]? optionalParameterTypes)
    {
        IL.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes);
        InstructionsBuilder?.AppendLine($"{opcode} {callingConvention} ({returnType} {string.Join(", ", parameterTypes?.Select(v => v.ToString()) ?? Enumerable.Empty<string>())} {string.Join(", ", optionalParameterTypes?.Select(v => v.ToString()) ?? Enumerable.Empty<string>())})");
    }

    public void EmitWriteLine(LocalBuilder localBuilder)
    {
        IL.EmitWriteLine(localBuilder);
        InstructionsBuilder?.AppendLine($"WriteLine {localBuilder}");
    }

    public void EmitWriteLine(FieldInfo fld)
    {
        IL.EmitWriteLine(fld);
        InstructionsBuilder?.AppendLine($"WriteLine {fld}");
    }

    public void EmitWriteLine(string value)
    {
        IL.EmitWriteLine(value);
        InstructionsBuilder?.AppendLine($"WriteLine \"{value.Escape()}\"");
    }

    public void MarkLabel(Label loc)
    {
        IL.MarkLabel(loc);
        InstructionsBuilder?.AppendLine($"{loc}:");
    }

    public void MarkSequencePoint(ISymbolDocumentWriter document, int startLine, int startColumn, int endLine, int endColumn)
    {
        IL.MarkSequencePoint(document, startLine, startColumn, endLine, endColumn);
    }

    public void ThrowException([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type excType)
    {
        IL.ThrowException(excType);
        InstructionsBuilder?.AppendLine($"throw {excType}");
    }

    public void UsingNamespace(string usingNamespace)
    {
        IL.UsingNamespace(usingNamespace);
        UsingsBuilder?.AppendLine($"using {usingNamespace}");
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (UsingsBuilder?.Length > 0)
        {
            result.Append(UsingsBuilder);
            result.AppendLine();
        }

        if (LocalsBuilder?.Length > 0)
        {
            result.AppendLine($".locals init (");
            result.AppendIndented("  ", LocalsBuilder.ToString().Trim());
            result.AppendLine($")");
            result.AppendLine();
        }

        if (InstructionsBuilder?.Length > 0)
        {
            result.Append(InstructionsBuilder);
            result.AppendLine();
        }

        return result.ToString();
    }
}
