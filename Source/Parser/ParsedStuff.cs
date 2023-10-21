using System;
using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.Parser
{
    using LanguageCore.BBCode.Compiler;
    using LanguageCore.Tokenizing;
    using Statement;

    public enum TypeInstanceKind
    {
        Simple,
        Template,
        Function,
        StackArray,
    }

    public class TypeInstance : IEquatable<TypeInstance>, IThingWithPosition
    {
        public readonly Token Identifier;
        public readonly TypeInstanceKind Kind;

        public readonly List<TypeInstance> GenericTypes;
        public readonly List<TypeInstance> ParameterTypes;
        public readonly StatementWithValue? StackArraySize;

        public TypeInstance(Token identifier, TypeInstanceKind kind) : base()
        {
            this.Identifier = identifier;
            this.Kind = kind;

            this.GenericTypes = new List<TypeInstance>();
            this.ParameterTypes = new List<TypeInstance>();
            this.StackArraySize = null;
        }

        public TypeInstance(Token identifier, TypeInstanceKind kind, StatementWithValue? sizeValue) : base()
        {
            this.Identifier = identifier;
            this.Kind = kind;

            this.GenericTypes = new List<TypeInstance>();
            this.ParameterTypes = new List<TypeInstance>();
            this.StackArraySize = sizeValue;
        }

        public Position Position
        {
            get
            {
                Position result = Identifier.GetPosition();
                result.Extend(GenericTypes);
                return result;
            }
        }
        public Position GetPosition() => Position;

        public static TypeInstance CreateAnonymous(LiteralType literalType, Func<string, string?>? typeDefinitionReplacer)
            => TypeInstance.CreateAnonymous(literalType.ToStringRepresentation(), typeDefinitionReplacer);
        public static TypeInstance CreateAnonymous(string name, Func<string, string?>? typeDefinitionReplacer)
        {
            string? definedType = typeDefinitionReplacer?.Invoke(name);
            if (definedType == null)
            { return new TypeInstance(Token.CreateAnonymous(name), TypeInstanceKind.Simple); }
            else
            { return new TypeInstance(Token.CreateAnonymous(definedType), TypeInstanceKind.Simple); }
        }

        public override string ToString()
        {
            string result = this.Identifier.Content;

            if (GenericTypes != null && GenericTypes.Count > 0)
            { result += $"<{string.Join(", ", GenericTypes)}>"; }

            if (StackArraySize != null)
            { result += $"[{StackArraySize}]"; }

            return result;
        }

        public static bool operator ==(TypeInstance? a, string? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            if (a.Kind != TypeInstanceKind.Simple) return false;

            return a.Identifier.Content == b;
        }
        public static bool operator !=(TypeInstance? a, string? b) => !(a == b);

        public static bool operator ==(string? a, TypeInstance? b) => b == a;
        public static bool operator !=(string? a, TypeInstance? b) => !(b == a);

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is null) return false;
            if (obj is not TypeInstance other) return false;
            return this.Equals(other);
        }

        public bool Equals(TypeInstance? other)
        {
            if (other is null) return false;
            if (!Identifier.Equals(other.Identifier)) return false;
            if (Kind != other.Kind) return false;
            if (GenericTypes.Count != other.GenericTypes.Count) return false;
            for (int i = 0; i < GenericTypes.Count; i++)
            { if (!GenericTypes[i].Equals(other.GenericTypes[i])) return false; }
            if (this.StackArraySize is null != other.StackArraySize is null) return false;
            return true;
        }

        public override int GetHashCode() => HashCode.Combine(Identifier);

        static bool TryGetAnalyzedType(CompiledType type, out TokenAnalysedType analyzedType)
        {
            analyzedType = default;
            if (type.IsClass)
            {
                analyzedType = TokenAnalysedType.Class;
                return true;
            }

            if (type.IsStruct)
            {
                analyzedType = TokenAnalysedType.Struct;
                return true;
            }

            if (type.IsGeneric)
            {
                analyzedType = TokenAnalysedType.TypeParameter;
                return true;
            }

            if (type.IsBuiltin)
            {
                analyzedType = TokenAnalysedType.BuiltinType;
                return true;
            }

            if (type.IsFunction)
            {
                return TryGetAnalyzedType(type.Function.ReturnType, out analyzedType);
            }

            if (type.IsEnum)
            {
                analyzedType = TokenAnalysedType.Enum;
                return true;
            }

            return false;
        }

        public void SetAnalyzedType(CompiledType type)
        {
            if (TryGetAnalyzedType(type, out var analyzedType))
            { this.Identifier.AnalyzedType = analyzedType; }

            if (type.IsFunction &&
                this.Kind == TypeInstanceKind.Function &&
                this.ParameterTypes.Count == type.Function.Parameters.Length)
            {
                for (int i = 0; i < type.Function.Parameters.Length; i++)
                {
                    this.ParameterTypes[i].SetAnalyzedType(type.Function.Parameters[i]);
                }
            }

            if (type.IsStackArray &&
                this.Kind == TypeInstanceKind.StackArray)
            {
                if (TryGetAnalyzedType(type.StackArrayOf, out analyzedType))
                { this.Identifier.AnalyzedType = analyzedType; }
            }
        }
    }
}

