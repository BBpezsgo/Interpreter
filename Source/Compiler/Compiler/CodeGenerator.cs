using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode.Compiler
{
    using IngameCoding.BBCode.Parser;
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;
    using IngameCoding.Errors;

    public struct Reference<T>
    {
        public string FilePath;
        public T Ref;
        public bool IsAnything;

        public Reference(T @ref)
        {
            this.FilePath = string.Empty;
            this.Ref = @ref;
            this.IsAnything = true;
        }

        public override string ToString()
        {
            return IsAnything ? $"Ref<{(Ref == null ? "?" : Ref.GetType().Name)}> {{ \"{FilePath}\" {(Ref == null ? "Ref is null" : "Have ref")} }}" : $"Ref<?> null";
        }
    }

    public enum TokenSubSubtype
    {
        None,
        Attribute,
        Type,
        Struct,
        Keyword,
        FunctionName,
        VariableName,
        FieldName,
        ParameterName,
        Namespace,
    }

    readonly struct CleanupItem
    {
        /// <summary>
        /// The actual data size on the stack
        /// </summary>
        internal readonly int Size;
        /// <summary>
        /// The element count
        /// </summary>
        internal readonly int Count;

        public CleanupItem(int size, int count)
        {
            Size = size;
            Count = count;
        }
    }

    public class DebugInfo
    {
        internal Position Position;
        internal int InstructionStart;
        internal int InstructionEnd;
    }

    public class CodeGenerator
    {
        static readonly string[] BuiltinFunctions = new string[]
        {
            "return",
            "break",
            "type",
        };
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

        #region Fields

        bool BlockCodeGeneration;

        internal List<CompiledStruct> compiledStructs;
        internal List<CompiledClass> compiledClasses;
        internal List<CompiledFunction> compiledFunctions;
        internal List<KeyValuePair<string, CompiledVariable>> compiledVariables;
        List<CompiledParameter> parameters;

        internal Stack<CleanupItem> cleanupStack;

        Dictionary<string, BuiltinFunction> builtinFunctions;

        List<int> returnInstructions;
        List<List<int>> breakInstructions;
        string[] CurrentNamespace;
        /// <summary>
        /// Namespace.Namespace. ... .Namespace.
        /// </summary>
        string CurrentNamespaceText
        {
            get
            {
                if (CurrentNamespace == null) return "";
                if (CurrentNamespace.Length == 0) return "";
                return string.Join('.', CurrentNamespace) + ".";
            }
        }

        List<Instruction> compiledCode;

        List<UndefinedFunctionOffset> undefinedFunctionOffsets;

        bool OptimizeCode;
        bool AddCommentsToCode = true;
        readonly bool TrimUnreachableCode = true;
        bool GenerateDebugInstructions = true;
        Compiler.CompileLevel CompileLevel;
        internal string CurrentFunction_ForRecursionProtection;

        public List<Error> errors;
        public List<Warning> warnings;
        internal List<Information> informations;
        public List<Hint> hints;
        string CurrentFile;
        bool SaveDefinitionReferences;
        internal readonly List<DebugInfo> GeneratedDebugInfo = new();

        #endregion

        #region Helper Functions

        /// <returns>
        /// <list type="bullet">
        /// <item><see cref="CompiledStruct"/></item>
        /// <item><see cref="CompiledClass"/></item>
        /// <item><see langword="null"/> if <paramref name="returnNull"/> is set to <see langword="true"/></item>
        /// </list>
        /// </returns>
        /// <exception cref="InternalException"></exception>
        ITypeDefinition GetCustomType(string name, string targetNamespace = null, bool returnNull = false)
        {
            List<string> checkThese = new()
            {
                CurrentNamespaceText + name,
                name,
            };

            if (!string.IsNullOrEmpty(targetNamespace))
            {
                checkThese.Insert(0, targetNamespace + name);
                checkThese.Insert(0, CurrentNamespaceText + targetNamespace + name);
            }

            foreach (var checkThis in checkThese)
            {
                if (compiledStructs.ContainsKey(checkThis)) return compiledStructs.Get<string, ITypeDefinition>(checkThis);
                if (compiledClasses.ContainsKey(checkThis)) return compiledClasses.Get<string, ITypeDefinition>(checkThis);
            }

            if (returnNull) return null;

            throw new InternalException($"Unknown type '{name}'");
        }

        string GetReadableID(Statement_FunctionCall functionCall)
        {
            string readableID = functionCall.TargetNamespacePathPrefix + functionCall.FunctionName;
            readableID += "(";
            bool addComma = false;
            if (functionCall.IsMethodCall && functionCall.PrevStatement != null)
            {
                readableID += "this " + FindStatementType(functionCall.PrevStatement);
                addComma = true;
            }
            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                if (addComma) { readableID += ", "; }
                readableID += FindStatementType(functionCall.Parameters[i]);
                addComma = true;
            }
            readableID += ")";
            return readableID;
        }

        bool GetCompiledVariable(string variableName, out CompiledVariable compiledVariable) => compiledVariables.TryGetValue(variableName, out compiledVariable);

        bool GetParameter(string parameterName, out CompiledParameter parameters)
        {
            for (int i = 0; i < this.parameters.Count; i++)
            {
                if (this.parameters[i].Identifier.Content == parameterName)
                {
                    parameters = this.parameters[i];
                    return true;
                }
            }
            parameters = null;
            return false;
        }

        bool GetCompiledFunction(Statement_FunctionCall functionCallStatement, out CompiledFunction compiledFunction)
        {
            compiledFunction = null;
            string callID = "";

            string namespacePath = TryGetFunctionNamespacePath(functionCallStatement);
            if (namespacePath != "")
            {
                if (namespacePath.EndsWith(".")) namespacePath = namespacePath[..^1];
                functionCallStatement.TargetNamespacePathPrefix = namespacePath;
                functionCallStatement.PrevStatement = null;

                for (int i = 0; i < functionCallStatement.Parameters.Length; i++)
                { callID += "," + FindStatementType(functionCallStatement.Parameters[i]); }
            }
            else
            {
                if (functionCallStatement.PrevStatement != null)
                { callID += "," + FindStatementType(functionCallStatement.PrevStatement); }
                for (int i = 0; i < functionCallStatement.Parameters.Length; i++)
                { callID += "," + FindStatementType(functionCallStatement.Parameters[i]); }
            }

            List<string> searchForThese = new()
            {
                functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID,
                functionCallStatement.NamespacePathPrefix + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID
            };

            foreach (var searchName in searchForThese)
            {
                if (compiledFunctions.TryGetValue(searchName, out compiledFunction))
                { return true; }
            }

            return false;
        }

        string TryGetFunctionNamespacePath(Statement_FunctionCall functionCallStatement)
        {
            string[] Get(Statement statement)
            {
                if (statement is Statement_Variable s1)
                {
                    if (GetParameter(s1.VariableName.Content, out _))
                    {
                        return null;
                    }
                    else if (GetCompiledVariable(s1.VariableName.Content, out _))
                    {
                        return null;
                    }
                    s1.VariableName.Analysis.CompilerReached = true;
                    s1.VariableName.Analysis.SubSubtype = TokenSubSubtype.Namespace;
                    return new string[] { s1.VariableName.Content };
                }
                if (statement is Statement_Field s2)
                {
                    var prev_ = Get(s2.PrevStatement);
                    if (prev_ == null) { return null; }

                    s2.FieldName.Analysis.CompilerReached = true;
                    s2.FieldName.Analysis.SubSubtype = TokenSubSubtype.Namespace;

                    var prev = prev_.ToList();
                    prev.Insert(0, s2.FieldName.Content);
                    return prev.ToArray();
                }
                return null;
            }

            if (functionCallStatement.PrevStatement != null)
            {
                string[] path = Get(functionCallStatement.PrevStatement);
                if (path == null) return "";
                if (!functionCallStatement.TargetNamespacePathPrefixIsReversed)
                {
                    functionCallStatement.TargetNamespacePathPrefixIsReversed = true;
                    List<string> pathReversed = path.ToList();
                    pathReversed.Reverse();
                    path = pathReversed.ToArray();
                }
                return string.Join(".", path) + ".";
            }
            else
            {
                return "";
            }
        }

        /// <exception cref="ArgumentNullException"></exception>
        bool GetCompiledStruct(Statement_NewInstance newStructStatement, out CompiledStruct compiledStruct)
        {
            if (compiledStructs.TryGetValue(newStructStatement.TypeName.Content, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.NamespacePathPrefix + newStructStatement.TypeName.Content, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.NamespacePathPrefix + newStructStatement.TargetNamespacePathPrefix + newStructStatement.TypeName.Content, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.TargetNamespacePathPrefix + newStructStatement.TypeName.Content, out compiledStruct))
            {
                return true;
            }
            return false;
        }

        /// <exception cref="ArgumentNullException"></exception>
        bool GetCompiledClass(Statement_NewInstance newClassStatement, out CompiledClass compiledClass)
        {
            if (compiledClasses.TryGetValue(newClassStatement.TypeName.Content, out compiledClass))
            {
                return true;
            }
            else if (compiledClasses.TryGetValue(newClassStatement.NamespacePathPrefix + newClassStatement.TypeName.Content, out compiledClass))
            {
                return true;
            }
            else if (compiledClasses.TryGetValue(newClassStatement.NamespacePathPrefix + newClassStatement.TargetNamespacePathPrefix + newClassStatement.TypeName.Content, out compiledClass))
            {
                return true;
            }
            else if (compiledClasses.TryGetValue(newClassStatement.TargetNamespacePathPrefix + newClassStatement.TypeName.Content, out compiledClass))
            {
                return true;
            }
            return false;
        }

        /// <exception cref="ArgumentNullException"></exception>
        bool GetCompiledClass(string className, out CompiledClass compiledClass) => compiledClasses.TryGetValue(className, out compiledClass);

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        static DataItem GetInitialValue(TypeToken type)
        {
            if (type.IsList)
            {
                throw new NotImplementedException();
            }

            return type.Type switch
            {
                TypeTokenType.INT => new DataItem((int)0),
                TypeTokenType.BYTE => new DataItem((byte)0),
                TypeTokenType.FLOAT => new DataItem((float)0f),
                TypeTokenType.STRING => new DataItem((string)""),
                TypeTokenType.BOOLEAN => new DataItem((bool)false),

                TypeTokenType.AUTO => throw new CompilerException("Undefined type", type),
                TypeTokenType.VOID => throw new CompilerException("Invalid type", type),
                _ => throw new InternalException($"Initial value for type {type.Type} is unimplemented"),
            };
        }

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="InternalException"></exception>
        static DataItem GetInitialValue(CompiledType type)
        {
            if (type.IsList)
            {
                throw new NotImplementedException();
            }

            if (type.IsStruct)
            {
                throw new NotImplementedException();
            }

            if (type.IsClass)
            {
                return new DataItem((int)Utils.NULL_POINTER);
            }

            return type.BuiltinType switch
            {
                CompiledType.CompiledTypeType.BYTE => new DataItem((byte)0),
                CompiledType.CompiledTypeType.INT => new DataItem((int)0),
                CompiledType.CompiledTypeType.FLOAT => new DataItem((float)0f),
                CompiledType.CompiledTypeType.STRING => new DataItem((string)""),
                CompiledType.CompiledTypeType.BOOL => new DataItem((bool)false),

                CompiledType.CompiledTypeType.VOID => throw new NotImplementedException(),
                CompiledType.CompiledTypeType.NONE => throw new NotImplementedException(),
                _ => throw new InternalException($"Initial value for type {type.FullName} is unimplemented"),
            };
        }

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(TypeToken type, string tag = "")
        {
            ITypeDefinition instanceType = GetCustomType(type.Content, type.NamespacePrefix, true);

            if (instanceType is null)
            {
                AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type), tag);
                return 1;
            }

            if (instanceType is CompiledStruct @struct)
            {
                int size = 0;
                foreach (FieldDefinition field in @struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                }
                return size;
            }

            if (instanceType is CompiledClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, Utils.NULL_POINTER, $"(pointer) {tag}");
                return 1;
            }

            throw new CompilerException("Unknown type definition " + instanceType.GetType().Name, type, CurrentFile);
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type, string tag = "")
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (FieldDefinition field in type.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, Utils.NULL_POINTER, $"(pointer) {tag}");
                return 1;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type), tag);
            return 1;
        }

        int ParameterSizeSum()
        {
            int sum = 0;
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Type.IsClass) sum++;
                else sum += parameters[i].Type.Size;
            }
            return sum;
        }
        int ParameterSizeSum(int beforeThis)
        {
            int sum = 0;
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Index < beforeThis) continue;
                if (parameters[i].Type.IsClass) sum++;
                else sum += parameters[i].Type.Size;
            }
            return sum;
        }

        #endregion

        #region AddInstruction()

        void AddInstruction(Instruction instruction)
        {
            if (BlockCodeGeneration) return;
            if (instruction.opcode == Opcode.COMMENT && !AddCommentsToCode) return;
            if (instruction.opcode == Opcode.DEBUG_SET_TAG && !GenerateDebugInstructions) return;

            compiledCode.Add(instruction);
        }
        void AddInstruction(Opcode opcode) => AddInstruction(new Instruction(opcode));
        void AddInstruction(Opcode opcode, object param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, DataItem.List param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, DataItem param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, IStruct param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, string param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, int param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, bool param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, float param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, byte param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });

        void AddInstruction(Opcode opcode, AddressingMode addressingMode) => AddInstruction(new Instruction(opcode, addressingMode));
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, object param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, DataItem.List param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, DataItem param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, IStruct param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, string param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, int param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, bool param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, float param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, byte param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, param0) { tag = tag ?? string.Empty });

        #endregion

        #region FindStatementType()
        CompiledType FindStatementType(Statement_FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "type") return new CompiledType(BuiltinType.STRING);

            if (functionCall.FunctionName == "Dealloc") return new CompiledType(BuiltinType.VOID);

            if (functionCall.FunctionName == "Alloc") return new CompiledType(BuiltinType.INT);

            if (!GetCompiledFunction(functionCall, out var calledFunc))
            { throw new CompilerException("Function '" + GetReadableID(functionCall) + "' not found!", functionCall.Identifier, CurrentFile); }
            return calledFunc.Type;
        }
        CompiledType FindStatementType(Statement_Operator @operator)
        {
            Opcode opcode = Opcode.UNKNOWN;

            if (@operator.Operator.Content == "!")
            {
                if (@operator.ParameterCount != 1) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_NOT;
            }
            else if (@operator.Operator.Content == "+")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_ADD;
            }
            else if (@operator.Operator.Content == "<")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_LT;
            }
            else if (@operator.Operator.Content == ">")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_MT;
            }
            else if (@operator.Operator.Content == "-")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_SUB;
            }
            else if (@operator.Operator.Content == "*")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_MULT;
            }
            else if (@operator.Operator.Content == "/")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_DIV;
            }
            else if (@operator.Operator.Content == "%")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_MOD;
            }
            else if (@operator.Operator.Content == "==")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_EQ;
            }
            else if (@operator.Operator.Content == "!=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_NEQ;
            }
            else if (@operator.Operator.Content == "&")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_AND;
            }
            else if (@operator.Operator.Content == "|")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_OR;
            }
            else if (@operator.Operator.Content == "^")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_XOR;
            }
            else if (@operator.Operator.Content == "<=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_LTEQ;
            }
            else if (@operator.Operator.Content == ">=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_MTEQ;
            }
            else if (@operator.Operator.Content == "<<")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.BITSHIFT_LEFT;
            }
            else if (@operator.Operator.Content == ">>")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.BITSHIFT_RIGHT;
            }

            if (opcode != Opcode.UNKNOWN)
            {
                var leftType = FindStatementType(@operator.Left);
                if (@operator.Right != null)
                {
                    var rightType = FindStatementType(@operator.Right);

                    if (leftType.IsBuiltin && rightType.IsBuiltin && leftType.GetBuiltinType() != BuiltinType.VOID && rightType.GetBuiltinType() != BuiltinType.VOID)
                    {
                        var leftValue = GetInitialValue(TypeToken.CreateAnonymous(leftType.Name, leftType.GetBuiltinType()));
                        var rightValue = GetInitialValue(TypeToken.CreateAnonymous(rightType.Name, rightType.GetBuiltinType()));

                        var predictedValue = PredictStatementValue(@operator.Operator.Content, leftValue, rightValue);
                        if (predictedValue.HasValue)
                        {
                            switch (predictedValue.Value.type)
                            {
                                case DataType.BYTE: return new CompiledType(BuiltinType.BYTE);
                                case DataType.INT: return new CompiledType(BuiltinType.INT);
                                case DataType.FLOAT: return new CompiledType(BuiltinType.FLOAT);
                                case DataType.STRING: return new CompiledType(BuiltinType.STRING);
                                case DataType.BOOLEAN: return new CompiledType(BuiltinType.BOOLEAN);
                            }
                        }
                    }

                    warnings.Add(new Warning("Thats not good :(", @operator.TotalPosition(), CurrentFile));
                    return null;
                }
                else
                { return leftType; }
            }
            else if (@operator.Operator.Content == "=")
            {
                throw new NotImplementedException();
            }
            else
            { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }
        }
        CompiledType FindStatementType(Statement_Literal literal) => literal.Type.Type switch
        {
            TypeTokenType.INT => new CompiledType(BuiltinType.INT),
            TypeTokenType.BYTE => new CompiledType(BuiltinType.BYTE),
            TypeTokenType.FLOAT => new CompiledType(BuiltinType.FLOAT),
            TypeTokenType.STRING => new CompiledType(BuiltinType.STRING),
            TypeTokenType.BOOLEAN => new CompiledType(BuiltinType.BOOLEAN),
            _ => throw new CompilerException($"Unknown literal type {literal.Type.Type}", literal, CurrentFile),
        };
        CompiledType FindStatementType(Statement_Variable variable)
        {
            if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
            {
                if (variable.ListIndex != null)
                { throw new NotImplementedException(); }
                return param.Type;
            }
            else if (GetCompiledVariable(variable.VariableName.Content, out CompiledVariable val))
            {
                return val.Type;
            }
            else
            {
                throw new CompilerException("Unknown variable '" + variable.VariableName.Content + "'", variable.VariableName, CurrentFile);
            }
        }
        CompiledType FindStatementType(Statement_MemoryAddressGetter _) => new(CompiledType.CompiledTypeType.INT);
        CompiledType FindStatementType(Statement_MemoryAddressFinder _) => new(CompiledType.CompiledTypeType.UNKNOWN);
        CompiledType FindStatementType(Statement_NewInstance newStruct)
        {
            if (GetCompiledStruct(newStruct, out var structDefinition))
            {
                return new CompiledType(structDefinition);
            }
            else if (GetCompiledClass(newStruct, out var classDefinition))
            {
                return new CompiledType(classDefinition);
            }
            else
            {
                throw new CompilerException("Unknown type '" + newStruct.TypeName.Content + "'", newStruct.TypeName, CurrentFile);
            }
        }
        CompiledType FindStatementType(TypeToken type)
        {
            return new CompiledType(type, name => GetCustomType(name));
        }
        CompiledType FindStatementType(Statement_Field field)
        {
            var prevStatementType = FindStatementType(field.PrevStatement);

            if (prevStatementType.Name == "string" || prevStatementType.Name.EndsWith("[]"))
            {
                if (field.FieldName.Content == "Length") return new CompiledType(BuiltinType.INT);
            }

            foreach (var strct in compiledStructs)
            {
                if (strct.Key != prevStatementType.Name) continue;

                foreach (var sField in strct.Fields)
                {
                    if (sField.Identifier.Content != field.FieldName.Content) continue;
                    return FindStatementType(sField.Type);
                }

                break;
            }

            foreach (var @class_ in compiledClasses)
            {
                if (@class_.Key != prevStatementType.Name) continue;

                foreach (var sField in @class_.Fields)
                {
                    if (sField.Identifier.Content != field.FieldName.Content) continue;
                    return FindStatementType(sField.Type);
                }

                break;
            }

            throw new CompilerException("Unknown type '" + prevStatementType + "'", field.TotalPosition(), CurrentFile);
        }

        CompiledType FindStatementType(StatementWithReturnValue st)
        {
            try
            {
                if (st is Statement_FunctionCall functionCall)
                { return FindStatementType(functionCall); }
                else if (st is Statement_Operator @operator)
                { return FindStatementType(@operator); }
                else if (st is Statement_Literal literal)
                { return FindStatementType(literal); }
                else if (st is Statement_Variable variable)
                { return FindStatementType(variable); }
                else if (st is Statement_MemoryAddressGetter memoryAddressGetter)
                { return FindStatementType(memoryAddressGetter); }
                else if (st is Statement_MemoryAddressFinder memoryAddressFinder)
                { return FindStatementType(memoryAddressFinder); }
                else if (st is Statement_NewInstance newStruct)
                { return FindStatementType(newStruct); }
                else if (st is Statement_Field field)
                { return FindStatementType(field); }
                else if (st is Statement_ListValue list)
                {
                    if (list.Values.Length > 0) return new CompiledType(FindStatementType(list.Values[0]));
                    throw new NotImplementedException();
                }
                else if (st is Statement_Index index)
                {
                    var type = FindStatementType(index.PrevStatement);
                    if (type.IsList) return type.ListOf;
                    else if (type.Name == "string") return new CompiledType(BuiltinType.STRING);
                    throw new NotImplementedException();
                }
                throw new CompilerException($"Statement without value type: {st.GetType().Name} {st}", st, CurrentFile);
            }
            catch (InternalException error)
            {
                errors.Add(new Error(error.Message, st.TotalPosition()));
                throw;
            }
        }
        #endregion

        #region PredictStatementValue()
        static DataItem? PredictStatementValue(string @operator, DataItem left, DataItem right) => @operator switch
        {
            "!" => !left,

            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "%" => left % right,

            "&" => left & right,
            "|" => left | right,
            "^" => left ^ right,

            "<<" => DataItem.BitshiftLeft(left, right),
            ">>" => DataItem.BitshiftRight(left, right),

            "<" => new DataItem(left < right, null),
            ">" => new DataItem(left > right, null),
            "==" => new DataItem(left == right, null),
            "!=" => new DataItem(left != right, null),
            "<=" => new DataItem(left <= right, null),
            ">=" => new DataItem(left >= right, null),
            _ => null,
        };
        static DataItem? PredictStatementValue(Statement_Operator @operator)
        {
            var leftValue = PredictStatementValue(@operator.Left);
            if (!leftValue.HasValue) return null;

            if (@operator.Operator.Content == "!")
            {
                return !leftValue;
            }

            if (@operator.Right != null)
            {
                var rightValue = PredictStatementValue(@operator.Right);
                if (!rightValue.HasValue) return null;

                return PredictStatementValue(@operator.Operator.Content, leftValue.Value, rightValue.Value);
            }
            else
            { return leftValue; }
        }
        static DataItem? PredictStatementValue(Statement_Literal literal)
        {
            return literal.Type.Type switch
            {
                TypeTokenType.INT => new DataItem(int.Parse(literal.Value), null),
                TypeTokenType.FLOAT => new DataItem(float.Parse(literal.Value), null),
                TypeTokenType.BYTE => new DataItem(byte.Parse(literal.Value), null),
                TypeTokenType.STRING => new DataItem(literal.Value, null),
                TypeTokenType.BOOLEAN => new DataItem(bool.Parse(literal.Value), null),
                TypeTokenType.USER_DEFINED => new DataItem(new UnassignedStruct(), null),
                _ => null,
            };
        }
        static DataItem? PredictStatementValue(StatementWithReturnValue st)
        {
            if (st is Statement_Literal literal)
            { return PredictStatementValue(literal); }
            else if (st is Statement_Operator @operator)
            { return PredictStatementValue(@operator); }

            return null;
        }
        #endregion

        #region GenerateCodeForStatement

        void GenerateCodeForStatement(Statement_NewVariable newVariable)
        {
            newVariable.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
            newVariable.VariableName.Analysis.CompilerReached = true;

            if (!GetCompiledVariable(newVariable.VariableName.Content, out CompiledVariable variable))
            { throw new CompilerException("Unknown variable '" + newVariable.VariableName.Content + "'", newVariable.VariableName, CurrentFile); }

            if (newVariable.InitialValue == null) return;

            AddInstruction(Opcode.COMMENT, $"New Variable \'{newVariable.VariableName.Content}\' {{");

            if (newVariable.InitialValue is Statement_ListValue initialListValue)
            {
                if (initialListValue.Values.Length == 0)
                { AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(newVariable.Type)); }
                else
                { GenerateCodeForStatement(newVariable.InitialValue); }
            }
            else
            { GenerateCodeForStatement(newVariable.InitialValue); }

            if (variable.IsStoredInHEAP)
            {
                AddInstruction(Opcode.STORE_VALUE, variable.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, variable.MemoryAddress);
            }
            else
            {
                MemoryCopyStack(variable.MemoryAddress, variable.Type.Size, !variable.IsGlobal);
                for (int i = 0; i < variable.Type.Size; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }


            AddInstruction(Opcode.COMMENT, "}");
        }
        void GenerateCodeForStatement(Statement_FunctionCall functionCall)
        {
            AddInstruction(Opcode.COMMENT, $"Call {functionCall.FunctionName} {{");

            functionCall.Identifier.Analysis.CompilerReached = true;

            if (functionCall.FunctionName == "return")
            {
                if (functionCall.Parameters.Length > 1)
                { throw new CompilerException("Wrong number of parameters passed to 'return'", functionCall.TotalPosition(), CurrentFile); }
                else if (functionCall.Parameters.Length == 1)
                {
                    AddInstruction(Opcode.COMMENT, " Param 0:");

                    StatementWithReturnValue returnValue = functionCall.Parameters[0];
                    CompiledType returnValueType = FindStatementType(returnValue);

                    GenerateCodeForStatement(returnValue);
                    int offset = GetReturnValueAddress();

                    for (int i = 0; i < returnValueType.SizeOnStack; i++)
                    { AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset - i); }
                }

                functionCall.Identifier.Analysis.Subtype = TokenSubtype.Statement;
                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinFunction("return", "void", new string[] { "any" }, new string[] { "p" });

                /*
                // Clear variables
                int variableCount = cleanupStack.GetAllInStatements();
                if (AddCommentsToCode && variableCount > 0)
                { AddInstruction(Opcode.COMMENT, " Clear Local Variables:"); }
                for (int i = 0; i < variableCount; i++)
                { AddInstruction(Opcode.POP_VALUE); }
                */

                AddInstruction(Opcode.COMMENT, " .:");

                returnInstructions.Add(compiledCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                AddInstruction(Opcode.COMMENT, "}");

                return;
            }

            if (functionCall.FunctionName == "throw")
            {
                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException("Wrong number of parameters passed to 'throw'", functionCall.TotalPosition(), CurrentFile); }

                AddInstruction(Opcode.COMMENT, " Param 0:");

                StatementWithReturnValue throwValue = functionCall.Parameters[0];
                CompiledType throwValueType = FindStatementType(throwValue);

                GenerateCodeForStatement(throwValue);
                AddInstruction(Opcode.THROW);

                functionCall.Identifier.Analysis.Subtype = TokenSubtype.Statement;
                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinFunction("throw", "void", new string[] { "any" }, new string[] { "errorMessage" });

                return;
            }

            if (functionCall.FunctionName == "break")
            {
                if (breakInstructions.Count <= 0)
                { throw new CompilerException("The keyword 'break' does not avaiable in the current context", functionCall.Identifier, CurrentFile); }

                breakInstructions.Last().Add(compiledCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                AddInstruction(Opcode.COMMENT, "}");

                return;
            }

            if (functionCall.FunctionName == "type")
            {
                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException("Wrong number of parameters passed to 'type'", functionCall.TotalPosition(), CurrentFile); }

                AddInstruction(Opcode.COMMENT, " Param 0:");
                GenerateCodeForStatement(functionCall.Parameters[0]);

                AddInstruction(Opcode.COMMENT, " .:");
                AddInstruction(Opcode.TYPE_GET);

                AddInstruction(Opcode.COMMENT, "}");

                functionCall.Identifier.Analysis.Subtype = TokenSubtype.Statement;
                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinFunction("type", "string", new string[] { "any" }, new string[] { "p" });

                return;
            }

            if (functionCall.FunctionName == "Dealloc")
            {
                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException("Wrong number of parameters passed to 'Dealloc'", functionCall.TotalPosition(), CurrentFile); }

                var param0 = functionCall.Parameters[0];
                if (param0 is not Statement_Variable variableStatement) throw new CompilerException($"Wrong kind of statement passed to 'Dealloc''s parameter. Expected a variable.", param0, CurrentFile);

                if (!GetCompiledVariable(variableStatement.VariableName.Content, out var variable))
                { throw new CompilerException("Unknown variable '" + variableStatement.VariableName.Content + "'", variableStatement.VariableName, CurrentFile); }

                Deallocate(variable);

                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinFunction("Dealloc", "void", new string[] { "any" }, new string[] { "variable" });

                return;
            }

            if (functionCall.FunctionName == "Alloc")
            {
                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException("Wrong number of parameters passed to 'Alloc'", functionCall.TotalPosition(), CurrentFile); }

                Statement param0 = functionCall.Parameters[0];
                if (param0 is not Statement_Literal literal
                    || literal.Type.Type != TypeTokenType.INT) throw new CompilerException($"Wrong parameter passed to 'Alloc'. Expected an integer literal.", param0, CurrentFile);

                int param0Value = int.Parse(literal.Value);

                AddInstruction(Opcode.FIND_HEAP_FREE_SPACE, param0Value);

                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinFunction("Alloc", "int", new string[] { "int" }, new string[] { "size" });

                return;
            }

            if (functionCall.IsMethodCall)
            {
                if (functionCall.PrevStatement is Statement_Variable prevVar)
                {
                    if (GetCompiledVariable(prevVar.VariableName.Content, out var prevVarInfo))
                    {
                        prevVar.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                        prevVar.VariableName.Analysis.CompilerReached = true;
                        prevVar.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(prevVarInfo, prevVarInfo.IsGlobal, prevVarInfo.Type);

                        if (prevVarInfo.Type.IsList)
                        {
                            if (functionCall.FunctionName == "Push")
                            {
                                functionCall.Identifier.Analysis.SubSubtype = TokenSubSubtype.FunctionName;
                                functionCall.Identifier.Analysis.CompilerReached = true;
                                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinMethod("Push", "void", prevVarInfo.Type.Name, new string[] { prevVarInfo.Type.ListOf.Name }, new string[] { "newElement" });

                                AddInstruction(Opcode.COMMENT, " Param prev:");
                                AddInstruction(Opcode.LOAD_VALUE, prevVarInfo.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, prevVarInfo.Index);

                                if (functionCall.Parameters.Length != 1)
                                { throw new CompilerException($"Wrong number of parameters passed to '{prevVarInfo.Type}.Push'", functionCall.Identifier, CurrentFile); }

                                var paramType = FindStatementType(functionCall.Parameters[0]);
                                if (paramType.Name != prevVarInfo.Type.ListOf.Name)
                                { throw new CompilerException($"Wrong type passed to '{prevVarInfo.Type}.Push': {paramType}, expected {prevVarInfo.Type.ListOf.Name}", functionCall.Parameters[0].TotalPosition()); }

                                AddInstruction(Opcode.COMMENT, " Param 0:");
                                GenerateCodeForStatement(functionCall.Parameters[0]);

                                AddInstruction(Opcode.COMMENT, " .:");
                                AddInstruction(Opcode.LIST_PUSH_ITEM);

                                AddInstruction(Opcode.COMMENT, "}");

                                return;
                            }
                            else if (functionCall.FunctionName == "Pull")
                            {
                                functionCall.Identifier.Analysis.SubSubtype = TokenSubSubtype.FunctionName;
                                functionCall.Identifier.Analysis.CompilerReached = true;
                                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinMethod("Pull", prevVarInfo.Type.ListOf.Name, prevVarInfo.Type.Name, Array.Empty<string>(), Array.Empty<string>());

                                AddInstruction(Opcode.COMMENT, " Param prev:");
                                AddInstruction(Opcode.LOAD_VALUE, prevVarInfo.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, prevVarInfo.Index);

                                if (functionCall.Parameters.Length != 0)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Pull'", functionCall.Identifier, CurrentFile); }

                                AddInstruction(Opcode.COMMENT, " .:");
                                AddInstruction(Opcode.LIST_PULL_ITEM);

                                AddInstruction(Opcode.COMMENT, "}");

                                return;
                            }
                            else if (functionCall.FunctionName == "Add")
                            {
                                functionCall.Identifier.Analysis.SubSubtype = TokenSubSubtype.FunctionName;
                                functionCall.Identifier.Analysis.CompilerReached = true;
                                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinMethod("Add", "void", prevVarInfo.Type.Name, new string[] { prevVarInfo.Type.ListOf.Name, "int" }, new string[] { "newElement", "index" });

                                AddInstruction(Opcode.COMMENT, " Param prev:");
                                AddInstruction(Opcode.LOAD_VALUE, prevVarInfo.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, prevVarInfo.Index);

                                if (functionCall.Parameters.Length != 2)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Add'", functionCall.Identifier, CurrentFile); }

                                var paramType = FindStatementType(functionCall.Parameters[0]);
                                if (paramType.Name != prevVarInfo.Type.ListOf.Name)
                                { throw new CompilerException($"Wrong type passed to '<list>.Add': {paramType}, expected {prevVarInfo.Type.ListOf.Name}", functionCall.Parameters[0].TotalPosition()); }

                                AddInstruction(Opcode.COMMENT, " Param 0:");
                                GenerateCodeForStatement(functionCall.Parameters[0]);
                                AddInstruction(Opcode.COMMENT, " Param 1:");
                                GenerateCodeForStatement(functionCall.Parameters[1]);

                                AddInstruction(Opcode.COMMENT, " Param .:");
                                AddInstruction(Opcode.LIST_ADD_ITEM);

                                AddInstruction(Opcode.COMMENT, "}");

                                return;
                            }
                            else if (functionCall.FunctionName == "Remove")
                            {
                                functionCall.Identifier.Analysis.SubSubtype = TokenSubSubtype.FunctionName;
                                functionCall.Identifier.Analysis.CompilerReached = true;
                                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinMethod("Remove", "void", prevVarInfo.Type.Name, new string[] { "int" }, new string[] { "index" });

                                AddInstruction(Opcode.COMMENT, " Param prev:");
                                AddInstruction(Opcode.LOAD_VALUE, prevVarInfo.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, prevVarInfo.Index);

                                if (functionCall.Parameters.Length != 1)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Remove'", functionCall.Identifier, CurrentFile); }

                                AddInstruction(Opcode.COMMENT, " Param 0:");
                                GenerateCodeForStatement(functionCall.Parameters[0]);

                                AddInstruction(Opcode.COMMENT, " Param .:");
                                AddInstruction(Opcode.LIST_REMOVE_ITEM);

                                AddInstruction(Opcode.COMMENT, "}");

                                return;
                            }
                        }
                    }
                }
            }

            string searchedID = functionCall.TargetNamespacePathPrefix + functionCall.FunctionName;
            searchedID += "(";
            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                if (i > 0) { searchedID += ", "; }

                searchedID += FindStatementType(functionCall.Parameters[i]);
            }
            searchedID += ")";

            if (!GetCompiledFunction(functionCall, out CompiledFunction compiledFunction))
            {
                throw new CompilerException("Unknown function " + searchedID + "", functionCall.Identifier, CurrentFile);
            }

            compiledFunction.References?.Add(new DefinitionReference(functionCall.Identifier, CurrentFile));

            if (!compiledFunction.CanUse(CurrentFile))
            {
                errors.Add(new Error($"The {searchedID} function cannot be called due to its protection level", functionCall.Identifier, CurrentFile));
                AddInstruction(Opcode.COMMENT, "}");
                return;
            }

            if (CurrentFunction_ForRecursionProtection != null && compiledFunction.ID() == CurrentFunction_ForRecursionProtection)
            {
                warnings.Add(new Warning($"Potential infinite recursive call", functionCall.TotalPosition(), CurrentFile));
            }

            functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefFunction(compiledFunction);

            if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
            { throw new CompilerException("Wrong number of parameters passed to '" + searchedID + $"': requied {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall.TotalPosition(), CurrentFile); }

            if (functionCall.IsMethodCall != compiledFunction.IsMethod)
            { throw new CompilerException($"You called the {((compiledFunction.IsMethod) ? "method" : "function")} '{functionCall.FunctionName}' as {((functionCall.IsMethodCall) ? "method" : "function")}", functionCall.TotalPosition(), CurrentFile); }

            if (compiledFunction.IsBuiltin)
            {
                if (!builtinFunctions.TryGetValue(compiledFunction.BuiltinName, out var builtinFunction))
                {
                    errors.Add(new Error($"Builtin function '{compiledFunction.BuiltinName}' not found", functionCall.Identifier, CurrentFile));
                    AddInstruction(Opcode.COMMENT, "}");
                    return;
                }

                if (functionCall.PrevStatement != null)
                {
                    AddInstruction(Opcode.COMMENT, " Param prev:");
                    GenerateCodeForStatement(functionCall.PrevStatement);
                }
                for (int i = 0; i < functionCall.Parameters.Length; i++)
                {
                    AddInstruction(Opcode.COMMENT, $" Param {i}:");
                    GenerateCodeForStatement(functionCall.Parameters[i]);
                }

                AddInstruction(Opcode.COMMENT, " .:");
                AddInstruction(Opcode.PUSH_VALUE, compiledFunction.BuiltinName);
                AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                AddInstruction(Opcode.COMMENT, $" Clear Return Value:");
                if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
                { AddInstruction(Opcode.POP_VALUE); }

                AddInstruction(Opcode.COMMENT, "}");
                return;
            }

            int returnValueSize = 0;
            if (compiledFunction.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(compiledFunction.Type, "returnvalue");
            }

            if (functionCall.PrevStatement != null)
            {
                AddInstruction(Opcode.COMMENT, " Param prev:");
                // TODO: variable sized prev statement
                GenerateCodeForStatement(functionCall.PrevStatement);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");
            }

            int paramsSize = 0;

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                Statement param = functionCall.Parameters[i];
                ParameterDefinition definedParam = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];

                AddInstruction(Opcode.COMMENT, $" Param {i}:");
                GenerateCodeForStatement(param);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.Identifier);

                if (definedParam.Type.Type == TypeTokenType.USER_DEFINED)
                {
                    ITypeDefinition paramType = GetCustomType(definedParam.Type.ToString());
                    if (paramType is CompiledStruct @struct)
                    {
                        paramsSize += @struct.Size;
                    }
                    else if (paramType is CompiledClass)
                    {
                        paramsSize++;
                    }
                }
                else
                {
                    paramsSize++;
                }
            }

            AddInstruction(Opcode.COMMENT, " .:");

            if (compiledFunction.InstructionOffset == -1)
            { undefinedFunctionOffsets.Add(new UndefinedFunctionOffset(compiledCode.Count, functionCall, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

            AddInstruction(Opcode.CALL, compiledFunction.InstructionOffset - compiledCode.Count);

            AddInstruction(Opcode.COMMENT, " Clear Params:");
            for (int i = 0; i < paramsSize; i++)
            {
                AddInstruction(Opcode.POP_VALUE);
            }

            if (functionCall.PrevStatement != null)
            {
                // TODO: variable sized prev statement
                AddInstruction(Opcode.POP_VALUE);
            }

            if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
            {
                AddInstruction(Opcode.COMMENT, " Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddInstruction(Opcode.COMMENT, "}");
        }
        void GenerateCodeForStatement(Statement_Operator @operator)
        {
            @operator.Operator.Analysis.CompilerReached = true;

            if (OptimizeCode)
            {
                DataItem? predictedValueN = PredictStatementValue(@operator);
                if (predictedValueN.HasValue)
                {
                    var predictedValue = predictedValueN.Value;

                    switch (predictedValue.type)
                    {
                        case DataType.BYTE:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueByte);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueByte}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case DataType.INT:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueInt);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueInt}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case DataType.BOOLEAN:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueBoolean);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueBoolean}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case DataType.FLOAT:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueFloat);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueFloat}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case DataType.STRING:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueString);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueString}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                    }
                }
            }

            Dictionary<string, Opcode> operatorOpCodes = new()
            {
                { "!", Opcode.LOGIC_NOT },
                { "+", Opcode.MATH_ADD },
                { "<", Opcode.LOGIC_LT },
                { ">", Opcode.LOGIC_MT },
                { "-", Opcode.MATH_SUB },
                { "*", Opcode.MATH_MULT },
                { "/", Opcode.MATH_DIV },
                { "%", Opcode.MATH_MOD },
                { "==", Opcode.LOGIC_EQ },
                { "!=", Opcode.LOGIC_NEQ },
                { "&", Opcode.LOGIC_AND },
                { "|", Opcode.LOGIC_OR },
                { "^", Opcode.LOGIC_XOR },
                { "<=", Opcode.LOGIC_LTEQ },
                { ">=", Opcode.LOGIC_MTEQ },
                { "<<", Opcode.BITSHIFT_LEFT },
                { ">>", Opcode.BITSHIFT_RIGHT },
            };
            Dictionary<string, int> operatorParameterCounts = new()
            {
                { "!", 1 },
                { "+", 2 },
                { "<", 2 },
                { ">", 2 },
                { "-", 2 },
                { "*", 2 },
                { "/", 2 },
                { "%", 2 },
                { "==", 2 },
                { "!=", 2 },
                { "&", 2 },
                { "|", 2 },
                { "^", 2 },
                { "<=", 2 },
                { ">=", 2 },
                { "<<", 2 },
                { ">>", 2 },
            };


            if (operatorOpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
            {
                if (operatorParameterCounts[@operator.Operator.Content] != @operator.ParameterCount)
                { throw new CompilerException($"Wrong number of passed ({@operator.ParameterCount}) to operator '{@operator.Operator.Content}', requied: {operatorParameterCounts[@operator.Operator.Content]}", @operator.Operator, CurrentFile); }
            }
            else
            {
                opcode = Opcode.UNKNOWN;
            }

            if (opcode != Opcode.UNKNOWN)
            {
                GenerateCodeForStatement(@operator.Left);
                if (@operator.Right != null) GenerateCodeForStatement(@operator.Right);
                AddInstruction(opcode);
            }
            else if (@operator.Operator.Content == "=")
            {
                if (@operator.ParameterCount != 2)
                { throw new CompilerException("Wrong number of parameters passed to assigment operator '" + @operator.Operator.Content + "'", @operator.Operator, CurrentFile); }

                GenerateCodeForValueSetter(@operator.Left, @operator.Right);
            }
            else
            { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }
        }
        void GenerateCodeForStatement(Statement_Setter setter)
        {
            setter.Operator.Analysis.CompilerReached = true;

            GenerateCodeForValueSetter(setter.Left, setter.Right);
        }
        void GenerateCodeForStatement(Statement_Literal literal)
        {
            if (literal.ValueToken != null)
            { try { literal.ValueToken.Analysis.CompilerReached = true; } catch (NullReferenceException) { } }

            switch (literal.Type.Type)
            {
                case TypeTokenType.INT:
                    AddInstruction(Opcode.PUSH_VALUE, int.Parse(literal.Value));
                    break;
                case TypeTokenType.FLOAT:
                    AddInstruction(Opcode.PUSH_VALUE, float.Parse(literal.Value.TrimEnd('f'), System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case TypeTokenType.STRING:
                    AddInstruction(Opcode.PUSH_VALUE, literal.Value);
                    break;
                case TypeTokenType.BOOLEAN:
                    AddInstruction(Opcode.PUSH_VALUE, bool.Parse(literal.Value));
                    break;
                case TypeTokenType.BYTE:
                    AddInstruction(Opcode.PUSH_VALUE, byte.Parse(literal.Value));
                    break;
            }
        }
        void GenerateCodeForStatement(Statement_Variable variable)
        {
            variable.VariableName.Analysis.CompilerReached = true;

            if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
            {
                variable.VariableName.Analysis.SubSubtype = TokenSubSubtype.ParameterName;
                variable.VariableName.Analysis.Reference = new TokenAnalysis.RefParameter(param.Type.ToString());

                int offset = GetDataAddress(param);

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset);
            }
            else if (GetCompiledVariable(variable.VariableName.Content, out CompiledVariable val))
            {
                variable.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                variable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(val, val.IsGlobal, val.Type);

                if (val.IsStoredInHEAP)
                {
                    AddInstruction(Opcode.LOAD_VALUE, val.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, val.MemoryAddress);
                }
                else
                {
                    MemoryLoadStack(val.MemoryAddress, val.Type.Size, !val.IsGlobal);
                }
            }
            else
            {
                throw new CompilerException("Unknown variable '" + variable.VariableName.Content + "'", variable.VariableName, CurrentFile);
            }

            if (variable.ListIndex != null)
            {
                GenerateCodeForStatement(variable.ListIndex);
                AddInstruction(Opcode.LIST_INDEX);
            }
        }
        void GenerateCodeForStatement(Statement_MemoryAddressGetter memoryAddressGetter)
        {
            void GetVariableAddress(Statement_Variable variable)
            {
                variable.VariableName.Analysis.CompilerReached = true;

                if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
                {
                    variable.VariableName.Analysis.SubSubtype = TokenSubSubtype.ParameterName;
                    variable.VariableName.Analysis.Reference = new TokenAnalysis.RefParameter(param.Type.ToString());

                    AddInstruction(Opcode.GET_BASEPOINTER);
                    AddInstruction(Opcode.PUSH_VALUE, param.RealIndex);
                    AddInstruction(Opcode.MATH_ADD);
                }
                else if (GetCompiledVariable(variable.VariableName.Content, out CompiledVariable val))
                {
                    variable.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                    variable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(val, val.IsGlobal, val.Type);

                    if (val.IsStoredInHEAP)
                    {
                        AddInstruction(Opcode.PUSH_VALUE, val.MemoryAddress);
                    }
                    else
                    {
                        if (val.IsGlobal)
                        {
                            AddInstruction(Opcode.PUSH_VALUE, val.MemoryAddress);
                        }
                        else
                        {
                            AddInstruction(Opcode.GET_BASEPOINTER);
                            AddInstruction(Opcode.PUSH_VALUE, val.MemoryAddress);
                            AddInstruction(Opcode.MATH_ADD);
                        }
                    }
                }
                else
                {
                    throw new CompilerException("Unknown variable '" + variable.VariableName.Content + "'", variable.VariableName, CurrentFile);
                }
            }

            void GetFieldAddress(Statement_Field field)
            {
                var prevType = FindStatementType(field.PrevStatement);

                if (prevType.IsStruct)
                {
                    int offset = GetDataAddress(field, out AddressingMode addressingMode, out _);
                    AddInstruction(Opcode.PUSH_VALUE, offset);
                    if (addressingMode == AddressingMode.BASEPOINTER_RELATIVE)
                    {
                        AddInstruction(Opcode.GET_BASEPOINTER);
                        AddInstruction(Opcode.MATH_ADD);
                    }
                    return;
                }

                if (prevType.IsClass)
                {
                    int offset = GetFieldOffset(field);
                    int pointerOffset = GetBaseAdderss(field, out AddressingMode addressingMode);
                    GenerateCodeForValueGetter(pointerOffset, false, addressingMode);
                    AddInstruction(Opcode.PUSH_VALUE, offset);
                    AddInstruction(Opcode.MATH_ADD);
                    return;
                }
                throw new NotImplementedException();
            }

            void GetAddress(Statement statement)
            {
                if (statement is Statement_Variable variable)
                {
                    GetVariableAddress(variable);
                    return;
                }
                if (statement is Statement_Field field)
                {
                    GetFieldAddress(field);
                    return;
                }
                throw new NotImplementedException();
            }

            GetAddress(memoryAddressGetter.PrevStatement);
        }
        void GenerateCodeForStatement(Statement_MemoryAddressFinder memoryAddressFinder)
        {
            GenerateCodeForStatement(memoryAddressFinder.PrevStatement);
            // TODO: stack getter
            AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
        }
        void GenerateCodeForStatement(Statement_WhileLoop whileLoop)
        {
            var conditionValue_ = PredictStatementValue(whileLoop.Condition);
            if (conditionValue_.HasValue)
            {
                if (conditionValue_.Value.type != DataType.BOOLEAN)
                {
                    warnings.Add(new Warning($"Condition must be boolean", whileLoop.Condition.TotalPosition(), CurrentFile));
                }
                else if (TrimUnreachableCode)
                {
                    if (!conditionValue_.Value.ValueBoolean)
                    {
                        AddInstruction(Opcode.COMMENT, "Unreachable code not compiled");
                        informations.Add(new Information($"Unreachable code not compiled", new Position(whileLoop.BracketStart, whileLoop.BracketEnd), CurrentFile));
                        return;
                    }
                }
            }

            AddInstruction(Opcode.COMMENT, "while (...) {");
            AddInstruction(Opcode.COMMENT, "Condition");
            int conditionOffset = compiledCode.Count;
            GenerateCodeForStatement(whileLoop.Condition);

            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffset = compiledCode.Count - 1;

            breakInstructions.Add(new List<int>());

            AddInstruction(Opcode.COMMENT, "Statements {");
            for (int i = 0; i < whileLoop.Statements.Count; i++)
            {
                GenerateCodeForStatement(whileLoop.Statements[i]);
            }

            AddInstruction(Opcode.COMMENT, "}");

            AddInstruction(Opcode.COMMENT, "Jump Back");
            AddInstruction(Opcode.JUMP_BY, conditionOffset - compiledCode.Count);

            AddInstruction(Opcode.COMMENT, "}");

            compiledCode[conditionJumpOffset].Parameter = compiledCode.Count - conditionJumpOffset;
            List<int> currentBreakInstructions = breakInstructions.Last();

            if (currentBreakInstructions.Count == 0)
            {
                if (conditionValue_.HasValue)
                {
                    var conditionValue = conditionValue_.Value;
                    if (conditionValue.type == DataType.BOOLEAN)
                    {
                        if (conditionValue.ValueBoolean)
                        { warnings.Add(new Warning($"Infinity loop", whileLoop.Keyword, CurrentFile)); }
                        else
                        { warnings.Add(new Warning($"Why? this will never run", whileLoop.Keyword, CurrentFile)); }
                    }
                }
            }

            foreach (var breakInstruction in currentBreakInstructions)
            {
                compiledCode[breakInstruction].Parameter = compiledCode.Count - breakInstruction;
            }
            breakInstructions.RemoveAt(breakInstructions.Count - 1);
        }
        void GenerateCodeForStatement(Statement_ForLoop forLoop)
        {
            AddInstruction(Opcode.COMMENT, "for (...) {");

            AddInstruction(Opcode.COMMENT, "FOR Declaration");
            // Index variable
            GenerateCodeForVariable(forLoop.VariableDeclaration, false);
            cleanupStack.Push(new CleanupItem(1, 1));
            GenerateCodeForStatement(forLoop.VariableDeclaration);

            AddInstruction(Opcode.COMMENT, "FOR Condition");
            // Index condition
            int conditionOffsetFor = compiledCode.Count;
            GenerateCodeForStatement(forLoop.Condition);
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffsetFor = compiledCode.Count - 1;

            breakInstructions.Add(new List<int>());

            AddInstruction(Opcode.COMMENT, "Statements {");
            for (int i = 0; i < forLoop.Statements.Count; i++)
            {
                Statement currStatement = forLoop.Statements[i];
                GenerateCodeForStatement(currStatement);
            }

            AddInstruction(Opcode.COMMENT, "}");

            AddInstruction(Opcode.COMMENT, "FOR Expression");
            // Index expression
            GenerateCodeForStatement(forLoop.Expression);

            AddInstruction(Opcode.COMMENT, "Jump back");
            AddInstruction(Opcode.JUMP_BY, conditionOffsetFor - compiledCode.Count);
            compiledCode[conditionJumpOffsetFor].Parameter = compiledCode.Count - conditionJumpOffsetFor;

            foreach (var breakInstruction in breakInstructions.Last())
            {
                compiledCode[breakInstruction].Parameter = compiledCode.Count - breakInstruction;
            }
            breakInstructions.RemoveAt(breakInstructions.Count - 1);

            CleanupVariables(cleanupStack.Pop());

            AddInstruction(Opcode.COMMENT, "}");
        }
        void GenerateCodeForStatement(Statement_If @if)
        {
            List<int> jumpOutInstructions = new();

            foreach (var ifSegment in @if.Parts)
            {
                if (ifSegment is Statement_If_If partIf)
                {
                    var conditionValue_ = PredictStatementValue(partIf.Condition);
                    if (conditionValue_.HasValue)
                    {
                        var conditionValue = conditionValue_.Value;

                        if (conditionValue.type != DataType.BOOLEAN)
                        {
                            warnings.Add(new Warning($"Condition must be boolean", partIf.Condition.TotalPosition(), CurrentFile));
                        }
                    }

                    AddInstruction(Opcode.COMMENT, "if (...) {");

                    AddInstruction(Opcode.COMMENT, "IF Condition");
                    GenerateCodeForStatement(partIf.Condition);
                    AddInstruction(Opcode.COMMENT, "IF Jump to Next");
                    int jumpNextInstruction = compiledCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    cleanupStack.Push(CompileVariables(partIf, false));

                    AddInstruction(Opcode.COMMENT, "IF Statements");
                    for (int i = 0; i < partIf.Statements.Count; i++)
                    {
                        GenerateCodeForStatement(partIf.Statements[i]);
                    }

                    CleanupVariables(cleanupStack.Pop());

                    AddInstruction(Opcode.COMMENT, "IF Jump to End");
                    jumpOutInstructions.Add(compiledCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddInstruction(Opcode.COMMENT, "}");

                    compiledCode[jumpNextInstruction].Parameter = compiledCode.Count - jumpNextInstruction;
                }
                else if (ifSegment is Statement_If_ElseIf partElseif)
                {
                    AddInstruction(Opcode.COMMENT, "elseif (...) {");

                    AddInstruction(Opcode.COMMENT, "ELSEIF Condition");
                    GenerateCodeForStatement(partElseif.Condition);
                    AddInstruction(Opcode.COMMENT, "ELSEIF Jump to Next");
                    int jumpNextInstruction = compiledCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    cleanupStack.Push(CompileVariables(partElseif, false));

                    AddInstruction(Opcode.COMMENT, "ELSEIF Statements");
                    for (int i = 0; i < partElseif.Statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElseif.Statements[i]);
                    }

                    CleanupVariables(cleanupStack.Pop());

                    AddInstruction(Opcode.COMMENT, "IF Jump to End");
                    jumpOutInstructions.Add(compiledCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddInstruction(Opcode.COMMENT, "}");

                    compiledCode[jumpNextInstruction].Parameter = compiledCode.Count - jumpNextInstruction;
                }
                else if (ifSegment is Statement_If_Else partElse)
                {
                    AddInstruction(Opcode.COMMENT, "else {");

                    AddInstruction(Opcode.COMMENT, "ELSE Statements");

                    cleanupStack.Push(CompileVariables(partElse, false));

                    for (int i = 0; i < partElse.Statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElse.Statements[i]);
                    }

                    CleanupVariables(cleanupStack.Pop());

                    AddInstruction(Opcode.COMMENT, "}");
                }
            }

            foreach (var item in jumpOutInstructions)
            {
                compiledCode[item].Parameter = compiledCode.Count - item;
            }
        }
        void GenerateCodeForStatement(Statement_NewInstance newObject)
        {
            AddInstruction(Opcode.COMMENT, $"new {newObject.TypeName}: {{");
            newObject.TypeName.Analysis.CompilerReached = true;

            var instanceType = GetCustomType(newObject.TypeName.Content, newObject.TargetNamespacePathPrefix, true);

            if (instanceType is null)
            { throw new CompilerException("Unknown type '" + newObject.TypeName.Content + "'", newObject.TypeName, CurrentFile); }

            if (instanceType is CompiledStruct @struct)
            {
                newObject.TypeName.Analysis.Reference = new TokenAnalysis.RefStruct(@struct);
                @struct.References?.Add(new DefinitionReference(newObject.TypeName, CurrentFile));

                throw new NotImplementedException();
            }
            else if (instanceType is CompiledClass @class)
            {
                newObject.TypeName.Analysis.Reference = new TokenAnalysis.RefClass(@class);
                @class.References?.Add(new DefinitionReference(newObject.TypeName, CurrentFile));

                AddInstruction(Opcode.FIND_HEAP_FREE_SPACE, @class.Size, $"(pointer {@class.FullName})");
                int i = 0;
                foreach (FieldDefinition field in @class.Fields)
                {
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type));
                    AddInstruction(Opcode.PUSH_VALUE, i);
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -3);
                    AddInstruction(Opcode.MATH_ADD);
                    AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
                    i++;
                }
            }
            else
            { throw new CompilerException("Unknown type definition " + instanceType.GetType().Name, newObject.TypeName, CurrentFile); }
            AddInstruction(Opcode.COMMENT, "}");
        }
        void GenerateCodeForStatement(Statement_Field field)
        {
            field.FieldName.Analysis.CompilerReached = true;
            field.FieldName.Analysis.SubSubtype = TokenSubSubtype.FieldName;

            {
                var prevType = FindStatementType(field.PrevStatement);
                if (prevType.IsStruct || prevType.IsClass)
                {
                    var type = FindStatementType(field);

                    if (prevType.IsStruct)
                    { field.FieldName.Analysis.Reference = new TokenAnalysis.RefField(type.Name, field.FieldName, prevType.Struct.FullName, "struct", prevType.Struct.FilePath); }
                    else if (prevType.IsClass)
                    { field.FieldName.Analysis.Reference = new TokenAnalysis.RefField(type.Name, field.FieldName, prevType.Class.FullName, "class", prevType.Class.FilePath); }

                    if (prevType.IsStruct)
                    {
                        int offset = GetDataAddress(field, out AddressingMode addressingMode, out bool inHeap);
                        GenerateCodeForValueGetter(offset, inHeap, addressingMode);
                        return;
                    }

                    if (prevType.IsClass)
                    {
                        int offset = GetFieldOffset(field);
                        int pointerOffset = GetBaseAdderss(field, out AddressingMode addressingMode);
                        GenerateCodeForValueGetter(pointerOffset, offset, addressingMode);
                        return;
                    }
                }
            }

            throw new NotImplementedException();
            /*
            GenerateCodeForStatement(field.PrevStatement);
            AddInstruction(Opcode.PUSH_VALUE, field.FieldName.Content);
            AddInstruction(Opcode.LOAD_FIELD, AddressingMode.POP);
            */
        }
        void GenerateCodeForStatement(Statement_Index indexStatement)
        {
            GenerateCodeForStatement(indexStatement.PrevStatement);
            if (indexStatement.Expression == null)
            { throw new CompilerException($"Index expression for indexer is missing", indexStatement.TotalPosition(), CurrentFile); }
            GenerateCodeForStatement(indexStatement.Expression);
            AddInstruction(Opcode.LIST_INDEX);
        }
        void GenerateCodeForStatement(Statement_ListValue listValue)
        {
            BuiltinType? listType = null;
            for (int i = 0; i < listValue.Size; i++)
            {
                var itemType = FindStatementType(listValue.Values[i]);
                BuiltinType itemTypeName = itemType.GetBuiltinType();

                if (itemTypeName == BuiltinType.VOID)
                { throw new CompilerException($"Unknown list item type {itemType.Name}", listValue.Values[i], CurrentFile); }

                if (i == 0)
                { listType = itemTypeName; }
                else if (itemTypeName != listType)
                { throw new CompilerException($"Wrong type {itemType}. Expected {listType}", listValue.Values[i], CurrentFile); }
            }
            if (listType == null)
            { throw new CompilerException($"Failed to get the type of the list", listValue, CurrentFile); }

            DataItem newList = new(new DataItem.List(listType.Value.Convert()), null);
            AddInstruction(Opcode.COMMENT, "Generate List {");
            AddInstruction(Opcode.PUSH_VALUE, newList);
            for (int i = 0; i < listValue.Size; i++)
            {
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -1);
                GenerateCodeForStatement(listValue.Values[i]);
                AddInstruction(Opcode.LIST_PUSH_ITEM);
            }
            AddInstruction(Opcode.COMMENT, "}");
        }

        void GenerateCodeForStatement(Statement st)
        {
            DebugInfo debugInfo = new()
            {
                InstructionStart = compiledCode.Count,
                InstructionEnd = compiledCode.Count,
            };
            if (st is StatementParent statementParent)
            { cleanupStack.Push(CompileVariables(statementParent, false)); }

            if (st is Statement_ListValue listValue)
            { GenerateCodeForStatement(listValue); }
            else if (st is Statement_NewVariable newVariable)
            { GenerateCodeForStatement(newVariable); }
            else if (st is Statement_FunctionCall functionCall)
            { GenerateCodeForStatement(functionCall); }
            else if (st is Statement_Operator @operator)
            { GenerateCodeForStatement(@operator); }
            else if (st is Statement_Setter setter)
            { GenerateCodeForStatement(setter); }
            else if (st is Statement_Literal literal)
            { GenerateCodeForStatement(literal); }
            else if (st is Statement_Variable variable)
            { GenerateCodeForStatement(variable); }
            else if (st is Statement_MemoryAddressGetter memoryAddressGetter)
            { GenerateCodeForStatement(memoryAddressGetter); }
            else if (st is Statement_MemoryAddressFinder memoryAddressFinder)
            { GenerateCodeForStatement(memoryAddressFinder); }
            else if (st is Statement_WhileLoop whileLoop)
            { GenerateCodeForStatement(whileLoop); }
            else if (st is Statement_ForLoop forLoop)
            { GenerateCodeForStatement(forLoop); }
            else if (st is Statement_If @if)
            { GenerateCodeForStatement(@if); }
            else if (st is Statement_NewInstance newStruct)
            { GenerateCodeForStatement(newStruct); }
            else if (st is Statement_Index indexStatement)
            { GenerateCodeForStatement(indexStatement); }
            else if (st is Statement_Field field)
            { GenerateCodeForStatement(field); }
            else
            {
                Output.Debug.Debug.Log("[Compiler]: Unimplemented statement " + st.GetType().Name);
            }

            if (st is StatementParent)
            {
                CleanupVariables(cleanupStack.Pop());
            }

            debugInfo.InstructionEnd = compiledCode.Count - 1;
            debugInfo.Position = st.TotalPosition();
            GeneratedDebugInfo.Add(debugInfo);
        }

        void GenerateCodeForValueGetter(int offset, bool heap, AddressingMode addressingMode)
        {
            Opcode code = heap ? Opcode.HEAP_GET : Opcode.LOAD_VALUE;

            switch (addressingMode)
            {
                case AddressingMode.ABSOLUTE:
                    AddInstruction(code, AddressingMode.ABSOLUTE, offset);
                    break;
                case AddressingMode.BASEPOINTER_RELATIVE:
                    AddInstruction(code, AddressingMode.BASEPOINTER_RELATIVE, offset);
                    break;
                case AddressingMode.RELATIVE:
                    AddInstruction(code, AddressingMode.RELATIVE, offset);
                    break;
                case AddressingMode.POP:
                    AddInstruction(code, AddressingMode.POP);
                    break;
                case AddressingMode.RUNTIME:
                    AddInstruction(Opcode.PUSH_VALUE, offset);
                    AddInstruction(code, AddressingMode.RUNTIME);
                    break;
                default: throw new NotImplementedException();
            }
        }
        void GenerateCodeForValueGetter(int pointerOffset, int offset, AddressingMode addressingMode)
        {
            GenerateCodeForValueGetter(pointerOffset, false, addressingMode);
            AddInstruction(Opcode.PUSH_VALUE, offset);
            AddInstruction(Opcode.MATH_ADD);
            AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
        }

        void GenerateCodeForValueSetter(int offset, AddressingMode addressingMode)
        {
            Opcode code = Opcode.STORE_VALUE;

            switch (addressingMode)
            {
                case AddressingMode.ABSOLUTE:
                    AddInstruction(code, AddressingMode.ABSOLUTE, offset);
                    break;
                case AddressingMode.BASEPOINTER_RELATIVE:
                    AddInstruction(code, AddressingMode.BASEPOINTER_RELATIVE, offset);
                    break;
                case AddressingMode.RELATIVE:
                    AddInstruction(code, AddressingMode.RELATIVE, offset);
                    break;
                case AddressingMode.POP:
                    AddInstruction(code, AddressingMode.POP);
                    break;
                case AddressingMode.RUNTIME:
                    AddInstruction(Opcode.PUSH_VALUE, offset);
                    AddInstruction(code, AddressingMode.RUNTIME);
                    break;
                default: throw new NotImplementedException();
            }
        }
        void GenerateCodeForValueSetter(int pointerOffset, int offset, AddressingMode addressingMode)
        {
            GenerateCodeForValueGetter(pointerOffset, false, addressingMode);
            AddInstruction(Opcode.PUSH_VALUE, offset);
            AddInstruction(Opcode.MATH_ADD);
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
        }

        CleanupItem CompileVariables(StatementParent statement, bool isGlobal, bool addComments = true)
        {
            if (addComments) AddInstruction(Opcode.COMMENT, "Variables");
            int count = 0;
            int variableSizeSum = 0;
            foreach (var s in statement.Statements)
            {
                var v = GenerateCodeForVariable(s, isGlobal);
                variableSizeSum += v.Size;
                count += v.Count;
            }
            return new CleanupItem(variableSizeSum, count);
        }
        void Pop(int n, string comment = null)
        {
            if (n <= 0) return;
            if (comment != null) AddInstruction(Opcode.COMMENT, comment);
            for (int x = 0; x < n; x++)
            { AddInstruction(Opcode.POP_VALUE); }
        }
        void PopVariable(int n)
        {
            for (int x = 0; x < n; x++)
            { compiledVariables.Remove(compiledVariables.Last().Key); }
        }

        void CleanupVariables(CleanupItem cleanupItem)
        {
            Pop(cleanupItem.Size, "Clear variables");
            PopVariable(cleanupItem.Count);
        }

        void GenerateCodeForValueSetter(Statement statementToSet, StatementWithReturnValue value)
        {
            if (statementToSet is Statement_Variable variable)
            { GenerateCodeForValueSetter(variable, value); }
            else if (statementToSet is Statement_Field field)
            { GenerateCodeForValueSetter(field, value); }
            else if (statementToSet is Statement_Index index)
            { GenerateCodeForValueSetter(index, value); }
            else if (statementToSet is Statement_MemoryAddressFinder memoryAddressGetter)
            { GenerateCodeForValueSetter(memoryAddressGetter, value); }
            else
            { throw new CompilerException("Unexpected statement", statementToSet.TotalPosition(), CurrentFile); }
        }
        void GenerateCodeForValueSetter(Statement_Variable statementToSet, StatementWithReturnValue value)
        {
            statementToSet.VariableName.Analysis.CompilerReached = true;

            if (GetParameter(statementToSet.VariableName.Content, out CompiledParameter parameter))
            {
                statementToSet.VariableName.Analysis.SubSubtype = TokenSubSubtype.ParameterName;
                statementToSet.VariableName.Analysis.Reference = new TokenAnalysis.RefParameter(parameter.Type.ToString());

                GenerateCodeForStatement(value);
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, parameter.RealIndex);
            }
            else if (GetCompiledVariable(statementToSet.VariableName.Content, out CompiledVariable valueMemoryIndex))
            {
                statementToSet.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                statementToSet.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(valueMemoryIndex, valueMemoryIndex.IsGlobal, valueMemoryIndex.Type);

                GenerateCodeForStatement(value);
                if (false && valueMemoryIndex.IsStoredInHEAP)
                {
                    AddInstruction(Opcode.HEAP_SET, valueMemoryIndex.Index);
                }
                else
                {
                    AddInstruction(Opcode.STORE_VALUE, valueMemoryIndex.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, valueMemoryIndex.Index);
                }
            }
            else
            {
                throw new CompilerException("Unknown variable '" + statementToSet.VariableName.Content + "'", statementToSet.VariableName, CurrentFile);
            }
        }
        void GenerateCodeForValueSetter(Statement_Field statementToSet, StatementWithReturnValue value)
        {
            statementToSet.FieldName.Analysis.CompilerReached = true;
            statementToSet.FieldName.Analysis.SubSubtype = TokenSubSubtype.FieldName;

            {
                var prevType = FindStatementType(statementToSet.PrevStatement);
                if (prevType.IsStruct || prevType.IsClass)
                {
                    var type = FindStatementType(statementToSet);
                    if (prevType.IsStruct)
                    { statementToSet.FieldName.Analysis.Reference = new TokenAnalysis.RefField(type.Name, statementToSet.FieldName, prevType.Struct.FullName, "struct", prevType.Struct.FilePath); }
                    else if (prevType.IsClass)
                    { statementToSet.FieldName.Analysis.Reference = new TokenAnalysis.RefField(type.Name, statementToSet.FieldName, prevType.Class.FullName, "class", prevType.Class.FilePath); }

                    if (prevType.IsStruct)
                    {
                        GenerateCodeForStatement(value);

                        int offset = GetDataAddress(statementToSet, out var addressingMode, out _);
                        GenerateCodeForValueSetter(offset, addressingMode);
                        return;
                    }

                    if (prevType.IsClass)
                    {
                        GenerateCodeForStatement(value);

                        int offset = GetFieldOffset(statementToSet);
                        int pointerOffset = GetBaseAdderss(statementToSet, out AddressingMode addressingMode);
                        GenerateCodeForValueSetter(pointerOffset, offset, addressingMode);
                        return;
                    }
                }
            }

            throw new NotImplementedException();
            /*
            if (statementToSet.PrevStatement is Statement_Variable variable1)
            {
                variable1.VariableName.Analysis.CompilerReached = true;

                if (GetParameter(variable1.VariableName.Content, out CompiledParameter parameter))
                {
                    variable1.VariableName.Analysis.SubSubtype = TokenSubSubtype.ParameterName;
                    variable1.VariableName.Analysis.Reference = new TokenAnalysis.RefParameter(parameter.Type.ToString());

                    GenerateCodeForStatement(value);
                    AddInstruction(Opcode.PUSH_VALUE, statementToSet.FieldName.Content);
                    AddInstruction(Opcode.STORE_FIELD, AddressingMode.BASEPOINTER_RELATIVE, parameter.RealIndex);
                }
                else if (GetCompiledVariable(variable1.VariableName.Content, out CompiledVariable valueMemoryIndex))
                {
                    variable1.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                    variable1.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(valueMemoryIndex, valueMemoryIndex.IsGlobal, valueMemoryIndex.Type);

                    if (valueMemoryIndex.IsStoredInHEAP)
                    {
                        AddInstruction(Opcode.HEAP_GET, valueMemoryIndex.Index);
                        AddInstruction(Opcode.COPY_VALUE_RECURSIVE);
                        GenerateCodeForStatement(value);
                        AddInstruction(Opcode.PUSH_VALUE, statementToSet.FieldName.Content);
                        AddInstruction(Opcode.STORE_FIELD, AddressingMode.POP);
                        AddInstruction(Opcode.HEAP_SET, valueMemoryIndex.Index);
                    }
                    else
                    {
                        GenerateCodeForStatement(value);
                        AddInstruction(Opcode.PUSH_VALUE, statementToSet.FieldName.Content);
                        AddInstruction(Opcode.STORE_FIELD, valueMemoryIndex.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, valueMemoryIndex.Index);
                    }
                }
                else
                {
                    throw new CompilerException("Unknown variable '" + variable1.VariableName.Content + "'", variable1.VariableName, CurrentFile);
                }
            }
            else
            { errors.Add(new Error($"Not implemented", statementToSet.TotalPosition(), CurrentFile)); }
            */
        }
        void GenerateCodeForValueSetter(Statement_Index statementToSet, StatementWithReturnValue value)
        {
            if (statementToSet.PrevStatement is Statement_Variable variable1)
            {
                variable1.VariableName.Analysis.CompilerReached = true;

                if (GetCompiledVariable(variable1.VariableName.Content, out CompiledVariable valueMemoryIndex))
                {
                    variable1.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                    variable1.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(valueMemoryIndex, valueMemoryIndex.IsGlobal, valueMemoryIndex.Type);

                    GenerateCodeForStatement(value);
                    AddInstruction(Opcode.LOAD_VALUE, valueMemoryIndex.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, valueMemoryIndex.Index);
                    GenerateCodeForStatement(statementToSet.Expression);
                    AddInstruction(Opcode.LIST_SET_ITEM);

                    AddInstruction(Opcode.STORE_VALUE, valueMemoryIndex.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, valueMemoryIndex.Index);
                }
                else
                {
                    throw new CompilerException("Unknown variable '" + variable1.VariableName.Content + "'", variable1.VariableName, CurrentFile);
                }
            }
            { errors.Add(new Error($"Not implemented", statementToSet.TotalPosition(), CurrentFile)); }
        }
        void GenerateCodeForValueSetter(Statement_MemoryAddressFinder statementToSet, StatementWithReturnValue value)
        {
            CompiledType targetType = FindStatementType(statementToSet.PrevStatement);
            CompiledType valueType = FindStatementType(value);

            if (targetType != valueType)
            { throw new CompilerException($"Can't set {targetType.FullName} to value of type {valueType.FullName}", value.TotalPosition(), CurrentFile); }

            if (targetType.SizeOnStack != 1) throw new NotImplementedException();

            GenerateCodeForStatement(value);
            GenerateCodeForStatement(statementToSet.PrevStatement);

            // TODO: set value by stack address
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
        }

        #endregion

        void MemoryCopyHeap(int to, int size)
        {
            AddInstruction(Opcode.COMMENT, $"Copy to heap: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                int currentReversedOffset = size - currentOffset - 1;

                AddInstruction(Opcode.COMMENT, $"Element {currentOffset}:");

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, (-currentReversedOffset) - 1);

                AddInstruction(Opcode.PUSH_VALUE, to + size);
                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddInstruction(Opcode.COMMENT, "}");
        }
        void MemoryCopyStack(int to, int size, bool basepointerRelative)
        {
            AddInstruction(Opcode.COMMENT, $"Copy to stack: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                int currentReversedOffset = size - currentOffset - 1;

                AddInstruction(Opcode.COMMENT, $"Element {currentOffset}:");

                int loadFrom = (-currentReversedOffset) - 1;
                int storeTo = to + currentOffset;

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, loadFrom);

                if (basepointerRelative)
                {
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, storeTo);
                }
                else
                {
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.ABSOLUTE, storeTo);
                }
            }
            AddInstruction(Opcode.COMMENT, "}");
        }

        void MemoryLoadHeap(int from, int size)
        {
            AddInstruction(Opcode.COMMENT, $"Load from heap: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddInstruction(Opcode.COMMENT, $"Element {currentOffset}:");

                AddInstruction(Opcode.PUSH_VALUE, currentOffset + from);
                AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
            }
            AddInstruction(Opcode.COMMENT, "}");
        }
        void MemoryLoadStack(int from, int size, bool basepointerRelative)
        {
            AddInstruction(Opcode.COMMENT, $"Load from stack: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddInstruction(Opcode.COMMENT, $"Element {currentOffset}:");

                int loadFrom = from + currentOffset;

                if (basepointerRelative)
                {
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, loadFrom);
                }
                else
                {
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, loadFrom);
                }
            }
            AddInstruction(Opcode.COMMENT, "}");
        }

        int GetReturnValueAddress() => 0 - (ParameterSizeSum() + 2);

        int GetDataAddress(Statement_Variable variable, out AddressingMode addressingMode, out bool heap)
        {
            if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
            {
                addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                heap = false;
                return GetDataAddress(param);
            }
            else if (GetCompiledVariable(variable.VariableName.Content, out CompiledVariable val))
            {
                if (val.IsStoredInHEAP)
                {
                    if (val.IsGlobal)
                    { addressingMode = AddressingMode.ABSOLUTE; }
                    else
                    { addressingMode = AddressingMode.BASEPOINTER_RELATIVE; }
                    heap = true;
                    return val.MemoryAddress;
                }
                else
                {
                    if (val.IsGlobal)
                    { addressingMode = AddressingMode.ABSOLUTE; }
                    else
                    { addressingMode = AddressingMode.BASEPOINTER_RELATIVE; }
                    heap = false;
                    return val.MemoryAddress;
                }
            }
            else
            {
                throw new CompilerException("Unknown variable '" + variable.VariableName.Content + "'", variable.VariableName, CurrentFile);
            }
        }
        int GetDataAddress(Statement_Field field, out AddressingMode addressingMode, out bool heap)
        {
            var prevType = FindStatementType(field.PrevStatement);
            if (prevType.IsStruct)
            {
                var @struct = prevType.Struct;
                if (@struct.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Statement_Variable _prevVar)
                    {
                        if (GetParameter(_prevVar.VariableName.Content, out CompiledParameter param))
                        {
                            addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                            heap = false;

                            return GetDataAddress(param, fieldOffset);
                        }
                        return fieldOffset + GetDataAddress(_prevVar, out addressingMode, out heap);
                    }
                    if (field.PrevStatement is Statement_Field _prevField) return fieldOffset + GetDataAddress(_prevField, out addressingMode, out heap);
                }
            }
            else if (prevType.IsClass)
            {
                var @class = prevType.Class;
                if (@class.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Statement_Variable _prevVar)
                    {
                        if (GetParameter(_prevVar.VariableName.Content, out CompiledParameter param))
                        {
                            addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                            heap = false;

                            return GetDataAddress(param, fieldOffset);
                        }
                        return fieldOffset + GetDataAddress(_prevVar, out addressingMode, out heap);
                    }
                    if (field.PrevStatement is Statement_Field _prevField) return fieldOffset + GetDataAddress(_prevField, out addressingMode, out heap);
                }
            }

            throw new NotImplementedException();
        }

        int GetFieldOffset(Statement_Field field)
        {
            var prevType = FindStatementType(field.PrevStatement);
            if (prevType.IsStruct)
            {
                var @struct = prevType.Struct;
                if (@struct.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Statement_Variable) return fieldOffset;
                    if (field.PrevStatement is Statement_Field _prevField) return fieldOffset + GetFieldOffset(_prevField);
                }
            }
            else if (prevType.IsClass)
            {
                var @class = prevType.Class;
                if (@class.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Statement_Variable) return fieldOffset;
                    if (field.PrevStatement is Statement_Field _prevField) return fieldOffset + GetFieldOffset(_prevField);
                }
            }

            throw new NotImplementedException();
        }

        int GetDataAddress(CompiledParameter parameter) => 0 - (ParameterSizeSum(parameter.Index) + 1);
        int GetDataAddress(CompiledParameter parameter, int offset) => 0 - ((ParameterSizeSum(parameter.Index) - offset) + 1);
        /// <summary>
        /// Returns the variable's address
        /// </summary>
        /// <exception cref="CompilerException"></exception>
        int GetBaseAddress(Statement_Variable variable, out AddressingMode addressingMode)
        {
            if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
            {
                addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                return GetDataAddress(param);
            }
            else if (GetCompiledVariable(variable.VariableName.Content, out CompiledVariable val))
            {
                addressingMode = val.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE;
                return val.MemoryAddress;
            }
            else
            {
                throw new CompilerException("Unknown variable '" + variable.VariableName.Content + "'", variable.VariableName, CurrentFile);
            }
        }
        /// <summary>
        /// Returns the field's base address.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        int GetBaseAdderss(Statement_Field field, out AddressingMode addressingMode)
        {
            CompiledType prevType = FindStatementType(field.PrevStatement);

            if (prevType.IsStruct || prevType.IsClass)
            {
                if (field.PrevStatement is Statement_Variable _prevVar) return GetBaseAddress(_prevVar, out addressingMode);
                if (field.PrevStatement is Statement_Field _prevField) return GetBaseAdderss(_prevField, out addressingMode);
            }

            throw new NotImplementedException();
        }

        bool IsItInHeap(Statement_Variable variable)
        {
            if (GetParameter(variable.VariableName.Content, out _))
            {
                return false;
            }
            else if (GetCompiledVariable(variable.VariableName.Content, out CompiledVariable val))
            {
                return val.IsStoredInHEAP;
            }
            else
            {
                throw new CompilerException("Unknown variable '" + variable.VariableName.Content + "'", variable.VariableName, CurrentFile);
            }
        }
        bool IsItInHeap(Statement_Field field)
        {
            var prevType = FindStatementType(field.PrevStatement);
            if (prevType.IsStruct)
            {
                if (field.PrevStatement is Statement_Variable _prevVar) return IsItInHeap(_prevVar);
                if (field.PrevStatement is Statement_Field _prevField) return IsItInHeap(_prevField);
            }
            else if (prevType.IsClass)
            {
                if (field.PrevStatement is Statement_Variable _prevVar) return IsItInHeap(_prevVar);
                if (field.PrevStatement is Statement_Field _prevField) return IsItInHeap(_prevField);
            }

            throw new NotImplementedException();
        }

        void Deallocate(CompiledVariable variable)
        {
            if (!variable.IsStoredInHEAP) return;
            AddInstruction(Opcode.COMMENT, "Deallocate {");
            for (int offset = 0; offset < variable.Type.Size; offset++)
            {
                AddInstruction(Opcode.PUSH_VALUE, DataItem.Null);
                AddInstruction(Opcode.PUSH_VALUE, offset);
                AddInstruction(Opcode.LOAD_VALUE, variable.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, variable.MemoryAddress);
                AddInstruction(Opcode.MATH_ADD);
                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddInstruction(Opcode.COMMENT, "}");
        }

        #region GenerateCodeFor...

        /*
        void GenerateCodeForGlobalVariable(Statement st)
        {
            if (st is Statement_NewVariable newVariable)
            {
                newVariable.VariableName.Analysis.CompilerReached = true;
                newVariable.Type.Analysis.CompilerReached = true;

                if (Keywords.Contains(newVariable.VariableName.Content))
                { throw new CompilerException($"Illegal variable name '{newVariable.VariableName.Content}'", newVariable.VariableName, newVariable.FilePath); }

                switch (newVariable.Type.Type)
                {
                    case BuiltinType.INT:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue1 = 0;
                        if (newVariable.Type.IsList)
                        {
                            initialValue1 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.Type == newVariable.Type.Type)
                                { initialValue1 = int.Parse(literal.Value); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true, new CompiledType(newVariable.Type, v => GetCustomType(v)));
                        compiledVariables.Add(newVariable.VariableName.Content, new CompiledVariable(0, -1, new CompiledType(newVariable.Type, v => GetCustomType(v)), true, false, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue1, "var." + newVariable.VariableName.Content);
                        break;
                    case BuiltinType.BYTE:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue5 = 0;
                        if (newVariable.Type.IsList)
                        {
                            initialValue5 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.Type == newVariable.Type.Type)
                                { initialValue5 = byte.Parse(literal.Value); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true, new CompiledType(newVariable.Type, v => GetCustomType(v)));
                        compiledVariables.Add(newVariable.VariableName.Content, new CompiledVariable(0, -1, new CompiledType(newVariable.Type, v => GetCustomType(v)), true, false, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue5, "var." + newVariable.VariableName.Content);
                        break;
                    case BuiltinType.FLOAT:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue2 = 0;
                        if (newVariable.Type.IsList)
                        {
                            initialValue2 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.Type == BuiltinType.FLOAT || literal.Type.Type == IngameCoding.BBCode.BuiltinType.INT)
                                { initialValue2 = float.Parse(literal.Value); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true, new CompiledType(newVariable.Type, v => GetCustomType(v)));
                        compiledVariables.Add(newVariable.VariableName.Content, new CompiledVariable(0, -1, new CompiledType(newVariable.Type, v => GetCustomType(v)), true, false, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue2, "var." + newVariable.VariableName.Content);
                        break;
                    case BuiltinType.STRING:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue3 = "";
                        if (newVariable.Type.IsList)
                        {
                            initialValue3 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.Type == BuiltinType.STRING)
                                { initialValue3 = literal.Value; }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true, new CompiledType(newVariable.Type, v => GetCustomType(v)));
                        compiledVariables.Add(newVariable.VariableName.Content, new CompiledVariable(0, -1, new CompiledType(newVariable.Type, v => GetCustomType(v)), true, false, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue3, "var." + newVariable.VariableName.Content);
                        break;
                    case BuiltinType.BOOLEAN:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue4 = false;
                        if (newVariable.Type.IsList)
                        {
                            initialValue4 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.Type == newVariable.Type.Type)
                                { initialValue4 = bool.Parse(literal.Value); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true, new CompiledType(newVariable.Type, v => GetCustomType(v)));
                        compiledVariables.Add(newVariable.VariableName.Content, new CompiledVariable(0, -1, new CompiledType(newVariable.Type, v => GetCustomType(v)), true, false, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue4, "var." + newVariable.VariableName.Content);
                        break;
                    case BuiltinType.STRUCT:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Struct;

                        if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_NewInstance newStruct)
                            {
                                if (newStruct.TypeName.Content == newVariable.Type.Content)
                                { GenerateCodeForStatement(newStruct); }
                                else
                                { throw new CompilerException("Can't cast " + newStruct.TypeName.Content + " to " + newVariable.Type.Content, newStruct.TotalPosition(), newVariable.FilePath); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true, new CompiledType(newVariable.Type, v => GetCustomType(v)));
                        compiledVariables.Add(newVariable.VariableName.Content, new CompiledVariable(0, -1, new CompiledType(newVariable.Type, v => GetCustomType(v)), true, false, newVariable));
                        break;
                    case BuiltinType.AUTO:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Keyword;

                        if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                newVariable.Type.Type = literal.Type.Type;
                            }
                            else if (newVariable.InitialValue is Statement_NewInstance newStruct)
                            {
                                newVariable.Type.Type = BuiltinType.STRUCT;
                                newVariable.Type.Content = newStruct.TypeName.Content;
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true, null);
                        if (newVariable.Type.Type == BuiltinType.AUTO)
                        { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }
                        GenerateCodeForGlobalVariable(newVariable);
                        break;
                }
            }
        }
        */

        /// <returns>The variable's size</returns>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateCodeForVariable(Statement_NewVariable newVariable, bool isGlobal)
        {
            if (newVariable.Type.Type == TypeTokenType.AUTO)
            {
                if (newVariable.InitialValue != null)
                {
                    if (newVariable.InitialValue is Statement_Literal literal)
                    {
                        newVariable.Type.Type = literal.Type.Type;
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false, new CompiledType(literal.Type, v => GetCustomType(v)));
                    }
                    else if (newVariable.InitialValue is Statement_NewInstance newStruct)
                    {
                        newVariable.Type.Type = TypeTokenType.USER_DEFINED;
                        newVariable.Type.Content = newStruct.TypeName.Content;
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false, FindStatementType(newStruct));
                    }
                    else
                    {
                        var initialTypeRaw = FindStatementType(newVariable.InitialValue);

                        var initialType = Parser.ParseType(initialTypeRaw.Name);
                        newVariable.Type.Type = initialType.Type;
                        newVariable.Type.ListOf = initialType.ListOf;
                        newVariable.Type.Content = initialType.Content;

                        if (initialTypeRaw.IsStruct) newVariable.Type.NamespacePrefix = initialTypeRaw.Struct.NamespacePathString;
                        if (initialTypeRaw.IsClass) newVariable.Type.NamespacePrefix = initialTypeRaw.Class.NamespacePathString;

                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false, initialTypeRaw);

                        GenerateCodeForVariable(newVariable, isGlobal);
                        return 1;
                    }
                }
                else
                { throw new CompilerException($"Initial value for 'var' variable declaration is requied", newVariable.Type); }

                if (newVariable.Type.Type == TypeTokenType.AUTO)
                { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }

                GenerateCodeForVariable(newVariable, isGlobal);
                return 1;
            }

            newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false, new CompiledType(newVariable.Type, v => GetCustomType(v)));

            compiledVariables.Add(newVariable.VariableName.Content, GetVariableInfo(newVariable, compiledVariables.Count, GetVariableSizesSum(), isGlobal));

            AddInstruction(Opcode.COMMENT, $"Initial value {{");

            int size = GenerateInitialValue(newVariable.Type, "var." + newVariable.VariableName.Content);

            AddInstruction(Opcode.COMMENT, "}");

            return size;
        }
        CleanupItem GenerateCodeForVariable(Statement st, bool isGlobal)
        {
            if (st is Statement_NewVariable newVariable)
            {
                int size = GenerateCodeForVariable(newVariable, isGlobal);
                return new CleanupItem(size, 1);
            }
            return new CleanupItem(0, 0);
        }
        CleanupItem GenerateCodeForVariable(Statement[] sts, bool isGlobal)
        {
            int count = 0;
            int size = 0;
            for (int i = 0; i < sts.Length; i++)
            {
                var v = GenerateCodeForVariable(sts[i], isGlobal);
                size += v.Size;
                count += v.Count;
            }
            return new CleanupItem(size, count);
        }

        int GetVariableSizesSum()
        {
            int sum = 0;
            for (int i = 0; i < compiledVariables.Count; i++)
            {
                var key = compiledVariables.ElementAt(i).Key;
                if (compiledVariables.Get(key).IsGlobal) continue;
                if (compiledVariables.Get(key).Type.IsClass) sum++;
                else sum += compiledVariables.Get(key).Type.Size;
            }
            return sum;
        }

        void ClearLocalVariables()
        {
            for (int i = compiledVariables.Count - 1; i >= 0; i--)
            {
                var key = compiledVariables.ElementAt(i).Key;
                if (compiledVariables.Get(key).IsGlobal) continue;
                compiledVariables.Remove(key);
            }
        }

        void GenerateCodeForFunction(CompiledFunction function)
        {
            function.Identifier.Analysis.CompilerReached = true;
            function.TypeToken.Analysis.CompilerReached = true;
            CurrentFunction_ForRecursionProtection = function.ID();

            if (Keywords.Contains(function.Identifier.Content))
            { throw new CompilerException($"Illegal function name '{function.Identifier.Content}'", function.Identifier, function.FilePath); }

            function.Identifier.Analysis.SubSubtype = TokenSubSubtype.FunctionName;

            if (function.IsBuiltin) return;

            parameters.Clear();
            ClearLocalVariables();
            returnInstructions.Clear();

            // Compile parameters
            int paramIndex = 0;
            int paramsSize = 0;
            foreach (ParameterDefinition parameter in function.Parameters)
            {
                paramIndex++;
                parameter.Identifier.Analysis.CompilerReached = true;
                parameter.Type.Analysis.CompilerReached = true;
                parameters.Add(new CompiledParameter(paramIndex, paramsSize, function.Parameters.Length, new CompiledType(parameter.Type, v => GetCustomType(v)), parameter));

                if (parameter.Type.Type == TypeTokenType.USER_DEFINED)
                {
                    var paramType = GetCustomType(parameter.Type.Content);
                    if (paramType is CompiledStruct @struct)
                    {
                        paramsSize += @struct.Size;
                    }
                    else if (paramType is CompiledClass @class)
                    {
                        paramsSize += 1;
                    }
                }
                else
                {
                    paramsSize++;
                }
            }

            CurrentNamespace = function.NamespacePath;
            CurrentFile = function.FilePath;

            AddInstruction(Opcode.CS_PUSH, $"{function.ReadableID()};{CurrentFile};{compiledCode.Count};{function.Identifier.Position.Start.Line}");

            // Search for variables
            AddInstruction(Opcode.COMMENT, "Variables");
            cleanupStack.Push(GenerateCodeForVariable(function.Statements, false));

            // Compile statements
            if (function.Statements.Length > 0)
            {
                AddInstruction(Opcode.COMMENT, "Statements");
                foreach (Statement statement in function.Statements)
                {
                    GenerateCodeForStatement(statement);
                }
            }

            CurrentFile = null;
            CurrentNamespace = Array.Empty<string>();

            int cleanupCodeOffset = compiledCode.Count;

            for (int i = 0; i < returnInstructions.Count; i++)
            { compiledCode[returnInstructions[i]].Parameter = cleanupCodeOffset - returnInstructions[i]; }

            AddInstruction(Opcode.COMMENT, "Cleanup {");

            CleanupVariables(cleanupStack.Pop());

            AddInstruction(Opcode.COMMENT, "}");

            AddInstruction(Opcode.COMMENT, "Return");
            AddInstruction(Opcode.CS_POP);
            AddInstruction(Opcode.RETURN);

            parameters.Clear();
            ClearLocalVariables();
            returnInstructions.Clear();

            CurrentFunction_ForRecursionProtection = null;
        }

        void GenerateCodeForStruct(KeyValuePair<string, StructDefinition> @struct, Dictionary<string, Func<IStruct>> builtinStructs)
        {
            CurrentFile = @struct.Value.FilePath;
            CurrentNamespace = @struct.Value.NamespacePath;

            @struct.Value.Name.Analysis.CompilerReached = true;

            if (Keywords.Contains(@struct.Key))
            { throw new CompilerException($"Illegal struct name '{@struct.Value.FullName}'", @struct.Value.Name, CurrentFile); }

            @struct.Value.Name.Analysis.SubSubtype = TokenSubSubtype.Struct;

            if (compiledStructs.ContainsKey(@struct.Value.FullName))
            { throw new CompilerException($"Struct with name '{@struct.Value.FullName}' already exist", @struct.Value.Name, CurrentFile); }

            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in @struct.Value.Attributes)
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

