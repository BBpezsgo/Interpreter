using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;

public class CompiledFunction :
    FunctionDefinition,
    ISameCheck,
    ISameCheck<CompiledFunction>,
    IReferenceable<FunctionCall>,
    IReferenceable<IndexCall>,
    IReferenceable<Identifier>,
    IReferenceable<KeywordCall>,
    IDuplicatable<CompiledFunction>
{
    public new readonly CompiledType Type;
    public readonly ImmutableArray<CompiledType> ParameterTypes;

    public readonly CompiledStruct? Context;
    public readonly ImmutableDictionary<string, CompiledAttribute> CompiledAttributes;

    public int TimesUsed;
    public int TimesUsedTotal;

    public int InstructionOffset = -1;

    public bool ReturnSomething => Type.BuiltinType != LanguageCore.Compiler.Type.Void;

    readonly List<Reference<Statement>> references;
    public IReadOnlyList<Reference<Statement>> References => references;

    public TypeInstance TypeToken => base.Type;

    public override bool IsTemplate
    {
        get
        {
            if (TemplateInfo != null) return true;
            if (Context != null && Context.TemplateInfo != null) return true;
            return false;
        }
    }

    [MemberNotNullWhen(true, nameof(ExternalFunctionName))]
    public bool IsExternal => CompiledAttributes.ContainsKey("External");
    public string? ExternalFunctionName
    {
        get
        {
            if (CompiledAttributes.TryGetValue("External", out CompiledAttribute? attributeValues))
            {
                if (attributeValues.TryGetValue(0, out string name))
                { return name; }
            }
            return null;
        }
    }

    [MemberNotNullWhen(true, nameof(BuiltinFunctionName))]
    public bool IsBuiltin => CompiledAttributes.ContainsKey("Builtin");
    public string? BuiltinFunctionName
    {
        get
        {
            if (CompiledAttributes.TryGetValue("Builtin", out CompiledAttribute? attributeValues))
            {
                if (attributeValues.TryGetValue(0, out string name))
                { return name; }
            }
            return null;
        }
    }

    public CompiledFunction(CompiledType type, IEnumerable<CompiledType> parameterTypes, CompiledStruct? context, IEnumerable<KeyValuePair<string, CompiledAttribute>> compiledAttributes, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.CompiledAttributes = compiledAttributes.ToImmutableDictionary();
        this.Context = context;
        this.references = new List<Reference<Statement>>();
    }

    public CompiledFunction(CompiledType type, IEnumerable<CompiledType> parameterTypes, CompiledFunction other) : base(other)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.CompiledAttributes = other.CompiledAttributes;
        this.Context = other.Context;
        this.references = new List<Reference<Statement>>(other.references);
        this.TimesUsed = other.TimesUsed;
        this.TimesUsedTotal = other.TimesUsedTotal;
    }

    public void AddReference(FunctionCall referencedBy, Uri? file) => references.Add(new Reference<Statement>(referencedBy, file));
    public void AddReference(Identifier referencedBy, Uri? file) => references.Add(new Reference<Statement>(referencedBy, file));
    public void AddReference(KeywordCall referencedBy, Uri? file) => references.Add(new Reference<Statement>(referencedBy, file));
    public void AddReference(IndexCall referencedBy, Uri? file) => references.Add(new Reference<Statement>(referencedBy, file));
    public void ClearReferences() => references.Clear();

    public bool IsSame(CompiledFunction other)
    {
        if (this.Type != other.Type) return false;
        if (this.Identifier.Content != other.Identifier.Content) return false;
        if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
        for (int i = 0; i < this.ParameterTypes.Length; i++)
        { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

        return true;
    }
    public bool IsSame(ISameCheck? other) => other is CompiledFunction other2 && IsSame(other2);

    public new CompiledFunction Duplicate() => new(this.Type, new List<CompiledType>(this.ParameterTypes).ToArray(), Context, CompiledAttributes, this)
    {
        TimesUsed = TimesUsed,
        TimesUsedTotal = TimesUsedTotal,
    };

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsExport)
        { result.Append("export "); }

        result.Append(this.Type.ToString());
        result.Append(' ');

        result.Append(this.Identifier.Content);

        result.Append('(');
        if (this.ParameterTypes.Length > 0)
        {
            for (int i = 0; i < ParameterTypes.Length; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(ParameterTypes[i].ToString());
            }
        }
        result.Append(')');

        if (Block != null)
        {
            result.Append(' ');
            result.Append(Block.ToString());
        }
        else
        { result.Append(';'); }

        return result.ToString();
    }
}
