using System;
using System.Collections.Generic;
using System.Text;

namespace LanguageCore.Compiler
{
    using LanguageCore.Tokenizing;
    using Parser;
    using Parser.Statement;

    public class CompiledGeneralFunction :
        GeneralFunctionDefinition,
        ISameCheck,
        ISameCheck<CompiledGeneralFunction>,
        IReferenceable<KeywordCall>,
        IReferenceable<ConstructorCall>,
        IDuplicatable<CompiledGeneralFunction>
    {
        public CompiledType[] ParameterTypes;
        readonly List<Reference<Statement>> references;
        public CompiledStruct? Context;
        public CompiledType Type;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset = -1;

        public bool ReturnSomething => this.Type.BuiltinType != LanguageCore.Compiler.Type.Void;

        public IReadOnlyList<Reference<Statement>> References => references;

        public override bool IsTemplate
        {
            get
            {
                if (TemplateInfo is not null) return true;
                if (Context != null && Context.TemplateInfo != null) return true;
                return false;
            }
        }

        public CompiledGeneralFunction(CompiledType type, CompiledType[] parameterTypes, GeneralFunctionDefinition functionDefinition) : base(functionDefinition)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;
            this.references = new List<Reference<Statement>>();
            this.Context = null;
        }

        public void AddReference(KeywordCall referencedBy, Uri? file) => references.Add(new Reference<Statement>(referencedBy, file));
        public void AddReference(ConstructorCall referencedBy, Uri? file) => references.Add(new Reference<Statement>(referencedBy, file));
        public void ClearReferences() => references.Clear();

        public bool IsSame(CompiledGeneralFunction other)
        {
            if (this.Type != other.Type) return false;
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
            for (int i = 0; i < this.ParameterTypes.Length; i++)
            { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

            return true;
        }
        public bool IsSame(ISameCheck? other) => other is CompiledGeneralFunction other2 && IsSame(other2);

        public new CompiledGeneralFunction Duplicate() => new(Type, ParameterTypes, this)
        {
            TimesUsed = TimesUsed,
            TimesUsedTotal = TimesUsedTotal,
            Context = Context,
        };

        public override string ToString()
        {
            StringBuilder result = new();

            if (IsExport)
            { result.Append("export "); }

            result.Append(this.Identifier.Content);

            result.Append('(');
            if (this.ParameterTypes.Length > 0)
            {
                for (int i = 0; i < ParameterTypes.Length; i++)
                {
                    if (i > 0) result.Append(", ");
                    if (Parameters[i].Modifiers.Length >0)
                    {
                        result.Append(string.Join<Token>(' ', Parameters[i].Modifiers));
                        result.Append(' ');
                    }
                    result.Append(ParameterTypes[i].ToString());
                }
            }
            result.Append(')');

            result.Append(Block?.ToString() ?? ";");

            return result.ToString();
        }

        public CompiledGeneralFunctionTemplateInstance InstantiateTemplate(TypeArguments typeParameters)
        {
            CompiledGeneralFunctionTemplateInstance result = new(Type, ParameterTypes, this, this)
            {
                TimesUsed = TimesUsed,
                TimesUsedTotal = TimesUsedTotal,
                Context = Context,
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
