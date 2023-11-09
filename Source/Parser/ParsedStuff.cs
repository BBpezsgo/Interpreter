using System;
using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.Parser
{
    using BBCode.Compiler;
    using Tokenizing;

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

        public Position Position
            => new Position(Identifier, Type).Union(Modifiers);

        public override string ToString() => $"{string.Join<Token>(", ", Modifiers)} {Type} {Identifier}".TrimStart();
    }

    public class FieldDefinition : IThingWithPosition
    {
        public readonly Token Identifier;
        public readonly TypeInstance Type;
        public readonly Token? ProtectionToken;
        public Token? Semicolon;

        public Position Position
            => new(Identifier, Type, ProtectionToken);

        public FieldDefinition(Token identifier, TypeInstance type, Token? protectionToken)
        {
            Identifier = identifier;
            Type = type;
            ProtectionToken = protectionToken;
        }

        public override string ToString() => $"{(ProtectionToken is not null ? ProtectionToken.Content + " " : "")}{Type} {Identifier}";
    }

    public interface IExportable
    {
        public bool IsExport { get; }
    }

    public class EnumMemberDefinition : IHaveKey<string>, IThingWithPosition
    {
        public readonly Token Identifier;
        public readonly Statement.StatementWithValue? Value;

        public string Key => Identifier.Content;

        public EnumMemberDefinition(Token identifier, Statement.StatementWithValue? value)
        {
            Identifier = identifier;
            Value = value;
        }

        public Position Position
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

        public Position Position
        {
            get
            {
                Position result = new(Identifier);
                result.Union(Members);
                return result;
            }
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

        public Position Position
        {
            get
            {
                Position result = new(TypeParameters);
                result.Union(Keyword, LeftP, RightP);
                return result;
            }
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

        public virtual Position Position
        {
            get
            {
                Position result = new(Identifier);
                result.Union(Parameters);
                result.Union(Block);
                result.Union(Modifiers);
                return result;
            }
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

            public Position Position => new(Identifier);
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

        public bool IsSame(FunctionDefinition other)
        {
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.Parameters.Length != other.Parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (!this.Parameters[i].Type.Equals(other.Parameters[i])) return false;
            }
            return true;
        }

        public Attribute? GetAttribute(string identifier)
        {
            for (int i = 0; i < Attributes.Length; i++)
            {
                if (Attributes[i].Identifier.Content == identifier)
                { return Attributes[i]; }
            }
            return null;
        }

        public override Position Position => base.Position.Union(Type);
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

        public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;

        public virtual Position Position
        {
            get
            {
                Position result = new(Name);
                result.Union(BracketStart);
                result.Union(BracketEnd);
                return result;
            }
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

        public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;

        public virtual Position Position
        {
            get
            {
                Position result = new(Name);
                result.Union(BracketStart);
                result.Union(BracketEnd);
                return result;
            }
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
