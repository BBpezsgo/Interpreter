using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace LanguageCore
{
    public static partial class Utils
    {
        public const int NULL_POINTER = int.MinValue / 2;
    }
}

namespace LanguageCore.BBCode.Compiler
{
    using System.Diagnostics.CodeAnalysis;
    using LanguageCore.Runtime;
    using LanguageCore.Tokenizing;
    using Parser;
    using Parser.Statement;

    public interface IDuplicatable<T>
    {
        public T Duplicate();
    }

    public static class Extensions
    {
        internal static AddressingMode AddressingMode(this CompiledVariable v)
            => v.IsGlobal ? Runtime.AddressingMode.ABSOLUTE : Runtime.AddressingMode.BASEPOINTER_RELATIVE;

        internal static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> v, IEnumerable<KeyValuePair<TKey, TValue>> elements) where TKey : notnull
        {
            foreach (KeyValuePair<TKey, TValue> pair in elements)
            { v.Add(pair.Key, pair.Value); }
        }

        internal static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> v, List<TValue> values, Func<TValue, TKey> keys) where TKey : notnull
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

        public static bool ContainsSameDefinition(this IEnumerable<MacroDefinition> functions, MacroDefinition other)
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

        public static bool TryGetValue<T>(this IEnumerable<IHaveKey<T>> self, T key, [NotNullWhen(true)] out IHaveKey<T>? value) where T : notnull
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
        public static bool TryGetValue<T, TResult>(this IEnumerable<IHaveKey<T>> self, T key, [NotNullWhen(true)] out TResult? value) where TResult : IHaveKey<T> where T : notnull
        {
            bool result = self.TryGetValue<T>(key, out IHaveKey<T>? _value);
            value = (_value == null) ? default : (TResult)_value;
            return result;
        }
        public static bool ContainsKey<T>(this IEnumerable<IHaveKey<T>> self, T key) where T : notnull
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
        public static IHaveKey<T> Get<T>(this IEnumerable<IHaveKey<T>> self, T key) where T : notnull
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
        public static TResult Get<T, TResult>(this IEnumerable<IHaveKey<T>> self, T key) where T : notnull
            => (TResult)self.Get<T>(key);
        public static bool Remove<TKey>(this IList<IHaveKey<TKey>> self, TKey key) where TKey : notnull
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

        public static Dictionary<TKey, IHaveKey<TKey>> ToDictionary<TKey>(this IEnumerable<IHaveKey<TKey>> self) where TKey : notnull
        {
            Dictionary<TKey, IHaveKey<TKey>> result = new();
            foreach (IHaveKey<TKey> element in self)
            { result.Add(element.Key, element); }
            return result;
        }
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<IHaveKey<TKey>> self) where TKey : notnull
        {
            Dictionary<TKey, TValue> result = new();
            foreach (IHaveKey<TKey> element in self)
            { result.Add(element.Key, (TValue)element); }
            return result;
        }

        #region KeyValuePair<TKey, TValue>

        public static bool TryGetValue<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> self, TKey key, out TValue? value) where TKey : notnull
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
        public static bool ContainsKey<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> self, TKey key) where TKey : notnull
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
        public static TValue Get<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> self, TKey key) where TKey : notnull
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
        public static bool Remove<TKey, TValue>(this List<KeyValuePair<TKey, TValue>> self, TKey key) where TKey : notnull
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
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> self) where TKey : notnull
        {
            Dictionary<TKey, TValue> result = new();
            foreach (KeyValuePair<TKey, TValue> element in self)
            { result.Add(element.Key, element.Value); }
            return result;
        }

        #endregion
    }

    readonly struct UndefinedOperatorFunctionOffset
    {
        public readonly int CallInstructionIndex;

        public readonly CompiledOperator Operator;
        public readonly OperatorCall CallStatement;

        internal readonly string? CurrentFile;

        public UndefinedOperatorFunctionOffset(int callInstructionIndex, OperatorCall statement, CompiledOperator @operator, string? file)
        {
            this.CallInstructionIndex = callInstructionIndex;

            this.Operator = @operator;
            this.CallStatement = statement;

            this.CurrentFile = file;
        }
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    readonly struct UndefinedFunctionOffset
    {
        public readonly int CallInstructionIndex;

        public readonly CompiledFunction? Function;
        public readonly FunctionCall? CallStatement;
        public readonly Identifier? VariableStatement;
        public readonly IndexCall? IndexStatement;

        internal readonly string? CurrentFile;

        public UndefinedFunctionOffset(int callInstructionIndex, FunctionCall functionCallStatement, CompiledFunction function, string? file)
        {
            this.CallInstructionIndex = callInstructionIndex;
            this.CallStatement = functionCallStatement;
            this.VariableStatement = null;
            this.IndexStatement = null;
            this.Function = function;

            this.CurrentFile = file;
        }

        public UndefinedFunctionOffset(int callInstructionIndex, Identifier variable, CompiledFunction function, string? file)
        {
            this.CallInstructionIndex = callInstructionIndex;
            this.CallStatement = null;
            this.VariableStatement = variable;
            this.IndexStatement = null;
            this.Function = function;

            this.CurrentFile = file;
        }

        private readonly string GetDebuggerDisplay()
        {
            if (CallStatement != null) return CallStatement.ToString();
            if (VariableStatement != null) return VariableStatement.ToString();
            return "null";
        }
    }

    readonly struct UndefinedGeneralFunctionOffset
    {
        public readonly int CallInstructionIndex;

        public readonly Statement? CallStatement;
        public readonly CompiledGeneralFunction GeneralFunction;

        internal readonly string? CurrentFile;

        public UndefinedGeneralFunctionOffset(int callInstructionIndex, Statement? functionCallStatement, CompiledGeneralFunction generalFunction, string? file)
        {
            this.CallInstructionIndex = callInstructionIndex;

            this.CallStatement = functionCallStatement;
            this.GeneralFunction = generalFunction;

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

        public static void SetTypeParameters(CompiledType[] typeParameters, Dictionary<string, CompiledType> typeValues)
        {
            for (int i = 0; i < typeParameters.Length; i++)
            {
                if (typeParameters[i].IsGeneric)
                {
                    if (!typeValues.TryGetValue(typeParameters[i].Name, out CompiledType? eTypeParameter))
                    { throw new NotImplementedException(); }
                    typeParameters[i] = eTypeParameter;
                }
            }
        }
    }

    public readonly struct DefinitionReference
    {
        public readonly Range<SinglePosition> Source;
        public readonly string? SourceFile;

        public DefinitionReference(Range<SinglePosition> source, string? sourceFile)
        {
            Source = source;
            SourceFile = sourceFile;
        }

        public DefinitionReference(IThingWithPosition source, string? sourceFile)
        {
            Source = source.GetPosition().Range;
            SourceFile = sourceFile;
        }
    }

    public interface IReferenceable<T>
    {
        public void AddReference(T reference);
        public void ClearReferences();
    }

    public interface IFunctionThing
    {
        internal int InstructionOffset { get; set; }
        internal bool IsSame(IFunctionThing other);
    }

    public enum Protection
    {
        Private,
        Public,
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

    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    public class FunctionType : IEquatable<FunctionType>, IEquatable<CompiledFunction>
    {
        public readonly CompiledType ReturnType;
        public readonly CompiledType[] Parameters;

        public bool ReturnSomething => ReturnType != Type.VOID;

        public FunctionType(CompiledFunction function)
        {
            ReturnType = function.Type;
            Parameters = new CompiledType[function.ParameterTypes.Length];
            Array.Copy(function.ParameterTypes, Parameters, function.Parameters.Length);
        }

        public FunctionType(CompiledType returnType, CompiledType[] parameters)
        {
            ReturnType = returnType;
            Parameters = parameters;
        }

        public override string ToString()
        {
            StringBuilder result = new();
            result.Append(ReturnType?.ToString() ?? "null");
            result.Append('(');
            if (Parameters != null) for (int i = 0; i < Parameters.Length; i++)
                {
                    if (i > 0) result.Append(", ");
                    result.Append(Parameters[i]?.ToString() ?? "null");
                }
            result.Append(')');

            return result.ToString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (obj is not FunctionType other) return false;

            return Equals(other);
        }

        public bool Equals(FunctionType? other)
        {
            if (other is null) return false;

            if (!other.ReturnType.Equals(ReturnType)) return false;

            if (!CompiledType.Equals(Parameters, other.Parameters)) return false;

            return true;
        }

        public bool Equals(CompiledFunction? other)
        {
            if (other is null) return false;

            if (!other.Type.Equals(ReturnType)) return false;

            if (!CompiledType.Equals(Parameters, other.ParameterTypes)) return false;

            return true;
        }

        public override int GetHashCode() => HashCode.Combine(ReturnType, Parameters);

        public static bool operator ==(FunctionType a, FunctionType b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            return a.Equals(b);
        }

        public static bool operator !=(FunctionType a, FunctionType b) => !(a == b);
    }

    public delegate bool ComputeValue(StatementWithValue value, RuntimeType? expectedType, out DataItem computedValue);

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class CompiledType : IEquatable<CompiledType>, IEquatable<TypeInstance>, IEquatable<Type>, IEquatable<RuntimeType>
    {
        Type builtinType;

        CompiledStruct? @struct;
        CompiledClass? @class;
        CompiledEnum? @enum;
        FunctionType? function;
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

        internal CompiledStruct Struct => @struct ?? throw new InternalException($"This isn't a struct");
        internal CompiledClass Class => @class ?? throw new InternalException($"This isn't a class");
        internal CompiledEnum Enum => @enum ?? throw new InternalException($"This isn't an enum");
        internal FunctionType Function => function ?? throw new InternalException($"This isn't a function");
        internal CompiledType[] TypeParameters => typeParameters;

        string? genericName;
        CompiledType? stackArrayOf;
        StatementWithValue? stackArraySizeStatement;
        DataItem stackArraySize;

        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        internal string Name
        {
            get
            {
                if (IsGeneric) return genericName;

                if (stackArrayOf is not null) return stackArrayOf.Name;

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

                if (@struct is not null) return @struct.Name.Content;
                if (@class is not null) return @class.Name.Content;
                if (@enum is not null) return @enum.Identifier.Content;
                if (function is not null) return function.ToString();

                throw new ImpossibleException();
            }
        }
        /// <summary><c><see cref="Class"/> != <see langword="null"/></c></summary>
        [MemberNotNullWhen(true, nameof(@class))]
        internal bool IsClass => @class is not null;
        /// <summary><c><see cref="Enum"/> != <see langword="null"/></c></summary>
        [MemberNotNullWhen(true, nameof(@enum))]
        internal bool IsEnum => @enum is not null;
        /// <summary><c><see cref="Struct"/> != <see langword="null"/></c></summary>
        [MemberNotNullWhen(true, nameof(@struct))]
        internal bool IsStruct => @struct is not null;
        /// <summary><c><see cref="Function"/> != <see langword="null"/></c></summary>
        [MemberNotNullWhen(true, nameof(function))]
        internal bool IsFunction => function is not null;
        internal bool IsBuiltin => builtinType != Type.NONE;
        internal bool CanBeBuiltin
        {
            get
            {
                if (builtinType != Type.NONE) return true;
                if (IsEnum) return true;

                return false;
            }
        }
        internal bool InHEAP => IsClass;
        [MemberNotNullWhen(true, nameof(genericName))]
        internal bool IsGeneric => !string.IsNullOrEmpty(genericName);
        [MemberNotNullWhen(true, nameof(stackArrayOf))]
        internal bool IsStackArray => stackArrayOf is not null;

        public int Size
        {
            get
            {
                if (IsGeneric) throw new InternalException($"Can not get the size of a generic type");
                if (IsStruct) return @struct.Size;
                if (IsClass) return @class.Size;
                if (IsEnum) return 1;
                if (IsFunction) return 1;
                if (IsStackArray) return (stackArraySize * new DataItem(stackArrayOf.SizeOnStack)).Integer ?? throw new InternalException(); ;
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
        /// Returns the struct size or 1 if it is not a struct
        /// </summary>
        public int SizeOnStack
        {
            get
            {
                if (IsGeneric) throw new InternalException($"Can not get the size of a generic type");
                if (IsStruct) return @struct.Size;
                if (IsStackArray) return (stackArraySize * new DataItem(stackArrayOf.SizeOnStack)).ValueInt;
                return 1;
            }
        }

        public int StackArraySize
        {
            get
            {
                if (!IsStackArray) throw new InternalException($"Can not get the stack array size of a non stack array type");
                return stackArraySize.ValueInt;
            }
        }
        public CompiledType StackArrayOf
        {
            get
            {
                if (!IsStackArray) throw new InternalException($"Can not get the stack array type of a non stack array type");
                return stackArrayOf;
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
            this.stackArrayOf = null;
        }

        /// <exception cref="ArgumentNullException"/>
        internal CompiledType(CompiledStruct @struct) : this()
        {
            this.@struct = @struct ?? throw new ArgumentNullException(nameof(@struct));
        }

        /// <exception cref="ArgumentNullException"/>
        internal CompiledType(FunctionType function) : this()
        {
            this.function = function ?? throw new ArgumentNullException(nameof(function));
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
        public CompiledType(string type, Func<string, CompiledType>? typeFinder) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (Constants.BuiltinTypeMap3.TryGetValue(type, out this.builtinType))
            { return; }

            if (typeFinder == null) throw new InternalException($"Can't parse {type} to CompiledType");

            Set(typeFinder.Invoke(type));
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public CompiledType(TypeInstance type, Func<string, CompiledType>? typeFinder, ComputeValue? constComputer = null) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (type is TypeInstanceSimple simpleType)
            {
                Set(new CompiledType(simpleType, typeFinder, constComputer));
                return;
            }

            if (type is TypeInstanceFunction functionType)
            {
                Set(new CompiledType(functionType, typeFinder, constComputer));
                return;
            }

            if (type is TypeInstanceStackArray stackArrayType)
            {
                Set(new CompiledType(stackArrayType, typeFinder, constComputer));
                return;
            }

            throw new ImpossibleException();
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public CompiledType(TypeInstanceSimple type, Func<string, CompiledType>? typeFinder, ComputeValue? constComputer = null) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (Constants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out this.builtinType))
            { return; }

            if (type.Identifier.Content == "func")
            {
                type.Identifier.AnalyzedType = TokenAnalysedType.Keyword;

                CompiledType funcRet;
                CompiledType[] funcParams;

                if (type.GenericTypes == null || type.GenericTypes.Length == 0)
                {
                    funcRet = new(Type.VOID);
                    funcParams = Array.Empty<CompiledType>();
                }
                else
                {
                    funcRet = new(type.GenericTypes[0], typeFinder);
                    funcParams = new CompiledType[type.GenericTypes.Length - 1];
                    for (int i = 1; i < type.GenericTypes.Length; i++)
                    {
                        TypeInstance genericType = type.GenericTypes[i];
                        funcParams[i - 1] = new CompiledType(genericType, typeFinder);
                    }
                }
                function = new FunctionType(funcRet, funcParams);
                return;
            }

            if (typeFinder == null) throw new InternalException($"Can't parse \"{type}\" to \"{nameof(CompiledType)}\"");

            Set(typeFinder.Invoke(type.Identifier.Content));

            typeParameters = CompiledType.FromArray(type.GenericTypes, typeFinder);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public CompiledType(TypeInstanceFunction type, Func<string, CompiledType>? typeFinder, ComputeValue? constComputer = null) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            CompiledType returnType = new(type.FunctionReturnType, typeFinder, constComputer);
            CompiledType[] parameterTypes = CompiledType.FromArray(type.FunctionParameterTypes, typeFinder);

            function = new FunctionType(returnType, parameterTypes);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public CompiledType(TypeInstanceStackArray type, Func<string, CompiledType>? typeFinder, ComputeValue? constComputer = null) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (constComputer == null)
            { throw new ArgumentNullException(nameof(constComputer)); }

            stackArrayOf = new CompiledType(type.StackArrayOf, typeFinder, constComputer);
            stackArraySizeStatement = type.StackArraySize!;

            if (!constComputer.Invoke(type.StackArraySize!, RuntimeType.INT, out stackArraySize))
            { throw new CompilerException($"Failed to compute value", type.StackArraySize, null); }
        }

        /// <exception cref="ArgumentNullException"/>
        public CompiledType(CompiledType other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));

            this.builtinType = other.builtinType;
            this.@class = other.@class;
            this.@enum = other.@enum;
            this.function = other.function;
            this.genericName = other.genericName;
            this.@struct = other.@struct;
            this.typeParameters = new List<CompiledType>(other.typeParameters).ToArray();
            this.stackArrayOf = (other.stackArrayOf is null) ? null : new CompiledType(other.stackArrayOf);
            this.stackArraySize = other.stackArraySize;
            this.stackArraySizeStatement = other.stackArraySizeStatement;
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
            this.stackArrayOf = (other.stackArrayOf is null) ? null : new CompiledType(other.stackArrayOf);
            this.stackArraySize = other.stackArraySize;
            this.stackArraySizeStatement = other.stackArraySizeStatement;
        }

        public override string ToString()
        {
            if (IsStackArray)
            { return $"{Name}[{stackArraySize}]"; }

            string result = Name;

            if (TypeParameters.Length > 0)
            { result += $"<{string.Join<CompiledType>(", ", TypeParameters)}>"; }
            else if (@class != null && @class.TemplateInfo is not null)
            { result += $"<{string.Join<Token>(", ", @class.TemplateInfo.TypeParameters)}>"; }

            return result;
        }
        string GetDebuggerDisplay() => ToString();

        public static bool operator ==(CompiledType? a, CompiledType? b)
        {
            if (a is null && b is null) return true;
            if (a is null) return false;
            if (b is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType? a, CompiledType? b) => !(a == b);

        public static bool operator ==(CompiledType? a, RuntimeType b)
        {
            if (a is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType? a, RuntimeType b) => !(a == b);

        public static bool operator ==(CompiledType? a, Type b)
        {
            if (a is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType? a, Type b) => !(a == b);

        public static bool operator ==(CompiledType? a, TypeInstance? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType? a, TypeInstance? b) => !(a == b);

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (obj is not CompiledType other) return false;
            return this.Equals(other);
        }

        public bool Equals(CompiledType? other)
        {
            if (other is null) return false;

            if (this.genericName != other.genericName) return false;

            if (this.IsBuiltin != other.IsBuiltin) return false;
            if (this.IsClass != other.IsClass) return false;
            if (this.IsStruct != other.IsStruct) return false;
            if (this.IsFunction != other.IsFunction) return false;
            if (this.IsStackArray != other.IsStackArray) return false;

            if (!CompiledType.Equals(this.typeParameters, other.typeParameters)) return false;

            if (this.IsClass && other.IsClass) return this.@class.Name.Content == other.@class.Name.Content;
            if (this.IsStruct && other.IsStruct) return this.@struct.Name.Content == other.@struct.Name.Content;
            if (this.IsEnum && other.IsEnum) return this.@enum.Identifier.Content == other.@enum.Identifier.Content;
            if (this.IsFunction && other.IsFunction) return this.@function == other.@function;
            if (this.IsStackArray && other.IsStackArray) return this.stackArrayOf == other.stackArrayOf;

            if (this.IsBuiltin && other.IsBuiltin) return this.builtinType == other.builtinType;

            return true;
        }

        public bool Equals(TypeInstance? other)
        {
            if (other is null) return false;

            if (IsFunction)
            {
                if (other is not TypeInstanceFunction otherFunction) return false;
                if (!Function.ReturnType.Equals(otherFunction.FunctionReturnType)) return false;
                if (!CompiledType.Equals(Function.Parameters, otherFunction.FunctionParameterTypes)) return false;
                return true;
            }

            if (IsStackArray)
            {
                if (other is not TypeInstanceStackArray otherStackArray) return false;
                if (!stackArrayOf.Equals(otherStackArray.StackArrayOf)) return false;
                return true;
            }

            if (other is not TypeInstanceSimple otherSimple)
            { return false; }

            if (IsGeneric)
            {
                if (!CompiledType.Equals((CompiledType?[]?)this.typeParameters, (TypeInstance?[]?)otherSimple.GenericTypes)) return false;
            }

            if (Constants.BuiltinTypeMap3.TryGetValue(otherSimple.Identifier.Content, out var type))
            { return type == this.builtinType; }

            if (this.@struct != null && this.@struct.Name.Content == otherSimple.Identifier.Content)
            { return true; }

            if (this.@class != null && this.@class.Name.Content == otherSimple.Identifier.Content)
            { return true; }

            if (this.@enum != null && this.@enum.Identifier.Content == otherSimple.Identifier.Content)
            { return true; }

            return false;
        }
        public static bool Equals(CompiledType?[]? typesA, TypeInstance?[]? typesB)
        {
            if (typesA is null && typesB is null) return true;
            if (typesA is null || typesB is null) return false;

            if (typesA.Length != typesB.Length) return false;
            for (int i = 0; i < typesA.Length; i++)
            {
                CompiledType? typeA = typesA[i];
                TypeInstance? typeB = typesB[i];
                if (!(typeA?.Equals(typeB) ?? typeB is null)) return false;
            }
            return true;
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

        public static bool TryGetTypeParameters(CompiledType[]? definedParameters, CompiledType[]? passedParameters, [NotNullWhen(true)] out Dictionary<string, CompiledType>? typeParameters)
        {
            typeParameters = null;
            if (definedParameters is null || passedParameters is null) return false;
            if (definedParameters.Length != passedParameters.Length) return false;

            typeParameters = new Dictionary<string, CompiledType>();

            for (int i = 0; i < definedParameters.Length; i++)
            {
                CompiledType passed = passedParameters[i];
                CompiledType defined = definedParameters[i];

                if (passed.IsGeneric) throw new NotImplementedException($"This should be non-generic");

                if (defined.IsGeneric)
                {
                    if (typeParameters.TryGetValue(defined.Name, out CompiledType? addedTypeParameter))
                    {
                        if (addedTypeParameter != passed) return false;
                    }
                    else
                    {
                        typeParameters.Add(defined.Name, passed);
                    }

                    continue;
                }

                if (defined.IsClass && passed.IsClass)
                {
                    if (defined.Class.Name.Content != passed.Class.Name.Content) return false;
                    if (defined.Class.TemplateInfo is not null && passed.Class.TemplateInfo is not null)
                    {
                        if (defined.Class.TemplateInfo.TypeParameters.Length != passed.TypeParameters.Length)
                        { throw new NotImplementedException(); }
                        for (int j = 0; j < defined.Class.TemplateInfo.TypeParameters.Length; j++)
                        {
                            string typeParamName = defined.Class.TemplateInfo.TypeParameters[i].Content;
                            CompiledType typeParamValue = passed.TypeParameters[i];

                            if (typeParameters.TryGetValue(typeParamName, out CompiledType? addedTypeParameter))
                            { if (addedTypeParameter != typeParamValue) return false; }
                            else
                            { typeParameters.Add(typeParamName, typeParamValue); }
                        }
                        continue;
                    }
                }

                if (defined != passed) return false;
            }

            return true;
        }

        public static bool Equals(CompiledType[]? a, params CompiledType[]? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            { if (!a[i].Equals(b[i])) return false; }
            return true;
        }

        public override int GetHashCode() => HashCode.Combine(builtinType, @struct, @class);

        public static CompiledType CreateGeneric(string content)
            => new()
            { genericName = content, };

        public static CompiledType[] FromArray(IEnumerable<TypeInstance> types, Func<string, CompiledType> typeFinder)
            => CompiledType.FromArray(types.ToArray(), typeFinder);
        public static CompiledType[] FromArray(TypeInstance[]? types, Func<string, CompiledType>? typeFinder)
        {
            if (types is null || types.Length == 0) return Array.Empty<CompiledType>();
            CompiledType[] result = new CompiledType[types.Length];
            for (int i = 0; i < types.Length; i++)
            { result[i] = new CompiledType(types[i], typeFinder); }
            return result;
        }

        public bool IsReplacedType(string v)
        {
            if (@class != null)
            { return @class.CompiledAttributes.HasAttribute("Define", v); }

            if (@struct != null)
            { return @struct.CompiledAttributes.HasAttribute("Define", v); }

            if (@enum != null)
            { return @enum.CompiledAttributes.HasAttribute("Define", v); }

            return false;
        }

        public bool TryGetFieldOffsets([NotNullWhen(true)] out IReadOnlyDictionary<string, int>? fieldOffsets)
        {
            if (@class is not null)
            {
                fieldOffsets = @class.FieldOffsets;
                return true;
            }

            if (@struct is not null)
            {
                fieldOffsets = @struct.FieldOffsets;
                return true;
            }

            fieldOffsets = default;
            return false;
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
        public T? Context { get; set; }
    }
}
