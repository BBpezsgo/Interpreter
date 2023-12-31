using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LanguageCore.Parser
{
    using Tokenizing;

    public interface IInFile
    {
        public string? FilePath { get; set; }
    }

    public class ParameterDefinitionCollection :
        IPositioned,
        IReadOnlyCollection<ParameterDefinition>,
        IEquatable<ParameterDefinitionCollection>
    {
        public readonly Token LeftParenthesis;
        public readonly Token RightParenthesis;
        readonly ParameterDefinition[] Parameters;

        public Position Position => new Position(LeftParenthesis, RightParenthesis).Union(Parameters);

        public int Count => Parameters.Length;

        public ParameterDefinition this[int index] => Parameters[index];

        public ParameterDefinitionCollection(ParameterDefinitionCollection other)
        {
            this.Parameters = other.Parameters;
            this.LeftParenthesis = other.LeftParenthesis;
            this.RightParenthesis = other.RightParenthesis;
        }

        public ParameterDefinitionCollection(IEnumerable<ParameterDefinition> parameterDefinitions, Token leftParenthesis, Token rightParenthesis)
        {
            this.Parameters = parameterDefinitions.ToArray();
            this.LeftParenthesis = leftParenthesis;
            this.RightParenthesis = rightParenthesis;
        }

        public IEnumerator<ParameterDefinition> GetEnumerator() => ((IEnumerable<ParameterDefinition>)Parameters).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Parameters.GetEnumerator();

        public bool TypeEquals(ParameterDefinitionCollection? other)
        {
            if (other is null) return false;
            if (Parameters.Length != other.Parameters.Length) return false;
            for (int i = 0; i < Parameters.Length; i++)
            { if (!Parameters[i].Type.Equals(other.Parameters[i].Type)) return false; }
            return true;
        }

        public bool Equals(ParameterDefinitionCollection? other)
        {
            if (other is null) return false;
            if (Parameters.Length != other.Parameters.Length) return false;
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (!Parameters[i].Type.Equals(other.Parameters[i].Type)) return false;
                if (!Parameters[i].Identifier.Equals(other.Parameters[i].Identifier)) return false;
                if (Parameters[i].Modifiers.Length != other.Parameters[i].Modifiers.Length) return false;
                for (int j = 0; j < Parameters[i].Modifiers.Length; j++)
                {
                    if (!Parameters[i].Modifiers[j].Equals(other.Parameters[i].Modifiers[j])) return false;
                }
            }
            return true;
        }

        public ParameterDefinition[] ToArray() => Parameters;

        public override bool Equals(object? obj) => Equals(obj as ParameterDefinitionCollection);

        public override int GetHashCode() => HashCode.Combine(LeftParenthesis, RightParenthesis, Parameters);

        public static ParameterDefinitionCollection CreateAnonymous(IEnumerable<ParameterDefinition> parameterDefinitions)
            => new(parameterDefinitions, Token.CreateAnonymous("(", TokenType.Operator), Token.CreateAnonymous(")", TokenType.Operator));
    }

    public class ParameterDefinition : IPositioned
    {
        public readonly Token Identifier;
        public readonly TypeInstance Type;
        public readonly Token[] Modifiers;

        public Position Position
            => new Position(Identifier, Type).Union(Modifiers);

        public ParameterDefinition(ParameterDefinition other)
        {
            Modifiers = other.Modifiers;
            Type = other.Type;
            Identifier = other.Identifier;
        }

        public ParameterDefinition(Token[] modifiers, TypeInstance type, Token identifier)
        {
            Modifiers = modifiers;
            Type = type;
            Identifier = identifier;
        }

        public override string ToString() => $"{string.Join<Token>(", ", Modifiers)} {Type} {Identifier}".TrimStart();
    }

    public class FieldDefinition : IPositioned
    {
        public readonly Token Identifier;
        public readonly TypeInstance Type;
        public readonly Token? ProtectionToken;
        public Token? Semicolon;

        public Position Position
            => new(Identifier, Type, ProtectionToken);

        public FieldDefinition(FieldDefinition other)
        {
            Identifier = other.Identifier;
            Type = other.Type;
            ProtectionToken = other.ProtectionToken;
            Semicolon = other.Semicolon;
        }

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

    public class EnumMemberDefinition : IPositioned
    {
        public readonly Token Identifier;
        public readonly Statement.StatementWithValue? Value;

        public Position Position
            => new(Identifier, Value);

        public EnumMemberDefinition(EnumMemberDefinition other)
        {
            Identifier = other.Identifier;
            Value = other.Value;
        }

        public EnumMemberDefinition(Token identifier, Statement.StatementWithValue? value)
        {
            Identifier = identifier;
            Value = value;
        }
    }

    public class EnumDefinition : IInFile, IPositioned
    {
        public readonly Token Identifier;
        public readonly EnumMemberDefinition[] Members;
        public readonly AttributeUsage[] Attributes;

        public string? FilePath { get; set; }

        public Position Position =>
            new Position(Identifier)
            .Union(Members);

        public EnumDefinition(EnumDefinition other)
        {
            Identifier = other.Identifier;
            Attributes = other.Attributes;
            Members = other.Members;
            FilePath = other.FilePath;
        }

        public EnumDefinition(Token identifier, AttributeUsage[] attributes, EnumMemberDefinition[] members)
        {
            Identifier = identifier;
            Attributes = attributes;
            Members = members;
        }
    }

    public class TemplateInfo : IPositioned, IEquatable<TemplateInfo>
    {
        public Token Keyword;
        public Token LeftP;
        public Token[] TypeParameters;
        public Token RightP;

        public string[] TypeParameterNames => TypeParameters.Select(v => v.Content).ToArray();

        public Position Position =>
            new Position(TypeParameters)
            .Union(Keyword, LeftP, RightP);

        public TemplateInfo(TemplateInfo other)
        {
            Keyword = other.Keyword;
            LeftP = other.LeftP;
            TypeParameters = other.TypeParameters;
            RightP = other.RightP;
        }

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

        public override bool Equals(object? obj) => obj is TemplateInfo other && this.Equals(other);
        public bool Equals(TemplateInfo? other)
        {
            if (other is null) return false;
            if (this.TypeParameters.Length != other.TypeParameters.Length) return false;
            return true;
        }

        public static bool Equals(TemplateInfo? a, TemplateInfo? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return true;
            return a.Equals(b);
        }

        public override int GetHashCode() => TypeParameters.GetHashCode();
    }

    public abstract class FunctionThingDefinition :
        IExportable,
        IEquatable<FunctionThingDefinition>,
        IPositioned,
        ISimpleReadable
    {
        public Token[] Modifiers;
        public readonly Token Identifier;
        public ParameterDefinitionCollection Parameters;
        public Statement.Block? Block;

        public readonly TemplateInfo? TemplateInfo;

        /// <summary>
        /// The first parameter is labeled as 'this'
        /// </summary>
        public bool IsMethod => (Parameters.Count > 0) && Parameters[0].Modifiers.Contains("this");

        public int ParameterCount => Parameters.Count;

        public bool IsExport => Modifiers.Contains("export");

        public bool IsMacro => Modifiers.Contains("macro");

        public virtual bool IsTemplate => TemplateInfo is not null;

        public string? FilePath { get; set; }

        public virtual Position Position => new Position(Identifier)
            .Union(Parameters)
            .Union(Block)
            .Union(Modifiers);

        protected FunctionThingDefinition(FunctionThingDefinition other)
        {
            Modifiers = other.Modifiers;
            Identifier = other.Identifier;
            Parameters = other.Parameters;
            Block = other.Block;
            TemplateInfo = other.TemplateInfo;
            FilePath = other.FilePath;
        }

        protected FunctionThingDefinition(
            IEnumerable<Token> modifiers,
            Token identifier,
            ParameterDefinitionCollection parameters,
            TemplateInfo? templateInfo)
        {
            Modifiers = modifiers.ToArray();
            Identifier = identifier;
            Parameters = parameters;
            TemplateInfo = templateInfo;
        }

        public bool CanUse(string? sourceFile) => IsExport || sourceFile == null || sourceFile == FilePath;

        public string ToReadable()
        {
            StringBuilder result = new();
            result.Append(Identifier.ToString());
            result.Append('(');
            for (int j = 0; j < this.Parameters.Count; j++)
            {
                if (j > 0) result.Append(", ");
                result.Append(this.Parameters[j].Type.ToString());
            }
            result.Append(')');
            return result.ToString();
        }

        public string ToReadable(TypeArguments? typeArguments)
        {
            if (typeArguments == null) return ToReadable();
            StringBuilder result = new();
            result.Append(this.Identifier.ToString());

            result.Append('(');
            for (int j = 0; j < this.Parameters.Count; j++)
            {
                if (j > 0) { result.Append(", "); }
                result.Append(this.Parameters[j].Type.ToString(typeArguments));
            }
            result.Append(')');
            return result.ToString();
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

            if (!this.Parameters.TypeEquals(other.Parameters)) return false;

            if (this.Modifiers.Length != other.Modifiers.Length) return false;
            for (int i = 0; i < this.Modifiers.Length; i++)
            {
                if (!string.Equals(this.Modifiers[i].Content, other.Modifiers[i].Content)) return false;
            }

            if (!TemplateInfo.Equals(this.TemplateInfo, other.TemplateInfo))
            { return false; }

            return true;
        }

        public override int GetHashCode() => HashCode.Combine(
            Parameters,
            Block,
            Modifiers,
            FilePath,
            TemplateInfo,
            Identifier);
    }

    public class MacroDefinition :
        IExportable,
        IEquatable<MacroDefinition>,
        ISimpleReadable
    {
        public readonly Token Keyword;
        public readonly Token[] Modifiers;
        public readonly Token Identifier;
        public readonly Token[] Parameters;
        public readonly Statement.Block Block;

        public int ParameterCount => Parameters.Length;

        public bool IsExport => Modifiers.Contains("export");

        public string? FilePath { get; set; }

        public MacroDefinition(MacroDefinition other)
        {
            Keyword = other.Keyword;
            Modifiers = other.Modifiers;
            Identifier = other.Identifier;
            Parameters = other.Parameters;
            Block = other.Block;
            FilePath = other.FilePath;
        }

        public MacroDefinition(IEnumerable<Token> modifiers, Token keyword, Token identifier, IEnumerable<Token> parameters, Statement.Block block)
        {
            Keyword = keyword;
            Identifier = identifier;

            Parameters = parameters.ToArray();
            Block = block;
            Modifiers = modifiers.ToArray();
        }

        public bool CanUse(string sourceFile) => IsExport || sourceFile == null || sourceFile == FilePath;

        public string ToReadable()
        {
            StringBuilder result = new();
            result.Append(Identifier.ToString());
            result.Append('(');
            for (int j = 0; j < Parameters.Length; j++)
            {
                if (j > 0) { result.Append(", "); }
                result.Append("any"); // this.Parameters[j].ToString();
            }
            result.Append(')');
            return result.ToString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is not MacroDefinition other) return false;
            return Equals(other);
        }

        public bool Equals(MacroDefinition? other)
        {
            if (other is null) return false;
            if (!string.Equals(this.Identifier.Content, other.Identifier.Content, StringComparison.Ordinal)) return false;

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

            if (!string.Equals(a.Identifier.Content, b.Identifier.Content, StringComparison.Ordinal)) return false;

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

    public class AttributeUsage : IPositioned
    {
        public readonly Token Identifier;
        public readonly Statement.Literal[] Parameters;

        public Position Position => new Position(Parameters).Union(Identifier);

        public AttributeUsage(Token identifier, Statement.Literal[] parameters)
        {
            Identifier = identifier;
            Parameters = parameters;
        }
    }

    public class FunctionDefinition : FunctionThingDefinition
    {
        public readonly AttributeUsage[] Attributes;
        public readonly TypeInstance Type;

        public override Position Position => base.Position.Union(Type);

        public FunctionDefinition(FunctionDefinition other) : base(other)
        {
            Attributes = other.Attributes;
            Type = other.Type;
        }

        public FunctionDefinition(
            IEnumerable<AttributeUsage> attributes,
            IEnumerable<Token> modifiers,
            TypeInstance type,
            Token identifier,
            ParameterDefinitionCollection parameters,
            TemplateInfo? templateInfo)
            : base(modifiers, identifier, parameters, templateInfo)
        {
            Attributes = attributes.ToArray();
            Type = type;
        }

        public override string ToString()
        {
            StringBuilder result = new();
            if (IsExport)
            { result.Append("export "); }

            result.Append(Type.ToString());
            result.Append(' ');

            result.Append(Identifier.Content);

            result.Append('(');
            if (Parameters.Count > 0)
            {
                for (int i = 0; i < Parameters.Count; i++)
                {
                    if (i > 0) result.Append(", ");
                    result.Append(Parameters[i].Type.ToString());
                }
            }
            result.Append(')');

            result.Append(Block?.ToString() ?? ";");

            return result.ToString();
        }

        public bool IsSame(FunctionDefinition other)
        {
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (!this.Parameters.TypeEquals(other.Parameters)) return false;
            return true;
        }

        public AttributeUsage? GetAttribute(string identifier)
        {
            for (int i = 0; i < Attributes.Length; i++)
            {
                if (Attributes[i].Identifier.Content == identifier)
                { return Attributes[i]; }
            }
            return null;
        }
    }

    public class GeneralFunctionDefinition : FunctionThingDefinition
    {
        public GeneralFunctionDefinition(GeneralFunctionDefinition other) : base(other)
        {

        }

        public GeneralFunctionDefinition(
            Token identifier,
            IEnumerable<Token> modifiers,
            ParameterDefinitionCollection parameters)
            : base(modifiers, identifier, parameters, null)
        { }

        public override string ToString()
        {
            StringBuilder result = new();
            if (IsExport)
            { result.Append("export "); }
            result.Append(Identifier.Content);

            result.Append('(');
            if (Parameters.Count > 0)
            {
                for (int i = 0; i < Parameters.Count; i++)
                {
                    if (i > 0) result.Append(", ");
                    result.Append(Parameters[i].Type);
                }
            }
            result.Append(')');

            result.Append(Block?.ToString() ?? ";");

            return result.ToString();
        }
    }

    public class ClassDefinition : IExportable, IInFile, IPositioned
    {
        public readonly AttributeUsage[] Attributes;
        public readonly Token Name;
        public readonly Token BracketStart;
        public readonly Token BracketEnd;
        public readonly List<Statement.Statement> Statements;
        public string? FilePath { get; set; }
        public readonly FieldDefinition[] Fields;
        public Token[] Modifiers;
        public TemplateInfo? TemplateInfo;

        public IReadOnlyList<FunctionDefinition> Methods => methods;
        public IReadOnlyList<GeneralFunctionDefinition> GeneralMethods => generalMethods;
        public IReadOnlyList<FunctionDefinition> Operators => operators;

        public bool IsExport => Modifiers.Contains("export");

        readonly FunctionDefinition[] methods;
        readonly GeneralFunctionDefinition[] generalMethods;
        readonly FunctionDefinition[] operators;

        public ClassDefinition(ClassDefinition other)
        {
            Attributes = other.Attributes;
            Name = other.Name;
            BracketStart = other.BracketStart;
            BracketEnd = other.BracketEnd;
            Statements = other.Statements;
            FilePath = other.FilePath;
            Fields = other.Fields;
            Modifiers = other.Modifiers;
            TemplateInfo = other.TemplateInfo;
            methods = other.methods;
            generalMethods = other.generalMethods;
            operators = other.operators;
        }

        public ClassDefinition(
            Token name,
            Token bracketStart,
            Token bracketEnd,
            IEnumerable<AttributeUsage> attributes,
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
            StringBuilder result = new();
            result.Append("class ");

            result.Append(Name.Content);
            if (TemplateInfo is not null)
            {
                result.Append('<');
                result.Append(string.Join<Token>(", ", TemplateInfo.TypeParameters));
                result.Append('>');
            }
            result.Append(' ');
            result.Append("{...}");
            return result.ToString();
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

    public class StructDefinition : IExportable, IInFile, IPositioned
    {
        public readonly AttributeUsage[] Attributes;
        public readonly Token Name;
        public readonly Token BracketStart;
        public readonly Token BracketEnd;
        public readonly List<Statement.Statement> Statements;
        public readonly Token[] Modifiers;

        public string? FilePath { get; set; }
        public readonly FieldDefinition[] Fields;

        public bool IsExport => Modifiers.Contains("export");

        public IReadOnlyCollection<FunctionDefinition> Methods => methods;
        readonly FunctionDefinition[] methods;

        public virtual Position Position => new(Name, BracketStart, BracketEnd);

        public StructDefinition(StructDefinition other)
        {
            Attributes = other.Attributes;
            Name = other.Name;
            BracketStart = other.BracketStart;
            BracketEnd = other.BracketEnd;
            Statements = other.Statements;
            Modifiers = other.Modifiers;
            FilePath = other.FilePath;
            Fields = other.Fields;
            methods = other.methods;
        }

        public StructDefinition(
            Token name,
            Token bracketStart,
            Token bracketEnd,
            IEnumerable<AttributeUsage> attributes,
            IEnumerable<FieldDefinition> fields,
            IEnumerable<FunctionDefinition> methods,
            IEnumerable<Token> modifiers)
        {
            this.Name = name;
            this.BracketStart = bracketStart;
            this.BracketEnd = bracketEnd;
            this.Fields = fields.ToArray();
            this.methods = methods.ToArray();
            this.Attributes = attributes.ToArray();
            this.Statements = new List<Statement.Statement>();
            this.Modifiers = modifiers.ToArray();
        }

        public override string ToString() => $"struct {this.Name.Content} {{...}}";

        public bool CanUse(string sourceFile) => IsExport || sourceFile == FilePath;
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
                StringBuilder result = new();
                for (int i = 0; i < Path.Length; i++)
                {
                    if (i > 0) result.Append('.');
                    result.Append(Path[i].Content);
                }
                return result.ToString();
            }
        }
        public bool IsUrl => Path.Length == 1 && Uri.TryCreate(Path[0].Content, UriKind.Absolute, out Uri? uri) && uri.Scheme != "file:";

        public UsingDefinition(
            Token keyword,
            Token[] path)
        {
            Path = path;
            Keyword = keyword;
            CompiledUri = null;
            DownloadTime = null;
        }

        public static UsingDefinition CreateAnonymous(params string[] path)
        {
            Token[] pathTokens = new Token[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                pathTokens[i] = Token.CreateAnonymous(path[i]);
            }
            return new UsingDefinition(Token.CreateAnonymous("using"), pathTokens);
        }

        public static UsingDefinition CreateAnonymous(Uri uri)
        {
            return new UsingDefinition(Token.CreateAnonymous("using"), new Token[]
            {
                Token.CreateAnonymous(uri.ToString())
            });
        }
    }
}
