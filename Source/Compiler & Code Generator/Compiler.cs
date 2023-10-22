using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LanguageCore.BBCode.Compiler
{
    using LanguageCore.Tokenizing;
    using Parser;
    using Parser.Statement;
    using Runtime;

    public class Compiler
    {
        public struct CompilerSettings
        {
            public bool GenerateComments;
            public int RemoveUnusedFunctionsMaxIterations;
            public bool PrintInstructions;
            public bool DontOptimize;
            public bool GenerateDebugInstructions;
            public bool ExternalFunctionsCache;

            public static CompilerSettings Default => new()
            {
                GenerateComments = true,
                RemoveUnusedFunctionsMaxIterations = 10,
                PrintInstructions = false,
                DontOptimize = false,
                GenerateDebugInstructions = true,
                ExternalFunctionsCache = true,
            };
        }

        public enum CompileLevel
        {
            Minimal,
            Exported,
            All,
        }

        readonly List<Error> Errors;
        readonly List<Warning> Warnings;

        CompiledClass[] CompiledClasses;
        CompiledStruct[] CompiledStructs;
        CompiledOperator[] CompiledOperators;
        CompiledFunction[] CompiledFunctions;
        CompiledGeneralFunction[] CompiledGeneralFunctions;
        CompiledEnum[] CompiledEnums;

        readonly List<FunctionDefinition> Operators;
        readonly List<FunctionDefinition> Functions;
        readonly List<MacroDefinition> Macros;
        readonly List<StructDefinition> Structs;
        readonly List<ClassDefinition> Classes;
        readonly List<EnumDefinition> Enums;

        readonly Stack<Token[]> GenericParameters;

        readonly List<CompileTag> Hashes;

        Dictionary<string, ExternalFunctionBase> ExternalFunctions;

        public Compiler()
        {
            Functions = new List<FunctionDefinition>();
            Macros = new List<MacroDefinition>();
            Operators = new List<FunctionDefinition>();
            Structs = new List<StructDefinition>();
            Classes = new List<ClassDefinition>();
            Enums = new List<EnumDefinition>();
            Hashes = new List<CompileTag>();
            GenericParameters = new Stack<Token[]>();

            Warnings = new List<Warning>();
            Errors = new List<Error>();

            CompiledClasses = Array.Empty<CompiledClass>();
            CompiledStructs = Array.Empty<CompiledStruct>();
            CompiledOperators = Array.Empty<CompiledOperator>();
            CompiledFunctions = Array.Empty<CompiledFunction>();
            CompiledGeneralFunctions = Array.Empty<CompiledGeneralFunction>();
            CompiledEnums = Array.Empty<CompiledEnum>();

            ExternalFunctions = new Dictionary<string, ExternalFunctionBase>();
        }

        CompiledType GetCustomType(string name)
        {
            for (int i = 0; i < GenericParameters.Count; i++)
            {
                for (int j = 0; j < GenericParameters[i].Length; j++)
                {
                    if (GenericParameters[i][j].Content == name)
                    {
                        GenericParameters[i][j].AnalyzedType = TokenAnalysedType.TypeParameter;
                        return CompiledType.CreateGeneric(GenericParameters[i][j].Content);
                    }
                }
            }

            if (CompiledStructs.ContainsKey(name)) return new CompiledType(CompiledStructs.Get<string, ITypeDefinition>(name));
            if (CompiledClasses.ContainsKey(name)) return new CompiledType(CompiledClasses.Get<string, ITypeDefinition>(name));
            if (CompiledEnums.ContainsKey(name)) return new CompiledType(CompiledEnums.Get<string, ITypeDefinition>(name));

            throw new InternalException($"Unknown type '{name}'");
        }

        protected string? TypeDefinitionReplacer(string? typeName)
        {
            foreach (var @struct in CompiledStructs)
            {
                if (@struct.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                {
                    if (definedType == typeName)
                    {
                        return @struct.Name.Content;
                    }
                }
            }
            foreach (var @class in CompiledClasses)
            {
                if (@class.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                {
                    if (definedType == typeName)
                    {
                        return @class.Name.Content;
                    }
                }
            }
            foreach (var @enum in CompiledEnums)
            {
                if (@enum.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                {
                    if (definedType == typeName)
                    {
                        return @enum.Identifier.Content;
                    }
                }
            }
            return null;
        }

        public struct Result
        {
            public CompiledFunction[] Functions;
            public MacroDefinition[] Macros;
            public CompiledGeneralFunction[] GeneralFunctions;
            public CompiledOperator[] Operators;

            public Dictionary<string, ExternalFunctionBase> ExternalFunctions;

            public CompiledStruct[] Structs;
            public CompiledClass[] Classes;
            public CompileTag[] Hashes;
            public CompiledEnum[] Enums;

            public Error[] Errors;
            public Warning[] Warnings;
            public Statement[] TopLevelStatements;
            public Token[] Tokens;
        }

        static Dictionary<string, AttributeValues> CompileAttributes(FunctionDefinition.Attribute[] attributes)
        {
            Dictionary<string, AttributeValues> result = new();

            for (int i = 0; i < attributes.Length; i++)
            {
                FunctionDefinition.Attribute attribute = attributes[i];

                attribute.Identifier.AnalyzedType = TokenAnalysedType.Attribute;

                AttributeValues newAttribute = new()
                {
                    parameters = new(),
                    Identifier = attribute.Identifier,
                };

                if (attribute.Parameters != null)
                {
                    for (int j = 0; j < attribute.Parameters.Length; j++)
                    { newAttribute.parameters.Add(new Literal(attribute.Parameters[j])); }
                }

                result.Add(attribute.Identifier.Content, newAttribute);
            }
            return result;
        }

        static CompiledType[] CompileTypes(ParameterDefinition[] parameters, Func<string, CompiledType> unknownTypeCallback)
        {
            CompiledType[] result = new CompiledType[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            { result[i] = new CompiledType(parameters[i].Type, unknownTypeCallback); }
            return result;
        }

        CompiledStruct CompileStruct(StructDefinition @struct)
        {
            if (Constants.Keywords.Contains(@struct.Name.Content))
            { throw new CompilerException($"Illegal struct name '{@struct.Name.Content}'", @struct.Name, @struct.FilePath); }

            @struct.Name.AnalyzedType = TokenAnalysedType.Struct;

            if (CompiledStructs.ContainsKey(@struct.Name.Content))
            { throw new CompilerException($"Struct with name '{@struct.Name.Content}' already exist", @struct.Name, @struct.FilePath); }

            Dictionary<string, AttributeValues> attributes = CompileAttributes(@struct.Attributes);

            return new CompiledStruct(attributes, new CompiledField[@struct.Fields.Length], @struct);
        }

        CompiledClass CompileClass(ClassDefinition @class)
        {
            if (Constants.Keywords.Contains(@class.Name.Content))
            { throw new CompilerException($"Illegal class name '{@class.Name.Content}'", @class.Name, @class.FilePath); }

            @class.Name.AnalyzedType = TokenAnalysedType.Class;

            if (CompiledClasses.ContainsKey(@class.Name.Content))
            { throw new CompilerException($"Class with name '{@class.Name.Content}' already exist", @class.Name, @class.FilePath); }

            Dictionary<string, AttributeValues> attributes = CompileAttributes(@class.Attributes);

            return new CompiledClass(attributes, new CompiledField[@class.Fields.Length], @class);
        }

        CompiledFunction CompileFunction(FunctionDefinition function)
        {
            Dictionary<string, AttributeValues> attributes = CompileAttributes(function.Attributes);

            if (function.TemplateInfo != null)
            {
                GenericParameters.Push(function.TemplateInfo.TypeParameters);
                foreach (Token typeParameter in function.TemplateInfo.TypeParameters)
                { typeParameter.AnalyzedType = TokenAnalysedType.TypeParameter; }
            }

            CompiledType type = new(function.Type, GetCustomType);
            function.Type.SetAnalyzedType(type);

            if (attributes.TryGetAttribute("External", out string? name))
            {
                if (ExternalFunctions.TryGetValue(name, out var externalFunction))
                {
                    if (externalFunction.ParameterCount != function.Parameters.Length)
                    { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ID}'", function.Identifier, function.FilePath); }
                    if (externalFunction.ReturnSomething != (type != Type.VOID))
                    { throw new CompilerException($"Wrong type defined for function '{externalFunction.ID}'", function.Type, function.FilePath); }

                    for (int i = 0; i < externalFunction.ParameterTypes.Length; i++)
                    {
                        Type definedParameterType = externalFunction.ParameterTypes[i];
                        CompiledType passedParameterType = new(function.Parameters[i].Type, GetCustomType);

                        if (passedParameterType == definedParameterType)
                        { continue; }

                        if (passedParameterType.IsClass && definedParameterType == Type.INT)
                        { continue; }

                        throw new CompilerException($"Wrong type of parameter passed to function \"{externalFunction.ID}\". Parameter index: {i} Required type: {definedParameterType.ToString().ToLower()} Passed: {passedParameterType}", function.Parameters[i].Type, function.FilePath);
                    }

                    if (function.TemplateInfo != null)
                    { GenericParameters.Pop(); }

                    return new CompiledFunction(type, externalFunction.ParameterTypes.Select(v => new CompiledType(v)).ToArray(), function)
                    {
                        CompiledAttributes = attributes,
                    };
                }

                Errors.Add(new Error("External function '" + name + "' not found", Position.UnknownPosition, function.FilePath));
            }

            CompiledFunction result = new(
                type,
                CompileTypes(function.Parameters, GetCustomType),
                function
                )
            {
                CompiledAttributes = attributes,
            };

            if (function.TemplateInfo != null)
            { GenericParameters.Pop(); }

            return result;
        }

        CompiledOperator CompileOperator(FunctionDefinition function)
        {
            Dictionary<string, AttributeValues> attributes = CompileAttributes(function.Attributes);

            CompiledType type = new(function.Type, GetCustomType);
            function.Type.SetAnalyzedType(type);

            if (attributes.TryGetAttribute("External", out string? name))
            {
                if (ExternalFunctions.TryGetValue(name, out var externalFunction))
                {
                    if (externalFunction.ParameterCount != function.Parameters.Length)
                    { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ID}'", function.Identifier, function.FilePath); }
                    if (externalFunction.ReturnSomething != (type != Type.VOID))
                    { throw new CompilerException($"Wrong type defined for function '{externalFunction.ID}'", function.Type, function.FilePath); }

                    for (int i = 0; i < externalFunction.ParameterTypes.Length; i++)
                    {
                        if (Constants.BuiltinTypeMap3.TryGetValue(function.Parameters[i].Type.ToString(), out Type builtinType))
                        {
                            if (externalFunction.ParameterTypes[i] != builtinType)
                            { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ID}'. Parameter index: {i} Required type: {externalFunction.ParameterTypes[i].ToString().ToLower()} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type, function.FilePath); }
                        }
                        else
                        { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ID}'. Parameter index: {i} Required type: {externalFunction.ParameterTypes[i].ToString().ToLower()} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type, function.FilePath); }
                    }

                    return new CompiledOperator(type, externalFunction.ParameterTypes.Select(v => new CompiledType(v)).ToArray(), function)
                    {
                        CompiledAttributes = attributes,
                    };
                }

                Errors.Add(new Error($"External function \"{name}\" not found", Position.UnknownPosition, function.FilePath));
            }

            return new CompiledOperator(
                type,
                CompileTypes(function.Parameters, GetCustomType),
                function
                )
            {
                CompiledAttributes = attributes,
            };
        }

        CompiledGeneralFunction CompileGeneralFunction(GeneralFunctionDefinition function, CompiledType baseType)
        {
            return new CompiledGeneralFunction(
                baseType,
                CompileTypes(function.Parameters, GetCustomType),
                function
                );
        }

        static CompiledEnum CompileEnum(EnumDefinition @enum)
        {
            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in @enum.Attributes)
            {
                attribute.Identifier.AnalyzedType = TokenAnalysedType.Attribute;

                AttributeValues newAttribute = new()
                { parameters = new() };

                if (attribute.Parameters != null)
                {
                    foreach (object parameter in attribute.Parameters)
                    {
                        newAttribute.parameters.Add(new Literal(parameter));
                    }
                }

                attributes.Add(attribute.Identifier.Content, newAttribute);
            }

            CompiledEnum compiledEnum = new(@enum)
            {
                CompiledAttributes = attributes,
                FilePath = @enum.FilePath,
                Members = new CompiledEnumMember[@enum.Members.Length],
            };

            for (int i = 0; i < @enum.Members.Length; i++)
            {
                EnumMemberDefinition member = @enum.Members[i];
                CompiledEnumMember compiledMember = new(member);
                switch (member.Value!.Type)
                {
                    case LiteralType.INT:
                        compiledMember.Value = new DataItem(int.Parse(member.Value.Value));
                        break;
                    case LiteralType.FLOAT:
                        compiledMember.Value = new DataItem(float.Parse(member.Value.Value.TrimEnd('f'), System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    case LiteralType.BOOLEAN:
                        compiledMember.Value = new DataItem(bool.Parse(member.Value.Value) ? 1 : 0);
                        break;
                    case LiteralType.CHAR:
                        if (member.Value.Value.Length != 1) throw new InternalException($"Literal char contains {member.Value.Value.Length} characters but only 1 allowed", @enum.FilePath);
                        compiledMember.Value = new DataItem(member.Value.Value[0]);
                        break;
                    case LiteralType.STRING:
                        throw new CompilerException($"String literal is not valid for a enum member value", member.Value, @enum.FilePath);
                    default:
                        throw new ImpossibleException();
                }
                compiledEnum.Members[i] = compiledMember;
            }

            return compiledEnum;
        }

        void CompileFile(SourceCodeManager.CollectedAST collectedAST)
        {
            foreach (var func in collectedAST.ParserResult.Functions)
            {
                if (Functions.ContainsSameDefinition(func))
                { Errors.Add(new Error($"Function {func.ReadableID()} already defined", func.Identifier, func.FilePath)); continue; }

                Functions.Add(func);
            }

            foreach (var macro in collectedAST.ParserResult.Macros)
            {
                if (Macros.ContainsSameDefinition(macro))
                { Errors.Add(new Error($"Macro {macro.ReadableID()} already defined", macro.Identifier, macro.FilePath)); continue; }

                Macros.Add(macro);
            }

            /*
            foreach (var func in collectedAST.ParserResult.Operators)
            {
                if (Operators.ContainsSameDefinition(func))
                { Errors.Add(new Error($"Operator '{func.ReadableID()}' already defined", func.Identifier)); continue; }

                Operators.Add(func);
            }
            */

            foreach (var @struct in collectedAST.ParserResult.Structs)
            {
                if (Classes.ContainsKey(@struct.Name.Content) || Structs.ContainsKey(@struct.Name.Content) || Enums.ContainsKey(@struct.Name.Content))
                { Errors.Add(new Error($"Type \" {@struct.Name.Content} \" already defined", @struct.Name, @struct.FilePath)); }
                else
                { Structs.Add(@struct); }
            }

            foreach (var @class in collectedAST.ParserResult.Classes)
            {
                if (Classes.ContainsKey(@class.Name.Content) || Structs.ContainsKey(@class.Name.Content) || Enums.ContainsKey(@class.Name.Content))
                { Errors.Add(new Error($"Type \"{@class.Name.Content}\" already defined", @class.Name, @class.FilePath)); }
                else
                { Classes.Add(@class); }


                foreach (var func in @class.Operators)
                {
                    if (Operators.ContainsSameDefinition(func))
                    { Errors.Add(new Error($"Operator {func.ReadableID()} already defined", func.Identifier, func.FilePath)); continue; }

                    Operators.Add(func);
                }
            }

            foreach (var @enum in collectedAST.ParserResult.Enums)
            {
                if (Classes.ContainsKey(@enum.Identifier.Content) || Structs.ContainsKey(@enum.Identifier.Content) || Enums.ContainsKey(@enum.Identifier.Content))
                { Errors.Add(new Error($"Type \"{@enum.Identifier.Content}\" already defined", @enum.Identifier, @enum.FilePath)); }
                else
                { Enums.Add(@enum); }
            }

            Hashes.AddRange(collectedAST.ParserResult.Hashes);
        }

        Result CompileMainFile(
            ParserResult parserResult,
            Dictionary<string, ExternalFunctionBase> externalFunctions,
            FileInfo file,
            ParserSettings parserSettings,
            PrintCallback? printCallback,
            string? basePath)
        {
            Structs.AddRange(parserResult.Structs);
            Classes.AddRange(parserResult.Classes);
            Functions.AddRange(parserResult.Functions);
            Macros.AddRange(parserResult.Macros);

            SourceCodeManager.Result collectorResult;
            if (file != null)
            {
                collectorResult = SourceCodeManager.Collect(parserResult, file, parserSettings, printCallback, basePath);
            }
            else
            {
                collectorResult = new SourceCodeManager.Result()
                {
                    CollectedASTs = Array.Empty<SourceCodeManager.CollectedAST>(),
                    Errors = Array.Empty<Error>(),
                    Warnings = Array.Empty<Warning>(),
                };
            }

            this.Warnings.AddRange(collectorResult.Warnings);
            this.Errors.AddRange(collectorResult.Errors);

            for (int i = 0; i < collectorResult.CollectedASTs.Length; i++)
            { CompileFile(collectorResult.CollectedASTs[i]); }

            #region Compile test external functions

            foreach (var hash in Hashes)
            {
                switch (hash.HashName.Content)
                {
                    case "bf":
                        {
                            if (hash.Parameters.Length < 2)
                            { Errors.Add(new Error($"Hash '{hash.HashName}' requires minimum 2 parameter", hash.HashName, hash.FilePath)); break; }
                            string name = hash.Parameters[0].Value;

                            if (externalFunctions.ContainsKey(name)) break;

                            string[] bfParams = new string[hash.Parameters.Length - 1];
                            for (int i = 1; i < hash.Parameters.Length; i++)
                            { bfParams[i - 1] = hash.Parameters[i].Value; }

                            Type[] parameterTypes = new Type[bfParams.Length];
                            for (int i = 0; i < bfParams.Length; i++)
                            {
                                if (Constants.BuiltinTypeMap3.TryGetValue(bfParams[i], out var paramType))
                                {
                                    parameterTypes[i] = paramType;

                                    if (paramType == Type.VOID && i > 0)
                                    { Errors.Add(new Error($"Invalid type \"{bfParams[i]}\"", hash.Parameters[i + 1].ValueToken, hash.FilePath)); goto ExitBreak; }
                                }
                                else
                                {
                                    Errors.Add(new Error($"Unknown type \"{bfParams[i]}\"", hash.Parameters[i + 1].ValueToken, hash.FilePath));
                                    goto ExitBreak;
                                }
                            }

                            Type returnType = parameterTypes[0];
                            List<Type> x = parameterTypes.ToList();
                            x.RemoveAt(0);
                            Type[] pTypes = x.ToArray();

                            if (parameterTypes[0] == Type.VOID)
                            {
                                externalFunctions.AddExternalFunction(name, pTypes, (BytecodeProcessor sender, DataItem[] p) =>
                                {
                                    Output.Debug($"External function \"{name}\" called with params:\n  {string.Join(", ", p)}");
                                });
                            }
                            else
                            {
                                externalFunctions.AddExternalFunction(name, pTypes, (BytecodeProcessor sender, DataItem[] p) =>
                                {
                                    Output.Debug($"External function \"{name}\" called with params:\n  {string.Join(", ", p)}");
                                    return DataItem.GetDefaultValue(returnType);
                                });
                            }
                        }
                        break;
                    default:
                        Warnings.Add(new Warning($"Hash '{hash.HashName}' does not exists, so this is ignored", hash.HashName, hash.FilePath));
                        break;
                }

            ExitBreak:
                continue;
            }

            #endregion

            #region Compile Classes

            this.CompiledClasses = new CompiledClass[Classes.Count];
            for (int i = 0; i < Classes.Count; i++)
            { this.CompiledClasses[i] = CompileClass(Classes[i]); }

            #endregion

            #region Compile Enums

            this.CompiledEnums = new CompiledEnum[Enums.Count];
            for (int i = 0; i < Enums.Count; i++)
            { this.CompiledEnums[i] = CompileEnum(Enums[i]); }

            #endregion

            #region Compile Structs

            this.CompiledStructs = new CompiledStruct[Structs.Count];
            for (int i = 0; i < Structs.Count; i++)
            { this.CompiledStructs[i] = CompileStruct(Structs[i]); }

            #endregion

            for (int i = 0; i < CompiledStructs.Length; i++)
            {
                for (int j = 0; j < CompiledStructs[i].Fields.Length; j++)
                {
                    FieldDefinition field = ((StructDefinition)CompiledStructs[i]).Fields[j];
                    CompiledField compiledField = new(new CompiledType(field.Type, GetCustomType), null, field);
                    field.Type.SetAnalyzedType(compiledField.Type);
                    CompiledStructs[i].Fields[j] = compiledField;
                }
            }
            for (int i = 0; i < CompiledClasses.Length; i++)
            {
                CompiledClass @class = CompiledClasses[i];
                if (@class.TemplateInfo != null)
                {
                    GenericParameters.Push(@class.TemplateInfo.TypeParameters);
                    foreach (Token typeParameter in @class.TemplateInfo.TypeParameters)
                    { typeParameter.AnalyzedType = TokenAnalysedType.TypeParameter; }
                }

                for (int j = 0; j < @class.Fields.Length; j++)
                {
                    FieldDefinition field = ((ClassDefinition)@class).Fields[j];
                    CompiledField newField = new(new CompiledType(field.Type, GetCustomType), @class, field);
                    field.Type.SetAnalyzedType(newField.Type);
                    @class.Fields[j] = newField;
                }

                if (@class.TemplateInfo != null)
                { GenericParameters.Pop(); }
            }

            #region Compile Operators

            {
                List<CompiledOperator> compiledOperators = new();

                foreach (var function in Operators)
                {
                    CompiledOperator compiledFunction = CompileOperator(function);

                    if (compiledOperators.ContainsSameDefinition(compiledFunction))
                    { throw new CompilerException($"Operator '{compiledFunction.ReadableID()}' already defined", function.Identifier, function.FilePath); }

                    compiledOperators.Add(compiledFunction);
                }

                this.CompiledOperators = compiledOperators.ToArray();
            }

            #endregion

            #region Compile Functions

            {
                List<CompiledFunction> compiledFunctions = new();
                List<CompiledGeneralFunction> compiledGeneralFunctions = new();

                foreach (CompiledClass compiledClass in CompiledClasses)
                {
                    if (compiledClass.TemplateInfo != null)
                    {
                        GenericParameters.Push(compiledClass.TemplateInfo.TypeParameters);
                        foreach (Token typeParameter in compiledClass.TemplateInfo.TypeParameters)
                        { typeParameter.AnalyzedType = TokenAnalysedType.TypeParameter; }
                    }

                    foreach (var method in compiledClass.GeneralMethods)
                    {
                        foreach (var parameter in method.Parameters)
                        {
                            if (parameter.Modifiers.Contains("this"))
                            { throw new CompilerException($"Keyword 'this' is not valid in the current context", parameter.Identifier, compiledClass.FilePath); }
                        }

                        if (method.Identifier.Content == "destructor")
                        {
                            List<ParameterDefinition> parameters = method.Parameters.ToList();
                            parameters.Insert(0,
                                new ParameterDefinition(
                                    new Token[1] { Token.CreateAnonymous("this") },
                                    TypeInstanceSimple.CreateAnonymous(compiledClass.Name.Content, TypeDefinitionReplacer),
                                    Token.CreateAnonymous("this"))
                                );
                            method.Parameters = parameters.ToArray();
                        }

                        CompiledGeneralFunction methodInfo = CompileGeneralFunction(method, new CompiledType(compiledClass));

                        if (compiledGeneralFunctions.ContainsSameDefinition(methodInfo))
                        { throw new CompilerException($"Function with name '{methodInfo.ReadableID()}' already defined", method.Identifier, compiledClass.FilePath); }

                        methodInfo.Context = compiledClass;
                        compiledGeneralFunctions.Add(methodInfo);
                    }

                    foreach (FunctionDefinition method in compiledClass.Methods)
                    {
                        foreach (var parameter in method.Parameters)
                        {
                            if (parameter.Modifiers.Contains("this"))
                            { throw new CompilerException($"Keyword 'this' is not valid in the current context", parameter.Identifier, compiledClass.FilePath); }
                        }
                        List<ParameterDefinition> parameters = method.Parameters.ToList();
                        parameters.Insert(0,
                            new ParameterDefinition(
                                new Token[1] { Token.CreateAnonymous("this") },
                                TypeInstanceSimple.CreateAnonymous(compiledClass.Name.Content, TypeDefinitionReplacer),
                                Token.CreateAnonymous("this"))
                            );
                        method.Parameters = parameters.ToArray();

                        CompiledFunction methodInfo = CompileFunction(method);

                        if (compiledFunctions.ContainsSameDefinition(methodInfo))
                        { throw new CompilerException($"Function with name '{methodInfo.ReadableID}' already defined", method.Identifier, compiledClass.FilePath); }

                        methodInfo.Context = compiledClass;
                        compiledFunctions.Add(methodInfo);
                    }

                    if (compiledClass.TemplateInfo != null)
                    { GenericParameters.Pop(); }
                }

                foreach (var function in Functions)
                {
                    var compiledFunction = CompileFunction(function);

                    if (compiledFunctions.ContainsSameDefinition(compiledFunction))
                    { throw new CompilerException($"Function with name '{compiledFunction.ReadableID()}' already defined", function.Identifier, function.FilePath); }

                    compiledFunctions.Add(compiledFunction);
                }

                this.CompiledFunctions = compiledFunctions.ToArray();
                this.CompiledGeneralFunctions = compiledGeneralFunctions.ToArray();
            }

            #endregion

            return new Result()
            {
                Functions = this.CompiledFunctions,
                Macros = this.Macros.ToArray(),
                Operators = this.CompiledOperators,
                GeneralFunctions = this.CompiledGeneralFunctions,
                ExternalFunctions = externalFunctions,
                Classes = this.CompiledClasses,
                Structs = this.CompiledStructs,
                Enums = this.CompiledEnums,
                Hashes = this.Hashes.ToArray(),
                TopLevelStatements = parserResult.TopLevelStatements,
                Tokens = parserResult.Tokens,

                Errors = this.Errors.ToArray(),
                Warnings = this.Warnings.ToArray(),
            };
        }

        /// <summary>
        /// Does some checks and prepares the AST for the <see cref="CodeGenerator"/>
        /// </summary>
        /// <param name="file">
        /// The source code file
        /// </param>
        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="CompilerException"/>
        /// <exception cref="LanguageException"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="System.Exception"/>
        public static Result Compile(
            ParserResult parserResult,
            Dictionary<string, ExternalFunctionBase> externalFunctions,
            FileInfo file,
            ParserSettings parserSettings,
            PrintCallback? printCallback = null,
            string? basePath = null)
        {
            Compiler compiler = new()
            {
                ExternalFunctions = externalFunctions,
            };
            return compiler.CompileMainFile(
                parserResult,
                externalFunctions,
                file,
                parserSettings,
                printCallback,
                basePath
                );
        }

        public static Result Compile(
            FileInfo file,
            Dictionary<string, ExternalFunctionBase> externalFunctions,
            TokenizerSettings tokenizerSettings,
            ParserSettings parserSettings,
            PrintCallback? printCallback = null,
            string? basePath = null)
        {
            string sourceCode = File.ReadAllText(file.FullName);

            Token[] tokens;

            {
                Tokenizer tokenizer = new(tokenizerSettings, printCallback);
                List<Warning> warnings = new();

                tokens = tokenizer.Parse(sourceCode, warnings, file.FullName);

                foreach (Warning warning in warnings)
                { printCallback?.Invoke(warning.ToString(), LogType.Warning); }
            }

            ParserResult parserResult;

            {
                DateTime parseStarted = DateTime.Now;
                if (printCallback != null)
                { printCallback?.Invoke("Parsing ...", LogType.Debug); }

                parserResult = Parser.Parse(tokens);

                if (parserResult.Errors.Length > 0)
                { throw new LanguageException("Failed to parse", parserResult.Errors[0].ToException()); }

                if (printCallback != null)
                { printCallback?.Invoke($"Parsed in {(DateTime.Now - parseStarted).TotalMilliseconds} ms", LogType.Debug); }

                if (parserSettings.PrintInfo)
                { parserResult.WriteToConsole(); }
            }

            parserResult.SetFile(file.FullName);

            return Compile(parserResult, externalFunctions, file, parserSettings, printCallback, basePath);
        }
    }
}