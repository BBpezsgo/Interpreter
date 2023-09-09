﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProgrammingLanguage.BBCode.Compiler
{
    using Analysis;
    using Bytecode;
    using Core;
    using DataUtilities.Serializer;
    using Errors;
    using Parser;
    using Parser.Statement;

    public class Compiler
    {
        public class CompilerResult : ISerializable<CompilerResult>
        {
            public Instruction[] Code { get; set; }

            public CompiledFunction[] Functions { get; set; }
            public CompiledStruct[] Structs { get; set; }

            public Dictionary<string, int> Offsets { get; set; }
            internal DebugInfo[] DebugInfo { get; set; }

            public CompilerResult()
            {

            }

            void ISerializable<CompilerResult>.Serialize(Serializer serializer)
            {
                serializer.Serialize(Code);
                serializer.Serialize(Offsets);
            }

            void ISerializable<CompilerResult>.Deserialize(Deserializer deserializer)
            {
                this.Code = deserializer.DeserializeObjectArray<Instruction>();
                this.Offsets = deserializer.DeserializeDictionary<string, int>();
            }
        }

        public struct CompilerSettings
        {
            public bool GenerateComments;
            public byte RemoveUnusedFunctionsMaxIterations;
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

        List<Error> Errors;
        List<Warning> Warnings;

        // Action<string, Output.LogType> PrintCallback;

        CompiledClass[] CompiledClasses;
        CompiledStruct[] CompiledStructs;
        CompiledOperator[] CompiledOperators;
        CompiledFunction[] CompiledFunctions;
        CompiledGeneralFunction[] CompiledGeneralFunctions;
        CompiledEnum[] CompiledEnums;

        List<FunctionDefinition> Operators;
        List<FunctionDefinition> Functions;
        List<StructDefinition> Structs;
        List<ClassDefinition> Classes;
        List<EnumDefinition> Enums;

        Stack<Token[]> GenericParameters;

        List<CompileTag> Hashes;

        Dictionary<string, ExternalFunctionBase> ExternalFunctions;

        CompiledType GetCustomType(string name)
        {
            for (int i = 0; i < GenericParameters.Count; i++)
            {
                for (int j = 0; j < GenericParameters[i].Length; j++)
                {
                    if (GenericParameters[i][j].Content == name)
                    {
                        GenericParameters[i][j].AnalysedType = TokenAnalysedType.TypeParameter;
                        return CompiledType.CreateGeneric(GenericParameters[i][j].Content);
                    }
                }
            }

            if (CompiledStructs.ContainsKey(name)) return new CompiledType(CompiledStructs.Get<string, ITypeDefinition>(name));
            if (CompiledClasses.ContainsKey(name)) return new CompiledType(CompiledClasses.Get<string, ITypeDefinition>(name));
            if (CompiledEnums.ContainsKey(name)) return new CompiledType(CompiledEnums.Get<string, ITypeDefinition>(name));

            throw new InternalException($"Unknown type '{name}'");
        }

        protected string TypeDefinitionReplacer(string typeName)
        {
            foreach (var @struct in CompiledStructs)
            {
                if (@struct.CompiledAttributes.TryGetAttribute("Define", out string definedType))
                {
                    if (definedType == typeName)
                    {
                        return @struct.Name.Content;
                    }
                }
            }
            foreach (var @class in CompiledClasses)
            {
                if (@class.CompiledAttributes.TryGetAttribute("Define", out string definedType))
                {
                    if (definedType == typeName)
                    {
                        return @class.Name.Content;
                    }
                }
            }
            foreach (var @enum in CompiledEnums)
            {
                if (@enum.CompiledAttributes.TryGetAttribute("Define", out string definedType))
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

        internal static Dictionary<string, AttributeValues> CompileAttributes(FunctionDefinition.Attribute[] attributes)
        {
            Dictionary<string, AttributeValues> result = new();

            for (int i = 0; i < attributes.Length; i++)
            {
                FunctionDefinition.Attribute attribute = attributes[i];

                attribute.Identifier.AnalysedType = TokenAnalysedType.Attribute;

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

        internal static CompiledType[] CompileTypes(ParameterDefinition[] parameters, Func<string, CompiledType> unknownTypeCallback)
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

            @struct.Name.AnalysedType = TokenAnalysedType.Struct;

            if (CompiledStructs.ContainsKey(@struct.Name.Content))
            { throw new CompilerException($"Struct with name '{@struct.Name.Content}' already exist", @struct.Name, @struct.FilePath); }

            Dictionary<string, AttributeValues> attributes = CompileAttributes(@struct.Attributes);

            return new CompiledStruct(attributes, new CompiledField[@struct.Fields.Length], @struct)
            {
                References = new List<DefinitionReference>(),
            };
        }

        CompiledClass CompileClass(ClassDefinition @class)
        {
            if (Constants.Keywords.Contains(@class.Name.Content))
            { throw new CompilerException($"Illegal class name '{@class.Name.Content}'", @class.Name, @class.FilePath); }

            @class.Name.AnalysedType = TokenAnalysedType.Struct;

            if (CompiledClasses.ContainsKey(@class.Name.Content))
            { throw new CompilerException($"Class with name '{@class.Name.Content}' already exist", @class.Name, @class.FilePath); }

            Dictionary<string, AttributeValues> attributes = CompileAttributes(@class.Attributes);

            return new CompiledClass(attributes, new CompiledField[@class.Fields.Length], @class)
            {
                References = new List<DefinitionReference>(),
            };
        }

        CompiledFunction CompileFunction(FunctionDefinition function)
        {
            Dictionary<string, AttributeValues> attributes = CompileAttributes(function.Attributes);

            if (function.TemplateInfo != null)
            {
                GenericParameters.Push(function.TemplateInfo.TypeParameters);
                foreach (Token typeParameter in function.TemplateInfo.TypeParameters)
                { typeParameter.AnalysedType = TokenAnalysedType.TypeParameter; }
            }

            CompiledType type = new(function.Type, GetCustomType);

            if (attributes.TryGetAttribute("External", out string name))
            {
                if (ExternalFunctions.TryGetValue(name, out var externalFunction))
                {
                    if (externalFunction.ParameterCount != function.Parameters.Length)
                    { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ID}'", function.Identifier, function.FilePath); }
                    if (externalFunction.ReturnSomething != (type != Type.VOID))
                    { throw new CompilerException($"Wrong type definied for function '{externalFunction.ID}'", function.Type.Identifier, function.FilePath); }

                    for (int i = 0; i < externalFunction.ParameterTypes.Length; i++)
                    {
                        Type definedParameterType = externalFunction.ParameterTypes[i];
                        CompiledType passedParameterType = new(function.Parameters[i].Type, GetCustomType);

                        if (passedParameterType == definedParameterType)
                        { continue; }

                        if (passedParameterType.IsClass && definedParameterType == Type.INT)
                        { continue; }

                        throw new CompilerException($"Wrong type of parameter passed to function \"{externalFunction.ID}\". Parameter index: {i} Requied type: {definedParameterType.ToString().ToLower()} Passed: {passedParameterType}", function.Parameters[i].Type.Identifier, function.FilePath);
                    }

                    if (function.TemplateInfo != null)
                    { GenericParameters.Pop(); }

                    return new CompiledFunction(type, function)
                    {
                        ParameterTypes = externalFunction.ParameterTypes.Select(v => new CompiledType(v)).ToArray(),
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

            if (attributes.TryGetAttribute("External", out string name))
            {
                if (ExternalFunctions.TryGetValue(name, out var externalFunction))
                {
                    if (externalFunction.ParameterCount != function.Parameters.Length)
                    { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ID}'", function.Identifier, function.FilePath); }
                    if (externalFunction.ReturnSomething != (type != Type.VOID))
                    { throw new CompilerException($"Wrong type definied for function '{externalFunction.ID}'", function.Type.Identifier, function.FilePath); }

                    for (int i = 0; i < externalFunction.ParameterTypes.Length; i++)
                    {
                        if (Constants.BuiltinTypeMap3.TryGetValue(function.Parameters[i].Type.Identifier.Content, out Type builtinType))
                        {
                            if (externalFunction.ParameterTypes[i] != builtinType)
                            { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ID}'. Parameter index: {i} Requied type: {externalFunction.ParameterTypes[i].ToString().ToLower()} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type.Identifier, function.FilePath); }
                        }
                        else
                        { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ID}'. Parameter index: {i} Requied type: {externalFunction.ParameterTypes[i].ToString().ToLower()} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type.Identifier, function.FilePath); }
                    }

                    return new CompiledOperator(type, function)
                    {
                        ParameterTypes = externalFunction.ParameterTypes.Select(v => new CompiledType(v)).ToArray(),
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
                attribute.Identifier.AnalysedType = TokenAnalysedType.Attribute;

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

            CompiledEnum compiledEnum = new()
            {
                Attributes = @enum.Attributes,
                CompiledAttributes = attributes,
                FilePath = @enum.FilePath,
                Identifier = @enum.Identifier,
                Members = new CompiledEnumMember[@enum.Members.Length],
            };

            for (int i = 0; i < @enum.Members.Length; i++)
            {
                EnumMemberDefinition member = @enum.Members[i];
                CompiledEnumMember compiledMember = new()
                {
                    Identifier = member.Identifier,
                };
                switch (member.Value.Type)
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
            Action<string, Output.LogType> printCallback,
            string basePath)
        {
            Structs.AddRange(parserResult.Structs);
            Classes.AddRange(parserResult.Classes);
            Functions.AddRange(parserResult.Functions);

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
                            { Errors.Add(new Error($"Hash '{hash.HashName}' requies minimum 2 parameter", hash.HashName, hash.FilePath)); break; }
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
                                    Output.Output.Debug($"External function \"{name}\" called with params:\n  {string.Join(", ", p)}");
                                });
                            }
                            else
                            {
                                externalFunctions.AddExternalFunction(name, pTypes, (BytecodeProcessor sender, DataItem[] p) =>
                                {
                                    Output.Output.Debug($"External function \"{name}\" called with params:\n  {string.Join(", ", p)}");
                                    return DataItem.GetDefaultValue(returnType, "return value");
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

            #region Analyse Struct Fields

            foreach (var @struct in Structs)
            {
                foreach (var field in @struct.Fields)
                {
                    if (CompiledStructs.TryGetValue(field.Type.Identifier.Content, out CompiledStruct fieldStructType))
                    {
                        field.Type.Identifier = field.Type.Identifier.Struct(fieldStructType);
                    }
                }
            }

            #endregion

            for (int i = 0; i < CompiledStructs.Length; i++)
            {
                for (int j = 0; j < CompiledStructs[i].Fields.Length; j++)
                {
                    CompiledStructs[i].Fields[j] = new CompiledField(((StructDefinition)CompiledStructs[i]).Fields[j])
                    {
                        Type = new CompiledType(((StructDefinition)CompiledStructs[i]).Fields[j].Type, GetCustomType),
                    };
                }
            }
            for (int i = 0; i < CompiledClasses.Length; i++)
            {
                if (CompiledClasses[i].TemplateInfo != null)
                {
                    GenericParameters.Push(CompiledClasses[i].TemplateInfo.TypeParameters);
                    foreach (Token typeParameter in CompiledClasses[i].TemplateInfo.TypeParameters)
                    { typeParameter.AnalysedType = TokenAnalysedType.TypeParameter; }
                }

                for (int j = 0; j < CompiledClasses[i].Fields.Length; j++)
                {
                    CompiledClasses[i].Fields[j] = new CompiledField(((ClassDefinition)CompiledClasses[i]).Fields[j])
                    {
                        Type = new CompiledType(((ClassDefinition)CompiledClasses[i]).Fields[j].Type, GetCustomType),
                        Class = CompiledClasses[i],
                    };
                }

                if (CompiledClasses[i].TemplateInfo != null)
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
                        { typeParameter.AnalysedType = TokenAnalysedType.TypeParameter; }
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
                            parameters.Insert(0, new ParameterDefinition()
                            {
                                Identifier = Token.CreateAnonymous("this"),
                                Type = TypeInstance.CreateAnonymous(compiledClass.Name.Content, TypeDefinitionReplacer),
                                Modifiers = new Token[1] { Token.CreateAnonymous("this") }
                            });
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
                        parameters.Insert(0, new ParameterDefinition()
                        {
                            Identifier = Token.CreateAnonymous("this"),
                            Type = TypeInstance.CreateAnonymous(compiledClass.Name.Content, TypeDefinitionReplacer),
                            Modifiers = new Token[1] { Token.CreateAnonymous("this") },
                        });
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
        /// <param name="code">
        /// The source code
        /// </param>
        /// <param name="file">
        /// The source code file
        /// </param>
        /// <param name="result">
        /// The codeGenerator result
        /// </param>
        /// <param name="warnings">
        /// A list that this can fill with warnings
        /// </param>
        /// <param name="errors">
        /// A list that this can fill with errors
        /// </param>
        /// <param name="printCallback">
        /// Optional: Print callback
        /// </param>
        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="CompilerException"/>
        /// <exception cref="Exception"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="System.Exception"/>
        public static Result Compile(ParserResult parserResult,
            Dictionary<string, ExternalFunctionBase> externalFunctions,
            FileInfo file,
            ParserSettings parserSettings,
            Action<string, Output.LogType> printCallback = null,
            string basePath = "")
        {
            Compiler compiler = new()
            {
                Functions = new List<FunctionDefinition>(),
                Operators = new List<FunctionDefinition>(),
                Structs = new List<StructDefinition>(),
                Classes = new List<ClassDefinition>(),
                Enums = new List<EnumDefinition>(),
                Hashes = new List<CompileTag>(),
                GenericParameters = new Stack<Token[]>(),

                ExternalFunctions = externalFunctions,

                Warnings = new List<Warning>(),
                Errors = new List<Error>(),
                // PrintCallback = printCallback,
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
    }
}