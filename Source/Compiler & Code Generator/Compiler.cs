using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace LanguageCore.Compiler
{
    using Parser;
    using Parser.Statement;
    using Runtime;
    using Tokenizing;

    public readonly struct BuiltinFunctionNames
    {
        public const string Destructor = "destructor";
        public const string IndexerGet = "indexer_get";
        public const string IndexerSet = "indexer_set";
    }

    public readonly struct CompilerResult
    {
        public readonly CompiledFunction[] Functions;
        public readonly MacroDefinition[] Macros;
        public readonly CompiledGeneralFunction[] GeneralFunctions;
        public readonly CompiledOperator[] Operators;
        public readonly CompiledConstructor[] Constructors;

        public readonly ExternalFunctionCollection ExternalFunctions;

        public readonly CompiledStruct[] Structs;
        public readonly CompileTag[] Hashes;
        public readonly CompiledEnum[] Enums;

        public readonly Statement[] TopLevelStatements;

        public readonly Uri? File;

        public readonly IEnumerable<Uri> Files
        {
            get
            {
                HashSet<Uri> alreadyExists = new();

                foreach (CompiledFunction function in Functions)
                {
                    Uri? file = function.FilePath;
                    if (file is not null && !alreadyExists.Contains(file))
                    {
                        alreadyExists.Add(file);
                        yield return file;
                    }
                }

                foreach (MacroDefinition macro in Macros)
                {
                    Uri? file = macro.FilePath;
                    if (file is not null && !alreadyExists.Contains(file))
                    {
                        alreadyExists.Add(file);
                        yield return file;
                    }
                }

                foreach (CompiledGeneralFunction generalFunction in GeneralFunctions)
                {
                    Uri? file = generalFunction.FilePath;
                    if (file is not null && !alreadyExists.Contains(file))
                    {
                        alreadyExists.Add(file);
                        yield return file;
                    }
                }

                foreach (CompiledOperator @operator in Operators)
                {
                    Uri? file = @operator.FilePath;
                    if (file is not null && !alreadyExists.Contains(file))
                    {
                        alreadyExists.Add(file);
                        yield return file;
                    }
                }

                foreach (CompiledStruct @struct in Structs)
                {
                    Uri? file = @struct.FilePath;
                    if (file is not null && !alreadyExists.Contains(file))
                    {
                        alreadyExists.Add(file);
                        yield return file;
                    }
                }

                foreach (CompiledEnum @enum in Enums)
                {
                    Uri? file = @enum.FilePath;
                    if (file is not null && !alreadyExists.Contains(file))
                    {
                        alreadyExists.Add(file);
                        yield return file;
                    }
                }
            }
        }

        public readonly IEnumerable<Statement> Statements
        {
            get
            {
                for (int i = 0; i < TopLevelStatements.Length; i++)
                { yield return TopLevelStatements[i]; }

                foreach (CompiledFunction function in Functions)
                {
                    if (function.Block != null) yield return function.Block;
                }

                foreach (CompiledGeneralFunction function in GeneralFunctions)
                {
                    if (function.Block != null) yield return function.Block;
                }

                foreach (MacroDefinition macro in Macros)
                {
                    yield return macro.Block;
                }

                foreach (CompiledOperator @operator in Operators)
                {
                    if (@operator.Block != null) yield return @operator.Block;
                }
            }
        }

        public readonly ParserResult AST => new(
            Enumerable.Empty<Error>(),
            Functions,
            Operators,
            Macros,
            Structs,
            Enumerable.Empty<UsingDefinition>(),
            Enumerable.Empty<CompileTag>(),
            TopLevelStatements,
            Enums);

        public static CompilerResult Empty => new(
            Array.Empty<CompiledFunction>(),
            Array.Empty<MacroDefinition>(),
            Array.Empty<CompiledGeneralFunction>(),
            Array.Empty<CompiledOperator>(),
            Array.Empty<CompiledConstructor>(),
            new ExternalFunctionCollection(),
            Array.Empty<CompiledStruct>(),
            Array.Empty<CompileTag>(),
            Array.Empty<CompiledEnum>(),
            Array.Empty<Statement>(),
            null);

        public CompilerResult(
            IEnumerable<CompiledFunction> functions,
            IEnumerable<MacroDefinition> macros,
            IEnumerable<CompiledGeneralFunction> generalFunctions,
            IEnumerable<CompiledOperator> operators,
            IEnumerable<CompiledConstructor> constructors,
            ExternalFunctionCollection externalFunctions,
            IEnumerable<CompiledStruct> structs,
            IEnumerable<CompileTag> hashes,
            IEnumerable<CompiledEnum> enums,
            IEnumerable<Statement> topLevelStatements,
            Uri? file)
        {
            Functions = functions.ToArray();
            Macros = macros.ToArray();
            GeneralFunctions = generalFunctions.ToArray();
            Operators = operators.ToArray();
            Constructors = constructors.ToArray();
            ExternalFunctions = externalFunctions;
            Structs = structs.ToArray();
            Hashes = hashes.ToArray();
            Enums = enums.ToArray();
            TopLevelStatements = topLevelStatements.ToArray();
            File = file;
        }

        public CompiledFunction? GetFunctionAt(Uri file, SinglePosition position)
        {
            for (int i = 0; i < Functions.Length; i++)
            {
                if (Functions[i].FilePath != file)
                { continue; }

                if (!Functions[i].Identifier.Position.Range.Contains(position))
                { continue; }

                return Functions[i];
            }
            return null;
        }

        public CompiledGeneralFunction? GetGeneralFunctionAt(Uri file, SinglePosition position)
        {
            for (int i = 0; i < GeneralFunctions.Length; i++)
            {
                if (GeneralFunctions[i].FilePath != file)
                { continue; }

                if (!GeneralFunctions[i].Identifier.Position.Range.Contains(position))
                { continue; }

                return GeneralFunctions[i];
            }
            return null;
        }

        public CompiledOperator? GetOperatorAt(Uri file, SinglePosition position)
        {
            for (int i = 0; i < Operators.Length; i++)
            {
                if (Operators[i].FilePath != file)
                { continue; }

                if (!Operators[i].Identifier.Position.Range.Contains(position))
                { continue; }

                return Operators[i];
            }
            return null;
        }

        public CompiledStruct? GetStructAt(Uri file, SinglePosition position)
        {
            for (int i = 0; i < Structs.Length; i++)
            {
                if (Structs[i].FilePath != file)
                { continue; }

                if (!Structs[i].Identifier.Position.Range.Contains(position))
                { continue; }

                return Structs[i];
            }
            return null;
        }

        public CompiledEnum? GetEnumAt(Uri file, SinglePosition position)
        {
            for (int i = 0; i < Enums.Length; i++)
            {
                if (Enums[i].FilePath != file)
                { continue; }

                if (!Enums[i].Identifier.Position.Range.Contains(position))
                { continue; }

                return Enums[i];
            }
            return null;
        }
    }

    public struct CompilerSettings
    {
        public string? BasePath;

        public CompilerSettings(CompilerSettings other)
        {
            BasePath = other.BasePath;
        }

        public static CompilerSettings Default => new()
        {
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
        CompiledStruct[] CompiledStructs;
        CompiledOperator[] CompiledOperators;
        CompiledConstructor[] CompiledConstructors;
        CompiledFunction[] CompiledFunctions;
        CompiledGeneralFunction[] CompiledGeneralFunctions;
        CompiledEnum[] CompiledEnums;

        readonly List<FunctionDefinition> Operators;
        readonly List<FunctionDefinition> Functions;
        readonly List<ConstructorDefinition> Constructors;
        readonly List<MacroDefinition> Macros;
        readonly List<StructDefinition> Structs;
        readonly List<EnumDefinition> Enums;

        readonly Stack<Token[]> GenericParameters;

        readonly List<CompileTag> Tags;

        readonly CompilerSettings Settings;
        readonly ExternalFunctionCollection ExternalFunctions;
        readonly PrintCallback? PrintCallback;

        readonly AnalysisCollection? AnalysisCollection;

        readonly Dictionary<string, (CompiledType ReturnValue, CompiledType[] Parameters)> BuiltinFunctions = new()
        {
            { "alloc", (CompiledType.Pointer(new CompiledType(Type.Integer)), [ new CompiledType(Type.Integer) ]) },
            { "free", (new CompiledType(Type.Void), [ CompiledType.Pointer(new CompiledType(Type.Integer)) ]) },
        };

        Compiler(ExternalFunctionCollection? externalFunctions, PrintCallback? printCallback, CompilerSettings settings, AnalysisCollection? analysisCollection)
        {
            Functions = new List<FunctionDefinition>();
            Macros = new List<MacroDefinition>();
            Operators = new List<FunctionDefinition>();
            Constructors = new List<ConstructorDefinition>();
            Structs = new List<StructDefinition>();
            Enums = new List<EnumDefinition>();
            Tags = new List<CompileTag>();
            GenericParameters = new Stack<Token[]>();

            CompiledStructs = Array.Empty<CompiledStruct>();
            CompiledOperators = Array.Empty<CompiledOperator>();
            CompiledConstructors = Array.Empty<CompiledConstructor>();
            CompiledFunctions = Array.Empty<CompiledFunction>();
            CompiledGeneralFunctions = Array.Empty<CompiledGeneralFunction>();
            CompiledEnums = Array.Empty<CompiledEnum>();

            ExternalFunctions = externalFunctions ?? new ExternalFunctionCollection();
            Settings = settings;
            PrintCallback = printCallback;
            AnalysisCollection = analysisCollection;
        }

        bool FindType(Token name, [NotNullWhen(true)] out CompiledType? result)
        {
            if (CodeGenerator.GetStruct(CompiledStructs, name.Content, out CompiledStruct? @struct))
            {
                result = new CompiledType(@struct);
                return true;
            }

            if (CodeGenerator.GetEnum(CompiledEnums, name.Content, out CompiledEnum? @enum))
            {
                result = new CompiledType(@enum);
                return true;
            }

            for (int i = 0; i < GenericParameters.Count; i++)
            {
                for (int j = 0; j < GenericParameters[i].Length; j++)
                {
                    if (GenericParameters[i][j].Content == name.Content)
                    {
                        GenericParameters[i][j].AnalyzedType = TokenAnalyzedType.TypeParameter;
                        result = CompiledType.CreateGeneric(GenericParameters[i][j].Content);
                        return true;
                    }
                }
            }

            if (CodeGenerator.GetFunction(CompiledFunctions, name, out CompiledFunction? function))
            {
                result = new CompiledType(new FunctionType(function));
                return true;
            }

            result = null;
            return false;
        }

        protected string? TypeDefinitionReplacer(string? typeName)
        {
            foreach (CompiledStruct @struct in CompiledStructs)
            {
                if (@struct.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                {
                    if (definedType == typeName)
                    {
                        return @struct.Identifier.Content;
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

        static CompiledType[] CompileTypes(ParameterDefinition[] parameters, FindType unknownTypeCallback)
        {
            CompiledType[] result = new CompiledType[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                result[i] = new CompiledType(parameters[i].Type, unknownTypeCallback);
                parameters[i].Type.SetAnalyzedType(result[i]);
            }
            return result;
        }

        CompiledStruct CompileStruct(StructDefinition @struct)
        {
            if (LanguageConstants.Keywords.Contains(@struct.Identifier.Content))
            { throw new CompilerException($"Illegal struct name '{@struct.Identifier.Content}'", @struct.Identifier, @struct.FilePath); }

            @struct.Identifier.AnalyzedType = TokenAnalyzedType.Struct;

            if (CodeGenerator.GetStruct(CompiledStructs, @struct.Identifier.Content, out _))
            { throw new CompilerException($"Struct with name '{@struct.Identifier.Content}' already exist", @struct.Identifier, @struct.FilePath); }

            CompiledAttributeCollection attributes = CompileAttributes(@struct.Attributes);

            return new CompiledStruct(attributes, new CompiledField[@struct.Fields.Length], @struct);
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

            CompiledType type = new(function.Type, FindType);
            function.Type.SetAnalyzedType(type);

            if (attributes.TryGetAttribute<string>("External", out string? externalName, out AttributeValues? attribute))
            {
                if (!ExternalFunctions.TryGetValue(externalName, out ExternalFunctionBase? externalFunction))
                { AnalysisCollection?.Errors.Add(new Error($"External function \"{externalName}\" not found", attribute.Value, function.FilePath)); }
                else
                {
                    if (externalFunction.ParameterCount != function.Parameters.Count)
                    { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ToReadable()}'", function.Identifier, function.FilePath); }
                    if (externalFunction.ReturnSomething != (type != Type.Void))
                    { throw new CompilerException($"Wrong type defined for function '{externalFunction.ToReadable()}'", function.Type, function.FilePath); }

                    for (int i = 0; i < externalFunction.ParameterTypes.Length; i++)
                    {
                        Type definedParameterType = externalFunction.ParameterTypes[i];
                        CompiledType passedParameterType = new(function.Parameters[i].Type, FindType);
                        function.Parameters[i].Type.SetAnalyzedType(passedParameterType);

                        if (passedParameterType == definedParameterType)
                        { continue; }

                        throw new CompilerException($"Wrong type of parameter passed to function \"{externalFunction.ToReadable()}\". Parameter index: {i} Required type: {definedParameterType} Passed: {passedParameterType}", function.Parameters[i].Type, function.FilePath);
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
                { AnalysisCollection?.Errors.Add(new Error($"Builtin function \"{builtinName}\" not found", attribute.Value, function.FilePath)); }
                else
                {
                    if (builtinFunction.Parameters.Length != function.Parameters.Count)
                    { throw new CompilerException($"Wrong number of parameters passed to function \"{builtinName}\"", function.Identifier, function.FilePath); }

                    if (builtinFunction.ReturnValue != type)
                    { throw new CompilerException($"Wrong type defined for function \"{builtinName}\"", function.Type, function.FilePath); }

                    for (int i = 0; i < builtinFunction.Parameters.Length; i++)
                    {
                        CompiledType definedParameterType = builtinFunction.Parameters[i];
                        CompiledType passedParameterType = new(function.Parameters[i].Type, FindType);
                        function.Parameters[i].Type.SetAnalyzedType(passedParameterType);

                        if (passedParameterType == definedParameterType)
                        { continue; }

                        if (passedParameterType.IsPointer && definedParameterType.IsPointer)
                        { continue; }

                        throw new CompilerException($"Wrong type of parameter passed to function \"{builtinName}\". Parameter index: {i} Required type: {definedParameterType} Passed: {passedParameterType}", function.Parameters[i].Type, function.FilePath);
                    }
                }
            }

            CompiledFunction result = new(
                type,
                CompileTypes(function.Parameters.ToArray(), FindType),
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

            CompiledType type = new(function.Type, FindType);
            function.Type.SetAnalyzedType(type);

            if (attributes.TryGetAttribute<string>("External", out string? name, out AttributeValues? attribute))
            {
                if (ExternalFunctions.TryGetValue(name, out ExternalFunctionBase? externalFunction))
                {
                    if (externalFunction.ParameterCount != function.Parameters.Count)
                    { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ToReadable()}'", function.Identifier, function.FilePath); }
                    if (externalFunction.ReturnSomething != (type != Type.Void))
                    { throw new CompilerException($"Wrong type defined for function '{externalFunction.ToReadable()}'", function.Type, function.FilePath); }

                    for (int i = 0; i < externalFunction.ParameterTypes.Length; i++)
                    {
                        if (LanguageConstants.BuiltinTypeMap3.TryGetValue(function.Parameters[i].Type.ToString(), out Type builtinType))
                        {
                            if (externalFunction.ParameterTypes[i] != builtinType)
                            { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ToReadable()}'. Parameter index: {i} Required type: {externalFunction.ParameterTypes[i]} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type, function.FilePath); }
                        }
                        else
                        { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ToReadable()}'. Parameter index: {i} Required type: {externalFunction.ParameterTypes[i]} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type, function.FilePath); }
                    }

                    return new CompiledOperator(type, externalFunction.ParameterTypes.Select(v => new CompiledType(v)).ToArray(), function)
                    {
                        CompiledAttributes = attributes,
                    };
                }

                AnalysisCollection?.Errors.Add(new Error($"External function \"{name}\" not found", attribute.Value.Identifier, function.FilePath));
            }

            return new CompiledOperator(
                type,
                CompileTypes(function.Parameters.ToArray(), FindType),
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
                CompileTypes(function.Parameters.ToArray(), FindType),
                function
                );
        }

        CompiledConstructor CompileConstructor(ConstructorDefinition function)
        {
            if (function.TemplateInfo != null)
            {
                GenericParameters.Push(function.TemplateInfo.TypeParameters);
                foreach (Token typeParameter in function.TemplateInfo.TypeParameters)
                { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
            }

            CompiledType type = new(function.Identifier, FindType);
            function.Identifier.SetAnalyzedType(type);

            CompiledConstructor result = new(
                type,
                CompileTypes(function.Parameters.ToArray(), FindType),
                function);

            if (function.TemplateInfo != null)
            { GenericParameters.Pop(); }

            return result;
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

                if (!CodeGenerator.TryComputeSimple(member.Value, out compiledMember.ComputedValue))
                { throw new CompilerException($"I can't compute this. The developer should make a better preprocessor for this case I think...", member.Value, @enum.FilePath); }

                compiledEnum.Members[i] = compiledMember;
            }

            return compiledEnum;
        }

        bool IsSymbolExists(string symbol, [NotNullWhen(true)] out Token? where)
        {
            foreach (StructDefinition @struct in Structs)
            {
                if (@struct.Identifier.Content == symbol)
                {
                    where = @struct.Identifier;
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
                if (Functions.Any(function.IsSame))
                { AnalysisCollection?.Errors.Add(new Error($"Function {function.ToReadable()} already defined", function.Identifier, function.FilePath)); continue; }

                Functions.Add(function);
            }

            foreach (FunctionDefinition @operator in collectedAST.ParserResult.Operators)
            {
                if (Operators.Any(@operator.IsSame))
                { AnalysisCollection?.Errors.Add(new Error($"Operator {@operator.ToReadable()} already defined", @operator.Identifier, @operator.FilePath)); continue; }

                Operators.Add(@operator);
            }

            foreach (MacroDefinition macro in collectedAST.ParserResult.Macros)
            {
                if (Macros.Any(macro.IsSame))
                { AnalysisCollection?.Errors.Add(new Error($"Macro {macro.ToReadable()} already defined", macro.Identifier, macro.FilePath)); continue; }

                Macros.Add(macro);
            }

            /*
            foreach (var func in collectedAST.ParserResult.Operators)
            {
                if (Operators.ContainsSameDefinition(func))
                { AnalysisCollection?.Errors.Add(new Error($"Operator '{func.ReadableID()}' already defined", func.Identifier)); continue; }

                Operators.Add(func);
            }
            */

            foreach (StructDefinition @struct in collectedAST.ParserResult.Structs)
            {
                if (IsSymbolExists(@struct.Identifier.Content, out _))
                { AnalysisCollection?.Errors.Add(new Error($"Symbol {@struct.Identifier} already defined", @struct.Identifier, @struct.FilePath)); continue; }
                else
                { Structs.Add(@struct); }
            }

            foreach (EnumDefinition @enum in collectedAST.ParserResult.Enums)
            {
                if (IsSymbolExists(@enum.Identifier.Content, out _))
                { AnalysisCollection?.Errors.Add(new Error($"Symbol {@enum.Identifier} already defined", @enum.Identifier, @enum.FilePath)); continue; }
                else
                { Enums.Add(@enum); }
            }

            Tags.AddRange(collectedAST.ParserResult.Hashes);
        }

        CompilerResult CompileMainFile(ParserResult parserResult, Uri? file)
        {
            Structs.AddRange(parserResult.Structs);
            Functions.AddRange(parserResult.Functions);
            Operators.AddRange(parserResult.Operators);
            Macros.AddRange(parserResult.Macros);
            Enums.AddRange(parserResult.Enums);

            if (file != null)
            {
                CollectorResult collectorResult = SourceCodeManager.Collect(parserResult.Usings, file, PrintCallback, Settings.BasePath, AnalysisCollection);

                for (int i = 0; i < collectorResult.CollectedASTs.Length; i++)
                { CompileFile(collectorResult.CollectedASTs[i]); }
            }

            CompileInternal();

            return new CompilerResult(
                CompiledFunctions,
                Macros.ToArray(),
                CompiledGeneralFunctions,
                CompiledOperators,
                CompiledConstructors,
                ExternalFunctions,
                CompiledStructs,
                Tags.ToArray(),
                CompiledEnums,
                parserResult.TopLevelStatements,
                file);
        }

        CompilerResult CompileInteractiveInternal(Statement statement, UsingDefinition[] usings)
        {
            CollectorResult collectorResult = SourceCodeManager.Collect(usings, (Uri?)null, PrintCallback, Settings.BasePath, AnalysisCollection);

            for (int i = 0; i < collectorResult.CollectedASTs.Length; i++)
            { CompileFile(collectorResult.CollectedASTs[i]); }

            CompileInternal();

            return new CompilerResult(
                CompiledFunctions,
                Macros.ToArray(),
                CompiledGeneralFunctions,
                CompiledOperators,
                CompiledConstructors,
                ExternalFunctions,
                CompiledStructs,
                Tags.ToArray(),
                CompiledEnums,
                [statement],
                null);
        }

        void CompileTags()
        {
            foreach (CompileTag tag in Tags)
            {
                switch (tag.HashName.Content)
                {
                    case "bf":
                    {
                        if (tag.Parameters.Length < 2)
                        { AnalysisCollection?.Errors.Add(new Error($"Hash '{tag.HashName}' requires minimum 2 parameter", tag.HashName, tag.FilePath)); break; }
                        string name = tag.Parameters[0].Value;

                        if (ExternalFunctions.ContainsKey(name)) break;

                        string[] bfParams = new string[tag.Parameters.Length - 1];
                        for (int i = 1; i < tag.Parameters.Length; i++)
                        { bfParams[i - 1] = tag.Parameters[i].Value; }

                        Type[] parameterTypes = new Type[bfParams.Length];
                        for (int i = 0; i < bfParams.Length; i++)
                        {
                            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(bfParams[i], out Type paramType))
                            {
                                parameterTypes[i] = paramType;

                                if (paramType == Type.Void && i > 0)
                                { AnalysisCollection?.Errors.Add(new Error($"Invalid type \"{bfParams[i]}\"", tag.Parameters[i + 1].ValueToken, tag.FilePath)); goto ExitBreak; }
                            }
                            else
                            {
                                AnalysisCollection?.Errors.Add(new Error($"Unknown type \"{bfParams[i]}\"", tag.Parameters[i + 1].ValueToken, tag.FilePath));
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
                        AnalysisCollection?.Warnings.Add(new Warning($"Hash \"{tag.HashName}\" does not exists, so this is ignored", tag.HashName, tag.FilePath));
                        break;
                }

ExitBreak:
                continue;
            }
        }

        void CompileOperators()
        {
            List<CompiledOperator> compiledOperators = new();

            foreach (FunctionDefinition @operator in Operators)
            {
                CompiledOperator compiledOperator = CompileOperator(@operator);

                if (compiledOperators.Any(compiledOperator.IsSame))
                { throw new CompilerException($"Operator {compiledOperator.ToReadable()} already defined", @operator.Identifier, @operator.FilePath); }

                compiledOperators.Add(compiledOperator);
            }

            CompiledOperators = compiledOperators.ToArray();
        }

        void CompileFunctions()
        {
            List<CompiledFunction> compiledFunctions = new();

            foreach (FunctionDefinition function in Functions)
            {
                CompiledFunction compiledFunction = CompileFunction(function);

                if (compiledFunctions.Any(compiledFunction.IsSame))
                { throw new CompilerException($"Function {compiledFunction.ToReadable()} already defined", function.Identifier, function.FilePath); }

                compiledFunctions.Add(compiledFunction);
            }

            CompiledFunctions = compiledFunctions.ToArray();
        }

        void CompileInternal()
        {
            CompileTags();

            CompiledEnums = Enums.Select(CompileEnum).ToArray();
            CompiledStructs = Structs.Select(CompileStruct).ToArray();

            for (int i = 0; i < CompiledStructs.Length; i++)
            {
                CompiledStruct @struct = CompiledStructs[i];
                if (@struct.TemplateInfo != null)
                {
                    GenericParameters.Push(@struct.TemplateInfo.TypeParameters);
                    foreach (Token typeParameter in @struct.TemplateInfo.TypeParameters)
                    { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
                }

                for (int j = 0; j < @struct.Fields.Length; j++)
                {
                    FieldDefinition field = ((StructDefinition)@struct).Fields[j];
                    CompiledField newField = new(new CompiledType(field.Type, FindType), null, field);
                    field.Type.SetAnalyzedType(newField.Type);
                    @struct.Fields[j] = newField;
                }

                if (@struct.TemplateInfo != null)
                { GenericParameters.Pop(); }
            }

            CompileOperators();
            CompileFunctions();

            List<CompiledFunction> compiledFunctions = new(CompiledFunctions);
            List<CompiledGeneralFunction> compiledGeneralFunctions = new(CompiledGeneralFunctions);
            List<CompiledConstructor> compiledConstructors = new(CompiledConstructors);

            {
                foreach (CompiledStruct compiledStruct in CompiledStructs)
                {
                    if (compiledStruct.TemplateInfo != null)
                    {
                        GenericParameters.Push(compiledStruct.TemplateInfo.TypeParameters);
                        foreach (Token typeParameter in compiledStruct.TemplateInfo.TypeParameters)
                        { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
                    }

                    foreach (GeneralFunctionDefinition method in compiledStruct.GeneralMethods)
                    {
                        foreach (ParameterDefinition parameter in method.Parameters)
                        {
                            if (parameter.Modifiers.Contains("this"))
                            { throw new CompilerException($"Keyword 'this' is not valid in the current context", parameter.Identifier, compiledStruct.FilePath); }
                        }

                        CompiledType returnType = new(compiledStruct);

                        if (method.Identifier.Content == BuiltinFunctionNames.Destructor)
                        {
                            GeneralFunctionDefinition copy = method.Duplicate();

                            List<ParameterDefinition> parameters = copy.Parameters.ToList();
                            parameters.Insert(0,
                                new ParameterDefinition(
                                    new Token[] { Token.CreateAnonymous("ref"), Token.CreateAnonymous("this") },
                                    TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, compiledStruct.TemplateInfo?.TypeParameters),
                                    Token.CreateAnonymous("this"))
                                );
                            copy.Parameters = new ParameterDefinitionCollection(parameters, copy.Parameters.LeftParenthesis, copy.Parameters.RightParenthesis);
                            returnType = new CompiledType(Type.Void);

                            CompiledGeneralFunction methodWithRef = CompileGeneralFunction(copy, returnType);
                            methodWithRef.Context = compiledStruct;

                            copy = method.Duplicate();

                            parameters = copy.Parameters.ToList();
                            parameters.Insert(0,
                                new ParameterDefinition(
                                    new Token[] { Token.CreateAnonymous("this") },
                                    TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, compiledStruct.TemplateInfo?.TypeParameters)),
                                    Token.CreateAnonymous("this"))
                                );
                            copy.Parameters = new ParameterDefinitionCollection(parameters, copy.Parameters.LeftParenthesis, copy.Parameters.RightParenthesis);

                            CompiledGeneralFunction methodWithPointer = CompileGeneralFunction(copy, returnType);
                            methodWithPointer.Context = compiledStruct;

                            if (compiledGeneralFunctions.Any(methodWithRef.IsSame))
                            { throw new CompilerException($"Function with name '{methodWithRef.ToReadable()}' already defined", copy.Identifier, compiledStruct.FilePath); }

                            if (compiledGeneralFunctions.Any(methodWithPointer.IsSame))
                            { throw new CompilerException($"Function with name '{methodWithPointer.ToReadable()}' already defined", copy.Identifier, compiledStruct.FilePath); }

                            compiledGeneralFunctions.Add(methodWithRef);
                            compiledGeneralFunctions.Add(methodWithPointer);
                        }
                        else
                        {
                            List<ParameterDefinition> parameters = method.Parameters.ToList();

                            CompiledGeneralFunction methodWithRef = CompileGeneralFunction(method, returnType);
                            methodWithRef.Context = compiledStruct;

                            if (compiledGeneralFunctions.Any(methodWithRef.IsSame))
                            { throw new CompilerException($"Function with name '{methodWithRef.ToReadable()}' already defined", method.Identifier, compiledStruct.FilePath); }

                            compiledGeneralFunctions.Add(methodWithRef);
                        }
                    }

                    foreach (FunctionDefinition method in compiledStruct.Methods)
                    {
                        foreach (ParameterDefinition parameter in method.Parameters)
                        {
                            if (parameter.Modifiers.Contains("this"))
                            { throw new CompilerException($"Keyword 'this' is not valid in the current context", parameter.Identifier, compiledStruct.FilePath); }
                        }

                        FunctionDefinition copy = method.Duplicate();

                        List<ParameterDefinition> parameters = copy.Parameters.ToList();
                        parameters.Insert(0,
                            new ParameterDefinition(
                                new Token[] { Token.CreateAnonymous("ref"), Token.CreateAnonymous("this") },
                                TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, compiledStruct.TemplateInfo?.TypeParameters),
                                Token.CreateAnonymous("this"))
                            );
                        copy.Parameters = new ParameterDefinitionCollection(parameters, copy.Parameters.LeftParenthesis, copy.Parameters.RightParenthesis);

                        CompiledFunction methodWithRef = CompileFunction(copy);

                        copy = method.Duplicate();

                        parameters = copy.Parameters.ToList();
                        parameters.Insert(0,
                            new ParameterDefinition(
                                new Token[] { Token.CreateAnonymous("this") },
                                TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, compiledStruct.TemplateInfo?.TypeParameters)),
                                Token.CreateAnonymous("this"))
                            );
                        copy.Parameters = new ParameterDefinitionCollection(parameters, copy.Parameters.LeftParenthesis, copy.Parameters.RightParenthesis);

                        CompiledFunction methodWithPointer = CompileFunction(copy);

                        if (compiledFunctions.Any(methodWithRef.IsSame))
                        { throw new CompilerException($"Function with name '{methodWithRef.ToReadable()}' already defined", copy.Identifier, compiledStruct.FilePath); }

                        if (compiledFunctions.Any(methodWithPointer.IsSame))
                        { throw new CompilerException($"Function with name '{methodWithPointer.ToReadable()}' already defined", copy.Identifier, compiledStruct.FilePath); }

                        methodWithRef.Context = compiledStruct;
                        methodWithPointer.Context = compiledStruct;
                        compiledFunctions.Add(methodWithRef);
                        compiledFunctions.Add(methodWithPointer);
                    }

                    foreach (ConstructorDefinition constructor in compiledStruct.Constructors)
                    {
                        foreach (ParameterDefinition parameter in constructor.Parameters)
                        {
                            if (parameter.Modifiers.Contains("this"))
                            { throw new CompilerException($"Keyword 'this' is not valid in the current context", parameter.Identifier, compiledStruct.FilePath); }
                        }

                        List<ParameterDefinition> parameters = constructor.Parameters.ToList();
                        parameters.Insert(0,
                            new ParameterDefinition(
                                new Token[] { Token.CreateAnonymous("this") },
                                constructor.Identifier,
                                Token.CreateAnonymous("this"))
                            );
                        constructor.Parameters = new ParameterDefinitionCollection(parameters, constructor.Parameters.LeftParenthesis, constructor.Parameters.RightParenthesis);

                        CompiledConstructor methodInfo = CompileConstructor(constructor);

                        if (compiledConstructors.Any(methodInfo.IsSame))
                        { throw new CompilerException($"Constructor with name '{methodInfo.ToReadable()}' already defined", constructor.Identifier, compiledStruct.FilePath); }

                        methodInfo.Context = compiledStruct;
                        compiledConstructors.Add(methodInfo);
                    }

                    if (compiledStruct.TemplateInfo != null)
                    { GenericParameters.Pop(); }
                }
            }

            CompiledFunctions = compiledFunctions.ToArray();
            CompiledGeneralFunctions = compiledGeneralFunctions.ToArray();
            CompiledConstructors = compiledConstructors.ToArray();
        }

        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="CompilerException"/>
        /// <exception cref="LanguageException"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="Exception"/>
        public static CompilerResult Compile(
            ParserResult parserResult,
            ExternalFunctionCollection? externalFunctions,
            Uri? file,
            CompilerSettings settings,
            PrintCallback? printCallback = null,
            AnalysisCollection? analysisCollection = null)
        {
            Compiler compiler = new(externalFunctions, printCallback, settings, analysisCollection);
            return compiler.CompileMainFile(parserResult, file);
        }
        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="CompilerException"/>
        /// <exception cref="LanguageException"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="Exception"/>
        public static CompilerResult CompileFile(
            FileInfo file,
            ExternalFunctionCollection? externalFunctions,
            CompilerSettings settings,
            PrintCallback? printCallback = null,
            AnalysisCollection? analysisCollection = null)
        {
            ParserResult ast = Parser.ParseFile(file.FullName);
            Compiler compiler = new(externalFunctions, printCallback, settings, analysisCollection);
            return compiler.CompileMainFile(ast, new Uri(file.FullName, UriKind.Absolute));
        }

        public static CompilerResult CompileInteractive(
            Statement statement,
            ExternalFunctionCollection? externalFunctions,
            CompilerSettings settings,
            UsingDefinition[] usings,
            PrintCallback? printCallback = null,
            AnalysisCollection? analysisCollection = null)
            => new Compiler(externalFunctions, printCallback, settings, analysisCollection).CompileInteractiveInternal(statement, usings);
    }
}