using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LanguageCore.Compiler
{
    using Parser;
    using Tokenizing;

    public class CompiledClass : ClassDefinition,
        IDuplicatable<CompiledClass>,
        IReferenceable<TypeInstance>
    {
        public new readonly CompiledField[] Fields;
        public CompiledAttributeCollection CompiledAttributes;

        public IReadOnlyList<Reference<TypeInstance>> References => references;
        readonly List<Reference<TypeInstance>> references;

        readonly TypeArguments currentTypeArguments;
        public IReadOnlyDictionary<string, CompiledType> CurrentTypeArguments => currentTypeArguments;

        public IReadOnlyDictionary<string, int> FieldOffsets
        {
            get
            {
                Dictionary<string, int> result = new();
                int currentOffset = 0;
                foreach (CompiledField field in Fields)
                {
                    result.Add(field.Identifier.Content, currentOffset);
                    currentOffset += GetType(field.Type, field).SizeOnStack;
                }
                return result;
            }
        }

        public int SizeOnHeap
        {
            get
            {
                int size = 0;
                foreach (CompiledField field in Fields)
                { size += GetType(field.Type, field).SizeOnStack; }
                return size;
            }
        }

        public CompiledClass(CompiledAttributeCollection compiledAttributes, CompiledField[] fields, ClassDefinition definition) : base(definition.Name, definition.BracketStart, definition.BracketEnd, definition.Attributes, definition.Modifiers, definition.Fields, definition.Methods, definition.GeneralMethods, definition.Operators)
        {
            this.CompiledAttributes = compiledAttributes;
            this.Fields = fields;
            this.TemplateInfo = definition.TemplateInfo;
            this.currentTypeArguments = new TypeArguments();

            base.FilePath = definition.FilePath;
            base.Statements.Clear();
            base.Statements.AddRange(definition.Statements);

            this.references = new List<Reference<TypeInstance>>();
        }

        public int SizeWithTypeArguments(IReadOnlyDictionary<string, CompiledType> typeParameters)
        {
            int size = 0;
            foreach (CompiledField field in Fields)
            { size += GetType(field.Type, field, typeParameters).SizeOnStack; }
            return size;
        }

        public void SetTypeArguments(IEnumerable<CompiledType> typeParameters)
             => SetTypeArguments(typeParameters.ToArray());
        public void SetTypeArguments(CompiledType[] typeParameters)
        {
            currentTypeArguments.Clear();
            AddTypeArguments(typeParameters);
        }
        public void SetTypeArguments(IReadOnlyDictionary<string, CompiledType> typeParameters)
        {
            currentTypeArguments.Clear();
            AddTypeArguments(typeParameters);
        }

        public void AddTypeArguments(IEnumerable<CompiledType> typeParameters)
             => AddTypeArguments(typeParameters.ToArray());
        public void AddTypeArguments(CompiledType[] typeParameters)
        {
            if (TemplateInfo == null)
            { return; }

            if (typeParameters == null || typeParameters.Length == 0)
            { return; }

            string[] typeParameterNames = TemplateInfo.ToDictionary().Keys.ToArray();

            if (typeParameters.Length != typeParameterNames.Length)
            { throw new CompilerException("Ah"); }

            for (int i = 0; i < typeParameters.Length; i++)
            {
                CompiledType value = typeParameters[i];
                string key = typeParameterNames[i];

                currentTypeArguments[key] = new CompiledType(value);
            }
        }
        public void AddTypeArguments(IReadOnlyDictionary<string, CompiledType> typeParameters)
        {
            if (TemplateInfo == null)
            { return; }

            string[] typeParameterNames = TemplateInfo.ToDictionary().Keys.ToArray();

            for (int i = 0; i < typeParameterNames.Length; i++)
            {
                if (!typeParameters.TryGetValue(typeParameterNames[i], out CompiledType? typeParameterValue))
                { continue; }
                currentTypeArguments[typeParameterNames[i]] = new CompiledType(typeParameterValue);
            }
        }

        public void ClearTypeArguments() => currentTypeArguments.Clear();

        CompiledType GetType(CompiledType type, IPositioned position)
        {
            if (!type.IsGeneric) return type;
            if (!currentTypeArguments.TryGetValue(type.Name, out CompiledType? result))
            { throw new CompilerException($"Type argument \"{type.Name}\" not found", position, FilePath); }
            return result;
        }
        CompiledType GetType(CompiledType type, IPositioned position, IReadOnlyDictionary<string, CompiledType> typeParameters)
        {
            if (!type.IsGeneric) return type;
            if (!typeParameters.TryGetValue(type.Name, out CompiledType? result) &&
                !currentTypeArguments.TryGetValue(type.Name, out result))
            { throw new CompilerException($"Type argument \"{type.Name}\" not found", position, FilePath); }
            return result;
        }

        public CompiledClass Duplicate() => new(CompiledAttributes, Fields, this);

        public bool TryGetTypeArgumentIndex(string typeArgumentName, out int index)
        {
            index = 0;
            if (TemplateInfo == null) return false;
            for (int i = 0; i < TemplateInfo.TypeParameters.Length; i++)
            {
                if (TemplateInfo.TypeParameters[i].Content == typeArgumentName)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            StringBuilder result = new();
            result.Append("class ");

            result.Append(this.Name.Content);

            if (this.TemplateInfo != null)
            {
                result.Append('<');
                if (this.currentTypeArguments.Count > 0)
                {
                    for (int i = 0; i < this.TemplateInfo.TypeParameters.Length; i++)
                    {
                        if (i > 0) result.Append(", ");

                        string typeParameterName = this.TemplateInfo.TypeParameters[i].Content;
                        if (this.currentTypeArguments.TryGetValue(typeParameterName, out CompiledType? typeParameterValue))
                        {
                            result.Append(typeParameterValue.ToString());
                        }
                        else
                        {
                            result.Append('?');
                        }
                    }
                }
                else
                {
                    result.Append(string.Join<Token>(", ", this.TemplateInfo.TypeParameters));
                }
                result.Append('>');
            }
            return result.ToString();
        }

        public void AddReference(TypeInstance referencedBy, string? file) => references.Add(new Reference<TypeInstance>(referencedBy, file));
        public void ClearReferences() => references.Clear();
    }
}
