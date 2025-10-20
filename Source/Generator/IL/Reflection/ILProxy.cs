using System.Reflection;
using System.Reflection.Emit;

namespace LanguageCore.IL.Reflection;

public class ILProxy
{
    readonly ILGenerator IL;
    readonly List<LocalBuilder>? _locals;
    readonly List<string>? _usings;
    readonly List<ILInstruction>? _instructions;
    readonly Dictionary<int, int>? _labelOffsets;
    readonly List<Label>? _labels;

    public ILProxy(ILGenerator il, bool debugInfo)
    {
        IL = il;
        if (debugInfo)
        {
            _locals = new();
            _usings = new();
            _instructions = new();
            _labelOffsets = new();
            _labels = new();
        }
    }

    public LocalBuilder DeclareLocal(Type localType)
    {
        LocalBuilder v = IL.DeclareLocal(localType);
        _locals?.Add(v);
        return v;
    }

    public LocalBuilder DeclareLocal(Type localType, bool pinned)
    {
        LocalBuilder v = IL.DeclareLocal(localType, pinned);
        _locals?.Add(v);
        return v;
    }

    public Label DefineLabel()
    {
        Label label = IL.DefineLabel();
        _labels?.Add(label);
        return label;
    }

    public void Emit(OpCode opcode, string str)
    {
        IL.Emit(opcode, str);
        _instructions?.Add(new InlineStringInstruction(IL.ILOffset, opcode, str));
    }

    public void Emit(OpCode opcode, float arg)
    {
        IL.Emit(opcode, arg);
        _instructions?.Add(new InlineRInstruction(IL.ILOffset, opcode, arg));
    }

    public void Emit(OpCode opcode, sbyte arg)
    {
        IL.Emit(opcode, arg);
        _instructions?.Add(new InlineI8Instruction(IL.ILOffset, opcode, arg));
    }

    public void Emit(OpCode opcode, MethodInfo meth)
    {
        IL.Emit(opcode, meth);
        _instructions?.Add(new InlineMethodInstruction(IL.ILOffset, opcode, meth));
    }

    public void Emit(OpCode opcode, FieldInfo field)
    {
        IL.Emit(opcode, field);
        _instructions?.Add(new InlineFieldInstruction(IL.ILOffset, opcode, field));
    }

    public void Emit(OpCode opcode, Type cls)
    {
        IL.Emit(opcode, cls);
        _instructions?.Add(new InlineTypeInstruction(IL.ILOffset, opcode, cls));
    }

    // public void Emit(OpCode opcode, Label[] labels)
    // {
    //     IL.Emit(opcode, labels);
    //     InstructionsBuilder?.AppendLine($"{opcode} {string.Join(' ', labels.Select(v => $"{v}"))}");
    // }

    public void Emit(OpCode opcode, SignatureHelper signature)
    {
        IL.Emit(opcode, signature);
        _instructions?.Add(new InlineSigInstruction(IL.ILOffset, opcode, signature.GetSignature()));
    }

    public void Emit(OpCode opcode, LocalBuilder local)
    {
        IL.Emit(opcode, local);
        _instructions?.Add(new InlineLocalInstruction(IL.ILOffset, opcode, local));
    }

    public void Emit(OpCode opcode, ConstructorInfo con)
    {
        IL.Emit(opcode, con);
        _instructions?.Add(new InlineMethodInstruction(IL.ILOffset, opcode, con));
    }

    public void Emit(OpCode opcode, long arg)
    {
        IL.Emit(opcode, arg);
        _instructions?.Add(new InlineI8Instruction(IL.ILOffset, opcode, arg));
    }

    public void Emit(OpCode opcode, int arg)
    {
        IL.Emit(opcode, arg);
        _instructions?.Add(new InlineIInstruction(IL.ILOffset, opcode, arg));
    }

    public void Emit(OpCode opcode, short arg)
    {
        IL.Emit(opcode, arg);
        _instructions?.Add(new InlineIInstruction(IL.ILOffset, opcode, arg));
    }

    public void Emit(OpCode opcode, double arg)
    {
        IL.Emit(opcode, arg);
        _instructions?.Add(new InlineRInstruction(IL.ILOffset, opcode, arg));
    }

