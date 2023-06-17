using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IngameCoding.BBCode.Compiler
{
    using Bytecode;

    using Core;

    using DataUtilities.Serializer;

    using Errors;

    using IngameCoding.BBCode.Analysis;

    using Parser;
    using Parser.Statements;

    public class Compiler
    {
        public class CompilerResult : ISerializable<CompilerResult>
        {
            public Instruction[] compiledCode { get; set; }

            public CompiledFunction[] compiledFunctions { get; set; }
            public CompiledStruct[] compiledStructs { get; set; }

            public Dictionary<string, int> functionOffsets { get; set; }
            internal DebugInfo[] debugInfo { get; set; }

            public CompilerResult()
            {

            }

            public bool GetFunctionOffset(FunctionDefinition functionCallStatement, out int functionOffset)
                => functionOffsets.TryGetValue(functionCallStatement.Identifier.Content, out functionOffset);

            internal void WriteToConsole()
            {
                Console.WriteLine("\n\r === INSTRUCTIONS ===\n\r");
                int indent = 0;

                for (int i = 0; i < this.compiledCode.Length; i++)
                {
                    Instruction instruction = this.compiledCode[i];
                    if (instruction.opcode == Opcode.COMMENT)
                    {
                        if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("}"))
                        {
                            indent--;
                        }

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"{"  ".Repeat(indent)}{instruction.tag}");
                        Console.ResetColor();

                        if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("{"))
                        {
                            indent++;
                        }

                        continue;
                    }

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write($"{"  ".Repeat(indent)} {instruction.opcode}");
                    Console.Write($" ");

                    if (instruction.Parameter.type == RuntimeType.INT)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.Parameter.ValueInt}");
                        Console.Write($" ");
                    }
                    else if (instruction.Parameter.type == RuntimeType.FLOAT)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.Parameter.ValueFloat}");
                        Console.Write($" ");
                    }
                    else if (instruction.Parameter.type == RuntimeType.BOOLEAN)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write($"{instruction.Parameter.ValueBoolean}");
                        Console.Write($" ");
                    }
                    else if (instruction.Parameter.type == RuntimeType.STRING)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"\"{instruction.Parameter.ToString().Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n")}\"");
                        Console.Write($" ");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{instruction.Parameter}");
                        Console.Write($" ");
                    }

                    if (!string.IsNullOrEmpty(instruction.tag))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"{instruction.tag}");
                    }

                    Console.Write("\n\r");

                    Console.ResetColor();
                }

                Console.WriteLine("\n\r === ===\n\r");
            }

            void ISerializable<CompilerResult>.Serialize(Serializer serializer)
            {
                serializer.Serialize(compiledCode);
                serializer.Serialize(functionOffsets);
            }

            void ISerializable<CompilerResult>.Deserialize(Deserializer deserializer)
            {
                this.compiledCode = deserializer.DeserializeObjectArray<Instruction>();
                this.functionOffsets = deserializer.DeserializeDictionary<string, int>();
            }
        }

        public struct CompilerSettings
        {
            public bool GenerateComments;
            public byte RemoveUnusedFunctionsMaxIterations;
            public bool PrintInstructions;
            public bool DontOptimize;
            public bool GenerateDebugInstructions;

            public static CompilerSettings Default => new()
            {
                GenerateComments = true,
                RemoveUnusedFunctionsMaxIterations = 4,
                PrintInstructions = false,
                DontOptimize = false,
                GenerateDebugInstructions = true,
            };
        }

        public enum CompileLevel
        {
            Minimal,
            Exported,
            All,
        }

        static readonly string[] Keywords = new string[]
        {
            "struct",
            "class",

            "void",
            "namespace",
            "using",

            "byte",
            "int",
            "bool",
            "float",
            "string"
        };

        List<Error> Errors;
        List<Warning> Warnings;

        Action<string, Output.LogType> PrintCallback;

        CompiledClass[] CompiledClasses;
        CompiledStruct[] CompiledStructs;
        CompiledFunction[] CompiledFunctions;
        CompiledGeneralFunction[] CompiledGeneralFunctions;

        Dictionary<string, FunctionDefinition> Functions;
        Dictionary<string, StructDefinition> Structs;
        Dictionary<string, ClassDefinition> Classes;

        List<Statement_HashInfo> Hashes;

        Dictionary<string, BuiltinFunction> BuiltinFunctions;

        ITypeDefinition GetCustomType(string name)
        {
            if (CompiledStructs.ContainsKey(name)) return CompiledStructs.Get<string, ITypeDefinition>(name);
            if (CompiledClasses.ContainsKey(name)) return CompiledClasses.Get<string, ITypeDefinition>(name);

            throw new InternalException($"Unknown type '{name}'");
        }

        public struct Result
        {
            public CompiledFunction[] Functions;
            public CompiledGeneralFunction[] GeneralFunctions;

            public Dictionary<string, BuiltinFunction> BuiltinFunctions;

            public CompiledStruct[] Structs;
            public CompiledClass[] Classes;
            public Statement_HashInfo[] Hashes;

            public Error[] Errors;
            public Warning[] Warnings;
            public Statement[] TopLevelStatements;
        }

        CompiledStruct CompileStruct(StructDefinition @struct)
        {
            @struct.Name.Analysis.CompilerReached = true;

            if (Keywords.Contains(@struct.Name.Content))
            { throw new CompilerException($"Illegal struct name '{@struct.Name.Content}'", @struct.Name, @struct.FilePath); }

            @struct.Name.Analysis.SubSubtype = TokenSubSubtype.Struct;

            if (CompiledStructs.ContainsKey(@struct.Name.Content))
            { throw new CompilerException($"Struct with name '{@struct.Name.Content}' already exist", @struct.Name, @struct.FilePath); }

            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in @struct.Attributes)
            {
                attribute.Identifier.Analysis.CompilerReached = true;
                attribute.Identifier.Analysis.SubSubtype = TokenSubSubtype.Attribute;

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

            foreach (var field in @struct.Fields)
            {
                field.Identifier.Analysis.CompilerReached = true;
                field.Type.Analysis.CompilerReached = true;
            }

            return new CompiledStruct(attributes, new CompiledField[@struct.Fields.Length], @struct)
            {
                References = new List<DefinitionReference>(),
            };
        }

        CompiledClass CompileClass(ClassDefinition @class)
        {
            @class.Name.Analysis.CompilerReached = true;

            if (Keywords.Contains(@class.Name.Content))
            { throw new CompilerException($"Illegal class name '{@class.Name.Content}'", @class.Name, @class.FilePath); }

            @class.Name.Analysis.SubSubtype = TokenSubSubtype.Struct;

            if (CompiledClasses.ContainsKey(@class.Name.Content))
            { throw new CompilerException($"Class with name '{@class.Name.Content}' already exist", @class.Name, @class.FilePath); }

            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in @class.Attributes)
            {
                attribute.Identifier.Analysis.CompilerReached = true;
                attribute.Identifier.Analysis.SubSubtype = TokenSubSubtype.Attribute;

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

            foreach (var field in @class.Fields)
            {
                field.Identifier.Analysis.CompilerReached = true;
                field.Type.Analysis.CompilerReached = true;
                if (field.ProtectionToken != null) field.ProtectionToken.Analysis.CompilerReached = true;
            }

            return new CompiledClass(attributes, new CompiledField[@class.Fields.Length], @class)
            {
                References = new List<DefinitionReference>(),
            };
        }

        CompiledFunction CompileFunction(FunctionDefinition function)
        {
            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in function.Attributes)
            {
                attribute.Identifier.Analysis.CompilerReached = true;
                attribute.Identifier.Analysis.SubSubtype = TokenSubSubtype.Attribute;

                AttributeValues newAttribute = new()
                {
                    parameters = new(),
                    Identifier = attribute.Identifier,
                };

                if (attribute.Parameters != null)
                {
                    foreach (var parameter in attribute.Parameters)
                    {
                        newAttribute.parameters.Add(new Literal(parameter));
                    }
                }

                attributes.Add(attribute.Identifier.Content, newAttribute);
            }

            CompiledType type = new(function.Type, name => GetCustomType(name));

            if (attributes.TryGetValue("Builtin", out var attributeBuiltin))
            {
                if (attributeBuiltin.parameters.Count != 1)
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.Identifier, function.FilePath); }
                if (attributeBuiltin.TryGetValue(0, out string paramBuiltinName))
                {
                    foreach (var builtinFunction in BuiltinFunctions)
                    {
                        if (builtinFunction.Key.ToLower() == paramBuiltinName.ToLower())
                        {
                            if (builtinFunction.Value.ParameterCount != function.Parameters.Length)
                            { throw new CompilerException("Wrong number of parameters passed to builtin function '" + builtinFunction.Key + "'", function.Identifier, function.FilePath); }
                            if (builtinFunction.Value.ReturnSomething != (function.Type.Type != TypeTokenType.VOID))
                            { throw new CompilerException("Wrong type definied for builtin function '" + builtinFunction.Key + "'", function.Type, function.FilePath); }

                            for (int i = 0; i < builtinFunction.Value.ParameterTypes.Length; i++)
                            {
                                if (builtinFunction.Value.ParameterTypes[i] == BuiltinType.ANY) continue;

                                if (builtinFunction.Value.ParameterTypes[i] != function.Parameters[i].Type.Type.Convert())
                                { throw new CompilerException("Wrong type of parameter passed to builtin function '" + builtinFunction.Key + $"'. Parameter index: {i} Requied type: {builtinFunction.Value.ParameterTypes[i].ToString().ToLower()} Passed: {function.Parameters[i].Type.Type.ToString().ToLower()}", function.Parameters[i].Type, function.FilePath); }
                            }

                            return new CompiledFunction(function.ID(), type, function)
                            {
                                ParameterTypes = builtinFunction.Value.ParameterTypes.Select(v => new CompiledType(v, GetCustomType)).ToArray(),
                                CompiledAttributes = attributes,
                                References = new List<DefinitionReference>(),
                            };
                        }
                    }

                    Errors.Add(new Error("Builtin function '" + paramBuiltinName.ToLower() + "' not found", attributeBuiltin.Identifier, function.FilePath));
                    return new CompiledFunction(
                        function.ID(),
                        type,
                        function.Parameters.Select(v => new CompiledType(v.Type, t => GetCustomType(t))).ToArray(),
                        function
                        )
                    {
                        CompiledAttributes = attributes,
                        References = new List<DefinitionReference>(),
                    };
                }
                else
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.Identifier, function.FilePath); }
            }

            return new CompiledFunction(
                function.ID(),
                type,
                function.Parameters.Select(v => new CompiledType(v.Type, t => GetCustomType(t))).ToArray(),
                function
                )
            {
                CompiledAttributes = attributes,
                References = new List<DefinitionReference>(),
            };
        }

        CompiledGeneralFunction CompileGeneralFunction(GeneralFunctionDefinition function, CompiledType baseType)
        {
            return new CompiledGeneralFunction(
                baseType,
                function.Parameters.Select(v => new CompiledType(v.Type, t => GetCustomType(t))).ToArray(),
                function
                )
            {
                References = new List<DefinitionReference>(),
            };
        }

        void CompileFile(SourceCodeManager.CollectedAST collectedAST)
        {
            foreach (var func in collectedAST.ParserResult.Functions)
            {
                var id = func.ID();

                if (Functions.ContainsKey(id))
                { Errors.Add(new Error($"Function '{id}' already exists", func.Identifier)); continue; }

                Functions.Add(id, func);
            }

            foreach (var @struct in collectedAST.ParserResult.Structs)
            {
                if (Classes.ContainsKey(@struct.Key) || Structs.ContainsKey(@struct.Key))
                { Errors.Add(new Error($"Type '{@struct.Value.Name.Content}' already exists", @struct.Value.Name)); }
                else
                { Structs.Add(@struct.Key, @struct.Value); }
            }

            foreach (var @class in collectedAST.ParserResult.Classes)
            {
                if (Classes.ContainsKey(@class.Key) || Structs.ContainsKey(@class.Key))
                { Errors.Add(new Error($"Type '{@class.Value.Name.Content}' already exists", @class.Value.Name)); }
                else
                { Classes.Add(@class.Key, @class.Value); }
            }

            Hashes.AddRange(collectedAST.ParserResult.Hashes);
        }

        Result CompileMainFile(
            ParserResult parserResult,
            Dictionary<string, BuiltinFunction> builtinFunctions,
            FileInfo file,
            ParserSettings parserSettings,
            Action<string, Output.LogType> printCallback,
            string basePath)
        {
            Structs.AddRange(parserResult.Structs);
            Classes.AddRange(parserResult.Classes);
            Functions.AddRange(parserResult.Functions, v => v.ID());

            SourceCodeManager.Result collectorResult = SourceCodeManager.Collect(parserResult, file, parserSettings, printCallback, basePath);

            this.Warnings.AddRange(collectorResult.Warnings);
            this.Errors.AddRange(collectorResult.Errors);

            for (int i = 0; i < collectorResult.CollectedASTs.Length; i++)
            { CompileFile(collectorResult.CollectedASTs[i]); }

            #region Compile test built-in functions

            foreach (var hash in Hashes)
            {
                switch (hash.HashName.Content)
                {
                    case "bf":
                        {
                            if (hash.Parameters.Length < 2)
                            { Errors.Add(new Error($"Hash '{hash.HashName}' requies minimum 2 parameter", hash.HashName, hash.FilePath)); break; }
                            string bfName = hash.Parameters[0].Value;

                            if (builtinFunctions.ContainsKey(bfName)) break;

                            string[] bfParams = new string[hash.Parameters.Length - 1];
                            for (int i = 1; i < hash.Parameters.Length; i++)
                            { bfParams[i - 1] = hash.Parameters[i].Value; }

                            BuiltinType[] parameterTypes = new BuiltinType[bfParams.Length];
                            for (int i = 0; i < bfParams.Length; i++)
                            {
                                switch (bfParams[i])
                                {
                                    case "void":
                                        if (i > 0)
                                        { Errors.Add(new Error($"Invalid type \"{bfParams[i]}\"", hash.Parameters[i + 1].ValueToken, hash.FilePath)); goto ExitBreak; }
                                        parameterTypes[i] = BuiltinType.VOID;
                                        break;
                                    case "int":
                                        parameterTypes[i] = BuiltinType.INT;
                                        break;
                                    case "float":
                                        parameterTypes[i] = BuiltinType.FLOAT;
                                        break;
                                    case "bool":
                                        parameterTypes[i] = BuiltinType.BOOLEAN;
                                        break;
                                    case "byte":
                                        parameterTypes[i] = BuiltinType.BYTE;
                                        break;
                                    case "char":
                                        parameterTypes[i] = BuiltinType.CHAR;
                                        break;
                                    default:
                                        Errors.Add(new Error($"Unknown type \"{bfParams[i]}\"", hash.Parameters[i + 1].ValueToken, hash.FilePath));
                                        goto ExitBreak;
                                }
                            }

                            var returnType = parameterTypes[0];
                            var x = parameterTypes.ToList();
                            x.RemoveAt(0);
                            var pTypes = x.ToArray();

                            if (parameterTypes[0] == BuiltinType.VOID)
                            {
                                builtinFunctions.AddBuiltinFunction(bfName, pTypes, (DataItem[] p) =>
                                {
                                    Output.Output.Debug($"Built-in function \"{bfName}\" called with params:\n  {string.Join(", ", p)}");
                                });
                            }
                            else
                            {
                                builtinFunctions.AddBuiltinFunction(bfName, pTypes, (DataItem[] p) =>
                                {
                                    Output.Output.Debug($"Built-in function \"{bfName}\" called with params:\n  {string.Join(", ", p)}");
                                    DataItem returnValue = returnType switch
                                    {
                                        BuiltinType.INT => new DataItem((int)0),
                                        BuiltinType.BYTE => new DataItem((byte)0),
                                        BuiltinType.FLOAT => new DataItem((float)0),
                                        BuiltinType.BOOLEAN => new DataItem((bool)false),
                                        BuiltinType.CHAR => new DataItem((char)'\0'),
                                        _ => throw new RuntimeException($"Invalid return type \"{returnType}\"/{returnType.ToString().ToLower()} from built-in function \"{bfName}\""),
                                    };
                                    returnValue.Tag = "return value";
                                    return (returnValue);
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
            for (int i = Classes.Count - 1; i >= 0; i--)
            { this.CompiledClasses[i] = CompileClass(Classes.ElementAt(i).Value); }

            #endregion

            #region Compile Structs

            this.CompiledStructs = new CompiledStruct[Structs.Count];
            for (int i = Structs.Count - 1; i >= 0; i--)
            { this.CompiledStructs[i] = CompileStruct(Structs.ElementAt(i).Value); }

            #endregion

            #region Analyse Struct Fields

            foreach (var @struct in Structs)
            {
                foreach (var field in @struct.Value.Fields)
                {
                    if (CompiledStructs.TryGetValue(field.Type.Content, out CompiledStruct fieldStructType))
                    {
                        field.Type = field.Type.Struct(fieldStructType);
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
                for (int j = 0; j < CompiledClasses[i].Fields.Length; j++)
                {
                    CompiledClasses[i].Fields[j] = new CompiledField(((ClassDefinition)CompiledClasses[i]).Fields[j])
                    {
                        Type = new CompiledType(((ClassDefinition)CompiledClasses[i]).Fields[j].Type, GetCustomType),
                        Class = CompiledClasses[i],
                    };
                }
            }

            #region Set DataStructure Sizes

            foreach (var @struct in CompiledStructs)
            {
                int size = 0;
                foreach (var field in @struct.Fields)
                {
                    size++;
                }
                @struct.Size = size;
            }
            foreach (var @class in CompiledClasses)
            {
                int size = 0;
                foreach (var field in @class.Fields)
                {
                    size += field.Type.SizeOnStack;
                }
                @class.Size = size;
            }

            #endregion

            #region Set Field Offsets

            foreach (var @struct in CompiledStructs)
            {
                int currentOffset = 0;
                foreach (var field in @struct.Fields)
                {
                    @struct.FieldOffsets.Add(field.Identifier.Content, currentOffset);
                    switch (field.Type.BuiltinType)
                    {
                        case CompiledType.CompiledTypeType.BYTE:
                        case CompiledType.CompiledTypeType.INT:
                        case CompiledType.CompiledTypeType.FLOAT:
                        case CompiledType.CompiledTypeType.BOOL:
                        case CompiledType.CompiledTypeType.CHAR:
                            currentOffset++;
                            break;
                        default: throw new NotImplementedException();
                    }
                }
            }
            foreach (var @class in CompiledClasses)
            {
                int currentOffset = 0;
                foreach (var field in @class.Fields)
                {
                    @class.FieldOffsets.Add(field.Identifier.Content, currentOffset);
                    switch (field.Type.BuiltinType)
                    {
                        case CompiledType.CompiledTypeType.BYTE:
                        case CompiledType.CompiledTypeType.INT:
                        case CompiledType.CompiledTypeType.FLOAT:
                        case CompiledType.CompiledTypeType.BOOL:
                        case CompiledType.CompiledTypeType.CHAR:
                            currentOffset++;
                            break;
                        default: throw new NotImplementedException();
                    }
                }
            }

            #endregion

            #region Compile Functions

            {
                List<CompiledFunction> compiledFunctions = new();
                List<CompiledGeneralFunction> compiledGeneralFunctions = new();

                foreach (CompiledClass compiledClass in CompiledClasses)
                {
                    foreach (var method in compiledClass.GeneralMethods)
                    {
                        foreach (var parameter in method.Parameters)
                        {
                            if (parameter.withThisKeyword)
                            { throw new CompilerException($"Keyword 'this' is not valid in the current context", parameter.Identifier, compiledClass.FilePath); }
                        }

                        if (method.Identifier.Content == "destructor")
                        {
                            List<ParameterDefinition> parameters = method.Parameters.ToList();
                            parameters.Insert(0, new ParameterDefinition()
                            {
                                Identifier = Token.CreateAnonymous("this"),
                                Type = TypeToken.CreateAnonymous(compiledClass.Name.Content, BuiltinType.STRUCT),
                                withThisKeyword = true,
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
                            if (parameter.withThisKeyword)
                            { throw new CompilerException($"Keyword 'this' is not valid in the current context", parameter.Identifier, compiledClass.FilePath); }
                        }
                        List<ParameterDefinition> parameters = method.Parameters.ToList();
                        parameters.Insert(0, new ParameterDefinition()
                        {
                            Identifier = Token.CreateAnonymous("this"),
                            Type = TypeToken.CreateAnonymous(compiledClass.Name.Content, BuiltinType.STRUCT),
                            withThisKeyword = true,
                        });
                        method.Parameters = parameters.ToArray();

                        CompiledFunction methodInfo = CompileFunction(method);

                        if (compiledFunctions.ContainsSameDefinition(methodInfo))
                        { throw new CompilerException($"Function with name '{methodInfo.ReadableID}' already defined", method.Identifier, compiledClass.FilePath); }

                        methodInfo.Context = compiledClass;
                        compiledFunctions.Add(methodInfo);
                    }
                }

                foreach (var function in Functions.Values)
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
                GeneralFunctions = this.CompiledGeneralFunctions,
                BuiltinFunctions = builtinFunctions,
                Classes = this.CompiledClasses,
                Structs = this.CompiledStructs,
                Hashes = this.Hashes.ToArray(),
                TopLevelStatements = parserResult.TopLevelStatements,

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
            Dictionary<string, BuiltinFunction> builtinFunctions,
            FileInfo file,
            ParserSettings parserSettings,
            Action<string, Output.LogType> printCallback = null,
            string basePath = "")
        {
            Compiler compiler = new()
            {
                Functions = new Dictionary<string, FunctionDefinition>(),
                Structs = new Dictionary<string, StructDefinition>(),
                Classes = new Dictionary<string, ClassDefinition>(),
                Hashes = new List<Statement_HashInfo>(),

                BuiltinFunctions = builtinFunctions,
                Warnings = new List<Warning>(),
                Errors = new List<Error>(),
                PrintCallback = printCallback,
            };
            return compiler.CompileMainFile(
                parserResult,
                builtinFunctions,
                file,
                parserSettings,
                printCallback,
                basePath
                );
        }
    }
}