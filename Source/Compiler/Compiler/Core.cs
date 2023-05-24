using System;
using System.Collections.Generic;

namespace IngameCoding.BBCode
{
    public static class Utils
    {
        public const int NULL_POINTER = -1;
    }
}

namespace IngameCoding.BBCode.Compiler
{
    using Bytecode;

    using IngameCoding.Core;
    using IngameCoding.Tokenizer;

    using Parser;
    using Parser.Statements;

    public static class Extensions
    {
        internal static DataType Convert(this BuiltinType v) => v switch
        {
            BuiltinType.INT => DataType.INT,
            BuiltinType.BYTE => DataType.BYTE,
            BuiltinType.FLOAT => DataType.FLOAT,
            BuiltinType.STRING => DataType.STRING,
            BuiltinType.BOOLEAN => DataType.BOOLEAN,

            BuiltinType.ANY => throw new NotImplementedException(),
            BuiltinType.AUTO => throw new NotImplementedException(),
            BuiltinType.VOID => throw new NotImplementedException(),

            BuiltinType.STRUCT => throw new NotImplementedException(),
            BuiltinType.LISTOF => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
        internal static BuiltinType Convert(this TypeTokenType v) => v switch
        {
            TypeTokenType.INT => BuiltinType.INT,
            TypeTokenType.BYTE => BuiltinType.BYTE,
            TypeTokenType.FLOAT => BuiltinType.FLOAT,
            TypeTokenType.STRING => BuiltinType.STRING,
            TypeTokenType.BOOLEAN => BuiltinType.BOOLEAN,
            TypeTokenType.USER_DEFINED => BuiltinType.STRUCT,
            TypeTokenType.AUTO => BuiltinType.AUTO,
            TypeTokenType.VOID => BuiltinType.VOID,
            TypeTokenType.ANY => BuiltinType.ANY,
            _ => throw new NotImplementedException(),
        };
        internal static BuiltinType Convert(this DataType v) => v switch
        {
            DataType.INT => BuiltinType.INT,
            DataType.BYTE => BuiltinType.BYTE,
            DataType.FLOAT => BuiltinType.FLOAT,
            DataType.STRING => BuiltinType.STRING,
            DataType.BOOLEAN => BuiltinType.BOOLEAN,
            _ => BuiltinType.ANY,
        };

        internal static int RemoveInstruction(this List<Instruction> self, int index, List<int> watchTheseIndexesToo)
        {
            if (index < -1 || index >= self.Count) throw new IndexOutOfRangeException();

            int changedInstructions = 0;
            for (int instructionIndex = 0; instructionIndex < self.Count; instructionIndex++)
            {
                Instruction instruction = self[instructionIndex];
                if (instruction.opcode == Opcode.CALL || instruction.opcode == Opcode.JUMP_BY || instruction.opcode == Opcode.JUMP_BY_IF_FALSE)
                {
                    if (instruction.Parameter is int jumpBy)
                    {
                        if (jumpBy + instructionIndex < index && instructionIndex < index) continue;
                        if (jumpBy + instructionIndex > index && instructionIndex > index) continue;
                        if (jumpBy + instructionIndex == index) throw new Exception($"Can't remove instruction at {index} becouse instruction {instruction} is referencing to this position");

                        if (instructionIndex < index)
                        { instruction.Parameter = jumpBy - 1; changedInstructions++; }
                        else if (instructionIndex > index)
                        { instruction.Parameter = jumpBy + 1; changedInstructions++; }
                    }
                }
            }

            for (int i = 0; i < watchTheseIndexesToo.Count; i++)
            {
                int instructionIndex = watchTheseIndexesToo[i];

                if (instructionIndex > index)
                { watchTheseIndexesToo[i] = instructionIndex - 1; }
            }

            self.RemoveAt(index);

            return changedInstructions;
        }

        public static string ID(this FunctionDefinition function)
        {
            string result = function.FullName;
            for (int i = 0; i < function.Parameters.Length; i++)
            {
                var param = function.Parameters[i];
                // var paramType = (param.type.typeName == BuiltinType.STRUCT) ? param.type.text : param.type.typeName.ToString().ToLower();
                result += "," + param.Type.ToString();
            }
            return result;
        }

        public static bool TryGetValue<T>(this IEnumerable<IElementWithKey<T>> self, T key, out IElementWithKey<T> value)
        {
            foreach (var element in self)
            {
                if (element.Key.Equals(key))
                {
                    value = element;
                    return true;
                }
            }
            value = null;
            return false;
        }
        public static bool TryGetValue<T, TResult>(this IEnumerable<IElementWithKey<T>> self, T key, out TResult value)
        {
            bool result = self.TryGetValue<T>(key, out IElementWithKey<T> _value);
            value = (_value == null) ? default : (TResult)_value;
            return result;
        }
        public static bool ContainsKey<T>(this IEnumerable<IElementWithKey<T>> self, T key)
        {
            foreach (var element in self)
            {
                if (element.Key.Equals(key))
                {
                    return true;
                }
            }
            return false;
        }
        /// <exception cref="KeyNotFoundException"></exception>
        public static IElementWithKey<T> Get<T>(this IEnumerable<IElementWithKey<T>> self, T key)
        {
            foreach (var element in self)
            {
                if (element.Key.Equals(key))
                {
                    return element;
                }
            }
            throw new KeyNotFoundException($"Key {key} not found in list {self}");
        }
        /// <exception cref="KeyNotFoundException"></exception>
        public static TResult Get<T, TResult>(this IEnumerable<IElementWithKey<T>> self, T key)
            => (TResult)self.Get<T>(key);

        public static bool TryGetValue<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> self, TKey key, out TValue value)
        {
            foreach (KeyValuePair<TKey, TValue> element in self)
            {
                if (element.Key.Equals(key))
                {
                    value = element.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
        public static bool ContainsKey<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> self, TKey key)
        {
            foreach (KeyValuePair<TKey, TValue> element in self)
            {
                if (element.Key.Equals(key))
                {
                    return true;
                }
            }
            return false;
        }
        /// <exception cref="KeyNotFoundException"></exception>
        public static TValue Get<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> self, TKey key)
        {
            foreach (KeyValuePair<TKey, TValue> element in self)
            {
                if (element.Key.Equals(key))
                {
                    return element.Value;
                }
            }
            throw new KeyNotFoundException($"Key {key} not found in list {self}");
        }
        public static void Add<TKey, TValue>(this List<KeyValuePair<TKey, TValue>> self, TKey key, TValue value)
            => self.Add(new KeyValuePair<TKey, TValue>(key, value));
        public static bool Remove<TKey, TValue>(this List<KeyValuePair<TKey, TValue>> self, TKey key)
        {
            for (int i = self.Count - 1; i >= 0; i--)
            {
                KeyValuePair<TKey, TValue> element = self[i];
                if (element.Key.Equals(key))
                {
                    self.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> self)
        {
            Dictionary<TKey, TValue> result = new();
            foreach (KeyValuePair<TKey, TValue> element in self)
            { result.Add(element.Key, element.Value); }
            return result;
        }
        public static Dictionary<TKey, IElementWithKey<TKey>> ToDictionary<TKey>(this IEnumerable<IElementWithKey<TKey>> self)
        {
            Dictionary<TKey, IElementWithKey<TKey>> result = new();
            foreach (IElementWithKey<TKey> element in self)
            { result.Add(element.Key, element); }
            return result;
        }
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<IElementWithKey<TKey>> self)
        {
            Dictionary<TKey, TValue> result = new();
            foreach (IElementWithKey<TKey> element in self)
            { result.Add(element.Key, (TValue)element); }
            return result;
        }
        public static bool Remove<TKey>(this List<IElementWithKey<TKey>> self, TKey key)
        {
            for (int i = self.Count - 1; i >= 0; i--)
            {
                IElementWithKey<TKey> element = self[i];
                if (element.Key.Equals(key))
                {
                    self.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
        public static bool Remove<TKey>(this List<CompiledFunction> self, TKey key)
        {
            for (int i = self.Count - 1; i >= 0; i--)
            {
                CompiledFunction element = self[i];
                if (element.Key.Equals(key))
                {
                    self.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }

    struct UndefinedFunctionOffset
    {
        public int CallInstructionIndex;

        public Statement_FunctionCall CallStatement;
        public List<CompiledParameter> currentParameters;
        public Dictionary<string, CompiledVariable> currentVariables;
        internal string CurrentFile;

        public UndefinedFunctionOffset(int callInstructionIndex, Statement_FunctionCall functionCallStatement, CompiledParameter[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables, string file)
        {
            this.CallInstructionIndex = callInstructionIndex;
            this.CallStatement = functionCallStatement;

            this.currentParameters = new();
            this.currentVariables = new();

            foreach (var item in currentParameters)
            { this.currentParameters.Add(item); }
            foreach (var item in currentVariables)
            { this.currentVariables.Add(item.Key, item.Value); }
            this.CurrentFile = file;
        }
    }

    public struct AttributeValues
    {
        public List<Literal> parameters;
        public Token Identifier;

        public bool TryGetValue(int index, out string value)
        {
            value = string.Empty;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == Literal.Type.String)
            {
                value = parameters[index].ValueString;
            }
            return true;
        }
        public bool TryGetValue(int index, out int value)
        {
            value = 0;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == Literal.Type.Integer)
            {
                value = parameters[index].ValueInt;
            }
            return true;
        }
        public bool TryGetValue(int index, out float value)
        {
            value = 0;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == Literal.Type.Float)
            {
                value = parameters[index].ValueFloat;
            }
            return true;
        }
        public bool TryGetValue(int index, out bool value)
        {
            value = false;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == Literal.Type.Boolean)
            {
                value = parameters[index].ValueBool;
            }
            return true;
        }
    }

    public readonly struct Literal
    {
        public enum Type
        {
            Integer,
            Float,
            String,
            Boolean,
        }

        public readonly int ValueInt;
        public readonly float ValueFloat;
        public readonly string ValueString;
        public readonly bool ValueBool;
        public readonly Type type;

        public Literal(int value)
        {
            this.type = Type.Integer;

            this.ValueInt = value;
            this.ValueFloat = 0;
            this.ValueString = string.Empty;
            this.ValueBool = false;
        }
        public Literal(float value)
        {
            this.type = Type.Float;

            this.ValueInt = 0;
            this.ValueFloat = value;
            this.ValueString = string.Empty;
            this.ValueBool = false;
        }
        public Literal(string value)
        {
            this.type = Type.String;

            this.ValueInt = 0;
            this.ValueFloat = 0;
            this.ValueString = value;
            this.ValueBool = false;
        }
        public Literal(bool value)
        {
            this.type = Type.Boolean;

            this.ValueInt = 0;
            this.ValueFloat = 0;
            this.ValueString = string.Empty;
            this.ValueBool = value;
        }
        public Literal(object value)
        {
            this.ValueInt = 0;
            this.ValueFloat = 0;
            this.ValueString = string.Empty;
            this.ValueBool = false;

            if (value is int @int)
            {
                this.type = Type.Integer;
                this.ValueInt = @int;
            }
            else if (value is float @float)
            {
                this.type = Type.Float;
                this.ValueFloat = @float;
            }
            else if (value is string @string)
            {
                this.type = Type.String;
                this.ValueString = @string;
            }
            else if (value is bool @bool)
            {
                this.type = Type.Boolean;
                this.ValueBool = @bool;
            }
            else
            {
                throw new System.Exception($"Invalid type '{value.GetType().FullName}'");
            }
        }
    }

    public readonly struct DefinitionReference
    {
        public readonly Range<SinglePosition> Source;
        public readonly string SourceFile;

        public DefinitionReference(Range<SinglePosition> source, string sourceFile)
        {
            Source = source;
            SourceFile = sourceFile;
        }

        public DefinitionReference(BaseToken source, string sourceFile)
        {
            Source = source.Position;
            SourceFile = sourceFile;
        }
    }

    public class CompiledFunction : FunctionDefinition, IElementWithKey<string>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        internal int InstructionOffset = -1;

        public int ParameterCount => ParameterTypes.Length;
        public bool ReturnSomething => this.Type.BuiltinType != CompiledType.CompiledTypeType.VOID;

        public Dictionary<string, AttributeValues> CompiledAttributes;

        public List<DefinitionReference> References = null;

        public new CompiledType Type;
        public TypeToken TypeToken => base.Type;

        public bool IsBuiltin => CompiledAttributes.ContainsKey("Builtin");
        public string BuiltinName
        {
            get
            {
                if (CompiledAttributes.TryGetValue("Builtin", out var attributeValues))
                {
                    if (attributeValues.TryGetValue(0, out string builtinName))
                    {
                        return builtinName;
                    }
                }
                return string.Empty;
            }
        }

        public readonly string ID;
        public string Key => ID;

        public CompiledFunction(string id, CompiledType type, FunctionDefinition functionDefinition) : base(functionDefinition.NamespacePath, functionDefinition.Identifier)
        {
            this.ID = id;
            this.Type = type;

            base.Attributes = functionDefinition.Attributes;
            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.Type = functionDefinition.Type;
            base.FilePath = functionDefinition.FilePath;
            base.ExportKeyword = functionDefinition.ExportKeyword;
        }
        public CompiledFunction(string id, CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition) : base(functionDefinition.NamespacePath, functionDefinition.Identifier)
        {
            this.ID = id;
            this.Type = type;
            this.ParameterTypes = parameterTypes;
            this.CompiledAttributes = new();

            base.Attributes = functionDefinition.Attributes;
            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.Type = functionDefinition.Type;
            base.FilePath = functionDefinition.FilePath;
            base.ExportKeyword = functionDefinition.ExportKeyword;
        }
    }

    internal class CompiledVariable : Statement_NewVariable
    {
        public readonly new CompiledType Type;

        public readonly int Index;
        public readonly int MemoryAddress;
        public readonly bool IsGlobal;
        public readonly bool IsStoredInHEAP;

        public CompiledVariable(int index, int memoryOffset, CompiledType type, bool isGlobal, bool storedInHeap, Statement_NewVariable declaration)
        {
            this.Type = type;

            this.MemoryAddress = memoryOffset;
            this.Index = index;
            this.IsStoredInHEAP = storedInHeap;
            this.IsGlobal = isGlobal;

            base.FilePath = declaration.FilePath;
            base.InitialValue = declaration.InitialValue;
            base.VariableName = declaration.VariableName;
        }
    }

    public interface ITypeDefinition : IDefinition
    {

    }

    public interface IDataStructure
    {
        public int Size { get; }
    }

    public class CompiledStruct : StructDefinition, ITypeDefinition, IDataStructure, IElementWithKey<string>
    {
        internal Dictionary<string, AttributeValues> CompiledAttributes;
        public List<DefinitionReference> References = null;
        internal Dictionary<string, int> FieldOffsets = new();
        public int Size { get; set; }
        public string Key => this.FullName;

        public CompiledStruct(Dictionary<string, AttributeValues> compiledAttributes, StructDefinition definition) : base(definition.NamespacePath, definition.Name, definition.Attributes, definition.Fields, definition.Methods)
        {
            this.CompiledAttributes = compiledAttributes;

            base.FilePath = definition.FilePath;
            base.BracketEnd = definition.BracketEnd;
            base.BracketStart = definition.BracketStart;
            base.Statements = definition.Statements;
            base.ExportKeyword = definition.ExportKeyword;
        }
    }

    public class CompiledClass : ClassDefinition, ITypeDefinition, IDataStructure, IElementWithKey<string>
    {
        internal Dictionary<string, AttributeValues> CompiledAttributes;
        public List<DefinitionReference> References = null;
        internal Dictionary<string, int> FieldOffsets = new();
        public int Size { get; set; }
        public string Key => this.FullName;

        public CompiledClass(Dictionary<string, AttributeValues> compiledAttributes, ClassDefinition definition) : base(definition.NamespacePath, definition.Name, definition.Attributes, definition.Fields, definition.Methods)
        {
            this.CompiledAttributes = compiledAttributes;

            base.FilePath = definition.FilePath;
            base.BracketEnd = definition.BracketEnd;
            base.BracketStart = definition.BracketStart;
            base.Statements = definition.Statements;
            base.ExportKeyword = definition.ExportKeyword;
        }
    }

    public class CompiledParameter : ParameterDefinition
    {
        public new CompiledType Type;

        readonly int index;
        readonly int allParamCount;
        readonly int currentParamsSize;

        public int Index => index;
        public int RealIndex
        {
            get
            {
                var v = -1 - (currentParamsSize + 1 - index);
                return v;
            }
        }

        public CompiledParameter(int index, int currentParamsSize, int allParamCount, CompiledType type, ParameterDefinition definition)
        {
            this.index = index;
            this.allParamCount = allParamCount;
            this.currentParamsSize = currentParamsSize;
            this.Type = type;

            base.Identifier = definition.Identifier;
            base.withThisKeyword = definition.withThisKeyword;
        }

        public override string ToString() => $"{index} {Identifier}";
    }

    public class CompiledType
    {
        internal enum CompiledTypeType
        {
            NONE,
            VOID,
            BYTE,
            INT,
            FLOAT,
            STRING,
            BOOL,
            /// <summary>
            /// Only used when get a value by it's memory address!
            /// </summary>
            UNKNOWN,
        }

        readonly CompiledTypeType builtinType;

        CompiledStruct @struct;
        CompiledClass @class;
        readonly CompiledType listOf;

        internal CompiledTypeType BuiltinType => builtinType;

        internal CompiledStruct Struct => @struct;
        internal CompiledClass Class => @class;
        internal CompiledType ListOf => listOf;

        internal string Name
        {
            get
            {
                if (builtinType != CompiledTypeType.NONE) return builtinType switch
                {
                    CompiledTypeType.VOID => "void",
                    CompiledTypeType.BYTE => "byte",
                    CompiledTypeType.INT => "int",
                    CompiledTypeType.FLOAT => "float",
                    CompiledTypeType.STRING => "string",
                    CompiledTypeType.BOOL => "bool",
                    CompiledTypeType.UNKNOWN => "unknown",
                    _ => throw new Errors.InternalException($"WTF???"),
                };

                if (@struct != null) return @struct.Name.Content;
                if (@class != null) return @class.Name.Content;
                if (listOf != null) return listOf.Name + "[]";

                return null;
            }
        }
        public string FullName
        {
            get
            {
                if (builtinType != CompiledTypeType.NONE) return builtinType switch
                {
                    CompiledTypeType.VOID => "void",
                    CompiledTypeType.BYTE => "byte",
                    CompiledTypeType.INT => "int",
                    CompiledTypeType.FLOAT => "float",
                    CompiledTypeType.STRING => "string",
                    CompiledTypeType.BOOL => "bool",
                    _ => throw new Errors.InternalException($"WTF???"),
                };

                if (@struct != null) return @struct.NamespacePathString + @struct.Name.Content;
                if (@class != null) return @class.NamespacePathString + @class.Name.Content;
                if (listOf != null) return listOf.Name + "[]";

                return null;
            }
        }
        /// <summary><c><see cref="ListOf"/> != <see langword="null"/></c></summary>
        internal bool IsList => listOf != null;
        /// <summary><c><see cref="Class"/> != <see langword="null"/></c></summary>
        internal bool IsClass => @class != null;
        /// <summary><c><see cref="Struct"/> != <see langword="null"/></c></summary>
        internal bool IsStruct => @struct != null;
        internal bool IsBuiltin => builtinType != CompiledTypeType.NONE;

        public int Size
        {
            get
            {
                if (IsStruct) return @struct.Size;
                if (IsClass) return @class.Size;
                return 1;
            }
        }
        public int SizeOnStack
        {
            get
            {
                if (IsStruct) return @struct.Size;
                if (IsClass) return 1;
                return 1;
            }
        }

        CompiledType()
        {
            this.builtinType = CompiledTypeType.NONE;
            this.@struct = null;
            this.@class = null;
            this.listOf = null;
        }

        internal CompiledType(CompiledStruct @struct) : this()
        {
            this.@struct = @struct ?? throw new ArgumentNullException(nameof(@struct));
        }

        internal CompiledType(CompiledClass @class) : this()
        {
            this.@class = @class ?? throw new ArgumentNullException(nameof(@class));
        }

        internal CompiledType(CompiledTypeType type) : this()
        {
            this.builtinType = type;
        }

        internal CompiledType(CompiledType listOf) : this()
        {
            this.listOf = listOf ?? throw new ArgumentNullException(nameof(listOf));
        }

        internal CompiledType(string type, Func<string, ITypeDefinition> UnknownTypeCallback) : this()
        {
            if (string.IsNullOrEmpty(type)) throw new ArgumentException($"'{nameof(type)}' cannot be null or empty.", nameof(type));


            if (type.EndsWith("[]"))
            {
                this.listOf = new CompiledType(type[..^2], UnknownTypeCallback);
                return;
            }

            switch (type)
            {
                case "void":
                    this.builtinType = CompiledTypeType.VOID;
                    return;
                case "byte":
                    this.builtinType = CompiledTypeType.BYTE;
                    return;
                case "int":
                    this.builtinType = CompiledTypeType.INT;
                    return;
                case "float":
                    this.builtinType = CompiledTypeType.FLOAT;
                    return;
                case "string":
                    this.builtinType = CompiledTypeType.STRING;
                    return;
                case "bool":
                    this.builtinType = CompiledTypeType.BOOL;
                    return;
                default:
                    break;
            };

            if (UnknownTypeCallback == null) throw new Errors.InternalException($"Can't parse {type} to CompiledType");

            SetCustomType(type, UnknownTypeCallback);
        }

        public CompiledType(BuiltinType type) : this()
        {
            this.builtinType = type switch
            {
                BBCode.BuiltinType.VOID => CompiledTypeType.VOID,
                BBCode.BuiltinType.BYTE => CompiledTypeType.BYTE,
                BBCode.BuiltinType.INT => CompiledTypeType.INT,
                BBCode.BuiltinType.FLOAT => CompiledTypeType.FLOAT,
                BBCode.BuiltinType.STRING => CompiledTypeType.STRING,
                BBCode.BuiltinType.BOOLEAN => CompiledTypeType.BOOL,
                _ => throw new Errors.InternalException($"Can't cast BuiltinType {type} to CompiledType"),
            };
        }

        public CompiledType(TypeToken type, Func<string, ITypeDefinition> UnknownTypeCallback) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (type.IsList)
            {
                this.listOf = new CompiledType(type.ListOf, UnknownTypeCallback);
                return;
            }

            switch (type.Type)
            {
                case TypeTokenType.VOID:
                    this.builtinType = CompiledTypeType.VOID;
                    return;
                case TypeTokenType.BYTE:
                    this.builtinType = CompiledTypeType.BYTE;
                    return;
                case TypeTokenType.INT:
                    this.builtinType = CompiledTypeType.INT;
                    return;
                case TypeTokenType.FLOAT:
                    this.builtinType = CompiledTypeType.FLOAT;
                    return;
                case TypeTokenType.STRING:
                    this.builtinType = CompiledTypeType.STRING;
                    return;
                case TypeTokenType.BOOLEAN:
                    this.builtinType = CompiledTypeType.BOOL;
                    return;
                default:
                    break;
            };

            SetCustomType(type.Content, UnknownTypeCallback);
        }

        void SetCustomType(string typeName, Func<string, ITypeDefinition> UnknownTypeCallback)
        {
            if (UnknownTypeCallback == null) throw new Errors.InternalException($"Can't parse {typeName} to CompiledType");

            ITypeDefinition customType = UnknownTypeCallback.Invoke(typeName);
            if (customType is CompiledStruct @struct)
            {
                this.@struct = @struct;
                return;
            }
            if (customType is CompiledClass @class)
            {
                this.@class = @class;
                return;
            }

            throw new Errors.InternalException($"WTF???");
        }

        public override string ToString() => Name;

        public BuiltinType GetBuiltinType()
        {
            if (IsStruct) return BBCode.BuiltinType.STRUCT;
            if (IsClass) return BBCode.BuiltinType.STRUCT;
            if (IsList) return BBCode.BuiltinType.LISTOF;
            if (IsBuiltin) return builtinType switch
            {
                CompiledTypeType.BYTE => BBCode.BuiltinType.BYTE,
                CompiledTypeType.INT => BBCode.BuiltinType.INT,
                CompiledTypeType.FLOAT => BBCode.BuiltinType.FLOAT,
                CompiledTypeType.STRING => BBCode.BuiltinType.STRING,
                CompiledTypeType.BOOL => BBCode.BuiltinType.BOOLEAN,
                _ => BBCode.BuiltinType.VOID,
            };
            return BBCode.BuiltinType.VOID;
        }

        public static bool operator ==(CompiledType a, CompiledType b)
        {
            if (a is null && b is null) return true;
            if (a is null) return false;
            if (b is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType a, CompiledType b) => !(a == b);

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is not CompiledType _obj) return false;
            return this.Equals(_obj);
        }
        public bool Equals(CompiledType b)
        {
            if (b is null) return false;

            if (this.IsBuiltin != b.IsBuiltin) return false;
            if (this.IsClass != b.IsClass) return false;
            if (this.IsStruct != b.IsStruct) return false;
            if (this.IsList != b.IsList) return false;

            if (this.IsList && b.IsList) return this.listOf == b.listOf;

            if (this.IsClass && b.IsClass)
            {
                var classA = this.@class;
                var classB = b.@class;
                return classA.FullName == classB.FullName;
            }
            if (this.IsStruct && b.IsStruct)
            {
                var classA = this.@class;
                var classB = b.@class;
                return classA.FullName == classB.FullName;
            }

            if (this.IsBuiltin && b.IsBuiltin) return this.builtinType == b.builtinType;

            return true;
        }
    }

    public interface IElementWithKey<T>
    {
        public T Key { get; }
    }
}