namespace LanguageCore.Parser
{
    using LanguageCore.BBCode.Compiler;
    using LanguageCore.Tokenizing;

    public interface IDefinition
    {
        public string? FilePath { get; set; }
    }

    public class ParameterDefinition : IHaveKey<string>, IThingWithPosition
    {
        public readonly Token Identifier;
        public readonly TypeInstance Type;
        public readonly Token[] Modifiers;

        public string Key => Identifier.Content;

        public ParameterDefinition(Token[] modifiers, TypeInstance type, Token identifier)
        {
            Modifiers = modifiers;
            Type = type;
            Identifier = identifier;
        }

        public Position GetPosition()
            => new Position(Identifier, Type).Extend(Modifiers);

        public override string ToString() => $"{string.Join<Token>(", ", Modifiers)} {Type} {Identifier}".TrimStart();
        internal string PrettyPrint() => $"{string.Join<Token>(", ", Modifiers)} {Type} {Identifier}".TrimStart();
    }

    public class FieldDefinition : IThingWithPosition
    {
        public readonly Token Identifier;
        public readonly TypeInstance Type;
        public readonly Token? ProtectionToken;
        public Token? Semicolon;

        public Position GetPosition()
            => new(Identifier, Type, ProtectionToken);

        public FieldDefinition(Token identifier, TypeInstance type, Token? protectionToken)
        {
            Identifier = identifier;
            Type = type;
            ProtectionToken = protectionToken;
        }

        public override string ToString() => $"{(ProtectionToken is not null ? ProtectionToken.Content + " " : "")}{Type} {Identifier}";
        internal string PrettyPrint(int ident = 0) => $"{" ".Repeat(ident)}{(ProtectionToken is not null ? ProtectionToken.Content + " " : "")}{Type} {Identifier}";
    }

    public interface IExportable
    {
        public bool IsExport { get; }
    }

    public class EnumMemberDefinition : IHaveKey<string>, IThingWithPosition
    {
        public readonly Token Identifier;
        public readonly Statement.Literal? Value;

        public string Key => Identifier.Content;

        public EnumMemberDefinition(Token identifier, Statement.Literal? value)
        {
            Identifier = identifier;
            Value = value;
        }

        public Position GetPosition()
            => new(Identifier, Value);
    }

    public class EnumDefinition : IDefinition, IHaveKey<string>, IThingWithPosition
    {
        public string? FilePath { get; set; }

        public string Key => Identifier.Content;

        public readonly Token Identifier;
        public readonly EnumMemberDefinition[] Members;
        public readonly FunctionDefinition.Attribute[] Attributes;

        public EnumDefinition(Token identifier, FunctionDefinition.Attribute[] attributes, EnumMemberDefinition[] members)
        {
            Identifier = identifier;
            Attributes = attributes;
            Members = members;
        }

        public Position GetPosition()
        {
            Position result = new(Identifier);
            result.Extend(Members);
            return result;
        }
    }

