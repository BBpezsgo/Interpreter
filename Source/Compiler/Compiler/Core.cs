using System;
using System.Collections.Generic;

namespace IngameCoding.BBCode
{
    public static class Utils
    {
        public const int NULL_POINTER = int.MinValue / 2;
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
        internal static AddressingMode AddressingMode(this CompiledVariable v)
            => v.IsGlobal ? Bytecode.AddressingMode.ABSOLUTE : Bytecode.AddressingMode.BASEPOINTER_RELATIVE;

        internal static int RemoveInstruction(this List<Instruction> self, int index, List<int> watchTheseIndexesToo)
        {
            if (index < -1 || index >= self.Count) throw new IndexOutOfRangeException();

            int changedInstructions = 0;
            for (int instructionIndex = 0; instructionIndex < self.Count; instructionIndex++)
            {
                Instruction instruction = self[instructionIndex];
                if (instruction.opcode == Opcode.CALL || instruction.opcode == Opcode.JUMP_BY || instruction.opcode == Opcode.JUMP_BY_IF_FALSE)
                {
                    if (instruction.Parameter.type == RuntimeType.INT)
                    {
                        if (instruction.Parameter.ValueInt + instructionIndex < index && instructionIndex < index) continue;
                        if (instruction.Parameter.ValueInt + instructionIndex > index && instructionIndex > index) continue;
                        if (instruction.Parameter.ValueInt + instructionIndex == index) throw new Exception($"Can't remove instruction at {index} becouse instruction {instruction} is referencing to this position");

                        if (instructionIndex < index)
                        { instruction.Parameter = new DataItem(instruction.Parameter.ValueInt - 1); changedInstructions++; }
                        else if (instructionIndex > index)
                        { instruction.Parameter = new DataItem(instruction.Parameter.ValueInt + 1); changedInstructions++; }
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

        internal static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> v, Dictionary<TKey, TValue> elements)
        {
            foreach (KeyValuePair<TKey, TValue> pair in elements)
            { v.Add(pair.Key, pair.Value); }
        }

        internal static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> v, List<TValue> values, Func<TValue, TKey> keys)
        {
            foreach (TValue value in values)
            { v.Add(keys.Invoke(value), value); }
        }

        public static bool ContainsSameDefinition<T>(this IEnumerable<IDefinitionComparer<T>> functions, T other)
        {
            foreach (var function in functions)
            { if (function.IsSame(other)) return true; }
            return false;
        }
        public static bool ContainsSameDefinition(this IEnumerable<CompiledFunction> functions, CompiledFunction other)
        {
            foreach (var function in functions)
            { if (function.IsSame(other)) return true; }
            return false;
        }
        public static bool ContainsSameDefinition(this IEnumerable<CompiledGeneralFunction> functions, CompiledGeneralFunction other)
        {
            foreach (CompiledGeneralFunction function in functions)
            { if (function.IsSame(other)) return true; }
            return false;
        }

        public static string ID(this FunctionDefinition function)
        {
            string result = function.Identifier.Content;
            for (int i = 0; i < function.Parameters.Length; i++)
            {
                var param = function.Parameters[i];
                // var paramType = (param.type.typeName == BuiltinType.STRUCT) ? param.type.text : param.type.typeName.ToString().ToLower();
                result += "," + param.Type.ToString();
            }
            return result;
        }

        public static string ID(this GeneralFunctionDefinition function)
        {
            string result = function.Identifier.Content;
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
            foreach (IElementWithKey<T> element in self)
            {
                if (element == null) continue;
                if (element.Key.Equals(key))
                {
                    value = element;
                    return true;
                }
            }
            value = null;
            return false;
        }
        public static bool TryGetValue<T, TResult>(this IEnumerable<IElementWithKey<T>> self, T key, out TResult value) where TResult : IElementWithKey<T>
        {
            bool result = self.TryGetValue<T>(key, out IElementWithKey<T> _value);
            value = (_value == null) ? default : (TResult)_value;
            return result;
        }
        public static bool ContainsKey<T>(this IEnumerable<IElementWithKey<T>> self, T key)
        {
            foreach (IElementWithKey<T> element in self)
            {
                if (element == null) continue;
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
            foreach (IElementWithKey<T> element in self)
            {
                if (element == null) continue;
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
        public static bool Remove<TKey>(this IList<IElementWithKey<TKey>> self, TKey key)
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
        public static bool Remove<TKey>(this IList<CompiledFunction> self, TKey key)
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

        #region KeyValuePair<TKey, TValue>

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

        #endregion

        public static bool GetDefinition<T>(this IEnumerable<IDefinitionComparer<T>> self, T other, out IDefinitionComparer<T> value)
        {
            foreach (IDefinitionComparer<T> element in self)
            {
                if (element == null) continue;
                if (element.IsSame(other))
                {
                    value = element;
                    return true;
                }
            }
            value = null;
            return false;
        }
        public static IDefinitionComparer<T> GetDefinition<T>(this IEnumerable<IDefinitionComparer<T>> self, T other)
        {
            foreach (IDefinitionComparer<T> element in self)
            {
                if (element == null) continue;
                if (element.IsSame(other))
                {
                    return element;
                }
            }
            throw new KeyNotFoundException($"Key {other} not found in list {self}");
        }

        public static bool GetDefinition<T>(this IDefinitionComparer<T>[] self, T other, out IDefinitionComparer<T> value)
        {
            foreach (IDefinitionComparer<T> element in self)
            {
                if (element == null) continue;
                if (element.IsSame(other))
                {
                    value = element;
                    return true;
                }
            }
            value = null;
            return false;
        }
        public static IDefinitionComparer<T> GetDefinition<T>(this IDefinitionComparer<T>[] self, T other)
        {
            foreach (IDefinitionComparer<T> element in self)
            {
                if (element == null) continue;
                if (element.IsSame(other))
                {
                    return element;
                }
            }
            throw new KeyNotFoundException($"Key {other} not found in list {self}");
        }

        public static bool GetDefinition<T, TResult>(this IEnumerable<IDefinitionComparer<T>> self, T other, out TResult value) where TResult : IDefinitionComparer<T>
        {
            foreach (IDefinitionComparer<T> element in self)
            {
                if (element == null) continue;
                if (element.IsSame(other))
                {
                    value = (TResult)element;
                    return true;
                }
            }
            value = default;
            return false;
        }
        public static TResult GetDefinition<T, TResult>(this IEnumerable<IDefinitionComparer<T>> self, T other) where TResult : IDefinitionComparer<T>
        {
            foreach (IDefinitionComparer<T> element in self)
            {
                if (element == null) continue;
                if (element.IsSame(other))
                {
                    return (TResult)element;
                }
            }
            throw new KeyNotFoundException($"Key {other} not found in list {self}");
        }

        public static bool GetDefinition<T, TResult>(this IDefinitionComparer<T>[] self, T other, out TResult value) where TResult : IDefinitionComparer<T>
        {
            foreach (IDefinitionComparer<T> element in self)
            {
                if (element == null) continue;
                if (element.IsSame(other))
                {
                    value = (TResult)element;
                    return true;
                }
            }
            value = default;
            return false;
        }
        public static TResult GetDefinition<T, TResult>(this IDefinitionComparer<T>[] self, T other) where TResult : IDefinitionComparer<T>
        {
            foreach (IDefinitionComparer<T> element in self)
            {
                if (element == null) continue;
                if (element.IsSame(other))
                {
                    return (TResult)element;
                }
            }
            throw new KeyNotFoundException($"Key {other} not found in list {self}");
        }

    }

    struct UndefinedOperatorFunctionOffset
    {
        public int CallInstructionIndex;

        public Statement_Operator CallStatement;
        public List<CompiledParameter> currentParameters;
        public Dictionary<string, CompiledVariable> currentVariables;
        internal string CurrentFile;

        public UndefinedOperatorFunctionOffset(int callInstructionIndex, Statement_Operator functionCallStatement, CompiledParameter[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables, string file)
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

    struct UndefinedFunctionOffset
    {
        public int CallInstructionIndex;

        public Statement_FunctionCall CallStatement;
        public Statement_Variable VariableStatement;
        public List<CompiledParameter> currentParameters;
        public Dictionary<string, CompiledVariable> currentVariables;
        internal string CurrentFile;

        public UndefinedFunctionOffset(int callInstructionIndex, Statement_FunctionCall functionCallStatement, CompiledParameter[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables, string file)
        {
            this.CallInstructionIndex = callInstructionIndex;
            this.CallStatement = functionCallStatement;
            this.VariableStatement = null;

            this.currentParameters = new();
            this.currentVariables = new();

            foreach (var item in currentParameters)
            { this.currentParameters.Add(item); }
            foreach (var item in currentVariables)
            { this.currentVariables.Add(item.Key, item.Value); }
            this.CurrentFile = file;
        }

        public UndefinedFunctionOffset(int callInstructionIndex, Statement_Variable variable, CompiledParameter[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables, string file)
        {
            this.CallInstructionIndex = callInstructionIndex;
            this.CallStatement = null;
            this.VariableStatement = variable;

            this.currentParameters = new();
            this.currentVariables = new();

            foreach (var item in currentParameters)
            { this.currentParameters.Add(item); }
            foreach (var item in currentVariables)
            { this.currentVariables.Add(item.Key, item.Value); }
            this.CurrentFile = file;
        }
    }

    struct UndefinedGeneralFunctionOffset
    {
        public int CallInstructionIndex;

        public Statement CallStatement;
        public List<CompiledParameter> currentParameters;
        public Dictionary<string, CompiledVariable> currentVariables;
        internal string CurrentFile;

        public UndefinedGeneralFunctionOffset(int callInstructionIndex, Statement functionCallStatement, CompiledParameter[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables, string file)
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

        /// <exception cref="NotImplementedException"/>
        static Literal.Type ConvertType(System.Type type)
        {
            if (type == typeof(int))
            { return Literal.Type.Integer; }
            if (type == typeof(float))
            { return Literal.Type.Float; }
            if (type == typeof(bool))
            { return Literal.Type.Boolean; }
            if (type == typeof(string))
            { return Literal.Type.String; }
            throw new NotImplementedException($"Unknown attribute type requested: \"{type.FullName}\"");
        }

        public readonly bool TryGetValue<T>(int index, out T value)
        {
            value = default;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            Literal.Type type = ConvertType(typeof(T));
            switch (type)
            {
                case Literal.Type.Integer:
                    value = (T)(object)parameters[index].ValueInt;
                    break;
                case Literal.Type.Float:
                    value = (T)(object)parameters[index].ValueFloat;
                    break;
                case Literal.Type.String:
                    value = (T)(object)parameters[index].ValueString;
                    break;
                case Literal.Type.Boolean:
                    value = (T)(object)parameters[index].ValueBool;
                    break;
                default:
                    break;
            }
            return true;
        }

        public readonly bool TryGetValue(int index, out string value)
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
        public readonly bool TryGetValue(int index, out int value)
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
        public readonly bool TryGetValue(int index, out float value)
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
        public readonly bool TryGetValue(int index, out bool value)
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

    public interface IReferenceable<T>
    {
        public void AddReference(T reference);
        public void ClearReferences();
    }

    public class CompiledOperator : FunctionDefinition, IFunctionThing, IDefinitionComparer<CompiledOperator>, IDefinitionComparer<(string name, CompiledType[] parameters)>, IInContext<CompiledClass>, IReferenceable<Statement_Operator>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset { get; set; } = -1;

        public int ParameterCount => ParameterTypes.Length;

        public Dictionary<string, AttributeValues> CompiledAttributes;

        public IReadOnlyList<Statement_Operator> ReferencesOperator => references;
        readonly List<Statement_Operator> references = new();

        public new CompiledType Type;
        public TypeInstance TypeToken => base.Type;

        public bool IsBuiltin => CompiledAttributes.ContainsKey("Builtin");
        public string BuiltinName => CompiledAttributes.TryGetAttribute("Builtin", out string builtinName) ? builtinName : string.Empty;

        public string Key => this.ID();

        public CompiledClass Context { get; set; }

        public CompiledOperator(CompiledType type, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier)
        {
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
        public CompiledOperator(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier)
        {
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

        public void AddReference(Statement_Operator statement) => references.Add(statement);
        public void ClearReferences() => references.Clear();

        public bool IsSame(CompiledOperator other)
        {
            if (this.Type != other.Type) return false;
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
            for (int i = 0; i < this.ParameterTypes.Length; i++)
            { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

            return true;
        }

        public bool IsSame((string name, CompiledType[] parameters) other)
        {
            if (this.Identifier.Content != other.name) return false;
            if (this.ParameterTypes.Length != other.parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            { if (this.ParameterTypes[i] != other.parameters[i]) return false; }
            return true;
        }

        public bool IsSame(IFunctionThing other)
        {
            if (other is not CompiledOperator other2) return false;
            return IsSame(other2);
        }
    }

    public class CompiledFunction : FunctionDefinition, IFunctionThing, IDefinitionComparer<CompiledFunction>, IDefinitionComparer<(string name, CompiledType[] parameters)>, IInContext<CompiledClass>, IReferenceable<Statement_FunctionCall>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset { get; set; } = -1;

        public int ParameterCount => ParameterTypes.Length;
        public bool ReturnSomething => this.Type.BuiltinType != BBCode.Compiler.Type.VOID;

        public Dictionary<string, AttributeValues> CompiledAttributes;

        public IReadOnlyList<Statement_FunctionCall> ReferencesFunction => references;
        readonly List<Statement_FunctionCall> references = new();

        public new CompiledType Type;
        public TypeInstance TypeToken => base.Type;

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

        public string Key => this.ID();

        CompiledClass context;
        public CompiledClass Context
        {
            get => context;
            set => context = value;
        }

        public CompiledFunction(CompiledType type, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier)
        {
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
        public CompiledFunction(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier)
        {
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

        public void AddReference(Statement_FunctionCall statement) => references.Add(statement);
        public void ClearReferences() => references.Clear();

        public bool IsSame(CompiledFunction other)
        {
            if (this.Type != other.Type) return false;
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
            for (int i = 0; i < this.ParameterTypes.Length; i++)
            { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

            return true;
        }

        public bool IsSame((string name, CompiledType[] parameters) other)
        {
            if (this.Identifier.Content != other.name) return false;
            if (this.ParameterTypes.Length != other.parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            { if (this.ParameterTypes[i] != other.parameters[i]) return false; }
            return true;
        }

        public bool IsSame(IFunctionThing other)
        {
            if (other is not CompiledFunction other2) return false;
            return IsSame(other2);
        }
    }

    public class CompiledGeneralFunction : GeneralFunctionDefinition, IFunctionThing, IDefinitionComparer<CompiledGeneralFunction>, IDefinitionComparer<(string name, CompiledType[] parameters)>, IInContext<CompiledClass>, IReferenceable<Statement_KeywordCall>, IReferenceable<Statement_ConstructorCall>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset { get; set; } = -1;

        public int ParameterCount => ParameterTypes.Length;
        public bool ReturnSomething => this.Type.BuiltinType != BBCode.Compiler.Type.VOID;

        public IReadOnlyList<Statement> References => references;
        readonly List<Statement> references = new();

        public CompiledType Type;

        CompiledClass context;
        public CompiledClass Context
        {
            get => context;
            set => context = value;
        }

        public CompiledGeneralFunction(CompiledType type, GeneralFunctionDefinition functionDefinition) : base(functionDefinition.Identifier)
        {
            this.Type = type;

            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.FilePath = functionDefinition.FilePath;
            base.ExportKeyword = functionDefinition.ExportKeyword;
        }
        public CompiledGeneralFunction(CompiledType type, CompiledType[] parameterTypes, GeneralFunctionDefinition functionDefinition) : base(functionDefinition.Identifier)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;

            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.FilePath = functionDefinition.FilePath;
            base.ExportKeyword = functionDefinition.ExportKeyword;
        }

        public void AddReference(Statement_KeywordCall statement) => references.Add(statement);
        public void AddReference(Statement_ConstructorCall statement) => references.Add(statement);
        public void ClearReferences() => references.Clear();

        public bool IsSame(CompiledGeneralFunction other)
        {
            if (this.Type != other.Type) return false;
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
            for (int i = 0; i < this.ParameterTypes.Length; i++)
            { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

            return true;
        }

        public bool IsSame((string name, CompiledType[] parameters) other)
        {
            if (this.Identifier.Content == other.name) return false;
            if (this.ParameterTypes.Length != other.parameters.Length) return false;
            for (int i = 0; i < this.Parameters.Length; i++)
            { if (this.ParameterTypes[i] != other.parameters[i]) return false; }
            return true;
        }

        public bool IsSame(IFunctionThing other)
        {
            if (other is not CompiledGeneralFunction other2) return false;
            return IsSame(other2);
        }
    }

    public interface IFunctionThing : IDefinitionComparer<IFunctionThing>
    {
        internal int InstructionOffset { get; set; }
    }

    public class CompiledVariable : Statement_NewVariable
    {
        public readonly new CompiledType Type;

        public readonly int MemoryAddress;
        public readonly bool IsGlobal;
        public readonly bool IsStoredInHEAP;

        public CompiledVariable(int memoryOffset, CompiledType type, bool isGlobal, bool storedInHeap, Statement_NewVariable declaration)
        {
            this.Type = type;

            this.MemoryAddress = memoryOffset;
            this.IsStoredInHEAP = storedInHeap;
            this.IsGlobal = isGlobal;

            base.FilePath = declaration.FilePath;
            base.InitialValue = declaration.InitialValue;
            base.VariableName = declaration.VariableName;
        }
    }

    public class CompiledEnumMember : EnumMemberDefinition, IElementWithKey<string>
    {
        public new DataItem Value;
    }

    public class CompiledEnum : EnumDefinition, ITypeDefinition, IElementWithKey<string>
    {
        public new CompiledEnumMember[] Members;
        internal Dictionary<string, AttributeValues> CompiledAttributes;
    }

    public class CompiledStruct : StructDefinition, ITypeDefinition, IDataStructure, IElementWithKey<string>
    {
        public new readonly CompiledField[] Fields;
        internal Dictionary<string, AttributeValues> CompiledAttributes;
        public List<DefinitionReference> References = null;
        internal Dictionary<string, int> FieldOffsets = new();
        public int Size { get; set; }

        public CompiledStruct(Dictionary<string, AttributeValues> compiledAttributes, CompiledField[] fields, StructDefinition definition) : base(definition.Name, definition.Attributes, definition.Fields, definition.Methods)
        {
            this.CompiledAttributes = compiledAttributes;
            this.Fields = fields;

            base.FilePath = definition.FilePath;
            base.BracketEnd = definition.BracketEnd;
            base.BracketStart = definition.BracketStart;
            base.Statements = definition.Statements;
            base.ExportKeyword = definition.ExportKeyword;
        }
    }

    public class CompiledClass : ClassDefinition, ITypeDefinition, IDataStructure, IElementWithKey<string>
    {
        public new readonly CompiledField[] Fields;
        internal Dictionary<string, AttributeValues> CompiledAttributes;
        public List<DefinitionReference> References = null;
        internal Dictionary<string, int> FieldOffsets = new();
        public int Size { get; set; }

        public CompiledClass(Dictionary<string, AttributeValues> compiledAttributes, CompiledField[] fields, ClassDefinition definition) : base(definition.Name, definition.Attributes, definition.Fields, definition.Methods, definition.GeneralMethods, definition.Operators)
        {
            this.CompiledAttributes = compiledAttributes;
            this.Fields = fields;

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

    public enum Protection
    {
        Private,
        Public,
    }

    public class CompiledField : FieldDefinition
    {
        public new CompiledType Type;
        public Protection Protection
        {
            get
            {
                if (ProtectionToken == null) return Protection.Public;
                return ProtectionToken.Content switch
                {
                    "private" => Protection.Private,
                    "public" => Protection.Public,
                    _ => Protection.Public,
                };
            }
        }
        public CompiledClass Class;

        public CompiledField(FieldDefinition definition) : base()
        {
            base.Identifier = definition.Identifier;
            base.Type = definition.Type;
            base.ProtectionToken = definition.ProtectionToken;
        }
    }

    public enum Type
    {
        /// <summary>
        /// Reserved for indicating that the <see cref="CompiledType"/> is not a built-in type
        /// </summary>
        NONE,

        VOID,

        BYTE,
        INT,
        FLOAT,
        CHAR,

        /// <summary>
        /// Only used when get a value by it's memory address
        /// </summary>
        UNKNOWN,
    }

    public class FunctionType : ITypeDefinition, IEquatable<FunctionType>
    {
        public readonly CompiledFunction Function;

        public FunctionType(CompiledFunction function)
        {
            Function = function ?? throw new ArgumentNullException(nameof(function));
        }

        public string FilePath { get => Function.FilePath; set => Function.FilePath = value; }

        public override bool Equals(object obj) => obj is FunctionType other && Equals(other);

        public bool Equals(FunctionType other) =>
            other is not null &&
            EqualityComparer<CompiledFunction>.Default.Equals(Function, other.Function);

        public override int GetHashCode() => HashCode.Combine(Function);

        public static bool operator ==(FunctionType left, FunctionType right) =>
            EqualityComparer<FunctionType>.Default.Equals(left, right);

        public static bool operator !=(FunctionType left, FunctionType right) => !(left == right);
    }

    public class CompiledType : IEquatable<CompiledType>
    {
        readonly Type builtinType;

        CompiledStruct @struct;
        CompiledClass @class;
        CompiledEnum @enum;
        FunctionType function;

        internal Type BuiltinType => builtinType;
        /// <exception cref="Errors.InternalException"/>
        /// <exception cref="NotImplementedException"/>
        internal RuntimeType RuntimeType => builtinType switch
        {
            Type.BYTE => RuntimeType.BYTE,
            Type.INT => RuntimeType.INT,
            Type.FLOAT => RuntimeType.FLOAT,
            Type.CHAR => RuntimeType.CHAR,

            Type.NONE => throw new Errors.InternalException($"This ({this}) is not a built-in type"),

            _ => throw new NotImplementedException($"Type conversion for {builtinType} is not implemented"),
        };

        internal CompiledStruct Struct => @struct;
        internal CompiledClass Class => @class;
        internal CompiledEnum Enum => @enum;
        internal FunctionType Function => function;


        /// <exception cref="Errors.InternalException"/>
        /// <exception cref="NotImplementedException"/>
        internal string Name
        {
            get
            {
                if (builtinType != Type.NONE) return builtinType switch
                {
                    Type.VOID => "void",
                    Type.BYTE => "byte",
                    Type.INT => "int",
                    Type.FLOAT => "float",
                    Type.CHAR => "char",

                    Type.UNKNOWN => throw new Errors.InternalException($"This ({this}) is an unknown type"),
                    Type.NONE => throw new Errors.InternalException($"This should never occur"),

                    _ => throw new NotImplementedException($"Type conversion for {builtinType} is not implemented"),
                };

                if (@struct != null) return @struct.Name.Content;
                if (@class != null) return @class.Name.Content;
                if (@enum != null) return @enum.Identifier.Content;
                if (function != null) return function.Function.Identifier.Content;

                throw new NotImplementedException();
            }
        }
        /// <summary><c><see cref="Class"/> != <see langword="null"/></c></summary>
        internal bool IsClass => @class != null;
        /// <summary><c><see cref="Enum"/> != <see langword="null"/></c></summary>
        internal bool IsEnum => @enum != null;
        /// <summary><c><see cref="Struct"/> != <see langword="null"/></c></summary>
        internal bool IsStruct => @struct != null;
        /// <summary><c><see cref="Function"/> != <see langword="null"/></c></summary>
        internal bool IsFunction => function != null;
        internal bool IsBuiltin => builtinType != Type.NONE;
        internal bool InHEAP => IsClass;

        public int Size
        {
            get
            {
                if (IsStruct) return @struct.Size;
                if (IsClass) return @class.Size;
                if (IsEnum) return 1;
                if (IsFunction) return 1;
                return 1;
            }
        }
        public int SizeOnHeap
        {
            get
            {
                if (IsClass) return @class.Size;
                return 0;
            }
        }
        public int SizeOnStack
        {
            get
            {
                if (IsStruct) return @struct.Size;
                return 1;
            }
        }

        CompiledType()
        {
            this.builtinType = Type.NONE;
            this.@struct = null;
            this.@class = null;
            this.@enum = null;
            this.function = null;
        }

        /// <exception cref="ArgumentNullException"/>
        internal CompiledType(CompiledStruct @struct) : this()
        {
            this.@struct = @struct ?? throw new ArgumentNullException(nameof(@struct));
        }

        /// <exception cref="ArgumentNullException"/>
        internal CompiledType(CompiledClass @class) : this()
        {
            this.@class = @class ?? throw new ArgumentNullException(nameof(@class));
        }

        /// <exception cref="ArgumentNullException"/>
        internal CompiledType(CompiledEnum @enum) : this()
        {
            this.@enum = @enum ?? throw new ArgumentNullException(nameof(@enum));
        }

        /// <exception cref="ArgumentNullException"/>
        internal CompiledType(CompiledFunction @function) : this()
        {
            this.function = new FunctionType(@function);
        }

        internal CompiledType(Type type) : this()
        {
            this.builtinType = type;
        }

        /// <exception cref="Errors.InternalException"/>
        internal CompiledType(RuntimeType type) : this()
        {
            this.builtinType = type switch
            {
                RuntimeType.BYTE => Type.BYTE,
                RuntimeType.INT => Type.INT,
                RuntimeType.FLOAT => Type.FLOAT,
                RuntimeType.CHAR => Type.CHAR,
                _ => throw new NotImplementedException(),
            };
        }

        /// <exception cref="ArgumentException"/>
        /// <exception cref="Errors.InternalException"/>
        internal CompiledType(string typeName, Func<string, ITypeDefinition> UnknownTypeCallback) : this()
        {
            if (string.IsNullOrEmpty(typeName)) throw new ArgumentException($"'{nameof(typeName)}' cannot be null or empty.", nameof(typeName));

            if (Constants.BuiltinTypeMap3.TryGetValue(typeName, out this.builtinType))
            { return; }

            if (UnknownTypeCallback == null) throw new Errors.InternalException($"Can't parse {typeName} to CompiledType");

            SetCustomType(typeName, UnknownTypeCallback);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="Errors.InternalException"/>
        public CompiledType(TypeInstance type, Func<string, ITypeDefinition> UnknownTypeCallback) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (Constants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out this.builtinType))
            { return; }

            if (UnknownTypeCallback == null) throw new Errors.InternalException($"Can't parse {type} to CompiledType");

            SetCustomType(type.Identifier.Content, UnknownTypeCallback);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="Errors.InternalException"/>
        public CompiledType(ITypeDefinition type) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (type is CompiledStruct @struct)
            {
                this.@struct = @struct;
                return;
            }

            if (type is CompiledClass @class)
            {
                this.@class = @class;
                return;
            }

            if (type is CompiledEnum @enum)
            {
                this.@enum = @enum;
                return;
            }

            if (type is FunctionType function)
            {
                this.function = function;
                return;
            }

            throw new Errors.InternalException($"Unknown type definition {type.GetType().FullName}");
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="Errors.InternalException"/>
        void SetCustomType(string typeName, Func<string, ITypeDefinition> UnknownTypeCallback)
        {
            if (UnknownTypeCallback is null) throw new ArgumentNullException(nameof(UnknownTypeCallback));

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
            if (customType is CompiledEnum @enum)
            {
                this.@enum = @enum;
                return;
            }
            if (customType is FunctionType function)
            {
                this.function = function;
                return;
            }

            throw new Errors.InternalException($"Unknown type definition {customType.GetType().FullName}");
        }

        public override string ToString() => Name;

        public static bool operator ==(CompiledType a, CompiledType b)
        {
            if (a is null && b is null) return true;
            if (a is null) return false;
            if (b is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType a, CompiledType b) => !(a == b);

        public static bool operator ==(CompiledType a, TypeInstance b)
        {
            if (a is null && b is null) return true;
            if (a is null) return false;
            if (b is null) return false;

            if (Constants.BuiltinTypeMap3.TryGetValue(b.Identifier.Content, out var type3))
            {
                return type3 == a.builtinType;
            }

            if (a.@struct != null && a.@struct.Name.Content == b.Identifier.Content)
            { return true; }

            if (a.@class != null && a.@class.Name.Content == b.Identifier.Content)
            { return true; }

            if (a.@enum != null && a.@enum.Identifier.Content == b.Identifier.Content)
            { return true; }

            return false;
        }
        public static bool operator !=(CompiledType a, TypeInstance b) => !(a == b);

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
            if (this.IsFunction != b.IsFunction) return false;

            if (this.IsClass && b.IsClass) return this.@class.Name.Content == b.@class.Name.Content;
            if (this.IsStruct && b.IsStruct) return this.@struct.Name.Content == b.@struct.Name.Content;
            if (this.IsEnum && b.IsEnum) return this.@enum.Identifier.Content == b.@enum.Identifier.Content;
            if (this.IsFunction && b.IsFunction) return this.@function == b.@function;

            if (this.IsBuiltin && b.IsBuiltin) return this.builtinType == b.builtinType;

            return true;
        }

        public override int GetHashCode() => HashCode.Combine(builtinType, @struct, @class);

        public static bool operator ==(CompiledType a, string b)
        {
            if (a is null && b is null) return true;
            if (a is not null && b is null) return false;
            if (a is null && b is not null) return false;
            return a.Name == b;
        }
        public static bool operator !=(CompiledType a, string b) => !(a == b);
        public static bool operator ==(string a, CompiledType b) => b == a;
        public static bool operator !=(string a, CompiledType b) => !(b == a);
    }

    public interface ITypeDefinition : IDefinition
    {

    }

    public interface IDataStructure
    {
        public int Size { get; }
    }

    public interface IElementWithKey<T>
    {
        public T Key { get; }
    }

    public interface IDefinitionComparer<T>
    {
        public bool IsSame(T other);
    }

    public interface IInContext<T>
    {
        public T Context { get; set; }
    }
}
