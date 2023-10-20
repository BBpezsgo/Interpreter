﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore.BBCode.Compiler
{

    public class CompiledConstant
    {
        public readonly VariableDeclaration Declaration;
        public readonly DataItem Value;

        public CompiledConstant(VariableDeclaration declaration, DataItem value)
        {
            Declaration = declaration;
            Value = value;
        }
    }

    public struct AttributeValues
    {
        public List<Literal> parameters;
        public Token Identifier;

        public readonly bool TryGetValue<T>(int index, out T value)
        {
            value = default;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            Literal.Type type = Utils.ConvertType(typeof(T));
            value = type switch
            {
                Literal.Type.Integer => (T)(object)parameters[index].ValueInt,
                Literal.Type.Float => (T)(object)parameters[index].ValueFloat,
                Literal.Type.String => (T)(object)parameters[index].ValueString,
                Literal.Type.Boolean => (T)(object)parameters[index].ValueBool,
                _ => default,
            };
            return true;
        }

        public readonly bool TryGetValue(int index, out string value)
        {
            value = string.Empty;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == Literal.Type.String)
            {
                value = parameters[index].ValueString;
            }
            return true;
        }
        public readonly bool TryGetValue(int index, out int value)
        {
            value = 0;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == Literal.Type.Integer)
            {
                value = parameters[index].ValueInt;
            }
            return true;
        }
        public readonly bool TryGetValue(int index, out float value)
        {
            value = 0;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == Literal.Type.Float)
            {
                value = parameters[index].ValueFloat;
            }
            return true;
        }
        public readonly bool TryGetValue(int index, out bool value)
        {
            value = false;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == Literal.Type.Boolean)
            {
                value = parameters[index].ValueBool;
            }
            return true;
        }
    }

    public readonly struct Literal
    {
        public enum Type
        {
            Integer,
            Float,
            String,
            Boolean,
        }

        public readonly int ValueInt;
        public readonly float ValueFloat;
        public readonly string ValueString;
        public readonly bool ValueBool;
        public readonly Type type;

        public Literal(int value)
        {
            this.type = Type.Integer;

            this.ValueInt = value;
            this.ValueFloat = 0;
            this.ValueString = string.Empty;
            this.ValueBool = false;
        }
        public Literal(float value)
        {
            this.type = Type.Float;

            this.ValueInt = 0;
            this.ValueFloat = value;
            this.ValueString = string.Empty;
            this.ValueBool = false;
        }
        public Literal(string value)
        {
            this.type = Type.String;

            this.ValueInt = 0;
            this.ValueFloat = 0;
            this.ValueString = value;
            this.ValueBool = false;
        }
        public Literal(bool value)
        {
            this.type = Type.Boolean;

            this.ValueInt = 0;
            this.ValueFloat = 0;
            this.ValueString = string.Empty;
            this.ValueBool = value;
        }
        public Literal(object value)
        {
            this.ValueInt = 0;
            this.ValueFloat = 0;
            this.ValueString = string.Empty;
            this.ValueBool = false;

            if (value is int @int)
            {
                this.type = Type.Integer;
                this.ValueInt = @int;
            }
            else if (value is float @float)
            {
                this.type = Type.Float;
                this.ValueFloat = @float;
            }
            else if (value is string @string)
            {
                this.type = Type.String;
                this.ValueString = @string;
            }
            else if (value is bool @bool)
            {
                this.type = Type.Boolean;
                this.ValueBool = @bool;
            }
            else
            {
                throw new InternalException($"Invalid type '{value.GetType().FullName}'");
            }
        }

        public readonly bool TryConvert<T>(out T value)
        {
            if (!Utils.TryConvertType(typeof(T), out Type type))
            {
                value = default;
                return false;
            }

            if (type != this.type)
            {
                value = default;
                return false;
            }

            value = type switch
            {
                Type.Integer => (T)(object)ValueInt,
                Type.Float => (T)(object)ValueFloat,
                Type.String => (T)(object)ValueString,
                Type.Boolean => (T)(object)ValueBool,
                _ => default,
            };
            return true;
        }
    }

    public class CompiledVariable : VariableDeclaration
    {
        public readonly new CompiledType Type;

        public readonly int MemoryAddress;
        public readonly bool IsGlobal;
        public bool IsInitialized;

        public CompiledVariable(int memoryOffset, CompiledType type, bool isGlobal, VariableDeclaration declaration)
            : base(declaration.Modifiers, declaration.Type, declaration.VariableName, declaration.InitialValue)
        {
            this.Type = type;

            this.MemoryAddress = memoryOffset;
            this.IsGlobal = isGlobal;

            base.FilePath = declaration.FilePath;

            this.IsInitialized = false;
        }
    }

    public class CompiledEnumMember : EnumMemberDefinition, IHaveKey<string>
    {
        public new DataItem Value;
    }

    public class CompiledEnum : EnumDefinition, ITypeDefinition, IHaveKey<string>
    {
        public new CompiledEnumMember[] Members;
        internal Dictionary<string, AttributeValues> CompiledAttributes;
    }

    public class CompiledStruct : StructDefinition, ITypeDefinition, IDataStructure, IHaveKey<string>
    {
        public new readonly CompiledField[] Fields;
        internal Dictionary<string, AttributeValues> CompiledAttributes;
        public List<DefinitionReference> References = null;
        internal IReadOnlyDictionary<string, int> FieldOffsets
        {
            get
            {
                Dictionary<string, int> result = new();
                int currentOffset = 0;
                foreach (CompiledField field in Fields)
                {
                    result.Add(field.Identifier.Content, currentOffset);
                    currentOffset += field.Type.SizeOnStack;
                }
                return result;
            }
        }
        public int Size
        {
            get
            {
                int size = 0;
                for (int i = 0; i < Fields.Length; i++)
                {
                    CompiledField field = Fields[i];
                    size += field.Type.SizeOnStack;
                }
                return size;
            }
        }

        public CompiledStruct(Dictionary<string, AttributeValues> compiledAttributes, CompiledField[] fields, StructDefinition definition) : base(definition.Name, definition.Attributes, definition.Fields, definition.Methods, definition.Modifiers)
        {
            this.CompiledAttributes = compiledAttributes;
            this.Fields = fields;

            base.FilePath = definition.FilePath;
            base.BracketEnd = definition.BracketEnd;
            base.BracketStart = definition.BracketStart;
            base.Statements = definition.Statements;
        }
    }

    public class CompiledClass : ClassDefinition, ITypeDefinition, IDataStructure, IHaveKey<string>, IDuplicatable<CompiledClass>
    {
        public new readonly CompiledField[] Fields;
        internal Dictionary<string, AttributeValues> CompiledAttributes;
        public List<DefinitionReference> References = null;
        readonly Dictionary<string, CompiledType> currentTypeArguments;
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
        public int Size
        {
            get
            {
                int size = 0;
                foreach (CompiledField field in Fields)
                { size += GetType(field.Type, field).SizeOnStack; }
                return size;
            }
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
                if (TemplateInfo == null)
                { throw new CompilerException("Ah"); }

                CompiledType value = typeParameters[i];
                string key = typeParameterNames[i];

                currentTypeArguments[key] = new CompiledType(value);
            }
        }
        internal void AddTypeArguments(Dictionary<string, CompiledType> typeParameters)
        {
            if (TemplateInfo == null)
            { return; }

            string[] typeParameterNames = TemplateInfo.ToDictionary().Keys.ToArray();

            for (int i = 0; i < typeParameterNames.Length; i++)
            {
                if (!typeParameters.TryGetValue(typeParameterNames[i], out CompiledType typeParameterValue))
                { continue; }
                currentTypeArguments[typeParameterNames[i]] = new CompiledType(typeParameterValue);
            }
        }

        public void ClearTypeArguments() => currentTypeArguments.Clear();

        CompiledType GetType(CompiledType type, IThingWithPosition position)
        {
            if (!type.IsGeneric) return type;
            if (!currentTypeArguments.TryGetValue(type.Name, out CompiledType result))
            { throw new CompilerException($"Type argument \"{type.Name}\" not found", position, FilePath); }
            return result;
        }

        public CompiledClass Duplicate() => new(CompiledAttributes, Fields, this)
        {

        };

        public CompiledClass(Dictionary<string, AttributeValues> compiledAttributes, CompiledField[] fields, ClassDefinition definition) : base(definition.Name, definition.Attributes, definition.Modifiers, definition.Fields, definition.Methods, definition.GeneralMethods, definition.Operators)
        {
            this.CompiledAttributes = compiledAttributes;
            this.Fields = fields;
            this.TemplateInfo = definition.TemplateInfo;
            this.currentTypeArguments = new Dictionary<string, CompiledType>();

            base.FilePath = definition.FilePath;
            base.BracketEnd = definition.BracketEnd;
            base.BracketStart = definition.BracketStart;
            base.Statements = definition.Statements;
        }

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
            string result = $"class";
            result += ' ';
            result += $"{this.Name.Content}";
            if (this.TemplateInfo != null)
            {
                result += '<';
                if (this.currentTypeArguments.Count > 0)
                {
                    for (int i = 0; i < this.TemplateInfo.TypeParameters.Length; i++)
                    {
                        if (i > 0) result += ", ";

                        string typeParameterName = this.TemplateInfo.TypeParameters[i].Content;
                        if (this.currentTypeArguments.TryGetValue(typeParameterName, out var typeParameterValue))
                        {
                            result += typeParameterValue.ToString();
                        }
                        else
                        {
                            result += "?";
                        }
                    }
                }
                else
                {
                    result += string.Join<Token>(", ", this.TemplateInfo.TypeParameters);
                }
                result += '>';
            }
            return result;
        }
    }

    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    public class CompiledParameter : ParameterDefinition
    {
        public new CompiledType Type;

        readonly int currentParamsSize;

        public readonly int Index;
        public int RealIndex => -(currentParamsSize + 1 + CodeGenerator.TagsBeforeBasePointer);
        public bool IsRef => Modifiers.Contains("ref");

        public CompiledParameter(int index, int currentParamsSize, CompiledType type, ParameterDefinition definition)
        {
            this.Index = index;
            this.currentParamsSize = currentParamsSize;
            this.Type = type;
            this.Modifiers = definition.Modifiers;

            base.Identifier = definition.Identifier;
            base.Modifiers = definition.Modifiers;
        }

        public CompiledParameter(CompiledType type, ParameterDefinition definition)
            : this(-1, -1, type, definition) { }

        public override string ToString() => $"{(IsRef ? "ref " : string.Empty)}{Type} {Identifier} {{ Index: {Index} RealIndex: {RealIndex} }}";
    }

    public class CompiledField : FieldDefinition
    {
        public new CompiledType Type;
        public Protection Protection
        {
            get
            {
                if (ProtectionToken == null) return Protection.Public;
                return ProtectionToken.Content switch
                {
                    "private" => Protection.Private,
                    "public" => Protection.Public,
                    _ => Protection.Public,
                };
            }
        }
        public CompiledClass Class;

        public CompiledField(FieldDefinition definition) : base()
        {
            base.Identifier = definition.Identifier;
            base.Type = definition.Type;
            base.ProtectionToken = definition.ProtectionToken;
        }
    }

    public class CompiledOperator : FunctionDefinition, IFunctionThing, IAmInContext<CompiledClass>, IReferenceable<OperatorCall>, IDuplicatable<CompiledOperator>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset { get; set; } = -1;

        public Dictionary<string, AttributeValues> CompiledAttributes;

        public IReadOnlyList<OperatorCall> ReferencesOperator => references;
        readonly List<OperatorCall> references = new();

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

        public bool IsExternal => CompiledAttributes.ContainsKey("External");
        public string ExternalFunctionName => CompiledAttributes.TryGetAttribute("External", out string name) ? name : string.Empty;

        public string Key => this.ID();

        public CompiledClass Context { get; set; }

        public CompiledOperator(CompiledType type, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers, functionDefinition.TemplateInfo)
        {
            this.Type = type;

            base.Attributes = functionDefinition.Attributes;
            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.Type = functionDefinition.Type;
            base.FilePath = functionDefinition.FilePath;
        }
        public CompiledOperator(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers, functionDefinition.TemplateInfo)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;
            this.CompiledAttributes = new();

            base.Attributes = functionDefinition.Attributes;
            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.Type = functionDefinition.Type;
            base.FilePath = functionDefinition.FilePath;
        }

        public void AddReference(OperatorCall statement) => references.Add(statement);
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

        public bool IsSame((string name, CompiledType[] parameters) other)
        {
            if (this.Identifier.Content != other.name) return false;
            if (this.ParameterTypes.Length != other.parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            { if (this.ParameterTypes[i] != other.parameters[i]) return false; }
            return true;
        }

        public bool IsSame(IFunctionThing other)
        {
            if (other is not CompiledOperator other2) return false;
            return IsSame(other2);
        }

        CompiledOperator IDuplicatable<CompiledOperator>.Duplicate() => new(this.Type, this)
        {
            CompiledAttributes = this.CompiledAttributes,
            Modifiers = this.Modifiers,
            ParameterTypes = new List<CompiledType>(this.ParameterTypes).ToArray(),
            TimesUsed = TimesUsed,
            TimesUsedTotal = TimesUsedTotal,
        };
        public CompiledOperatorTemplateInstance InstantiateTemplate(Dictionary<string, CompiledType> typeParameters)
        {
            CompiledOperatorTemplateInstance result = new(Type, ParameterTypes, this)
            {
                CompiledAttributes = this.CompiledAttributes,
                Modifiers = this.Modifiers,
                ParameterTypes = new List<CompiledType>(this.ParameterTypes).ToArray(),
                TimesUsed = TimesUsed,
                TimesUsedTotal = TimesUsedTotal,
                Template = this,
            };

            Utils.SetTypeParameters(result.ParameterTypes, typeParameters);

            if (result.Type.IsGeneric)
            {
                if (!typeParameters.TryGetValue(result.Type.Name, out CompiledType typeParameter))
                { throw new NotImplementedException(); }
                result.Type = typeParameter;
            }

            return result;
        }
    }

    public class CompiledFunction : FunctionDefinition, IFunctionThing, IAmInContext<CompiledClass>, IReferenceable<FunctionCall>, IReferenceable<IndexCall>, IDuplicatable<CompiledFunction>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset { get; set; } = -1;

        public bool ReturnSomething => this.Type.BuiltinType != BBCode.Compiler.Type.VOID;

        public Dictionary<string, AttributeValues> CompiledAttributes;

        public IReadOnlyList<Statement> ReferencesFunction => references;
        readonly List<Statement> references = new();

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

        public bool IsExternal => CompiledAttributes.ContainsKey("External");
        public string ExternalFunctionName
        {
            get
            {
                if (CompiledAttributes.TryGetValue("External", out var attributeValues))
                {
                    if (attributeValues.TryGetValue(0, out string name))
                    {
                        return name;
                    }
                }
                return string.Empty;
            }
        }

        public string Key => this.ID();

        public CompiledClass Context { get; set; }

        public CompiledFunction(CompiledType type, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers, functionDefinition.TemplateInfo)
        {
            this.Type = type;

            base.Attributes = functionDefinition.Attributes;
            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.Type = functionDefinition.Type;
            base.FilePath = functionDefinition.FilePath;
        }
        public CompiledFunction(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers, functionDefinition.TemplateInfo)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;
            this.CompiledAttributes = new();

            base.Attributes = functionDefinition.Attributes;
            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.Type = functionDefinition.Type;
            base.FilePath = functionDefinition.FilePath;
        }

        public void AddReference(FunctionCall statement) => references.Add(statement);
        public void AddReference(KeywordCall statement) => references.Add(statement);
        public void AddReference(IndexCall statement) => references.Add(statement);
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

        public bool IsSame(IFunctionThing other)
        {
            if (other is not CompiledFunction other2) return false;
            return IsSame(other2);
        }

        public CompiledFunction Duplicate() => new(this.Type, this)
        {
            CompiledAttributes = this.CompiledAttributes,
            Context = this.Context,
            Modifiers = this.Modifiers,
            ParameterTypes = new List<CompiledType>(this.ParameterTypes).ToArray(),
            TimesUsed = TimesUsed,
            TimesUsedTotal = TimesUsedTotal,
        };

        public override string ToString()
        {
            string result = "";
            if (IsExport)
            {
                result += "export ";
            }
            result += this.Type.ToString();
            result += ' ';

            result += this.Identifier.Content;

            result += '(';
            if (this.ParameterTypes.Length > 0)
            {
                for (int i = 0; i < ParameterTypes.Length; i++)
                {
                    if (i > 0) result += ", ";
                    result += ParameterTypes[i].ToString();
                }
            }
            result += ')';

            result += ' ';

            result += '{';
            if (this.Statements.Length > 0)
            { result += "..."; }
            result += '}';

            return result;
        }

        public CompiledFunctionTemplateInstance InstantiateTemplate(Dictionary<string, CompiledType> typeParameters)
        {
            CompiledFunctionTemplateInstance result = new(Type, this)
            {
                CompiledAttributes = this.CompiledAttributes,
                Context = this.Context,
                Modifiers = this.Modifiers,
                ParameterTypes = new List<CompiledType>(this.ParameterTypes).ToArray(),
                TimesUsed = TimesUsed,
                TimesUsedTotal = TimesUsedTotal,
                Template = this,
            };

            Utils.SetTypeParameters(result.ParameterTypes, typeParameters);

            if (result.Type.IsGeneric)
            {
                if (!typeParameters.TryGetValue(result.Type.Name, out CompiledType typeParameter))
                { throw new NotImplementedException(); }
                result.Type = typeParameter;
            }

            return result;
        }
    }

    public class CompiledGeneralFunction : GeneralFunctionDefinition, IFunctionThing, IAmInContext<CompiledClass>, IReferenceable<KeywordCall>, IReferenceable<ConstructorCall>, IDuplicatable<CompiledGeneralFunction>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset { get; set; } = -1;

        public bool ReturnSomething => this.Type.BuiltinType != BBCode.Compiler.Type.VOID;

        public IReadOnlyList<Statement> References => references;
        readonly List<Statement> references = new();

        public override bool IsTemplate
        {
            get
            {
                if (TemplateInfo != null) return true;
                if (context != null && context.TemplateInfo != null) return true;
                return false;
            }
        }

        public CompiledType Type;

        CompiledClass context;
        public CompiledClass Context
        {
            get => context;
            set => context = value;
        }

        public CompiledGeneralFunction(CompiledType type, GeneralFunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers)
        {
            this.Type = type;

            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.FilePath = functionDefinition.FilePath;
        }
        public CompiledGeneralFunction(CompiledType type, CompiledType[] parameterTypes, GeneralFunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;

            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.FilePath = functionDefinition.FilePath;
        }

        public void AddReference(KeywordCall statement) => references.Add(statement);
        public void AddReference(ConstructorCall statement) => references.Add(statement);
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

        public bool IsSame((string name, CompiledType[] parameters) other)
        {
            if (this.Identifier.Content == other.name) return false;
            if (this.ParameterTypes.Length != other.parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            { if (this.ParameterTypes[i] != other.parameters[i]) return false; }
            return true;
        }

        public bool IsSame(IFunctionThing other)
        {
            if (other is not CompiledGeneralFunction other2) return false;
            return IsSame(other2);
        }

        public CompiledGeneralFunction Duplicate() => new(Type, ParameterTypes, this)
        {
            context = this.context,
            Modifiers = this.Modifiers,
            TimesUsed = this.TimesUsed,
            TimesUsedTotal = this.TimesUsedTotal,
        };

        public override string ToString()
        {
            string result = "";
            if (IsExport)
            {
                result += "export ";
            }
            result += this.Identifier.Content;

            result += '(';
            if (this.ParameterTypes.Length > 0)
            {
                for (int i = 0; i < ParameterTypes.Length; i++)
                {
                    if (i > 0) result += ", ";
                    result += ParameterTypes[i].ToString();
                }
            }
            result += ')';

            result += ' ';

            result += '{';
            if (this.Statements.Length > 0)
            { result += "..."; }
            result += '}';

            return result;
        }

        public CompiledGeneralFunctionTemplateInstance InstantiateTemplate(Dictionary<string, CompiledType> typeParameters)
        {
            CompiledGeneralFunctionTemplateInstance result = new(Type, ParameterTypes, this)
            {
                Modifiers = this.Modifiers,
                TimesUsed = this.TimesUsed,
                TimesUsedTotal = this.TimesUsedTotal,
                Template = this,
            };

            Utils.SetTypeParameters(result.ParameterTypes, typeParameters);

            if (result.Type.IsGeneric)
            {
                if (!typeParameters.TryGetValue(result.Type.Name, out CompiledType typeParameter))
                { throw new NotImplementedException(); }
                result.Type = typeParameter;
            }

            return result;
        }
    }

    public class CompiledFunctionTemplateInstance : CompiledFunction
    {
        public CompiledFunction Template;

        public CompiledFunctionTemplateInstance(CompiledType type, FunctionDefinition functionDefinition)
            : base(type, functionDefinition)
        { }

        public CompiledFunctionTemplateInstance(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition)
            : base(type, parameterTypes, functionDefinition)
        { }
    }

    public class CompiledGeneralFunctionTemplateInstance : CompiledGeneralFunction
    {
        public CompiledGeneralFunction Template;

        public CompiledGeneralFunctionTemplateInstance(CompiledType type, GeneralFunctionDefinition functionDefinition)
            : base(type, functionDefinition)
        { }

        public CompiledGeneralFunctionTemplateInstance(CompiledType type, CompiledType[] parameterTypes, GeneralFunctionDefinition functionDefinition)
            : base(type, parameterTypes, functionDefinition)
        { }
    }

    public class CompiledOperatorTemplateInstance : CompiledOperator
    {
        public CompiledOperator Template;

        public CompiledOperatorTemplateInstance(CompiledType type, FunctionDefinition functionDefinition)
            : base(type, functionDefinition)
        { }

        public CompiledOperatorTemplateInstance(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition)
            : base(type, parameterTypes, functionDefinition)
        { }
    }

}