    public class TemplateInfo : IThingWithPosition, IEquatable<TemplateInfo>
    {
        public Token Keyword;
        public Token LeftP;
        public Token[] TypeParameters;
        public Token RightP;

        public string[] TypeParameterNames => TypeParameters.Select(v => v.Content).ToArray();

        public TemplateInfo(Token keyword, Token leftP, IEnumerable<Token> typeParameters, Token rightP)
        {
            Keyword = keyword;
            LeftP = leftP;
            TypeParameters = typeParameters.ToArray();
            RightP = rightP;
        }

        public Dictionary<string, Token> ToDictionary()
        {
            Dictionary<string, Token> result = new();
            for (int i = 0; i < TypeParameters.Length; i++)
            {
                result.Add(TypeParameters[i].Content, TypeParameters[i]);
            }
            return result;
        }

        public Dictionary<string, T> ToDictionary<T>(T[] typeArgumentValues)
        {
            Dictionary<string, T> result = new();

            if (TypeParameters.Length != typeArgumentValues.Length)
            { throw new NotImplementedException(); }

            for (int i = 0; i < TypeParameters.Length; i++)
            {
                result.Add(TypeParameters[i].Content, typeArgumentValues[i]);
            }
            return result;
        }

        public Position GetPosition()
        {
            Position result = new(TypeParameters);
            result.Extend(Keyword, LeftP, RightP);
            return result;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not TemplateInfo other) return false;
            return this.Equals(other);
        }

        public bool Equals(TemplateInfo? other)
        {
            if (other is null) return false;
            if (this.TypeParameters.Length != other.TypeParameters.Length) return false;
            return true;
        }

        public override int GetHashCode() => TypeParameters.GetHashCode();

        public static bool operator ==(TemplateInfo? a, TemplateInfo? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            return a.Equals(b);
        }

