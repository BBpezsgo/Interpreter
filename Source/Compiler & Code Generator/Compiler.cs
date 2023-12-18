using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace LanguageCore.Compiler
{
    using BBCode.Generator;
    using Parser;
    using Parser.Statement;
    using Runtime;
    using Tokenizing;

    public readonly struct BuiltinFunctionNames
    {
        public const string Destructor = "destructor";
        public const string Cloner = "clone";
        public const string Constructor = "constructor";
        public const string IndexerGet = "indexer_get";
        public const string IndexerSet = "indexer_set";
    }

    public readonly struct CompilerResult
    {
        public readonly CompiledFunction[] Functions;
        public readonly MacroDefinition[] Macros;
        public readonly CompiledGeneralFunction[] GeneralFunctions;
        public readonly CompiledOperator[] Operators;

        public readonly Dictionary<string, ExternalFunctionBase> ExternalFunctions;

        public readonly CompiledStruct[] Structs;
        public readonly CompiledClass[] Classes;
        public readonly CompileTag[] Hashes;
        public readonly CompiledEnum[] Enums;

        public readonly Error[] Errors;
        public readonly Warning[] Warnings;
        public readonly Statement[] TopLevelStatements;

        public readonly FileInfo? File;

        public static CompilerResult Empty => new(
            Array.Empty<CompiledFunction>(),
            Array.Empty<MacroDefinition>(),
            Array.Empty<CompiledGeneralFunction>(),
            Array.Empty<CompiledOperator>(),
            new Dictionary<string, ExternalFunctionBase>(),
            Array.Empty<CompiledStruct>(),
            Array.Empty<CompiledClass>(),
            Array.Empty<CompileTag>(),
            Array.Empty<CompiledEnum>(),
            Array.Empty<Error>(),
            Array.Empty<Warning>(),
            Array.Empty<Statement>(),
            null);

        public CompilerResult(
            CompiledFunction[] functions,
            MacroDefinition[] macros,
            CompiledGeneralFunction[] generalFunctions,
            CompiledOperator[] operators,
            Dictionary<string, ExternalFunctionBase> externalFunctions,
            CompiledStruct[] structs,
            CompiledClass[] classes,
            CompileTag[] hashes,
            CompiledEnum[] enums,
            Error[] errors,
            Warning[] warnings,
            Statement[] topLevelStatements,
            FileInfo? file)
        {
            Functions = functions;
            Macros = macros;
            GeneralFunctions = generalFunctions;
            Operators = operators;
            ExternalFunctions = externalFunctions;
            Structs = structs;
            Classes = classes;
            Hashes = hashes;
            Enums = enums;
            Errors = errors;
            Warnings = warnings;
            TopLevelStatements = topLevelStatements;
            File = file;
        }
    }

    public struct CompilerSettings
    {
        public bool GenerateComments;
        public int RemoveUnusedFunctionsMaxIterations;
        public bool PrintInstructions;
        public bool DontOptimize;
        public bool GenerateDebugInstructions;
        public bool ExternalFunctionsCache;
        public bool CheckNullPointers;
        public string? BasePath;

        public static CompilerSettings Default => new()
        {
            GenerateComments = true,
            RemoveUnusedFunctionsMaxIterations = 10,
            PrintInstructions = false,
            DontOptimize = false,
            GenerateDebugInstructions = true,
            ExternalFunctionsCache = true,
            CheckNullPointers = true,
            BasePath = null,
        };
    }

    public enum CompileLevel
    {
        Minimal,
        Exported,
        All,
    }

    public class Compiler
    {
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

        readonly string? BasePath;
        readonly Dictionary<string, ExternalFunctionBase> ExternalFunctions;
        readonly PrintCallback? PrintCallback;

        readonly Dictionary<string, (CompiledType ReturnValue, CompiledType[] Parameters)> BuiltinFunctions = new()
        {
            { "alloc", (new CompiledType(Type.Integer), [ new CompiledType(Type.Integer) ]) },
            { "free", (new CompiledType(Type.Void), [ new CompiledType(Type.Integer) ]) },
        };

        Compiler(Dictionary<string, ExternalFunctionBase>? externalFunctions, PrintCallback? printCallback, string? basePath)
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

            ExternalFunctions = externalFunctions ?? new Dictionary<string, ExternalFunctionBase>();
            BasePath = basePath;
            PrintCallback = printCallback;
        }

        CompiledType GetCustomType(string name)
        {
            for (int i = 0; i < GenericParameters.Count; i++)
            {
                for (int j = 0; j < GenericParameters[i].Length; j++)
                {
                    if (GenericParameters[i][j].Content == name)
                    {
                        GenericParameters[i][j].AnalyzedType = TokenAnalyzedType.TypeParameter;
                        return CompiledType.CreateGeneric(GenericParameters[i][j].Content);
                    }
                }
            }

            if (CodeGenerator.GetStruct(CompiledStructs, name, out CompiledStruct? @struct)) return new CompiledType(@struct);
            if (CodeGenerator.GetClass(CompiledClasses, name, out CompiledClass? @class)) return new CompiledType(@class);
            if (CodeGenerator.GetEnum(CompiledEnums, name, out CompiledEnum? @enum)) return new CompiledType(@enum);

            throw new InternalException($"Type \"{name}\" not found");
        }

        protected string? TypeDefinitionReplacer(string? typeName)
        {
            foreach (CompiledStruct @struct in CompiledStructs)
            {
                if (@struct.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                {
                    if (definedType == typeName)
                    {
                        return @struct.Name.Content;
                    }
                }
            }
            foreach (CompiledClass @class in CompiledClasses)
            {
                if (@class.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                {
                    if (definedType == typeName)
                    {
                        return @class.Name.Content;
                    }
                }
            }
            foreach (CompiledEnum @enum in CompiledEnums)
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

        static CompiledAttributeCollection CompileAttributes(AttributeUsage[] attributes)
        {
            CompiledAttributeCollection result = new();

            for (int i = 0; i < attributes.Length; i++)
            {
                AttributeUsage attribute = attributes[i];

                attribute.Identifier.AnalyzedType = TokenAnalyzedType.Attribute;

                AttributeValues newAttribute = new()
                {
                    parameters = new(),
                    Identifier = attribute.Identifier,
                };

                if (attribute.Parameters != null)
                {
                    for (int j = 0; j < attribute.Parameters.Length; j++)
                    { newAttribute.parameters.Add(new CompiledLiteral(attribute.Parameters[j])); }
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
            if (LanguageConstants.Keywords.Contains(@struct.Name.Content))
            { throw new CompilerException($"Illegal struct name '{@struct.Name.Content}'", @struct.Name, @struct.FilePath); }

            @struct.Name.AnalyzedType = TokenAnalyzedType.Struct;

            if (CodeGenerator.GetStruct(CompiledStructs, @struct.Name.Content, out _))
            { throw new CompilerException($"Struct with name '{@struct.Name.Content}' already exist", @struct.Name, @struct.FilePath); }

            CompiledAttributeCollection attributes = CompileAttributes(@struct.Attributes);

            return new CompiledStruct(attributes, new CompiledField[@struct.Fields.Length], @struct);
        }

        CompiledClass CompileClass(ClassDefinition @class)
        {
            if (LanguageConstants.Keywords.Contains(@class.Name.Content))
            { throw new CompilerException($"Illegal class name '{@class.Name.Content}'", @class.Name, @class.FilePath); }

            @class.Name.AnalyzedType = TokenAnalyzedType.Class;

            if (CodeGenerator.GetClass(CompiledClasses, @class.Name.Content, out _))
            { throw new CompilerException($"Class with name '{@class.Name.Content}' already exist", @class.Name, @class.FilePath); }

            CompiledAttributeCollection attributes = CompileAttributes(@class.Attributes);

            return new CompiledClass(attributes, new CompiledField[@class.Fields.Length], @class);
        }

        CompiledFunction CompileFunction(FunctionDefinition function)
        {
            CompiledAttributeCollection attributes = CompileAttributes(function.Attributes);

            if (function.TemplateInfo != null)
            {
                GenericParameters.Push(function.TemplateInfo.TypeParameters);
                foreach (Token typeParameter in function.TemplateInfo.TypeParameters)
                { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
            }

            CompiledType type = new(function.Type, GetCustomType);
            function.Type.SetAnalyzedType(type);

            if (attributes.TryGetAttribute<string>("External", out string? externalName, out AttributeValues? attribute))
            {
                if (!ExternalFunctions.TryGetValue(externalName, out ExternalFunctionBase? externalFunction))
                { Errors.Add(new Error($"External function \"{externalName}\" not found", attribute.Value, function.FilePath)); }
                else
                {
                    if (externalFunction.ParameterCount != function.Parameters.Count)
                    { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ID}'", function.Identifier, function.FilePath); }
                    if (externalFunction.ReturnSomething != (type != Type.Void))
                    { throw new CompilerException($"Wrong type defined for function '{externalFunction.ID}'", function.Type, function.FilePath); }

                    for (int i = 0; i < externalFunction.ParameterTypes.Length; i++)
                    {
                        Type definedParameterType = externalFunction.ParameterTypes[i];
                        CompiledType passedParameterType = new(function.Parameters[i].Type, GetCustomType);

                        if (passedParameterType == definedParameterType)
                        { continue; }

                        if (passedParameterType.IsClass && definedParameterType == Type.Integer)
                        { continue; }

                        throw new CompilerException($"Wrong type of parameter passed to function \"{externalFunction.ID}\". Parameter index: {i} Required type: {definedParameterType.ToString().ToLowerInvariant()} Passed: {passedParameterType}", function.Parameters[i].Type, function.FilePath);
                    }

                    if (function.TemplateInfo != null)
                    { GenericParameters.Pop(); }

                    return new CompiledFunction(type, externalFunction.ParameterTypes.Select(v => new CompiledType(v)).ToArray(), function)
                    {
                        CompiledAttributes = attributes,
                    };
                }
            }

            if (attributes.TryGetAttribute<string>("Builtin", out string? builtinName, out attribute))
            {
                if (!BuiltinFunctions.TryGetValue(builtinName, out (CompiledType ReturnValue, CompiledType[] Parameters) builtinFunction))
                { Errors.Add(new Error($"Builtin function \"{builtinName}\" not found", attribute.Value, function.FilePath)); }
                else
                {
                    if (builtinFunction.Parameters.Length != function.Parameters.Count)
                    { throw new CompilerException($"Wrong number of parameters passed to function \"{builtinName}\"", function.Identifier, function.FilePath); }

                    if (builtinFunction.ReturnValue != type)
                    { throw new CompilerException($"Wrong type defined for function \"{builtinName}\"", function.Type, function.FilePath); }

                    for (int i = 0; i < builtinFunction.Parameters.Length; i++)
                    {
                        CompiledType definedParameterType = builtinFunction.Parameters[i];
                        CompiledType passedParameterType = new(function.Parameters[i].Type, GetCustomType);

                        if (passedParameterType == definedParameterType)
                        { continue; }

                        if (passedParameterType.IsClass && definedParameterType == Type.Integer)
                        { continue; }

                        throw new CompilerException($"Wrong type of parameter passed to function \"{builtinName}\". Parameter index: {i} Required type: {definedParameterType.ToString().ToLowerInvariant()} Passed: {passedParameterType}", function.Parameters[i].Type, function.FilePath);
                    }
                }
            }

            CompiledFunction result = new(
                type,
                CompileTypes(function.Parameters.ToArray(), GetCustomType),
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
            CompiledAttributeCollection attributes = CompileAttributes(function.Attributes);

            CompiledType type = new(function.Type, GetCustomType);
            function.Type.SetAnalyzedType(type);

            if (attributes.TryGetAttribute<string>("External", out string? name, out AttributeValues? attribute))
            {
                if (ExternalFunctions.TryGetValue(name, out ExternalFunctionBase? externalFunction))
                {
                    if (externalFunction.ParameterCount != function.Parameters.Count)
                    { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ID}'", function.Identifier, function.FilePath); }
                    if (externalFunction.ReturnSomething != (type != Type.Void))
                    { throw new CompilerException($"Wrong type defined for function '{externalFunction.ID}'", function.Type, function.FilePath); }

                    for (int i = 0; i < externalFunction.ParameterTypes.Length; i++)
                    {
                        if (LanguageConstants.BuiltinTypeMap3.TryGetValue(function.Parameters[i].Type.ToString(), out Type builtinType))
                        {
                            if (externalFunction.ParameterTypes[i] != builtinType)
                            { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ID}'. Parameter index: {i} Required type: {externalFunction.ParameterTypes[i].ToString().ToLowerInvariant()} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type, function.FilePath); }
                        }
                        else
                        { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ID}'. Parameter index: {i} Required type: {externalFunction.ParameterTypes[i].ToString().ToLowerInvariant()} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type, function.FilePath); }
                    }

                    return new CompiledOperator(type, externalFunction.ParameterTypes.Select(v => new CompiledType(v)).ToArray(), function)
                    {
                        CompiledAttributes = attributes,
                    };
                }

                Errors.Add(new Error($"External function \"{name}\" not found", attribute.Value.Identifier, function.FilePath));
            }

            return new CompiledOperator(
                type,
                CompileTypes(function.Parameters.ToArray(), GetCustomType),
                function
                )
            {
                CompiledAttributes = attributes,
            };
        }

        CompiledGeneralFunction CompileGeneralFunction(GeneralFunctionDefinition function, CompiledType returnType)
        {
            return new CompiledGeneralFunction(
                returnType,
                CompileTypes(function.Parameters.ToArray(), GetCustomType),
                function
                );
        }

        static CompiledEnum CompileEnum(EnumDefinition @enum)
        {
            CompiledAttributeCollection attributes = new();

            foreach (AttributeUsage attribute in @enum.Attributes)
            {
                attribute.Identifier.AnalyzedType = TokenAnalyzedType.Attribute;

                AttributeValues newAttribute = new()
                { parameters = new() };

                if (attribute.Parameters != null)
                {
                    foreach (Literal parameter in attribute.Parameters)
                    {
                        newAttribute.parameters.Add(new CompiledLiteral(parameter));
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

                if (!CodeGenerator.TryComputeSimple(member.Value, null, out compiledMember.ComputedValue))
                { throw new CompilerException($"I can't compute this. The developer should make a better preprocessor for this case I think...", member.Value, @enum.FilePath); }

                compiledEnum.Members[i] = compiledMember;
            }

            return compiledEnum;
        }

        bool IsSymbolExists(string symbol, [NotNullWhen(true)] out Token? where)
        {
            foreach (ClassDefinition @class in Classes)
            {
                if (@class.Name.Content == symbol)
                {
                    where = @class.Name;
                    return true;
                }
            }
            foreach (StructDefinition @struct in Structs)
            {
                if (@struct.Name.Content == symbol)
                {
                    where = @struct.Name;
                    return true;
                }
            }
            foreach (EnumDefinition @enum in Enums)
            {
                if (@enum.Identifier.Content == symbol)
                {
                    where = @enum.Identifier;
                    return true;
                }
            }
            foreach (FunctionDefinition function in this.Functions)
            {
                if (function.Identifier.Content == symbol)
                {
                    where = function.Identifier;
                    return true;
                }
            }
            foreach (MacroDefinition macro in this.Macros)
            {
                if (macro.Identifier.Content == symbol)
                {
                    where = macro.Identifier;
                    return true;
                }
            }
            where = null;
            return false;
        }

        void CompileFile(CollectedAST collectedAST)
        {
            foreach (FunctionDefinition function in collectedAST.ParserResult.Functions)
            {
                if (Functions.Any(other => function.IsSame(other)))
                { Errors.Add(new Error($"Function {function.ReadableID()} already defined", function.Identifier, function.FilePath)); continue; }

                Functions.Add(function);
            }

            foreach (MacroDefinition macro in collectedAST.ParserResult.Macros)
            {
                if (Macros.Any(other => macro.IsSame(other)))
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

            foreach (StructDefinition @struct in collectedAST.ParserResult.Structs)
            {
                if (IsSymbolExists(@struct.Name.Content, out _))
                { Errors.Add(new Error($"Symbol {@struct.Name} already defined", @struct.Name, @struct.FilePath)); continue; }
                else
                { Structs.Add(@struct); }
            }

            foreach (ClassDefinition @class in collectedAST.ParserResult.Classes)
            {
                if (IsSymbolExists(@class.Name.Content, out _))
                { Errors.Add(new Error($"Symbol {@class.Name} already defined", @class.Name, @class.FilePath)); continue; }
                else
                { Classes.Add(@class); }

                foreach (FunctionDefinition @operator in @class.Operators)
                {
                    if (Operators.Any(other => @operator.IsSame(other)))
                    { Errors.Add(new Error($"Operator {@operator.ReadableID()} already defined", @operator.Identifier, @operator.FilePath)); continue; }
                    else
                    { Operators.Add(@operator); }
                }
            }

            foreach (EnumDefinition @enum in collectedAST.ParserResult.Enums)
            {
                if (IsSymbolExists(@enum.Identifier.Content, out _))
                { Errors.Add(new Error($"Symbol {@enum.Identifier} already defined", @enum.Identifier, @enum.FilePath)); continue; }
                else
                { Enums.Add(@enum); }
            }

            Hashes.AddRange(collectedAST.ParserResult.Hashes);
        }

        CompilerResult CompileMainFile(ParserResult parserResult, FileInfo? file)
        {
            Structs.AddRange(parserResult.Structs);
            Classes.AddRange(parserResult.Classes);
            Functions.AddRange(parserResult.Functions);
            Macros.AddRange(parserResult.Macros);

            CollectorResult collectorResult;
            if (file != null)
            {
                collectorResult = SourceCodeManager.Collect(parserResult.Usings, file, PrintCallback, BasePath);

                this.Warnings.AddRange(collectorResult.Warnings);
                this.Errors.AddRange(collectorResult.Errors);
            }
            else
            {
                collectorResult = CollectorResult.Empty;
            }

            CompileInternal(collectorResult);

            return new CompilerResult(
                CompiledFunctions,
                Macros.ToArray(),
                CompiledGeneralFunctions,
                CompiledOperators,
                ExternalFunctions,
                CompiledStructs,
                CompiledClasses,
                Hashes.ToArray(),
                CompiledEnums,
                Errors.ToArray(),
                Warnings.ToArray(),
                parserResult.TopLevelStatements,
                file);
        }

        CompilerResult CompileInteractiveInternal(Statement statement, UsingDefinition[] usings)
        {
            CollectorResult collectorResult = SourceCodeManager.Collect(usings, null, PrintCallback, BasePath);

            this.Warnings.AddRange(collectorResult.Warnings);
            this.Errors.AddRange(collectorResult.Errors);

            CompileInternal(collectorResult);

            return new CompilerResult(
                CompiledFunctions,
                Macros.ToArray(),
                CompiledGeneralFunctions,
                CompiledOperators,
                ExternalFunctions,
                CompiledStructs,
                CompiledClasses,
                Hashes.ToArray(),
                CompiledEnums,
                Errors.ToArray(),
                Warnings.ToArray(),
                [statement],
                null);
        }

        void CompileInternal(CollectorResult collectorResult)
        {
            for (int i = 0; i < collectorResult.CollectedASTs.Length; i++)
            { CompileFile(collectorResult.CollectedASTs[i]); }

            #region Compile test external functions

            foreach (CompileTag hash in Hashes)
            {
                switch (hash.HashName.Content)
                {
                    case "bf":
                    {
                        if (hash.Parameters.Length < 2)
                        { Errors.Add(new Error($"Hash '{hash.HashName}' requires minimum 2 parameter", hash.HashName, hash.FilePath)); break; }
                        string name = hash.Parameters[0].Value;

                        if (ExternalFunctions.ContainsKey(name)) break;

                        string[] bfParams = new string[hash.Parameters.Length - 1];
                        for (int i = 1; i < hash.Parameters.Length; i++)
                        { bfParams[i - 1] = hash.Parameters[i].Value; }

                        Type[] parameterTypes = new Type[bfParams.Length];
                        for (int i = 0; i < bfParams.Length; i++)
                        {
                            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(bfParams[i], out Type paramType))
                            {
                                parameterTypes[i] = paramType;

                                if (paramType == Type.Void && i > 0)
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

                        if (parameterTypes[0] == Type.Void)
                        {
                            ExternalFunctions.AddSimpleExternalFunction(name, pTypes, (BytecodeProcessor sender, DataItem[] p) =>
                            {
                                Output.LogDebug($"External function \"{name}\" called with params:\n  {string.Join(", ", p)}");
                            });
                        }
                        else
                        {
                            ExternalFunctions.AddSimpleExternalFunction(name, pTypes, (BytecodeProcessor sender, DataItem[] p) =>
                            {
                                Output.LogDebug($"External function \"{name}\" called with params:\n  {string.Join(", ", p)}");
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
                    { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
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

                foreach (FunctionDefinition function in Operators)
                {
                    CompiledOperator compiledFunction = CompileOperator(function);

                    if (compiledOperators.Any(other => compiledFunction.IsSame(other)))
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
                        { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
                    }

                    foreach (GeneralFunctionDefinition method in compiledClass.GeneralMethods)
                    {
                        foreach (ParameterDefinition parameter in method.Parameters)
                        {
                            if (parameter.Modifiers.Contains("this"))
                            { throw new CompilerException($"Keyword 'this' is not valid in the current context", parameter.Identifier, compiledClass.FilePath); }
                        }

                        CompiledType returnType = new(compiledClass);

                        if (method.Identifier.Content == "destructor")
                        {
                            List<ParameterDefinition> parameters = method.Parameters.ToList();
                            parameters.Insert(0,
                                new ParameterDefinition(
                                    new Token[1] { Token.CreateAnonymous("this") },
                                    TypeInstanceSimple.CreateAnonymous(compiledClass.Name.Content, compiledClass.TemplateInfo?.TypeParameters, TypeDefinitionReplacer),
                                    Token.CreateAnonymous("this"))
                                );
                            method.Parameters = new ParameterDefinitionCollection(parameters, method.Parameters.LeftParenthesis, method.Parameters.RightParenthesis);
                            returnType = new CompiledType(Type.Void);
                        }

                        CompiledGeneralFunction methodInfo = CompileGeneralFunction(method, returnType);

                        if (compiledGeneralFunctions.Any(other => methodInfo.IsSame(other)))
                        { throw new CompilerException($"Function with name '{methodInfo.ReadableID()}' already defined", method.Identifier, compiledClass.FilePath); }

                        methodInfo.Context = compiledClass;
                        compiledGeneralFunctions.Add(methodInfo);
                    }

                    foreach (FunctionDefinition method in compiledClass.Methods)
                    {
                        foreach (ParameterDefinition parameter in method.Parameters)
                        {
                            if (parameter.Modifiers.Contains("this"))
                            { throw new CompilerException($"Keyword 'this' is not valid in the current context", parameter.Identifier, compiledClass.FilePath); }
                        }
                        List<ParameterDefinition> parameters = method.Parameters.ToList();
                        parameters.Insert(0,
                            new ParameterDefinition(
                                new Token[1] { Token.CreateAnonymous("this") },
                                TypeInstanceSimple.CreateAnonymous(compiledClass.Name.Content, compiledClass.TemplateInfo?.TypeParameters, TypeDefinitionReplacer),
                                Token.CreateAnonymous("this"))
                            );
                        method.Parameters = new ParameterDefinitionCollection(parameters, method.Parameters.LeftParenthesis, method.Parameters.RightParenthesis);

                        CompiledFunction methodInfo = CompileFunction(method);

                        if (compiledFunctions.Any(other => methodInfo.IsSame(other)))
                        { throw new CompilerException($"Function with name '{methodInfo.ReadableID()}' already defined", method.Identifier, compiledClass.FilePath); }

                        methodInfo.Context = compiledClass;
                        compiledFunctions.Add(methodInfo);
                    }

                    if (compiledClass.TemplateInfo != null)
                    { GenericParameters.Pop(); }
                }

                foreach (FunctionDefinition function in Functions)
                {
                    CompiledFunction compiledFunction = CompileFunction(function);

                    if (compiledFunctions.Any(other => compiledFunction.IsSame(other)))
                    { throw new CompilerException($"Function with name '{compiledFunction.ReadableID()}' already defined", function.Identifier, function.FilePath); }

                    compiledFunctions.Add(compiledFunction);
                }

                this.CompiledFunctions = compiledFunctions.ToArray();
                this.CompiledGeneralFunctions = compiledGeneralFunctions.ToArray();
            }

            #endregion
        }

        /// <summary>
        /// Does some checks and prepares the AST for the <see cref="CodeGeneratorForMain"/>
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
        public static CompilerResult Compile(
            ParserResult parserResult,
            Dictionary<string, ExternalFunctionBase>? externalFunctions,
            FileInfo? file,
            string? basePath,
            PrintCallback? printCallback = null)
            => new Compiler(externalFunctions, printCallback, basePath).CompileMainFile(parserResult, file);

        public static CompilerResult CompileInteractive(
            Statement statement,
            Dictionary<string, ExternalFunctionBase>? externalFunctions,
            string? basePath,
            UsingDefinition[] usings,
            PrintCallback? printCallback = null)
            => new Compiler(externalFunctions, printCallback, basePath).CompileInteractiveInternal(statement, usings);
    }
}