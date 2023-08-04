﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgrammingLanguage.BBCode
{
    using Core;

    using Parser;

    public class TypeInstance : IEquatable<TypeInstance>, IThingWithPosition
    {
        public Token Identifier;
        public List<TypeInstance> GenericTypes;

        public TypeInstance(Token identifier) : base()
        {
            this.Identifier = identifier;
            this.GenericTypes = new List<TypeInstance>();
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

        public static TypeInstance CreateAnonymous(LiteralType literalType, Func<string, string> typeDefinitionReplacer)
            => TypeInstance.CreateAnonymous(literalType.ToStringRepresentation(), typeDefinitionReplacer);
        public static TypeInstance CreateAnonymous(string name, Func<string, string> typeDefinitionReplacer)
        {
            string definedType = typeDefinitionReplacer?.Invoke(name);
            if (definedType == null)
            { return new TypeInstance(Token.CreateAnonymous(name)); }
            else
            { return new TypeInstance(Token.CreateAnonymous(definedType)); }
        }
        public static TypeInstance CreateAnonymous(Compiler.CompiledType compiledType)
        {
            if (compiledType is null) throw new ArgumentNullException(nameof(compiledType));
            return new TypeInstance(Token.CreateAnonymous(compiledType.Name));
        }

        public override string ToString()
        {
            string result = this.Identifier.Content;
            if (GenericTypes != null && GenericTypes.Count > 0)
            {
                result += '<';
                result += string.Join(", ", GenericTypes);
                result += '>';
            }
            return result;
        }

        public static bool operator ==(TypeInstance a, string b)
        {
            if (a is null && b is null) return true;
            if (a is not null && b is null) return false;
            if (a is null && b is not null) return false;
            return a.Identifier.Content == b;
        }
        public static bool operator !=(TypeInstance a, string b) => !(a == b);

        public static bool operator ==(string a, TypeInstance b) => b == a;
        public static bool operator !=(string a, TypeInstance b) => !(b == a);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            { return true; }

            if (obj is null)
            { return false; }

            if (obj is TypeInstance other)
            { return this.Equals(other); }

            return false;
        }

        public bool Equals(TypeInstance other) => other is not null && this.Identifier.Equals(other.Identifier);

        public override int GetHashCode() => HashCode.Combine(Identifier);
    }
}

namespace ProgrammingLanguage.BBCode.Parser
{
    using Core;

    public interface IDefinition
    {
        public string FilePath { get; set; }
    }

    public class ParameterDefinition : Compiler.IHaveKey<string>
    {
        public Token Identifier;
        public TypeInstance Type;
        public Token[] Modifiers;

        public string Key => Identifier.Content;

        public override string ToString() => $"{string.Join<Token>(", ", Modifiers)} {Type} {Identifier}".TrimStart();
        internal string PrettyPrint() => $"{string.Join<Token>(", ", Modifiers)} {Type} {Identifier}".TrimStart();
    }

    public class FieldDefinition : IThingWithPosition
    {
        public Token Identifier;
        public TypeInstance Type;
        public Token ProtectionToken;

        public Position GetPosition() => new Position(Identifier, Type, ProtectionToken);

        public override string ToString() => $"{(ProtectionToken != null ? ProtectionToken.Content + " " : "")}{Type} {Identifier}";
        internal string PrettyPrint(int ident = 0) => $"{" ".Repeat(ident)}{(ProtectionToken != null ? ProtectionToken.Content + " " : "")}{Type} {Identifier}";
    }

    public interface IExportable
    {
        public bool IsExport { get; }
    }

    public class EnumMemberDefinition : Compiler.IHaveKey<string>
    {
        public Token Identifier;
        public Statement.Literal Value;

        public string Key => Identifier.Content;
    }

    public class EnumDefinition : IDefinition, Compiler.IHaveKey<string>
    {
        public string FilePath { get; set; }

        public string Key => Identifier.Content;

        public Token Identifier;
        public EnumMemberDefinition[] Members;
        public FunctionDefinition.Attribute[] Attributes;
    }

    public class TemplateInfo
    {
        public Token Keyword;
        public Token LeftP;
        public Token[] TypeParameters;
        public Token RightP;

        public string[] TypeParameterNames => TypeParameters.Select(v => v.Content).ToArray();

        public Dictionary<string, Token> ToDictionary()
        {
            Dictionary<string, Token> result = new();
            for (int i = 0; i < TypeParameters.Length; i++)
            { result.Add(TypeParameters[i].Content, TypeParameters[i]); }
            return result;
        }

        public TemplateInfo(Token keyword, Token leftP, IEnumerable<Token> typeParameters, Token rightP)
        {
            Keyword = keyword;
            LeftP = leftP;
            TypeParameters = typeParameters.ToArray();
            RightP = rightP;
        }
    }

    public abstract class FunctionThingDefinition : IExportable
    {
        public Token BracketStart;
        public Token BracketEnd;

        public ParameterDefinition[] Parameters;
        public Statement.Statement[] Statements;
        public Token[] Modifiers;

        /// <summary>
        /// The first parameter is labeled as 'this'
        /// </summary>
        public bool IsMethod => (Parameters.Length > 0) && Parameters[0].Modifiers.Contains("this");