        public static bool operator !=(TemplateInfo? a, TemplateInfo? b) => !(a == b);
    }

    public abstract class FunctionThingDefinition : IExportable, IEquatable<FunctionThingDefinition>, IThingWithPosition
    {
        public ParameterDefinition[] Parameters;
        public Token[] Modifiers;
        public Statement.Block? Block;

        /// <summary>
        /// The first parameter is labeled as 'this'
        /// </summary>
        public bool IsMethod => (Parameters.Length > 0) && Parameters[0].Modifiers.Contains("this");

        public int ParameterCount => Parameters.Length;

        public bool IsExport => Modifiers.Contains("export");

        public bool IsMacro => Modifiers.Contains("macro");

        public string? FilePath { get; set; }


        public readonly TemplateInfo? TemplateInfo;

        protected FunctionThingDefinition(Token identifier, TemplateInfo? templateInfo, IEnumerable<Token> modifiers)
        {
            Identifier = identifier;
            TemplateInfo = templateInfo;

            Parameters = Array.Empty<ParameterDefinition>();
            Modifiers = modifiers.ToArray();
        }

        public virtual bool IsTemplate => TemplateInfo is not null;

        public readonly Token Identifier;

        public bool CanUse(string? sourceFile) => IsExport || sourceFile == null || sourceFile == FilePath;

        public string ReadableID()
        {
            string result = this.Identifier.ToString();
            result += "(";
            for (int j = 0; j < this.Parameters.Length; j++)
            {
                if (j > 0) { result += ", "; }
                result += this.Parameters[j].Type.ToString();
            }
            result += ")";
            return result;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not FunctionThingDefinition other) return false;
            return Equals(other);
        }

        public bool Equals(FunctionThingDefinition? other)
        {
            if (other is null) return false;
            if (!string.Equals(this.Identifier.Content, other.Identifier.Content)) return false;

            if (this.Parameters.Length != other.Parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            { if (!this.Parameters[i].Type.Equals(other.Parameters[i].Type)) return false; }

            if (this.Modifiers.Length != other.Modifiers.Length) return false;
            for (int i = 0; i < this.Modifiers.Length; i++)
            { if (this.Modifiers[i].Content != other.Modifiers[i].Content) return false; }

            if (this.TemplateInfo is null != other.TemplateInfo is null) return false;
            if (this.TemplateInfo is null) return false;
            if (!this.TemplateInfo.Equals(other.TemplateInfo)) return false;

            return true;
        }

        public override int GetHashCode() => HashCode.Combine(
            Parameters,
            Block,
            Modifiers,
            FilePath,
            TemplateInfo,
            Identifier);

        public static bool operator ==(FunctionThingDefinition? a, FunctionThingDefinition? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            if (!string.Equals(a.Identifier.Content, b.Identifier.Content)) return false;

            if (a.Parameters.Length != b.Parameters.Length) return false;
            for (int i = 0; i < a.Parameters.Length; i++)
            { if (!a.Parameters[i].Type.Equals(b.Parameters[i].Type)) return false; }

            return true;
        }
        public static bool operator !=(FunctionThingDefinition? a, FunctionThingDefinition? b) => !(a == b);

        public virtual Position GetPosition()
        {
            Position result = new(Identifier);
            result.Extend(Parameters);
            result.Extend(Block);
            result.Extend(Modifiers);
            return result;
        }
    }

    public class MacroDefinition : IExportable, IEquatable<MacroDefinition>
    {
        public readonly Token Keyword;
        public readonly Token[] Parameters;
        public readonly Token[] Modifiers;

        public readonly Statement.Block Block;

        public int ParameterCount => Parameters.Length;

        public bool IsExport => Modifiers.Contains("export");

        public string? FilePath { get; set; }

        public MacroDefinition(IEnumerable<Token> modifiers, Token keyword, Token identifier, IEnumerable<Token> parameters, Statement.Block block)
        {
            Keyword = keyword;
            Identifier = identifier;

            Parameters = parameters.ToArray();
            Block = block;
            Modifiers = modifiers.ToArray();
        }


        public readonly Token Identifier;

        public bool CanUse(string sourceFile) => IsExport || sourceFile == null || sourceFile == FilePath;

        public string ReadableID()
        {
            string result = this.Identifier.ToString();
            result += "(";
            for (int j = 0; j < this.Parameters.Length; j++)
            {
                if (j > 0) { result += ", "; }
                result += "any"; // this.Parameters[j].ToString();
            }
            result += ")";
            return result;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not MacroDefinition other) return false;
            return Equals(other);
        }

        public bool Equals(MacroDefinition? other)
        {
            if (other is null) return false;
            if (!string.Equals(this.Identifier.Content, other.Identifier.Content)) return false;

            if (this.Parameters.Length != other.Parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            { if (!this.Parameters[i].Equals(other.Parameters[i])) return false; }

            if (this.Modifiers.Length != other.Modifiers.Length) return false;
            for (int i = 0; i < this.Modifiers.Length; i++)
            { if (this.Modifiers[i].Content != other.Modifiers[i].Content) return false; }

            return true;
        }

        public override int GetHashCode() => HashCode.Combine(
            Parameters,
            Block,
            Modifiers,
            FilePath,
            Identifier);

        public static bool operator ==(MacroDefinition a, MacroDefinition b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            if (!string.Equals(a.Identifier.Content, b.Identifier.Content)) return false;

            if (a.Parameters.Length != b.Parameters.Length) return false;
            for (int i = 0; i < a.Parameters.Length; i++)
            { if (!a.Parameters[i].Equals(b.Parameters[i])) return false; }

            return true;
        }
        public static bool operator !=(MacroDefinition a, MacroDefinition b) => !(a == b);

        public bool IsSame(MacroDefinition other)
        {
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.Parameters.Length != other.Parameters.Length) return false;
            return true;
        }
    }

    public class FunctionDefinition : FunctionThingDefinition
    {
        public class Attribute : IHaveKey<string>, IThingWithPosition
        {
            public readonly Token Identifier;
            public readonly object[] Parameters;

            public string Key => Identifier.Content;

            public Attribute(Token identifier, object[] parameters)
            {
                Identifier = identifier;
                Parameters = parameters;
            }

            public Position GetPosition()
                => new(Identifier);
        }

        public Attribute[] Attributes;

        public readonly TypeInstance Type;

        public FunctionDefinition(
            IEnumerable<Token> modifiers,
            TypeInstance type,
            Token identifier,
            TemplateInfo? templateInfo)
            : base(identifier, templateInfo, modifiers)
        {
            Type = type;
            Attributes = Array.Empty<Attribute>();
        }

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
            if (this.Parameters.Length > 0)
            {
                for (int i = 0; i < Parameters.Length; i++)
                {
                    if (i > 0) result += ", ";
                    result += Parameters[i].Type.ToString();
                }
            }
            result += ')';

            result += Block?.ToString() ?? ";";


            return result;
        }

        public string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var parameter in this.Parameters)
            {
                parameters.Add(parameter.PrettyPrint());
            }

            throw new NotImplementedException();
        }

        public bool IsSame(FunctionDefinition other)
        {
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.Parameters.Length != other.Parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (this.Parameters[i].Type.Identifier.Content != other.Parameters[i].Type.Identifier.Content) return false;
            }
            return true;
        }

        public override Position GetPosition() => base.GetPosition().Extend(Type);
    }

    public class GeneralFunctionDefinition : FunctionThingDefinition
    {
        public GeneralFunctionDefinition(
            Token identifier,
            IEnumerable<Token> modifiers)
            : base(identifier, null, modifiers)
        { }

        public override string ToString()
        {
            string result = "";
            if (IsExport)
            {
                result += "export ";
            }
            result += this.Identifier.Content;

            result += '(';
            if (this.Parameters.Length > 0)
            {
                for (int i = 0; i < Parameters.Length; i++)
                {
                    if (i > 0) result += ", ";
                    result += Parameters[i].Type;
                }
            }
            result += ')';

            result += Block?.ToString() ?? ";";

            return result;
        }

        public string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var parameter in this.Parameters)
            {
                parameters.Add(parameter.PrettyPrint());
            }

            throw new NotImplementedException();
        }
    }

    public class ClassDefinition : IExportable, IDefinition, IHaveKey<string>, IThingWithPosition
    {
        public readonly FunctionDefinition.Attribute[] Attributes;
        public readonly Token Name;
        public readonly Token BracketStart;
        public readonly Token BracketEnd;
        public readonly List<Statement.Statement> Statements;
        public string? FilePath { get; set; }
        public readonly FieldDefinition[] Fields;
        public Token[] Modifiers;
        public TemplateInfo? TemplateInfo;

        public string Key => Name.Content;

        public IReadOnlyList<FunctionDefinition> Methods => methods;
        public IReadOnlyList<GeneralFunctionDefinition> GeneralMethods => generalMethods;
        public IReadOnlyList<FunctionDefinition> Operators => operators;

        public bool IsExport => Modifiers.Contains("export");

        readonly FunctionDefinition[] methods;
        readonly GeneralFunctionDefinition[] generalMethods;
        readonly FunctionDefinition[] operators;

        public ClassDefinition(
            Token name,
            Token bracketStart,
            Token bracketEnd,
            IEnumerable<FunctionDefinition.Attribute> attributes,
            IEnumerable<Token> modifiers,
            IEnumerable<FieldDefinition> fields,
            IEnumerable<FunctionDefinition> methods,
            IEnumerable<GeneralFunctionDefinition> generalMethods,
            IEnumerable<FunctionDefinition> operators)
        {
            this.Name = name;
            this.BracketStart = bracketStart;
            this.BracketEnd = bracketEnd;
            this.Fields = fields.ToArray();
            this.methods = methods.ToArray();
            this.generalMethods = generalMethods.ToArray();
            this.Attributes = attributes.ToArray();
            this.Statements = new List<Statement.Statement>();
            this.operators = operators.ToArray();
            this.Modifiers = modifiers.ToArray();
        }

        public override string ToString()
        {
            string result = $"class";
            result += ' ';
            result += $"{this.Name.Content}";
            if (this.TemplateInfo is not null)
            {
                result += '<';
                result += string.Join<Token>(", ", this.TemplateInfo.TypeParameters);
                result += '>';
            }
            result += ' ';
            result += "{...}";
            return result;
        }

        public string PrettyPrint(int ident = 0)
        {
            List<string> fields = new();
            foreach (var field in this.Fields)
            {
                fields.Add($"{" ".Repeat(ident)}" + field.PrettyPrint((ident == 0) ? 2 : ident) + ";");
            }

            List<string> methods = new();

            foreach (var generalMethod in this.generalMethods)
            {
                methods.Add($"{" ".Repeat(ident)}" + generalMethod.PrettyPrint((ident == 0) ? 2 : ident));
            }

            foreach (var method in this.methods)
            {
                methods.Add($"{" ".Repeat(ident)}" + method.PrettyPrint((ident == 0) ? 2 : ident));
            }

            return $"{" ".Repeat(ident)}class {this.Name.Content} " + $"{{\n{string.Join("\n", fields)}\n\n{string.Join("\n", methods)}\n{" ".Repeat(ident)}}}";
        }

        public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;

        public virtual Position GetPosition()
        {
            Position result = new(Name);
            result.Extend(BracketStart);
            result.Extend(BracketEnd);
            return result;
        }
    }

    public class StructDefinition : IExportable, IDefinition, IHaveKey<string>, IThingWithPosition
    {
        public readonly FunctionDefinition.Attribute[] Attributes;
        public readonly Token Name;
        public readonly Token BracketStart;
        public readonly Token BracketEnd;
        public readonly List<Statement.Statement> Statements;
        public readonly Token[] Modifiers;

        public string? FilePath { get; set; }
        public readonly FieldDefinition[] Fields;

        public string Key => Name.Content;

        public bool IsExport => Modifiers.Contains("export");

        public IReadOnlyDictionary<string, FunctionDefinition> Methods => methods;
        readonly Dictionary<string, FunctionDefinition> methods;

        public StructDefinition(
            Token name,
            Token bracketStart,
            Token bracketEnd,
            IEnumerable<FunctionDefinition.Attribute> attributes,
            IEnumerable<FieldDefinition> fields,
            IEnumerable<KeyValuePair<string, FunctionDefinition>> methods,
            IEnumerable<Token> modifiers)
        {
            this.Name = name;
            this.BracketStart = bracketStart;
            this.BracketEnd = bracketEnd;
            this.Fields = fields.ToArray();
            this.methods = new Dictionary<string, FunctionDefinition>(methods);
            this.Attributes = attributes.ToArray();
            this.Statements = new List<Statement.Statement>();
            this.Modifiers = modifiers.ToArray();
        }

        public override string ToString()
        {
            return $"struct {this.Name.Content} " + "{...}";
        }

        public string PrettyPrint(int ident = 0)
        {
            List<string> fields = new();
            foreach (var field in this.Fields)
            {
                fields.Add($"{" ".Repeat(ident)}" + field.PrettyPrint((ident == 0) ? 2 : ident) + ";");
            }

            List<string> methods = new();
            foreach (var method in this.methods)
            {
                methods.Add($"{" ".Repeat(ident)}" + method.Value.PrettyPrint((ident == 0) ? 2 : ident) + ";");
            }

            return $"{" ".Repeat(ident)}struct {this.Name.Content} " + $"{{\n{string.Join("\n", fields)}\n\n{string.Join("\n", methods)}\n{" ".Repeat(ident)}}}";
        }

        public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;

        public virtual Position GetPosition()
        {
            Position result = new(Name);
            result.Extend(BracketStart);
            result.Extend(BracketEnd);
            return result;
        }
    }

    public class UsingDefinition
    {
        public readonly Token[] Path;
        public readonly Token Keyword;
        /// <summary> Set by the Compiler </summary>
        public string? CompiledUri;
        /// <summary> Set by the Compiler </summary>
        public double? DownloadTime;

        public string PathString
        {
            get
            {
                string result = "";
                for (int i = 0; i < Path.Length; i++)
                {
                    if (i > 0) result += ".";
                    result += Path[i].Content;
                }
                return result;
            }
        }
        public bool IsUrl => Path.Length == 1 && Uri.TryCreate(Path[0].Content, UriKind.Absolute, out var uri) && uri.Scheme != "file:";

        public UsingDefinition(
            Token keyword,
            Token[] path)
        {
            Path = path;
            Keyword = keyword;
            CompiledUri = null;
            DownloadTime = null;
        }
    }
}
