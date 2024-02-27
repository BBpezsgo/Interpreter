using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;

public class CompiledOperator :
    FunctionDefinition,
    ISameCheck,
    ISameCheck<CompiledOperator>,
    IReferenceable<OperatorCall>,
    IDuplicatable<CompiledOperator>
{
    public new readonly CompiledType Type;
    public readonly ImmutableArray<CompiledType> ParameterTypes;

    public readonly CompiledStruct? Context;
    public readonly ImmutableDictionary<string, CompiledAttribute> CompiledAttributes;

    public int TimesUsed;
    public int TimesUsedTotal;

    public int InstructionOffset = -1;

    public bool ReturnSomething => Type.BuiltinType != LanguageCore.Compiler.Type.Void;

    readonly List<Reference<OperatorCall>> references;
    public IReadOnlyList<Reference<OperatorCall>> ReferencesOperator => references;

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

    public bool IsExternal => CompiledAttributes.ContainsKey("External");
    public string ExternalFunctionName => CompiledAttributes.TryGetAttribute("External", out string? name) ? name : string.Empty;

    public CompiledOperator(CompiledType type, IEnumerable<CompiledType> parameterTypes, CompiledStruct? context, IEnumerable<KeyValuePair<string, CompiledAttribute>> compiledAttributes, FunctionDefinition functionDefinition) : base(functionDefinition)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.CompiledAttributes = compiledAttributes.ToImmutableDictionary();
        this.Context = context;
        this.references = new List<Reference<OperatorCall>>();
    }

    public CompiledOperator(CompiledType type, IEnumerable<CompiledType> parameterTypes, CompiledOperator other) : base(other)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.CompiledAttributes = other.CompiledAttributes;
        this.Context = other.Context;
        this.references = new List<Reference<OperatorCall>>(other.references);
        this.TimesUsed = other.TimesUsed;
        this.TimesUsedTotal = other.TimesUsedTotal;
    }

    public void AddReference(OperatorCall referencedBy, Uri? file) => references.Add(new Reference<OperatorCall>(referencedBy, file));
    public void ClearReferences() => references.Clear();

    public bool IsSame(CompiledOperator other)
    {
        if (this.Type != other.Type) return false;
        if (this.Identifier.Content != other.Identifier.Content) return false;
        if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
        for (int i = 0; i < this.ParameterTypes.Length; i++)
        { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

        return true;
    }
    public bool IsSame(ISameCheck? other) => other is CompiledOperator other2 && IsSame(other2);

    public new CompiledOperator Duplicate() => new(Type, new List<CompiledType>(ParameterTypes).ToArray(), Context, CompiledAttributes, this)
    {
        Modifiers = Modifiers,
        TimesUsed = TimesUsed,
        TimesUsedTotal = TimesUsedTotal,
    };
}