        public Statement.Block Block => new(Statements)
        {
            BracketStart = BracketStart,
            BracketEnd = BracketEnd,
        };

        public int ParameterCount => Parameters.Length;

        public bool IsExport => Modifiers.Contains("export");

        public string FilePath { get; set; }


        public readonly TemplateInfo TemplateInfo;

        protected FunctionThingDefinition(Token identifier, TemplateInfo templateInfo, IEnumerable<Token> modifiers)
        {
            Identifier = identifier;
            TemplateInfo = templateInfo;

            Parameters = Array.Empty<ParameterDefinition>();
            Statements = Array.Empty<Statement.Statement>();
            Modifiers = modifiers.ToArray();
        }

        public virtual bool IsTemplate => TemplateInfo != null;

        public readonly Token Identifier;

        public bool CanUse(string sourceFile) => IsExport || sourceFile == null || sourceFile == FilePath;

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
    }

    public class FunctionDefinition : FunctionThingDefinition
    {
        public class Attribute : Compiler.IHaveKey<string>, IThingWithPosition
        {
            public Token Identifier;
            public object[] Parameters;

            public string Key => Identifier.Content;

            public Position GetPosition()
            { return new Position(Identifier); }
        }

        public Attribute[] Attributes;

        public TypeInstance Type;

        public FunctionDefinition(
            Token identifier,
            IEnumerable<Token> modifiers,
            TemplateInfo templateInfo)
            : base(identifier, templateInfo, modifiers)
        {
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

            result += ' ';

            result += '{';
            if (this.Statements.Length > 0)
            { result += "..."; }
            result += '}';

            return result;
        }

        public string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var parameter in this.Parameters)
            {
                parameters.Add(parameter.PrettyPrint());
            }

            List<string> statements = new();
            foreach (var statement in this.Statements)
            {
                statements.Add($"{" ".Repeat(ident)}" + statement.PrettyPrint((ident == 0) ? 2 : ident) + ";");
            }

            return $"{" ".Repeat(ident)}{this.Type.Identifier.Content} {this.Identifier}" + ($"({string.Join(", ", parameters)})") + " " + (this.Statements.Length > 0 ? $"{{\n{string.Join("\n", statements)}\n}}" : "{}");
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

            result += ' ';

            result += '{';
            if (this.Statements.Length > 0)
            { result += "..."; }
            result += '}';

            return result;
        }

        public string PrettyPrint(int ident = 0)
        {
            List<string> parameters = new();
            foreach (var parameter in this.Parameters)
            {
                parameters.Add(parameter.PrettyPrint());
            }

            List<string> statements = new();
            foreach (var statement in this.Statements)
            {
                statements.Add($"{" ".Repeat(ident)}" + statement.PrettyPrint((ident == 0) ? 2 : ident) + ";");
            }

            return $"{" ".Repeat(ident)}{this.Identifier.Content}" + ($"({string.Join(", ", parameters)})") + " " + (this.Statements.Length > 0 ? $"{{\n{string.Join("\n", statements)}\n}}" : "{}");
        }
    }

    public class ClassDefinition : IExportable, IDefinition, Compiler.IHaveKey<string>
    {
        public readonly FunctionDefinition.Attribute[] Attributes;
        public readonly Token Name;
        public Token BracketStart;
        public Token BracketEnd;
        public List<Statement.Statement> Statements;
        public string FilePath { get; set; }
        public readonly FieldDefinition[] Fields;
        public Token[] Modifiers;
        public TemplateInfo TemplateInfo;

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
            IEnumerable<FunctionDefinition.Attribute> attributes,
            IEnumerable<Token> modifiers,
            IEnumerable<FieldDefinition> fields,
            IEnumerable<FunctionDefinition> methods,
            IEnumerable<GeneralFunctionDefinition> generalMethods,
            IEnumerable<FunctionDefinition> operators)
        {
            this.Name = name;
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
            if (this.TemplateInfo != null)
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
    }

    public class StructDefinition : IExportable, IDefinition, Compiler.IHaveKey<string>
    {
        public readonly FunctionDefinition.Attribute[] Attributes;
        public readonly Token Name;
        public Token BracketStart;
        public Token BracketEnd;
        public List<Statement.Statement> Statements;
        public Token[] Modifiers;

        public string FilePath { get; set; }
        public readonly FieldDefinition[] Fields;

        public string Key => Name.Content;

        public bool IsExport => Modifiers.Contains("export");

        public IReadOnlyDictionary<string, FunctionDefinition> Methods => methods;
        readonly Dictionary<string, FunctionDefinition> methods;

        public StructDefinition(
            Token name,
            IEnumerable<FunctionDefinition.Attribute> attributes,
            IEnumerable<FieldDefinition> fields,
            IEnumerable<KeyValuePair<string, FunctionDefinition>> methods,
            IEnumerable<Token> modifiers)
        {
            this.Name = name;
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
    }

    public class UsingDefinition
    {
        public Token[] Path;
        public Token Keyword;
        /// <summary> Set by the Compiler </summary>
        public string CompiledUri;
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
    }
}