#if false
            if (attributes.TryGetValue("Builtin", out var attributeBuiltin))
            {
                if (attributeBuiltin.parameters.Count != 1)
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.NameToken, CurrentFile); }
                if (attributeBuiltin.TryGetValue(0, out string paramBuiltinName))
                {
                    foreach (var builtinStruct in builtinStructs)
                    {
                        if (builtinStruct.Key.ToLower() == paramBuiltinName.ToLower())
                        {
                            this.compiledStructs.Add(@struct.Key,
                                new CompiledStruct(attributes, @struct.Value)
                                { CreateBuiltinStructCallback = builtinStruct.Value });

                            foreach (var method in @struct.Value.Methods)
                            {
                                if (compiledFunctions.ContainsKey(method.Key))
                                { throw new CompilerException($"Function with name '{method.Key}' already defined", method.Value.Name, CurrentFile); }

                                var methodInfo = GetFunctionInfo(method, true);

                                this.compiledFunctions.Add(method.Value.FullName, methodInfo);

                                AddInstruction(Opcode.COMMENT, @struct.Value.FullName + "." + method.Value.FullName + ((method.Value.Parameters.Count > 0) ? "(...)" : "()") + " {");
                                GenerateCodeForFunction(method, true);
                                AddInstruction(Opcode.COMMENT, "}");
                                this.compiledStructs.Last().Value.CompiledMethods.Add(method.Key, methodInfo);
                            }

                            CurrentFile = null;
                            return;
                        }
                    }
                    throw new CompilerException("Builtin struct '" + paramBuiltinName.ToLower() + "' not found", attributeBuiltin.NameToken, CurrentFile);
                }
                else
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.NameToken, CurrentFile); }
            }