    public void Emit(OpCode opcode, byte arg)
    {
        IL.Emit(opcode, arg);
        _instructions?.Add(new ShortInlineIInstruction(IL.ILOffset, opcode, arg));
    }

    public void Emit(OpCode opcode)
    {
        IL.Emit(opcode);
        _instructions?.Add(new InlineNoneInstruction(IL.ILOffset, opcode));
    }

    public void Emit(OpCode opcode, Label label)
    {
        IL.Emit(opcode, label);
        _instructions?.Add(new InlineLabelInstruction(IL.ILOffset, opcode, label));
    }

    // public void EmitCall(OpCode opcode, MethodInfo methodInfo, Type[]? optionalParameterTypes)
    // {
    //     IL.EmitCall(opcode, methodInfo, optionalParameterTypes);
    //     InstructionsBuilder?.AppendLine($"{opcode} {methodInfo} ({string.Join(", ", optionalParameterTypes?.Select(v => v.ToString()) ?? Enumerable.Empty<string>())})");
    // }
    // 
    // public void EmitCalli(OpCode opcode, CallingConvention unmanagedCallConv, Type? returnType, Type[]? parameterTypes)
    // {
    //     IL.EmitCalli(opcode, unmanagedCallConv, returnType, parameterTypes);
    //     InstructionsBuilder?.AppendLine($"{opcode} {unmanagedCallConv} ({returnType} {string.Join(", ", parameterTypes?.Select(v => v.ToString()) ?? Enumerable.Empty<string>())})");
    // }
    // 
    // public void EmitCalli(OpCode opcode, CallingConventions callingConvention, Type? returnType, Type[]? parameterTypes, Type[]? optionalParameterTypes)
    // {
    //     IL.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes);
    //     InstructionsBuilder?.AppendLine($"{opcode} {callingConvention} ({returnType} {string.Join(", ", parameterTypes?.Select(v => v.ToString()) ?? Enumerable.Empty<string>())} {string.Join(", ", optionalParameterTypes?.Select(v => v.ToString()) ?? Enumerable.Empty<string>())})");
    // }
    // 
    // public void EmitWriteLine(LocalBuilder localBuilder)
    // {
    //     IL.EmitWriteLine(localBuilder);
    //     InstructionsBuilder?.AppendLine($"WriteLine {localBuilder}");
    // }
    // 
    // public void EmitWriteLine(FieldInfo fld)
    // {
    //     IL.EmitWriteLine(fld);
    //     InstructionsBuilder?.AppendLine($"WriteLine {fld}");
    // }
    // 
    // public void EmitWriteLine(string value)
    // {
    //     IL.EmitWriteLine(value);
    //     InstructionsBuilder?.AppendLine($"WriteLine \"{value.Escape()}\"");
    // }

    public void MarkLabel(Label loc)
    {
        IL.MarkLabel(loc);
#if NET_STANDARD
        if (_labelOffsets is not null) _labelOffsets[IL.ILOffset] = loc.GetHashCode();
#else
        if (_labelOffsets is not null) _labelOffsets[IL.ILOffset] = loc.Id;
#endif
    }

    public void ThrowException([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type excType)
    {
        IL.ThrowException(excType);
        // InstructionsBuilder?.AppendLine($"throw {excType}");
    }

    public void UsingNamespace(string usingNamespace)
    {
        IL.UsingNamespace(usingNamespace);
        _usings?.Add(usingNamespace);
    }

    public override string ToString()
    {
        StringBuilder result = new();

        if (_usings?.Count > 0)
        {
            foreach (string _using in _usings)
            {
                result.Append($"using {_using};");
            }
            result.AppendLine();
        }

        if (_locals?.Count > 0)
        {
            result.AppendLine($".locals init (");
            foreach (LocalBuilder _local in _locals)
            {
                result.Indent(1);
                result.AppendLine($"[{_local.LocalIndex}] {_local.LocalType};");
            }
            result.AppendLine($")");
            result.AppendLine();
        }

        foreach (ILInstruction instruction in _instructions ?? Enumerable.Empty<ILInstruction>())
        {
            foreach ((int offset, int id) in _labelOffsets ?? Enumerable.Empty<KeyValuePair<int, int>>())
            {
                if (offset != instruction.Offset) continue;
                result.AppendLine($"L_{id}:");
            }

            result.AppendLine(instruction.ToString());
        }

        return result.ToString();
    }
}
