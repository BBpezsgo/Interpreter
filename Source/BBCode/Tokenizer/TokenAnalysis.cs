namespace ProgrammingLanguage.BBCode.Analysis
{
    using ProgrammingLanguage.BBCode.Compiler;
    using ProgrammingLanguage.BBCode.Parser.Statement;

    public static class Extensions
    {
        internal static AnalysedToken_Function BuiltinFunction(this Token token, string name, string type, string[] paramNames, string[] paramTypes) => new(token)
        {
            Name = name,
            Type = type,
            ParameterNames = paramNames,
            ParameterTypes = paramTypes,
            Kind = FunctionKind.Builtin
        };
        internal static AnalysedToken_Function Statement(this Token token, string name, string type, string[] paramNames, string[] paramTypes) => new(token)
        {
            Name = name,
            Type = type,
            ParameterNames = paramNames,
            ParameterTypes = paramTypes,
            Kind = FunctionKind.Statement
        };
        internal static AnalysedToken_UserDefinedFunction Function(this Token token, CompiledFunction function) => new(token)
        {
            Definition = function,
        };
        internal static AnalysedToken_Method Method(this Token token, string name, string type, string prevType, string file, string[] paramNames, string[] paramTypes) => new(token)
        {
            Name = name,
            Type = type,
            FilePath = file,
            PrevType = prevType,
            ParameterNames = paramNames,
            ParameterTypes = paramTypes,
            Kind = FunctionKind.UserDefined
        };
        internal static AnalysedToken_Variable Variable(this Token token, string name, string type, bool isGlobal) => new(token)
        {
            Name = name,
            Type = type,
            Kind = isGlobal ? VariableKind.Global : VariableKind.Local,
        };
        internal static AnalysedToken_Variable Variable(this Token token, CompiledVariable variable) => new(token)
        {
            FilePath = variable.FilePath,
            Name = variable.VariableName.Content,
            Type = variable.Type.Name,
            Kind = variable.IsGlobal ? VariableKind.Global : VariableKind.Local,
        };
        internal static AnalysedToken_Variable Variable(this Token token, CompiledParameter parameter) => new(token)
        {
            Name = parameter.Identifier.Content,
            Type = parameter.Type.Name,
            Kind = VariableKind.Parameter,
        };
        internal static AnalysedToken_ComplexType Struct(this Token token, CompiledStruct @struct) => new(token)
        {
            Name = @struct.Name.Content,
            FilePath = @struct.FilePath,
            Kind = ComplexTypeKind.Struct,
        };
        internal static AnalysedToken_ComplexType Class(this Token token, CompiledClass @class) => new(token)
        {
            Name = @class.Name.Content,
            FilePath = @class.FilePath,
            Kind = ComplexTypeKind.Class,
        };
        internal static AnalysedToken_Field Field(this Token token, CompiledStruct @struct, Field field, CompiledType type) => new(token)
        {
            Name = @struct.Name.Content,
            FilePath = @struct.FilePath,
            Type = type.Name,
            Kind = ComplexTypeKind.Struct,
        };
        internal static AnalysedToken_Field Field(this Token token, CompiledClass @class, Field field, CompiledType type) => new(token)
        {
            Name = @class.Name.Content,
            FilePath = @class.FilePath,
            Type = type.Name,
            Kind = ComplexTypeKind.Class,
        };
    }

    public class AnalysedToken : Token
    {
        internal AnalysedToken(Token token)
        {
            AbsolutePosition = token.AbsolutePosition;
            Content = new string(token.Content);
            Position = token.Position;
            TokenType = token.TokenType;
        }
    }

    public enum FunctionKind
    {
        UserDefined,
        Builtin,
        Statement,
    }

    public class AnalysedToken_Function : AnalysedToken
    {
        public string FilePath;
        public string Name;
        public string Type;
        public FunctionKind Kind;

        public string[] ParameterNames;
        public string[] ParameterTypes;

        internal AnalysedToken_Function(Token token) : base(token) { }
    }

    public class AnalysedToken_Field : AnalysedToken
    {
        public string FilePath;
        public string Name;
        public string Type;
        public ComplexTypeKind Kind;

        internal AnalysedToken_Field(Token token) : base(token) { }
    }

    public class AnalysedToken_UserDefinedFunction : AnalysedToken
    {
        public CompiledFunction Definition;

        internal AnalysedToken_UserDefinedFunction(Token token) : base(token) { }
    }

    public class AnalysedToken_Method : AnalysedToken_Function
    {
        public string PrevType;

        internal AnalysedToken_Method(Token token) : base(token) { }
    }

    public enum VariableKind
    {
        Local,
        Global,
        Parameter,
    }

    public class AnalysedToken_Variable : AnalysedToken
    {
        public string FilePath;
        public string Name;
        public string Type;
        public VariableKind Kind;

        internal AnalysedToken_Variable(Token token) : base(token) { }
    }

    public enum ComplexTypeKind
    {
        Struct,
        Class,
    }

    public class AnalysedToken_ComplexType : AnalysedToken
    {
        public string FilePath;
        public string Name;
        public ComplexTypeKind Kind;

        internal AnalysedToken_ComplexType(Token token) : base(token) { }
    }
}