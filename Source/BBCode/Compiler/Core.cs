using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgrammingLanguage.BBCode
{
    public static class Utils
    {
        public const int NULL_POINTER = int.MinValue / 2;
    }
}

namespace ProgrammingLanguage.BBCode.Compiler
{
    using Bytecode;

    using Parser;
    using Parser.Statement;

    using ProgrammingLanguage.Core;
    using ProgrammingLanguage.Errors;
    using ProgrammingLanguage.Tokenizer;

    public interface IDuplicateable<T>
    {
        public T Duplicate();
    }

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
                if (instruction.opcode == Opcode.JUMP_BY || instruction.opcode == Opcode.JUMP_BY_IF_FALSE)
                {
                    if (instruction.Parameter.Type == RuntimeType.INT)
                    {
                        if (instruction.Parameter.ValueInt + instructionIndex < index && instructionIndex < index)
                        { continue; }

                        if (instruction.Parameter.ValueInt + instructionIndex > index && instructionIndex > index)
                        { continue; }

                        // TODO: Think about that safe to remove instructions in this case:
                        if (instruction.Parameter.ValueInt + instructionIndex == index)
                        { continue; }
                        // { throw new Exception($"Can't remove instruction at {index} because instruction {instruction} is referencing to this position"); }

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

        public static bool ContainsSameDefinition(this IEnumerable<FunctionDefinition> functions, FunctionDefinition other)
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

        public static bool TryGetValue<T>(this IEnumerable<IHaveKey<T>> self, T key, out IHaveKey<T> value)
        {
            foreach (IHaveKey<T> element in self)
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
        public static bool TryGetValue<T, TResult>(this IEnumerable<IHaveKey<T>> self, T key, out TResult value) where TResult : IHaveKey<T>
        {
            bool result = self.TryGetValue<T>(key, out IHaveKey<T> _value);
            value = (_value == null) ? default : (TResult)_value;
            return result;
        }
        public static bool ContainsKey<T>(this IEnumerable<IHaveKey<T>> self, T key)
        {
            foreach (IHaveKey<T> element in self)
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
        public static IHaveKey<T> Get<T>(this IEnumerable<IHaveKey<T>> self, T key)
        {
            foreach (IHaveKey<T> element in self)
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
        public static TResult Get<T, TResult>(this IEnumerable<IHaveKey<T>> self, T key)
            => (TResult)self.Get<T>(key);
        public static bool Remove<TKey>(this IList<IHaveKey<TKey>> self, TKey key)
        {
            for (int i = self.Count - 1; i >= 0; i--)
            {
                IHaveKey<TKey> element = self[i];
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

        public static Dictionary<TKey, IHaveKey<TKey>> ToDictionary<TKey>(this IEnumerable<IHaveKey<TKey>> self)
        {
            Dictionary<TKey, IHaveKey<TKey>> result = new();
            foreach (IHaveKey<TKey> element in self)
            { result.Add(element.Key, element); }
            return result;
        }
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<IHaveKey<TKey>> self)
        {
            Dictionary<TKey, TValue> result = new();
            foreach (IHaveKey<TKey> element in self)
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
    }

    struct UndefinedOperatorFunctionOffset
    {
        public int CallInstructionIndex;

        public OperatorCall CallStatement;
        public List<CompiledParameter> currentParameters;
        public Dictionary<string, CompiledVariable> currentVariables;
        internal string CurrentFile;

        public UndefinedOperatorFunctionOffset(int callInstructionIndex, OperatorCall functionCallStatement, CompiledParameter[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables, string file)
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

        public FunctionCall CallStatement;
        public Identifier VariableStatement;
        public IndexCall IndexStatement;
        public bool IsSetter;
        public StatementWithValue ValueToAssign;

        public List<CompiledParameter> currentParameters;
        public Dictionary<string, CompiledVariable> currentVariables;
        internal string CurrentFile;

        public UndefinedFunctionOffset(int callInstructionIndex, FunctionCall functionCallStatement, CompiledParameter[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables, string file)
        {
            this.CallInstructionIndex = callInstructionIndex;
            this.CallStatement = functionCallStatement;
            this.VariableStatement = null;
            this.IndexStatement = null;
            this.IsSetter = false;
            this.ValueToAssign = null;

            this.currentParameters = new();
            this.currentVariables = new();

            foreach (var item in currentParameters)
            { this.currentParameters.Add(item); }

            foreach (var item in currentVariables)
            { this.currentVariables.Add(item.Key, item.Value); }

            this.CurrentFile = file;
        }

        public UndefinedFunctionOffset(int callInstructionIndex, Identifier variable, CompiledParameter[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables, string file)
        {
            this.CallInstructionIndex = callInstructionIndex;
            this.CallStatement = null;
            this.VariableStatement = variable;
            this.IndexStatement = null;
            this.IsSetter = false;
            this.ValueToAssign = null;

            this.currentParameters = new();
            this.currentVariables = new();

            foreach (var item in currentParameters)
            { this.currentParameters.Add(item); }
            foreach (var item in currentVariables)
            { this.currentVariables.Add(item.Key, item.Value); }
            this.CurrentFile = file;
        }

        public UndefinedFunctionOffset(int callInstructionIndex, IndexCall index, StatementWithValue valueToAssign, CompiledParameter[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables, string file)
        {
            this.CallInstructionIndex = callInstructionIndex;
            this.CallStatement = null;
            this.VariableStatement = null;
            this.IndexStatement = index;
            this.IsSetter = valueToAssign != null;
            this.ValueToAssign = valueToAssign;

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

    public static class Utils
    {
        /// <exception cref="NotImplementedException"/>
        public static Literal.Type ConvertType(System.Type type)
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

        /// <exception cref="NotImplementedException"/>
        public static bool TryConvertType(System.Type type, out Literal.Type result)
        {
            if (type == typeof(int))
            {
                result = Literal.Type.Integer;
                return true;
            }

            if (type == typeof(float))
            {
                result = Literal.Type.Float;
                return true;
            }

            if (type == typeof(bool))
            {
                result = Literal.Type.Boolean;
                return true;
            }

            if (type == typeof(string))
            {
                result = Literal.Type.String;
                return true;
            }

            result = default;
            return false;
        }
    }

    public struct AttributeValues
    {
        public List<Literal> parameters;
        public Token Identifier;

        public readonly bool TryGetValue<T>(int index, out T value)
        {
            value = default;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            Literal.Type type = Utils.ConvertType(typeof(T));
            value = type switch
            {
                Literal.Type.Integer => (T)(object)parameters[index].ValueInt,
                Literal.Type.Float => (T)(object)parameters[index].ValueFloat,
                Literal.Type.String => (T)(object)parameters[index].ValueString,
                Literal.Type.Boolean => (T)(object)parameters[index].ValueBool,
                _ => default,
            };
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

        public readonly bool TryConvert<T>(out T value)
        {
            if (!Utils.TryConvertType(typeof(T), out Type type))
            {
                value = default;
                return false;
            }

            if (type != this.type)
            {
                value = default;
                return false;
            }

            value = type switch
            {
                Type.Integer => (T)(object)ValueInt,
                Type.Float => (T)(object)ValueFloat,
                Type.String => (T)(object)ValueString,
                Type.Boolean => (T)(object)ValueBool,
                _ => default,
            };
            return true;
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

    public class CompiledOperator : FunctionDefinition, IFunctionThing, IAmInContext<CompiledClass>, IReferenceable<OperatorCall>, IDuplicateable<CompiledOperator>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset { get; set; } = -1;

        public Dictionary<string, AttributeValues> CompiledAttributes;

        public IReadOnlyList<OperatorCall> ReferencesOperator => references;
        readonly List<OperatorCall> references = new();

        public new CompiledType Type;
        public TypeInstance TypeToken => base.Type;

        public override bool IsTemplate
        {
            get
            {
                if (TemplateInfo != null) return true;
                if (Context != null && Context.TemplateInfo != null) return true;
                return false;
            }
        }

        public bool IsExternal => CompiledAttributes.ContainsKey("External");
        public string ExternalFunctionName => CompiledAttributes.TryGetAttribute("External", out string name) ? name : string.Empty;

        public string Key => this.ID();

        public CompiledClass Context { get; set; }

        public CompiledOperator(CompiledType type, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers, functionDefinition.TemplateInfo)
        {
            this.Type = type;

            base.Attributes = functionDefinition.Attributes;
            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.Type = functionDefinition.Type;
            base.FilePath = functionDefinition.FilePath;
        }
        public CompiledOperator(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers, functionDefinition.TemplateInfo)
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
        }

        public void AddReference(OperatorCall statement) => references.Add(statement);
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

        CompiledOperator IDuplicateable<CompiledOperator>.Duplicate() => new(this.Type, this)
        {
            CompiledAttributes = this.CompiledAttributes,
            Modifiers = this.Modifiers,
            ParameterTypes = new List<CompiledType>(this.ParameterTypes).ToArray(),
            TimesUsed = TimesUsed,
            TimesUsedTotal = TimesUsedTotal,
        };
    }

    public class CompiledFunction : FunctionDefinition, IFunctionThing, IAmInContext<CompiledClass>, IReferenceable<FunctionCall>, IReferenceable<IndexCall>, IDuplicateable<CompiledFunction>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset { get; set; } = -1;

        public bool ReturnSomething => this.Type.BuiltinType != BBCode.Compiler.Type.VOID;

        public Dictionary<string, AttributeValues> CompiledAttributes;

        public IReadOnlyList<Statement> ReferencesFunction => references;
        readonly List<Statement> references = new();

        public new CompiledType Type;
        public TypeInstance TypeToken => base.Type;

        public override bool IsTemplate
        {
            get
            {
                if (TemplateInfo != null) return true;
                if (Context != null && Context.TemplateInfo != null) return true;
                return false;
            }
        }

        public bool IsExternal => CompiledAttributes.ContainsKey("External");
        public string ExternalFunctionName
        {
            get
            {
                if (CompiledAttributes.TryGetValue("External", out var attributeValues))
                {
                    if (attributeValues.TryGetValue(0, out string name))
                    {
                        return name;
                    }
                }
                return string.Empty;
            }
        }

        public string Key => this.ID();

        public CompiledClass Context { get; set; }

        public CompiledFunction(CompiledType type, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers, functionDefinition.TemplateInfo)
        {
            this.Type = type;

            base.Attributes = functionDefinition.Attributes;
            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.Type = functionDefinition.Type;
            base.FilePath = functionDefinition.FilePath;
        }
        public CompiledFunction(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers, functionDefinition.TemplateInfo)
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
        }

        public void AddReference(FunctionCall statement) => references.Add(statement);
        public void AddReference(KeywordCall statement) => references.Add(statement);
        public void AddReference(IndexCall statement) => references.Add(statement);
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

        public bool IsSame(IFunctionThing other)
        {
            if (other is not CompiledFunction other2) return false;
            return IsSame(other2);
        }

        public CompiledFunction Duplicate() => new(this.Type, this)
        {
            CompiledAttributes = this.CompiledAttributes,
            Context = this.Context,
            Modifiers = this.Modifiers,
            ParameterTypes = new List<CompiledType>(this.ParameterTypes).ToArray(),
            TimesUsed = TimesUsed,
            TimesUsedTotal = TimesUsedTotal,
        };

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
            if (this.ParameterTypes.Length > 0)
            {
                for (int i = 0; i < ParameterTypes.Length; i++)
                {
                    if (i > 0) result += ", ";
                    result += ParameterTypes[i].ToString();
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
    }

    public class CompiledGeneralFunction : GeneralFunctionDefinition, IFunctionThing, IAmInContext<CompiledClass>, IReferenceable<KeywordCall>, IReferenceable<ConstructorCall>, IDuplicateable<CompiledGeneralFunction>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset { get; set; } = -1;

        public bool ReturnSomething => this.Type.BuiltinType != BBCode.Compiler.Type.VOID;

        public IReadOnlyList<Statement> References => references;
        readonly List<Statement> references = new();

        public override bool IsTemplate
        {
            get
            {
                if (TemplateInfo != null) return true;
                if (context != null && context.TemplateInfo != null) return true;
                return false;
            }
        }

        public CompiledType Type;

        CompiledClass context;
        public CompiledClass Context
        {
            get => context;
            set => context = value;
        }

        public CompiledGeneralFunction(CompiledType type, GeneralFunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers)
        {
            this.Type = type;

            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.FilePath = functionDefinition.FilePath;
        }
        public CompiledGeneralFunction(CompiledType type, CompiledType[] parameterTypes, GeneralFunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;

            base.BracketEnd = functionDefinition.BracketEnd;
            base.BracketStart = functionDefinition.BracketStart;
            base.Parameters = functionDefinition.Parameters;
            base.Statements = functionDefinition.Statements;
            base.FilePath = functionDefinition.FilePath;
        }

        public void AddReference(KeywordCall statement) => references.Add(statement);
        public void AddReference(ConstructorCall statement) => references.Add(statement);
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

        public CompiledGeneralFunction Duplicate() => new(Type, ParameterTypes, this)
        {
            context = this.context,
            Modifiers = this.Modifiers,
            TimesUsed = this.TimesUsed,
            TimesUsedTotal = this.TimesUsedTotal,
        };

        public override string ToString()
        {
            string result = "";
            if (IsExport)
            {
                result += "export ";
            }
            result += this.Identifier.Content;

            result += '(';
            if (this.ParameterTypes.Length > 0)
            {
                for (int i = 0; i < ParameterTypes.Length; i++)
                {
                    if (i > 0) result += ", ";
                    result += ParameterTypes[i].ToString();
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
    }

    public interface IFunctionThing
    {
        internal int InstructionOffset { get; set; }
        internal bool IsSame(IFunctionThing other);
    }

    public class CompiledVariable : VariableDeclaretion
    {
        public readonly new CompiledType Type;

        public readonly int MemoryAddress;
        public readonly bool IsGlobal;
        public readonly bool IsStoredInHEAP;

        public CompiledVariable(int memoryOffset, CompiledType type, bool isGlobal, bool storedInHeap, VariableDeclaretion declaration)
            : base(declaration.Modifiers, declaration.Type, declaration.VariableName, declaration.InitialValue)
        {
            this.Type = type;

            this.MemoryAddress = memoryOffset;
            this.IsStoredInHEAP = storedInHeap;
            this.IsGlobal = isGlobal;

            base.FilePath = declaration.FilePath;
        }
    }

    public class CompiledEnumMember : EnumMemberDefinition, IHaveKey<string>
    {
        public new DataItem Value;
    }

    public class CompiledEnum : EnumDefinition, ITypeDefinition, IHaveKey<string>
    {
        public new CompiledEnumMember[] Members;
        internal Dictionary<string, AttributeValues> CompiledAttributes;
    }

    public class CompiledStruct : StructDefinition, ITypeDefinition, IDataStructure, IHaveKey<string>
    {
        public new readonly CompiledField[] Fields;
        internal Dictionary<string, AttributeValues> CompiledAttributes;
        public List<DefinitionReference> References = null;
        internal IReadOnlyDictionary<string, int> FieldOffsets
        {
            get
            {
                Dictionary<string, int> result = new();
                int currentOffset = 0;
                foreach (var field in Fields)
                {
                    result.Add(field.Identifier.Content, currentOffset);
                    switch (field.Type.BuiltinType)
                    {
                        case Type.BYTE:
                        case Type.INT:
                        case Type.FLOAT:
                        case Type.CHAR:
                            currentOffset++;
                            break;
                        default: throw new NotImplementedException();
                    }
                }
                return result;
            }
        }
        public int Size
        {
            get
            {
                int size = 0;
                for (int i = 0; i < Fields.Length; i++)
                {
                    CompiledField field = Fields[i];
                    size += field.Type.SizeOnStack;
                }
                return size;
            }
        }

        public CompiledStruct(Dictionary<string, AttributeValues> compiledAttributes, CompiledField[] fields, StructDefinition definition) : base(definition.Name, definition.Attributes, definition.Fields, definition.Methods, definition.Modifiers)
        {
            this.CompiledAttributes = compiledAttributes;
            this.Fields = fields;

            base.FilePath = definition.FilePath;
            base.BracketEnd = definition.BracketEnd;
            base.BracketStart = definition.BracketStart;
            base.Statements = definition.Statements;
        }
    }

    public class CompiledClass : ClassDefinition, ITypeDefinition, IDataStructure, IHaveKey<string>, IDuplicateable<CompiledClass>
    {
        public new readonly CompiledField[] Fields;
        internal Dictionary<string, AttributeValues> CompiledAttributes;
        public List<DefinitionReference> References = null;
        readonly Dictionary<string, CompiledType> currentTypeArguments;
        public IReadOnlyDictionary<string, CompiledType> CurrentTypeArguments => currentTypeArguments;

        public IReadOnlyDictionary<string, int> FieldOffsets
        {
            get
            {
                Dictionary<string, int> result = new();
                int currentOffset = 0;
                foreach (CompiledField field in Fields)
                {
                    result.Add(field.Identifier.Content, currentOffset);
                    currentOffset += GetType(field.Type, field).SizeOnStack;
                }
                return result;
            }
        }
        public int Size
        {
            get
            {
                int size = 0;
                foreach (CompiledField field in Fields)
                { size += GetType(field.Type, field).SizeOnStack; }
                return size;
            }
        }

        public void AddTypeArguments(IEnumerable<CompiledType> typeArguments)
             => AddTypeArguments(typeArguments.ToArray());
        public void AddTypeArguments(CompiledType[] typeArguments)
        {
            if (TemplateInfo == null)
            { return; }

            if (typeArguments == null || typeArguments.Length == 0)
            { return; }

            string[] typeParameters = TemplateInfo.ToDictionary().Keys.ToArray();

            if (typeArguments.Length != typeParameters.Length)
            { throw new CompilerException("Ah"); }

            for (int i = 0; i < typeArguments.Length; i++)
            {
                if (TemplateInfo == null)
                { throw new CompilerException("Ah"); }

                CompiledType value = typeArguments[i];
                string key = typeParameters[i];

                currentTypeArguments[key] = value;
            }
        }
        internal void AddTypeArguments(Dictionary<string, CompiledType> typeArguments)
        {
            if (TemplateInfo == null)
            { return; }

            string[] typeParameters = TemplateInfo.ToDictionary().Keys.ToArray();

            for (int i = 0; i < typeParameters.Length; i++)
            {
                if (!typeArguments.TryGetValue(typeParameters[i], out var ehhh))
                { continue; }
                currentTypeArguments[typeParameters[i]] = ehhh;
            }
        }

        public void ClearTypeArguments() => currentTypeArguments.Clear();

        CompiledType GetType(CompiledType type, IThingWithPosition position)
        {
            if (!type.IsGeneric) return type;
            if (!currentTypeArguments.TryGetValue(type.Name, out CompiledType result))
            { throw new CompilerException($"Type argument \"{type.Name}\" not found", position, FilePath); }
            return result;
        }

        public CompiledClass Duplicate() => new(CompiledAttributes, Fields, this)
        {

        };

        public CompiledClass(Dictionary<string, AttributeValues> compiledAttributes, CompiledField[] fields, ClassDefinition definition) : base(definition.Name, definition.Attributes, definition.Modifiers, definition.Fields, definition.Methods, definition.GeneralMethods, definition.Operators)
        {
            this.CompiledAttributes = compiledAttributes;
            this.Fields = fields;
            this.TemplateInfo = definition.TemplateInfo;
            this.currentTypeArguments = new Dictionary<string, CompiledType>();

            base.FilePath = definition.FilePath;
            base.BracketEnd = definition.BracketEnd;
            base.BracketStart = definition.BracketStart;
            base.Statements = definition.Statements;
        }

        public bool TryGetTypeArgumentIndex(string typeArgumentName, out int index)
        {
            index = 0;
            if (TemplateInfo == null) return false;
            for (int i = 0; i < TemplateInfo.TypeParameters.Length; i++)
            {
                if (TemplateInfo.TypeParameters[i].Content == typeArgumentName)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            string result = $"class";
            result += ' ';
            result += $"{this.Name.Content}";
            if (this.TemplateInfo != null)
            {
                result += '<';
                if (this.currentTypeArguments.Count > 0)
                {
                    for (int i = 0; i < this.TemplateInfo.TypeParameters.Length; i++)
                    {
                        if (i > 0) result += ", ";

                        string typeParameterName = this.TemplateInfo.TypeParameters[i].Content;
                        if (this.currentTypeArguments.TryGetValue(typeParameterName, out var typeParameterValue))
                        {
                            result += typeParameterValue.ToString();
                        }
                        else
                        {
                            result += "?";
                        }
                    }
                }
                else
                {
                    result += string.Join<Token>(", ", this.TemplateInfo.TypeParameters);
                }
                result += '>';
            }
            result += ' ';
            result += "{...}";
            return result;
        }
    }

    public class CompiledParameter : ParameterDefinition
    {
        public new CompiledType Type;

        readonly int index;
        readonly int currentParamsSize;

        public int Index => index;
        public int RealIndex => (-2) - (currentParamsSize + 1 - index);

        public CompiledParameter(int index, int currentParamsSize, CompiledType type, ParameterDefinition definition)
        {
            this.index = index;
            this.currentParamsSize = currentParamsSize;
            this.Type = type;

            base.Identifier = definition.Identifier;
            base.Modifiers = definition.Modifiers;
        }

        public CompiledParameter(CompiledType type, ParameterDefinition definition)
            : this(-1, -1, type, definition) { }

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

        public string FilePath
        {
            get => Function.FilePath;
            set => Function.FilePath = value;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is not FunctionType other) return false;

            return Equals(other);
        }

        public bool Equals(FunctionType other)
        {
            if (other is null) return false;

            return Function.IsSame(other.Function);
        }

        public override int GetHashCode() => HashCode.Combine(Function);

        public static bool operator ==(FunctionType a, FunctionType b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            return a.Equals(b);
        }

        public static bool operator !=(FunctionType a, FunctionType b) => !(a == b);
    }

    public class CompiledType : IEquatable<CompiledType>, IEquatable<TypeInstance>, IEquatable<Type>, IEquatable<RuntimeType>
    {
        Type builtinType;

        CompiledStruct @struct;
        CompiledClass @class;
        CompiledEnum @enum;
        FunctionType function;
        CompiledType[] typeParameters;

        internal Type BuiltinType => builtinType;
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        internal RuntimeType RuntimeType => builtinType switch
        {
            Type.BYTE => RuntimeType.BYTE,
            Type.INT => RuntimeType.INT,
            Type.FLOAT => RuntimeType.FLOAT,
            Type.CHAR => RuntimeType.CHAR,

            Type.NONE => throw new InternalException($"{this} is not a built-in type"),

            _ => throw new NotImplementedException($"Type conversion for {builtinType} is not implemented"),
        };

        internal CompiledStruct Struct => @struct;
        internal CompiledClass Class => @class;
        internal CompiledEnum Enum => @enum;
        internal FunctionType Function => function;
        internal CompiledType[] TypeParameters => typeParameters;

        string genericName;


        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        internal string Name
        {
            get
            {
                if (IsGeneric) return genericName;

                if (builtinType != Type.NONE) return builtinType switch
                {
                    Type.VOID => "void",
                    Type.BYTE => "byte",
                    Type.INT => "int",
                    Type.FLOAT => "float",
                    Type.CHAR => "char",

                    Type.UNKNOWN => "unknown",
                    Type.NONE => throw new ImpossibleException(),

                    _ => throw new NotImplementedException($"Type conversion for {builtinType} is not implemented"),
                };

                if (@struct != null) return @struct.Name.Content;
                if (@class != null) return @class.Name.Content;
                if (@enum != null) return @enum.Identifier.Content;
                if (function != null) return function.Function.Identifier.Content;

                throw new ImpossibleException();
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
        internal bool IsGeneric => !string.IsNullOrEmpty(genericName);

        public int Size
        {
            get
            {
                if (IsGeneric) throw new InternalException($"Can not get the size of a generic type");
                if (IsStruct) return @struct.Size;
                if (IsClass) return @class.Size;
                if (IsEnum) return 1;
                if (IsFunction) return 1;
                return 1;
            }
        }
        /// <summary>
        /// Returns the class's size or 0 if it is not a class
        /// </summary>
        public int SizeOnHeap
        {
            get
            {
                if (IsGeneric) throw new InternalException($"Can not get the size of a generic type");
                if (IsClass) return @class.Size;
                return 0;
            }
        }
        /// <summary>
        /// Returns the struct's size or 1 if it is not a class
        /// </summary>
        public int SizeOnStack
        {
            get
            {
                if (IsGeneric) throw new InternalException($"Can not get the size of a generic type");
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
            this.genericName = null;
            this.typeParameters = Array.Empty<CompiledType>();
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
        internal CompiledType(CompiledClass @class, params CompiledType[][] typeParameters) : this()
        {
            this.@class = @class ?? throw new ArgumentNullException(nameof(@class));
            List<CompiledType> typeParameters1 = new();
            for (int i = 0; i < typeParameters.Length; i++)
            { typeParameters1.AddRange(typeParameters[i]); }
            this.typeParameters = typeParameters1.ToArray();
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

        /// <exception cref="InternalException"/>
        internal CompiledType(RuntimeType type) : this()
        {
            this.builtinType = type switch
            {
                RuntimeType.BYTE => Type.BYTE,
                RuntimeType.INT => Type.INT,
                RuntimeType.FLOAT => Type.FLOAT,
                RuntimeType.CHAR => Type.CHAR,
                _ => throw new ImpossibleException(),
            };
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public CompiledType(TypeInstance type, Func<string, CompiledType> typeFinder) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (Constants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out this.builtinType))
            { return; }

            if (typeFinder == null) throw new InternalException($"Can't parse {type} to CompiledType");

            Set(typeFinder.Invoke(type.Identifier.Content));

            typeParameters = CompiledType.FromArray(type.GenericTypes, typeFinder);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
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

            throw new InternalException($"Unknown type definition {type.GetType().FullName}");
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        void Set(CompiledType other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));

            this.builtinType = other.builtinType;
            this.@class = other.@class;
            this.@enum = other.@enum;
            this.function = other.function;
            this.genericName = other.genericName;
            this.@struct = other.@struct;
            this.typeParameters = new List<CompiledType>(other.typeParameters).ToArray();
        }

        public override string ToString()
        {
            string result = Name;

            if (TypeParameters.Length > 0)
            {
                result += '<';
                result += string.Join<CompiledType>(", ", TypeParameters);
                result += '>';
            }
            else
            {
                if (@class != null)
                {
                    if (@class.TemplateInfo != null)
                    {
                        result += '<';
                        result += string.Join<Token>(", ", @class.TemplateInfo.TypeParameters);
                        result += '>';
                    }
                }
            }

            return result;
        }

        public static bool operator ==(CompiledType a, CompiledType b)
        {
            if (a is null && b is null) return true;
            if (a is null) return false;
            if (b is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType a, CompiledType b) => !(a == b);

        public static bool operator ==(CompiledType a, RuntimeType b)
        {
            if (a is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType a, RuntimeType b) => !(a == b);

        public static bool operator ==(CompiledType a, Type b)
        {
            if (a is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType a, Type b) => !(a == b);

        public static bool operator ==(CompiledType a, TypeInstance b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType a, TypeInstance b) => !(a == b);

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is not CompiledType other) return false;
            return this.Equals(other);
        }

        public bool Equals(CompiledType other)
        {
            if (other is null) return false;

            if (this.genericName != other.genericName) return false;

            if (this.IsBuiltin != other.IsBuiltin) return false;
            if (this.IsClass != other.IsClass) return false;
            if (this.IsStruct != other.IsStruct) return false;
            if (this.IsFunction != other.IsFunction) return false;

            if (!CompiledType.Equals(this.typeParameters, other.typeParameters)) return false;

            if (this.IsClass && other.IsClass) return this.@class.Name.Content == other.@class.Name.Content;
            if (this.IsStruct && other.IsStruct) return this.@struct.Name.Content == other.@struct.Name.Content;
            if (this.IsEnum && other.IsEnum) return this.@enum.Identifier.Content == other.@enum.Identifier.Content;
            if (this.IsFunction && other.IsFunction) return this.@function == other.@function;

            if (this.IsBuiltin && other.IsBuiltin) return this.builtinType == other.builtinType;

            return true;
        }

        public bool Equals(TypeInstance other)
        {
            if (other is null) return false;

            if (!CompiledType.Equals(this.typeParameters, other.GenericTypes)) return false;

            if (Constants.BuiltinTypeMap3.TryGetValue(other.Identifier.Content, out var type))
            { return type == this.builtinType; }

            if (this.@struct != null && this.@struct.Name.Content == other.Identifier.Content)
            { return true; }

            if (this.@class != null && this.@class.Name.Content == other.Identifier.Content)
            { return true; }

            if (this.@enum != null && this.@enum.Identifier.Content == other.Identifier.Content)
            { return true; }

            return false;
        }

        public bool Equals(Type other)
        {
            if (!this.IsBuiltin) return false;
            return this.BuiltinType == other;
        }

        public bool Equals(RuntimeType other)
        {
            if (this.IsEnum)
            {
                for (int i = 0; i < this.@enum.Members.Length; i++)
                {
                    if (this.@enum.Members[i].Value.Type == other)
                    { return true; }
                }
            }

            if (!this.IsBuiltin) return false;
            return this.RuntimeType == other;
        }

        public static bool Equals(CompiledType[] a, CompiledType[] b, out Dictionary<string, CompiledType> typeParameters)
        {
            typeParameters = new Dictionary<string, CompiledType>();

            if (a is null || b is null) return false;

            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (b[i].IsGeneric)
                { throw new NotImplementedException(); }

                if (a[i].IsGeneric)
                {
                    if (typeParameters.TryGetValue(a[i].Name, out CompiledType addedGenericType))
                    { if (addedGenericType != b[i]) return false; }
                    else
                    { typeParameters.Add(a[i].Name, b[i]); }

                    continue;
                }

                if (a[i].IsClass && a[i].Class.TemplateInfo != null)
                {
                    Token[] classTypeParameters = a[i].Class.TemplateInfo.TypeParameters;
                    if (classTypeParameters.Length != b[i].TypeParameters.Length)
                    { throw new NotImplementedException("Bruh"); }

                    for (int j = 0; j < classTypeParameters.Length; j++)
                    {
                        string typeParameterName = classTypeParameters[j].Content;
                        CompiledType typeParameterValue = b[i].TypeParameters[j];

                        if (typeParameters.TryGetValue(typeParameterName, out CompiledType addedGenericType))
                        { if (addedGenericType != typeParameterValue) return false; }
                        else
                        { typeParameters.Add(typeParameterName, typeParameterValue); }
                    }

                    continue;
                }

                if (a[i] != b[i]) return false;
            }

            return true;
        }

        public static bool Equals(CompiledType[] a, CompiledType[] b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            { if (!a[i].Equals(b[i])) return false; }
            return true;
        }

        public static bool Equals(CompiledType[] a, TypeInstance[] b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            { if (a[i] != b[i]) return false; }
            return true;
        }

        public override int GetHashCode() => HashCode.Combine(builtinType, @struct, @class);

        public static CompiledType CreateGeneric(string content)
            => new()
            { genericName = content, };

        public static CompiledType[] FromArray(List<TypeInstance> types, Func<string, CompiledType> typeFinder)
            => CompiledType.FromArray(types.ToArray(), typeFinder);
        public static CompiledType[] FromArray(IEnumerable<TypeInstance> types, Func<string, CompiledType> typeFinder)
            => CompiledType.FromArray(types.ToArray(), typeFinder);
        public static CompiledType[] FromArray(TypeInstance[] types, Func<string, CompiledType> typeFinder)
        {
            if (types is null || types.Length == 0) return Array.Empty<CompiledType>();
            CompiledType[] result = new CompiledType[types.Length];
            for (int i = 0; i < types.Length; i++)
            { result[i] = new CompiledType(types[i], typeFinder); }
            return result;
        }
    }

    public interface ITypeDefinition : IDefinition
    {

    }

    public interface IDataStructure
    {
        public int Size { get; }
    }

    public interface IHaveKey<T>
    {
        public T Key { get; }
    }

    public interface IAmInContext<T>
    {
        public T Context { get; set; }
    }
}
