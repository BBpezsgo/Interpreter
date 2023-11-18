using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace LanguageCore.Compiler
{
    using Parser;
    using Parser.Statement;

    public class CompiledFunction :
        FunctionDefinition,
        ICanBeSame,
        IAmInContext<CompiledClass>, 
        IReferenceable<FunctionCall>,
        IReferenceable<IndexCall>, 
        IReferenceable<Identifier>,
        IReferenceable<KeywordCall>, 
        IDuplicatable<CompiledFunction>
    {
        public readonly CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset = -1;

        public bool ReturnSomething => this.Type.BuiltinType != LanguageCore.Compiler.Type.Void;

        public CompiledAttributeCollection CompiledAttributes;

        public IReadOnlyList<(Statement Statement, string? File)> References => references;
        readonly List<(Statement Statement, string? File)> references = new();

        public new CompiledType Type;
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
                if (CompiledAttributes.TryGetValue("External", out var attributeValues))
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
                if (CompiledAttributes.TryGetValue("Builtin", out var attributeValues))
                {
                    if (attributeValues.TryGetValue(0, out string name))
                    { return name; }
                }
                return null;
            }
        }

        public CompiledClass? Context { get; set; }

        public CompiledFunction(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition) : base(functionDefinition.Modifiers, functionDefinition.Type, functionDefinition.Identifier, functionDefinition.TemplateInfo)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;
            this.CompiledAttributes = new();

            base.Attributes = functionDefinition.Attributes;
            base.Parameters = functionDefinition.Parameters;
            base.Block = functionDefinition.Block;
            base.FilePath = functionDefinition.FilePath;
        }

        public void AddReference(FunctionCall statement, string? file) => references.Add((statement, file));
        public void AddReference(Identifier statement, string? file) => references.Add((statement, file));
        public void AddReference(KeywordCall statement, string? file) => references.Add((statement, file));
        public void AddReference(IndexCall statement, string? file) => references.Add((statement, file));
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
        public bool IsSame(ICanBeSame? other) => other is CompiledFunction other2 && IsSame(other2);

        public CompiledFunction Duplicate() => new(this.Type, new List<CompiledType>(this.ParameterTypes).ToArray(), this)
        {
            CompiledAttributes = this.CompiledAttributes,
            Context = this.Context,
            Modifiers = this.Modifiers,
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

        public CompiledFunctionTemplateInstance InstantiateTemplate(TypeArguments typeParameters)
        {
            CompiledFunctionTemplateInstance result = new(Type, new List<CompiledType>(this.ParameterTypes).ToArray(), this, this)
            {
                CompiledAttributes = this.CompiledAttributes,
                Context = this.Context,
                Modifiers = this.Modifiers,
                TimesUsed = TimesUsed,
                TimesUsedTotal = TimesUsedTotal,
            };

            Utils.SetTypeParameters(result.ParameterTypes, typeParameters);

            if (result.Type.IsGeneric)
            {
                if (!typeParameters.TryGetValue(result.Type.Name, out CompiledType? typeParameter))
                { throw new NotImplementedException(); }
                result.Type = typeParameter;
            }

            return result;
        }
    }
}