#endif

            foreach (var field in @struct.Value.Fields)
            {
                field.Identifier.Analysis.CompilerReached = true;
                field.Type.Analysis.CompilerReached = true;
            }

            this.compiledStructs.Add(new CompiledStruct(attributes, @struct.Value)
            { References = SaveDefinitionReferences ? new List<DefinitionReference>() : null });

            CurrentFile = null;
            CurrentNamespace = Array.Empty<string>();
        }

        void GenerateCodeForClass(KeyValuePair<string, ClassDefinition> @class)
        {
            CurrentFile = @class.Value.FilePath;
            CurrentNamespace = @class.Value.NamespacePath;

            @class.Value.Name.Analysis.CompilerReached = true;

            if (Keywords.Contains(@class.Key))
            { throw new CompilerException($"Illegal class name '{@class.Value.FullName}'", @class.Value.Name, CurrentFile); }

            @class.Value.Name.Analysis.SubSubtype = TokenSubSubtype.Struct;

            if (compiledClasses.ContainsKey(@class.Value.FullName))
            { throw new CompilerException($"Class with name '{@class.Value.FullName}' already exist", @class.Value.Name, CurrentFile); }

            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in @class.Value.Attributes)
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

            foreach (var field in @class.Value.Fields)
            {
                field.Identifier.Analysis.CompilerReached = true;
                field.Type.Analysis.CompilerReached = true;
            }

            this.compiledClasses.Add(new CompiledClass(attributes, @class.Value)
            { References = SaveDefinitionReferences ? new List<DefinitionReference>() : null });

            CurrentFile = null;
            CurrentNamespace = Array.Empty<string>();
        }

        #endregion

        CompiledVariable GetVariableInfo(Statement_NewVariable newVariable, int index, int memoryOffset, bool isGlobal)
        {
            newVariable.VariableName.Analysis.CompilerReached = true;
            newVariable.Type.Analysis.CompilerReached = true;

            if (Keywords.Contains(newVariable.VariableName.Content))
            { throw new CompilerException($"Illegal variable name '{newVariable.VariableName.Content}'", newVariable.VariableName, CurrentFile); }

            return new CompiledVariable(index, memoryOffset, new CompiledType(newVariable.Type, v => GetCustomType(v)), isGlobal, GetCompiledClass(newVariable.Type.Content, out _), newVariable);
        }

        #region Result Structs

        public struct CodeGeneratorResult
        {
            public Instruction[] compiledCode;
            internal DebugInfo[] DebugInfo;

            public CompiledFunction[] compiledFunctions;
            public CompiledStruct[] compiledStructs;

            public int clearGlobalVariablesInstruction;
            public int setGlobalVariablesInstruction;
        }

        #endregion

        CompiledFunction GetFunctionInfo(KeyValuePair<string, FunctionDefinition> function)
        {
            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in function.Value.Attributes)
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

            CompiledType type = new(function.Value.Type, name => GetCustomType(name, function.Value.NamespacePathString));

            if (attributes.TryGetValue("Builtin", out var attributeBuiltin))
            {
                if (attributeBuiltin.parameters.Count != 1)
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.Identifier, function.Value.FilePath); }
                if (attributeBuiltin.TryGetValue(0, out string paramBuiltinName))
                {
                    foreach (var builtinFunction in builtinFunctions)
                    {
                        if (builtinFunction.Key.ToLower() == paramBuiltinName.ToLower())
                        {
                            if (builtinFunction.Value.ParameterCount != function.Value.Parameters.Length)
                            { throw new CompilerException("Wrong number of parameters passed to builtin function '" + builtinFunction.Key + "'", function.Value.Identifier, function.Value.FilePath); }
                            if (builtinFunction.Value.ReturnSomething != (function.Value.Type.Type != TypeTokenType.VOID))
                            { throw new CompilerException("Wrong type definied for builtin function '" + builtinFunction.Key + "'", function.Value.Type, function.Value.FilePath); }

                            for (int i = 0; i < builtinFunction.Value.ParameterTypes.Length; i++)
                            {
                                if (builtinFunction.Value.ParameterTypes[i].Type == TypeTokenType.ANY) continue;

                                if (builtinFunction.Value.ParameterTypes[i].Type != function.Value.Parameters[i].Type.Type)
                                { throw new CompilerException("Wrong type of parameter passed to builtin function '" + builtinFunction.Key + $"'. Parameter index: {i} Requied type: {builtinFunction.Value.ParameterTypes[i].Type.ToString().ToLower()} Passed: {function.Value.Parameters[i].Type.Type.ToString().ToLower()}", function.Value.Parameters[i].Type, function.Value.FilePath); }
                            }

                            return new CompiledFunction(function.Value.ID(), type, function.Value)
                            {
                                ParameterTypes = builtinFunction.Value.ParameterTypes.Select(v => new CompiledType(v, t => GetCustomType(t))).ToArray(),
                                CompiledAttributes = attributes,
                                References = SaveDefinitionReferences ? new List<DefinitionReference>() : null,
                            };
                        }
                    }

                    errors.Add(new Error("Builtin function '" + paramBuiltinName.ToLower() + "' not found", attributeBuiltin.Identifier, function.Value.FilePath));
                    return new CompiledFunction(
                        function.Value.ID(),
                        type,
                        function.Value.Parameters.Select(v => new CompiledType(v.Type, t => GetCustomType(t))).ToArray(),
                        function.Value
                        )
                    {
                        CompiledAttributes = attributes,
                        References = SaveDefinitionReferences ? new List<DefinitionReference>() : null,
                    };
                }
                else
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.Identifier, function.Value.FilePath); }
            }

            return new CompiledFunction(
                function.Value.ID(),
                type,
                function.Value.Parameters.Select(v => new CompiledType(v.Type, t => GetCustomType(t))).ToArray(),
                function.Value
                )
            {
                CompiledAttributes = attributes,
                References = SaveDefinitionReferences ? new List<DefinitionReference>() : null,
            };
        }

        int AnalyzeFunctions(List<CompiledFunction> functions, Action<string, Output.LogType> printCallback = null)
        {
            printCallback?.Invoke($"  Remove unused functions ...", Output.LogType.Debug);

            // Remove unused functions
            {
                FunctionDefinition currentFunction = null;

                void AnalyzeNewVariable(Statement_NewVariable newVariable)
                {
                    if (newVariable.Type.Type == TypeTokenType.AUTO)
                    {
                        if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                newVariable.Type.Type = literal.Type.Type;
                            }
                            else if (newVariable.InitialValue is Statement_NewInstance newStruct)
                            {
                                newVariable.Type.Type = TypeTokenType.USER_DEFINED;
                                newVariable.Type.Content = newStruct.TypeName.Content;
                            }
                            else
                            {
                                var initialTypeRaw = FindStatementType(newVariable.InitialValue);
                                var initialType = Parser.ParseType(initialTypeRaw.Name);

                                newVariable.Type.Type = initialType.Type;
                                newVariable.Type.ListOf = initialType.ListOf;
                                newVariable.Type.Content = initialType.Content;
                            }
                        }
                        else
                        { throw new CompilerException($"Initial value for 'var' variable declaration is requied", newVariable.Type); }

                        if (newVariable.Type.Type == TypeTokenType.AUTO)
                        { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }
                    }
                    this.compiledVariables.Add(newVariable.VariableName.Content, GetVariableInfo(newVariable, -1, -1, false));
                }
                void AnalyzeStatements(IEnumerable<Statement> statements)
                {
                    int variablesAdded = 0;
                    foreach (var st in statements)
                    {
                        if (st is Statement_NewVariable newVar)
                        {
                            AnalyzeNewVariable(newVar);
                            variablesAdded++;
                        }
                        else if (st is Statement_ForLoop forLoop)
                        {
                            AnalyzeNewVariable(forLoop.VariableDeclaration);
                            variablesAdded++;
                        }
                    }

                    foreach (var st in statements)
                    {
                        AnalyzeStatement(st);
                        if (st is StatementParent pr)
                        {
                            AnalyzeStatements(pr.Statements);
                        }
                    }

                    for (int i = 0; i < variablesAdded; i++)
                    {
                        this.compiledVariables.Remove(this.compiledVariables.ElementAt(this.compiledVariables.Count - 1).Key);
                    }
                }

                void AnalyzeStatement(Statement st)
                {
                    if (st is Statement_ForLoop st0)
                    {
                        AnalyzeStatement(st0.VariableDeclaration);
                        AnalyzeStatement(st0.Condition);
                        AnalyzeStatement(st0.Expression);
                    }
                    else if (st is Statement_If st1)
                    {
                        foreach (var st2 in st1.Parts)
                        { AnalyzeStatement(st2); }
                    }
                    else if (st is Statement_If_If st2)
                    {
                        AnalyzeStatement(st2.Condition);
                        AnalyzeStatements(st2.Statements);
                    }
                    else if (st is Statement_If_ElseIf st3)
                    {
                        AnalyzeStatement(st3.Condition);
                        AnalyzeStatements(st3.Statements);
                    }
                    else if (st is Statement_If_Else st3a)
                    {
                        AnalyzeStatements(st3a.Statements);
                    }
                    else if (st is Statement_Index st4)
                    {
                        AnalyzeStatement(st4.Expression);
                    }
                    else if (st is Statement_NewVariable st5)
                    {
                        if (st5.InitialValue != null) AnalyzeStatement(st5.InitialValue);
                    }
                    else if (st is Statement_Operator st6)
                    {
                        if (st6.Left != null) AnalyzeStatement(st6.Left);
                        if (st6.Right != null) AnalyzeStatement(st6.Right);
                    }
                    else if (st is Statement_Setter setter)
                    {
                        AnalyzeStatement(setter.Left);
                        AnalyzeStatement(setter.Right);
                    }
                    else if (st is Statement_WhileLoop st7)
                    {
                        AnalyzeStatement(st7.Condition);
                    }
                    else if (st is Statement_FunctionCall st8)
                    {
                        foreach (var st9 in st8.Parameters)
                        { AnalyzeStatement(st9); }

                        if (st8.PrevStatement != null)
                        { AnalyzeStatement(st8.PrevStatement); }

                        if (!BuiltinFunctions.Contains(st8.FunctionName) && GetCompiledFunction(st8, out var cf))
                        {
                            var thisID = cf.ID();
                            var currentID = currentFunction.ID();
                            if (thisID != currentID)
                            {
                                cf.TimesUsed++;
                            }
                            cf.TimesUsedTotal++;
                        }
                    }
                    else if (st is Statement_Field st9)
                    { AnalyzeStatement(st9.PrevStatement); }
                    else if (st is Statement_Variable)
                    { }
                    else if (st is Statement_NewInstance)
                    { }
                    else if (st is Statement_Literal)
                    { }
                    else if (st is Statement_MemoryAddressGetter)
                    { }
                    else if (st is Statement_MemoryAddressFinder)
                    { }
                    else if (st is Statement_ListValue st10)
                    { AnalyzeStatements(st10.Values); }
                    else
                    { throw new CompilerException($"Unknown statement {st.GetType().Name}", st, CurrentFile); }
                }

                foreach (var f in functions)
                {
                    if (compiledFunctions.TryGetValue(f.ID, out CompiledFunction compiledFunction))
                    { compiledFunction.TimesUsed = 0; }
                }

                foreach (var f in functions)
                {
                    parameters.Clear();
                    foreach (ParameterDefinition parameter in f.Parameters)
                    { parameters.Add(new CompiledParameter(-1, -1, -1, new CompiledType(parameter.Type, v => GetCustomType(v)), parameter)); }
                    CurrentFile = f.FilePath;

                    currentFunction = f;
                    AnalyzeStatements(f.Statements);

                    CurrentFile = null;
                    parameters.Clear();
                }
            }

            printCallback?.Invoke($"   Processing ...", Output.LogType.Debug);

            int functionsRemoved = 0;

            for (int i = functions.Count - 1; i >= 0; i--)
            {
                var element = functions.ElementAt(i);

                if (!this.compiledFunctions.TryGetValue(element.ID, out CompiledFunction f)) continue;
                if (f.TimesUsed > 0) continue;
                foreach (var attr in f.CompiledAttributes)
                {
                    if (attr.Key == "CodeEntry") goto JumpOut;
                    if (attr.Key == "Catch") goto JumpOut;
                }

                if (CompileLevel == Compiler.CompileLevel.All) continue;
                if (CompileLevel == Compiler.CompileLevel.Exported && f.IsExport) continue;

                string readableID = element.ReadableID();

                printCallback?.Invoke($"      Remove function '{readableID}' ...", Output.LogType.Debug);
                informations.Add(new Information($"Unused function '{readableID}' is not compiled", element.Identifier, element.FilePath));

                bool _ = Extensions.Remove<string>(functions, element.Key);
                functionsRemoved++;

            JumpOut:;
            }

            return functionsRemoved;
        }

        internal CodeGeneratorResult GenerateCode(
            Dictionary<string, FunctionDefinition> functions,
            Dictionary<string, StructDefinition> structs,
            Dictionary<string, ClassDefinition> classes,
            Statement_HashInfo[] hashes,
            List<Statement_NewVariable> globalVariables,
            Dictionary<string, BuiltinFunction> builtinFunctions,
            Dictionary<string, Func<IStruct>> builtinStructs,
            Compiler.CompilerSettings settings,
            Action<string, Output.LogType> printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal,
            bool saveReferences = false)
        {
            BlockCodeGeneration = true;

            this.GenerateDebugInstructions = settings.GenerateDebugInstructions;
            this.AddCommentsToCode = settings.GenerateComments;
            this.compiledStructs = new();
            this.compiledClasses = new();
            this.compiledVariables = new();
            this.compiledCode = new();
            this.builtinFunctions = builtinFunctions;
            this.OptimizeCode = !settings.DontOptimize;
            this.compiledFunctions = new();
            this.GeneratedDebugInfo.Clear();
            this.CompileLevel = level;
            this.cleanupStack = new();
            this.parameters = new();
            this.returnInstructions = new();
            this.breakInstructions = new();
            this.undefinedFunctionOffsets = new();
            this.CurrentNamespace = Array.Empty<string>();
            this.SaveDefinitionReferences = saveReferences;

            #region Compile test built-in functions

            foreach (var hash in hashes)
            {
                switch (hash.HashName.Content)
                {
                    case "bf":
                        {
                            if (hash.Parameters.Length <= 1)
                            { errors.Add(new Error($"Hash '{hash.HashName}' requies minimum 2 parameter", hash.HashName, hash.FilePath)); break; }
                            string bfName = hash.Parameters[0].Value;

                            if (builtinFunctions.ContainsKey(bfName)) break;

                            string[] bfParams = new string[hash.Parameters.Length - 1];
                            for (int i = 1; i < hash.Parameters.Length; i++)
                            { bfParams[i - 1] = hash.Parameters[i].Value; }

                            TypeToken[] parameterTypes = new TypeToken[bfParams.Length];
                            for (int i = 0; i < bfParams.Length; i++)
                            {
                                switch (bfParams[i])
                                {
                                    case "void":
                                        if (i > 0)
                                        { errors.Add(new Error($"Invalid type \"{bfParams[i]}\"", hash.Parameters[i + 1].ValueToken, hash.FilePath)); goto ExitBreak; }
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.VOID);
                                        break;
                                    case "int":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.INT);
                                        break;
                                    case "string":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.STRING);
                                        break;
                                    case "float":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.FLOAT);
                                        break;
                                    case "bool":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.BOOLEAN);
                                        break;
                                    case "byte":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.BYTE);
                                        break;
                                    case "int[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("int", BuiltinType.INT));
                                        break;
                                    case "string[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("string", BuiltinType.STRING));
                                        break;
                                    case "float[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("float", BuiltinType.FLOAT));
                                        break;
                                    case "bool[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("bool", BuiltinType.BOOLEAN));
                                        break;
                                    case "byte[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("byte", BuiltinType.BYTE));
                                        break;
                                    default:
                                        errors.Add(new Error($"Unknown type \"{bfParams[i]}\"", hash.Parameters[i + 1].ValueToken, hash.FilePath));
                                        goto ExitBreak;
                                }
                            }

                            var returnType = parameterTypes[0];
                            var x = parameterTypes.ToList();
                            x.RemoveAt(0);
                            var pTypes = x.ToArray();

                            if (parameterTypes[0].Type == TypeTokenType.VOID)
                            {
                                builtinFunctions.AddBuiltinFunction(bfName, pTypes, new Action<DataItem[]>((p) =>
                                {
                                    Output.Output.Debug($"Built-in function \"{bfName}\" called with params:\n  {string.Join(", ", p)}");
                                }), false);
                            }
                            else
                            {
                                builtinFunctions.Add(bfName, new BuiltinFunction(new Action<DataItem[], BuiltinFunction>((p, self) =>
                                {
                                    Output.Output.Debug($"Built-in function \"{bfName}\" called with params:\n  {string.Join(", ", p)}");
                                    DataItem returnValue = returnType.Type switch
                                    {
                                        TypeTokenType.INT => returnType.IsList ? new DataItem(new DataItem.List(DataType.INT)) : new DataItem((int)0),
                                        TypeTokenType.BYTE => returnType.IsList ? new DataItem(new DataItem.List(DataType.BYTE)) : new DataItem((byte)0),
                                        TypeTokenType.FLOAT => returnType.IsList ? new DataItem(new DataItem.List(DataType.FLOAT)) : new DataItem((float)0),
                                        TypeTokenType.STRING => returnType.IsList ? new DataItem(new DataItem.List(DataType.STRING)) : new DataItem(""),
                                        TypeTokenType.BOOLEAN => returnType.IsList ? new DataItem(new DataItem.List(DataType.BOOLEAN)) : new DataItem((bool)false),
                                        TypeTokenType.USER_DEFINED => returnType.IsList ? new DataItem(new DataItem.List(DataType.STRUCT)) : new DataItem(new UnassignedStruct()),
                                        _ => throw new RuntimeException($"Invalid return type \"{returnType.Content}\"/{returnType.ToString().ToLower()} from built-in function \"{bfName}\""),
                                    };
                                    returnValue.Tag = "return value";
                                    self.RaiseReturnEvent(returnValue);
                                }), bfName, pTypes, true));
                            }
                        }
                        break;
                    default:
                        warnings.Add(new Warning($"Hash '{hash.HashName}' does not exists, so this is ignored", hash.HashName, hash.FilePath));
                        break;
                }

            ExitBreak:
                continue;
            }

            #endregion

            #region Compile Classes

            BlockCodeGeneration = false;

            foreach (var @class in classes)
            { GenerateCodeForClass(@class); }

            BlockCodeGeneration = true;

            #endregion

            #region Compile Structs

            BlockCodeGeneration = false;

            foreach (var @struct in structs)
            { GenerateCodeForStruct(@struct, builtinStructs); }

            BlockCodeGeneration = true;

            #endregion

            #region Analyse Struct Fields

            BlockCodeGeneration = false;

            foreach (var @struct in structs)
            {
                CurrentFile = @struct.Value.FilePath;
                CurrentNamespace = @struct.Value.NamespacePath;

                foreach (var field in @struct.Value.Fields)
                {
                    if (compiledStructs.TryGetValue(field.Type.Content, out var fieldStructType))
                    {
                        field.Type.Analysis.SubSubtype = TokenSubSubtype.Struct;
                        field.Type.Analysis.Reference = new TokenAnalysis.RefStruct((CompiledStruct)fieldStructType);
                    }
                }

                CurrentFile = null;
                CurrentNamespace = Array.Empty<string>();
            }

            BlockCodeGeneration = true;

            #endregion

            #region Set DataStructure Sizes

            foreach (var @struct in compiledStructs)
            {
                int size = 0;
                foreach (var field in @struct.Fields)
                {
                    size++;
                }
                @struct.Size = size;
            }
            foreach (var @class in compiledClasses)
            {
                int size = 0;
                foreach (var field in @class.Fields)
                {
                    size++;
                }
                @class.Size = size;
            }

            #endregion

            #region Set Field Offsets

            foreach (var @struct in compiledStructs)
            {
                int currentOffset = 0;
                foreach (var field in @struct.Fields)
                {
                    @struct.FieldOffsets.Add(field.Identifier.Content, currentOffset);
                    switch (field.Type.Type)
                    {
                        case TypeTokenType.BYTE:
                        case TypeTokenType.INT:
                        case TypeTokenType.FLOAT:
                        case TypeTokenType.STRING:
                        case TypeTokenType.BOOLEAN:
                            currentOffset++;
                            break;
                        default:
                            break;
                    }
                }
            }
            foreach (var @class in compiledClasses)
            {
                int currentOffset = 0;
                foreach (var field in @class.Fields)
                {
                    @class.FieldOffsets.Add(field.Identifier.Content, currentOffset);
                    switch (field.Type.Type)
                    {
                        case TypeTokenType.BYTE:
                        case TypeTokenType.INT:
                        case TypeTokenType.FLOAT:
                        case TypeTokenType.STRING:
                        case TypeTokenType.BOOLEAN:
                            currentOffset++;
                            break;
                        default:
                            break;
                    }
                }
            }

            #endregion

            #region Compile Functions

            foreach (var function in functions)
            {
                CurrentFile = function.Value.FilePath;
                CurrentNamespace = function.Value.NamespacePath;

                var id = function.Value.ID();

                if (this.compiledFunctions.ContainsKey(id))
                { throw new CompilerException($"Function with name '{id}' already defined", function.Value.Identifier, function.Value.FilePath); }

                this.compiledFunctions.Add(GetFunctionInfo(function));

                CurrentFile = null;
                CurrentNamespace = Array.Empty<string>();
            }

            #endregion

            #region Remove Unused Functions

            int iterations = settings.RemoveUnusedFunctionsMaxIterations;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int functionsRemoved = AnalyzeFunctions(this.compiledFunctions, printCallback);
                if (functionsRemoved == 0)
                {
                    printCallback?.Invoke($"  Deletion of unused functions is complete", Output.LogType.Debug);
                    break;
                }

                printCallback?.Invoke($"  Removed {functionsRemoved} unused functions (iteration {iteration})", Output.LogType.Debug);
            }

            #endregion

            #region Code Generation

            BlockCodeGeneration = false;

            AddInstruction(Opcode.COMMENT, "Global variables");
            var setGlobalVariablesInstruction = compiledCode.Count;
            if (globalVariables.Count > 0)
            {
                AddInstruction(Opcode.CS_PUSH, "state: SetGlobalVariables");
                foreach (var globalVariable in globalVariables)
                {
                    GenerateCodeForVariable(globalVariable, true);
                }
                AddInstruction(Opcode.CS_POP);
            }
            AddInstruction(Opcode.EXIT);

            foreach (var function in this.compiledFunctions)
            {
                function.InstructionOffset = compiledCode.Count;

                AddInstruction(Opcode.COMMENT, function.FullName + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Statements.Length > 0) ? "" : " }"));
                GenerateCodeForFunction(function);
                if (function.Statements.Length > 0) AddInstruction(Opcode.COMMENT, "}");
            }

            AddInstruction(Opcode.COMMENT, "Clear global variables");
            var clearGlobalVariablesInstruction = compiledCode.Count;
            if (globalVariables.Count > 0)
            {
                AddInstruction(Opcode.CS_PUSH, "state: DisposeGlobalVariables");
                for (int i = 0; i < globalVariables.Count; i++)
                { AddInstruction(Opcode.POP_VALUE); }
                compiledVariables.Clear();
                AddInstruction(Opcode.CS_POP);
            }
            AddInstruction(Opcode.EXIT);

            BlockCodeGeneration = true;

            #endregion

            foreach (UndefinedFunctionOffset item in undefinedFunctionOffsets)
            {
                foreach (var pair in item.currentParameters)
                { parameters.Add(pair); }
                foreach (var pair in item.currentVariables)
                { compiledVariables.Add(pair.Key, pair.Value); }

                if (!GetCompiledFunction(item.CallStatement, out var function))
                {
                    string searchedID = item.CallStatement.TargetNamespacePathPrefix + item.CallStatement.FunctionName;
                    searchedID += "(";
                    for (int i = 0; i < item.CallStatement.Parameters.Length; i++)
                    {
                        if (i > 0) { searchedID += ", "; }

                        searchedID += FindStatementType(item.CallStatement.Parameters[i]);
                    }
                    searchedID += ")";
                    throw new CompilerException("Unknown function " + searchedID + "", item.CallStatement.Identifier, CurrentFile);
                }

                if (function.InstructionOffset == -1)
                { throw new InternalException($"Function '{function.ReadableID()}' does not have instruction offset", item.CurrentFile); }

                compiledCode[item.CallInstructionIndex].Parameter = function.InstructionOffset - item.CallInstructionIndex;

                parameters.Clear();
                compiledVariables.Clear();
            }

            if (OptimizeCode)
            {
                int removedInstructions = 0;
                int changedInstructions = 0;
                for (int i = compiledCode.Count - 1; i >= 0; i--)
                {
                    var instruction = compiledCode[i];
                    if (instruction.opcode == Opcode.JUMP_BY || instruction.opcode == Opcode.JUMP_BY_IF_FALSE || instruction.opcode == Opcode.JUMP_BY_IF_TRUE)
                    {
                        if (instruction.Parameter is int jumpBy)
                        {
                            if (jumpBy == 1)
                            {
                                List<int> indexes = new()
                                {
                                    setGlobalVariablesInstruction,
                                    clearGlobalVariablesInstruction,
                                };

                                foreach (var item in this.compiledFunctions)
                                { indexes.Add(item.InstructionOffset); }

                                changedInstructions += compiledCode.RemoveInstruction(i, indexes);
                                removedInstructions++;

                                setGlobalVariablesInstruction = indexes[0];
                                clearGlobalVariablesInstruction = indexes[1];

                                for (int j = 0; j < this.compiledFunctions.Count; j++)
                                { this.compiledFunctions.Get<string, CompiledFunction>(this.compiledFunctions.ElementAt(j).Key).InstructionOffset = indexes[2 + j]; }
                            }
                        }
                    }
                }
                printCallback?.Invoke($"Optimalization: Removed {removedInstructions} & changed {changedInstructions} instructions", Output.LogType.Debug);
            }

            return new CodeGeneratorResult()
            {
                compiledCode = compiledCode.ToArray(),
                DebugInfo = GeneratedDebugInfo.ToArray(),

                compiledFunctions = this.compiledFunctions.ToArray(),
                compiledStructs = this.compiledStructs.ToArray(),

                clearGlobalVariablesInstruction = clearGlobalVariablesInstruction,
                setGlobalVariablesInstruction = setGlobalVariablesInstruction
            };
        }
    }
}