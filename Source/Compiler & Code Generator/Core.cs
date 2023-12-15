using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace LanguageCore.BBCode.Generator
{
    using Runtime;

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct ValueAddress
    {
        public readonly int Address;
        public readonly bool BasepointerRelative;
        public readonly bool IsReference;
        public readonly bool InHeap;
        public AddressingMode AddressingMode => BasepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE;

        public ValueAddress(int address, bool basepointerRelative, bool isReference, bool inHeap)
        {
            Address = address;
            BasepointerRelative = basepointerRelative;
            IsReference = isReference;
            InHeap = inHeap;
        }

        public ValueAddress(CompiledVariable variable)
        {
            Address = variable.MemoryAddress;
            BasepointerRelative = true;
            IsReference = false;
            InHeap = false;
        }

        public ValueAddress(CompiledParameter parameter, int address)
        {
            Address = address;
            BasepointerRelative = true;
            IsReference = parameter.IsRef;
            InHeap = false;
        }

        public static ValueAddress operator +(ValueAddress address, int offset) => new(address.Address + offset, address.BasepointerRelative, address.IsReference, address.InHeap);

        public override string ToString()
        {
            StringBuilder result = new();
            result.Append('(');
            result.Append($"{Address}");
            if (BasepointerRelative)
            { result.Append(" (BPR)"); }
            else
            { result.Append(" (ABS)"); }
            if (IsReference)
            { result.Append(" | IsRef"); }
            if (InHeap)
            { result.Append(" | InHeap"); }
            result.Append(')');
            return result.ToString();
        }
        string GetDebuggerDisplay() => ToString();
    }
}

namespace LanguageCore.Compiler
{
    using Parser;
    using Parser.Statement;
    using Runtime;
    using Tokenizing;

    public static class Extensions
    {
        public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> v, IEnumerable<KeyValuePair<TKey, TValue>> elements) where TKey : notnull
        {
            foreach (KeyValuePair<TKey, TValue> pair in elements)
            { v.Add(pair.Key, pair.Value); }
        }

        #region KeyValuePair<TKey, TValue>

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

        #endregion
    }

    readonly struct UndefinedOffset<T>
    {
        public readonly int CallInstructionIndex;

        public readonly Statement? Caller;
        public readonly T Function;

        public readonly string? CurrentFile;

        public UndefinedOffset(int callInstructionIndex, Statement? statement, T called, string? file)
        {
            this.CallInstructionIndex = callInstructionIndex;
            this.Caller = statement;
            this.CurrentFile = file;

            this.Function = called;
        }
    }

    public static class Utils
    {
        /// <exception cref="NotImplementedException"/>
        public static CompiledLiteralType ConvertType(System.Type type)
        {
            if (type == typeof(int))
            { return CompiledLiteralType.Integer; }

            if (type == typeof(float))
            { return CompiledLiteralType.Float; }

            if (type == typeof(bool))
            { return CompiledLiteralType.Boolean; }

            if (type == typeof(string))
            { return CompiledLiteralType.String; }

            throw new NotImplementedException($"Unknown attribute type requested: \"{type.FullName}\"");
        }

        /// <exception cref="NotImplementedException"/>
        public static bool TryConvertType(System.Type type, out CompiledLiteralType result)
        {
            if (type == typeof(int))
            {
                result = CompiledLiteralType.Integer;
                return true;
            }

            if (type == typeof(float))
            {
                result = CompiledLiteralType.Float;
                return true;
            }

            if (type == typeof(bool))
            {
                result = CompiledLiteralType.Boolean;
                return true;
            }

            if (type == typeof(string))
            {
                result = CompiledLiteralType.String;
                return true;
            }

            result = default;
            return false;
        }

        public static void SetTypeParameters(CompiledType[] typeParameters, TypeArguments typeValues)
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

        public static Dictionary<TKey, TValue> ConcatDictionary<TKey, TValue>(params IReadOnlyDictionary<TKey, TValue>?[] v) where TKey : notnull
        {
            Dictionary<TKey, TValue> result = new();
            for (int i = 0; i < v.Length; i++)
            {
                IReadOnlyDictionary<TKey, TValue>? dict = v[i];
                if (dict == null) continue;
                foreach (KeyValuePair<TKey, TValue> pair in dict)
                {
                    if (result.ContainsKey(pair.Key))
                    { result[pair.Key] = pair.Value; }
                    else
                    { result.Add(pair.Key, pair.Value); }
                }
            }
            return result;
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
            Source = source.Position.Range;
            SourceFile = sourceFile;
        }
    }

    public interface IReferenceable<T>
    {
        public void AddReference(T reference, string? file);
        public void ClearReferences();
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
        NotBuiltin,

        Void,

        Byte,
        Integer,
        Float,
        Char,

        /// <summary>
        /// Only used when get a value by it's memory address
        /// </summary>
        Unknown,
    }

    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    public class FunctionType : IEquatable<FunctionType>, IEquatable<CompiledFunction>
    {
        public readonly CompiledType ReturnType;
        public readonly CompiledType[] Parameters;

        public bool ReturnSomething => ReturnType != Type.Void;

        public FunctionType(CompiledFunction function)
        {
            ReturnType = function.Type;
            Parameters = new CompiledType[function.ParameterTypes.Length];
            Array.Copy(function.ParameterTypes, Parameters, function.Parameters.Count);
        }

        public FunctionType(CompiledType returnType, CompiledType[] parameters)
        {
            ReturnType = returnType;
            Parameters = parameters;
        }

        public override string ToString()
        {
            StringBuilder result = new();
            result.Append(ReturnType.ToString());
            result.Append('(');
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(Parameters[i].ToString());
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

        public Type BuiltinType => builtinType;
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        public RuntimeType RuntimeType => builtinType switch
        {
            Type.Byte => RuntimeType.UInt8,
            Type.Integer => RuntimeType.SInt32,
            Type.Float => RuntimeType.Single,
            Type.Char => RuntimeType.UInt16,

            Type.NotBuiltin => throw new InternalException($"{this} is not a built-in type"),

            _ => throw new NotImplementedException($"Type conversion for {builtinType} is not implemented"),
        };

        public CompiledStruct Struct => @struct ?? throw new InternalException($"This isn't a struct");
        public CompiledClass Class => @class ?? throw new InternalException($"This isn't a class");
        public CompiledEnum Enum => @enum ?? throw new InternalException($"This isn't an enum");
        public FunctionType Function => function ?? throw new InternalException($"This isn't a function");
        public CompiledType[] TypeParameters => typeParameters;

        string? genericName;
        CompiledType? stackArrayOf;
        StatementWithValue? stackArraySizeStatement;
        DataItem stackArraySize;

        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        public string Name
        {
            get
            {
                if (IsGeneric) return genericName;

                if (stackArrayOf is not null) return stackArrayOf.Name;

                if (builtinType != Type.NotBuiltin) return builtinType switch
                {
                    Type.Void => "void",
                    Type.Byte => "byte",
                    Type.Integer => "int",
                    Type.Float => "float",
                    Type.Char => "char",

                    Type.Unknown => "unknown",
                    Type.NotBuiltin => throw new ImpossibleException(),

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
        public bool IsClass => @class is not null;
        /// <summary><c><see cref="Enum"/> != <see langword="null"/></c></summary>
        [MemberNotNullWhen(true, nameof(@enum))]
        public bool IsEnum => @enum is not null;
        /// <summary><c><see cref="Struct"/> != <see langword="null"/></c></summary>
        [MemberNotNullWhen(true, nameof(@struct))]
        public bool IsStruct => @struct is not null;
        /// <summary><c><see cref="Function"/> != <see langword="null"/></c></summary>
        [MemberNotNullWhen(true, nameof(function))]
        public bool IsFunction => function is not null;
        public bool IsBuiltin => builtinType != Type.NotBuiltin;
        public bool CanBeBuiltin
        {
            get
            {
                if (builtinType != Type.NotBuiltin) return true;
                if (IsEnum) return true;

                return false;
            }
        }
        public bool InHEAP => IsClass;
        [MemberNotNullWhen(true, nameof(genericName))]
        public bool IsGeneric => !string.IsNullOrEmpty(genericName);
        [MemberNotNullWhen(true, nameof(stackArrayOf))]
        public bool IsStackArray => stackArrayOf is not null;

        public int Size
        {
            get
            {
                if (IsGeneric) throw new InternalException($"Can not get the size of a generic type");
                if (IsStruct) return @struct.Size;
                if (IsClass)
                {
                    TypeArguments saved = new(@class.CurrentTypeArguments);

                    @class.SetTypeArguments(typeParameters);

                    int size = @class.Size;

                    @class.SetTypeArguments(saved);

                    return size;
                }
                if (IsEnum) return 1;
                if (IsFunction) return 1;
                if (IsStackArray) return (stackArraySize * new DataItem(stackArrayOf.SizeOnStack)).Integer ?? throw new InternalException(); ;
                return 1;
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
                if (IsStackArray) return (stackArraySize * new DataItem(stackArrayOf.SizeOnStack)).ValueSInt32;
                return 1;
            }
        }

        public int StackArraySize
        {
            get
            {
                if (!IsStackArray) throw new InternalException($"Can not get the stack array size of a non stack array type");
                return stackArraySize.ValueSInt32;
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

        public System.Type SystemType
        {
            get
            {
                if (IsBuiltin)
                {
                    return builtinType switch
                    {
                        Type.Void => typeof(void),
                        Type.Byte => typeof(byte),
                        Type.Integer => typeof(int),
                        Type.Float => typeof(float),
                        Type.Char => typeof(char),
                        _ => throw new ImpossibleException(),
                    };
                }

                throw new NotImplementedException();
            }
        }

        CompiledType()
        {
            this.builtinType = Type.NotBuiltin;
            this.@struct = null;
            this.@class = null;
            this.@enum = null;
            this.function = null;
            this.genericName = null;
            this.typeParameters = Array.Empty<CompiledType>();
            this.stackArrayOf = null;
        }

        /// <exception cref="ArgumentNullException"/>
        public CompiledType(CompiledType? other, TypeArguments? typeArguments) : this()
        {
            if (other is null) throw new ArgumentNullException(nameof(other));

            this.Set(other);

            if (IsBuiltin)
            {
                return;
            }

            if (IsEnum)
            {
                return;
            }

            if (IsFunction)
            {
                function = new FunctionType(
                    new CompiledType(other.Function.ReturnType, typeArguments),
                    CompiledType.FromArray(other.Function.Parameters, typeArguments)
                    );
                return;
            }

            if (IsClass)
            {
                if (typeArguments != null && @class.TemplateInfo is not null)
                {
                    string[] keys = @class.TemplateInfo.TypeParameterNames;
                    CompiledType[] typeArgumentValues = new CompiledType[keys.Length];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        string key = keys[i];
                        if (!typeArguments.TryGetValue(key, out CompiledType? typeArgumentValue))
                        { return; }
                        typeArgumentValues[i] = typeArgumentValue;
                    }
                    typeParameters = typeArgumentValues;
                }
                return;
            }

            if (IsStruct)
            {
                return;
            }

            if (IsStackArray)
            {
                stackArrayOf = new CompiledType(other.StackArrayOf, typeArguments);
                return;
            }

            if (IsGeneric)
            {
                if (typeArguments != null && typeArguments.TryGetValue(genericName, out other))
                { this.Set(other); }
                return;
            }

            throw new NotImplementedException();
        }

        /// <exception cref="ArgumentNullException"/>
        public CompiledType(CompiledStruct @struct) : this()
        {
            this.@struct = @struct ?? throw new ArgumentNullException(nameof(@struct));
        }

        /// <exception cref="ArgumentNullException"/>
        public CompiledType(FunctionType function) : this()
        {
            this.function = function ?? throw new ArgumentNullException(nameof(function));
        }

        /// <exception cref="ArgumentNullException"/>
        public CompiledType(CompiledClass @class) : this()
        {
            this.@class = @class ?? throw new ArgumentNullException(nameof(@class));
        }

        /// <exception cref="ArgumentNullException"/>
        public CompiledType(CompiledClass @class, params CompiledType[][] typeParameters) : this()
        {
            this.@class = @class ?? throw new ArgumentNullException(nameof(@class));
            List<CompiledType> typeParameters1 = new();
            for (int i = 0; i < typeParameters.Length; i++)
            { typeParameters1.AddRange(typeParameters[i]); }
            this.typeParameters = typeParameters1.ToArray();
        }

        /// <exception cref="ArgumentNullException"/>
        public CompiledType(CompiledEnum @enum) : this()
        {
            this.@enum = @enum ?? throw new ArgumentNullException(nameof(@enum));
        }

        /// <exception cref="ArgumentNullException"/>
        public CompiledType(CompiledFunction @function) : this()
        {
            this.function = new FunctionType(@function);
        }

        public CompiledType(Type type) : this()
        {
            this.builtinType = type;
        }

        /// <exception cref="InternalException"/>
        public CompiledType(RuntimeType type) : this()
        {
            this.builtinType = type switch
            {
                RuntimeType.UInt8 => Type.Byte,
                RuntimeType.SInt32 => Type.Integer,
                RuntimeType.Single => Type.Float,
                RuntimeType.UInt16 => Type.Char,
                _ => throw new ImpossibleException(),
            };
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public CompiledType(string type, Func<string, CompiledType>? typeFinder) : this()
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(type, out this.builtinType))
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

            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out this.builtinType))
            { return; }

            if (type.Identifier.Content == "func")
            {
                type.Identifier.AnalyzedType = TokenAnalyzedType.Keyword;

                CompiledType funcRet;
                CompiledType[] funcParams;

                if (type.GenericTypes == null || type.GenericTypes.Length == 0)
                {
                    funcRet = new(Type.Void);
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

            if (!constComputer.Invoke(type.StackArraySize!, RuntimeType.SInt32, out stackArraySize))
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
            {
                if (TypeParameters.Length > 0) throw new InternalException();
                return $"{StackArrayOf}[{stackArraySize}]";
            }

            if (IsEnum)
            {
                if (TypeParameters.Length > 0) throw new InternalException();
                return Enum.Identifier.Content;
            }

            if (IsFunction)
            {
                if (TypeParameters.Length > 0) throw new InternalException();
                return Function.ToString();
            }

            if (IsBuiltin)
            {
                if (TypeParameters.Length > 0) throw new InternalException();
                return BuiltinType switch
                {
                    Type.Void => "void",
                    Type.Byte => "byte",
                    Type.Integer => "int",
                    Type.Float => "float",
                    Type.Char => "char",
                    Type.Unknown => "?",
                    Type.NotBuiltin => throw new ImpossibleException(),
                    _ => throw new ImpossibleException(),
                };
            }

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
            if (a is null || b is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType? a, CompiledType? b) => !(a == b);

        public static bool operator ==(CompiledType? a, RuntimeType b) => a is not null && a.Equals(b);
        public static bool operator !=(CompiledType? a, RuntimeType b) => !(a == b);

        public static bool operator ==(CompiledType? a, Type b) => a is not null && a.Equals(b);
        public static bool operator !=(CompiledType? a, Type b) => !(a == b);

        public static bool operator ==(CompiledType? a, TypeInstance? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            return a.Equals(b);
        }
        public static bool operator !=(CompiledType? a, TypeInstance? b) => !(a == b);

        public override bool Equals(object? obj) =>
            obj is not null &&
            obj is CompiledType other &&
            this.Equals(other);

        public bool Equals(CompiledType? other)
        {
            if (other is null) return false;

            if (this.IsBuiltin != other.IsBuiltin) return false;
            if (this.IsClass != other.IsClass) return false;
            if (this.IsStruct != other.IsStruct) return false;
            if (this.IsFunction != other.IsFunction) return false;
            if (this.IsStackArray != other.IsStackArray) return false;
            if (this.IsGeneric != other.IsGeneric) return false;

            if (!CompiledType.Equals(this.typeParameters, other.typeParameters)) return false;

            if (this.IsClass && other.IsClass) return this.@class.Name.Content == other.@class.Name.Content;
            if (this.IsStruct && other.IsStruct) return this.@struct.Name.Content == other.@struct.Name.Content;
            if (this.IsEnum && other.IsEnum) return this.@enum.Identifier.Content == other.@enum.Identifier.Content;
            if (this.IsFunction && other.IsFunction) return this.@function == other.@function;
            if (this.IsStackArray && other.IsStackArray) return this.stackArrayOf == other.stackArrayOf;
            if (this.IsGeneric && other.IsGeneric) return this.genericName == other.genericName;

            if (this.IsBuiltin && other.IsBuiltin) return this.builtinType == other.builtinType;

            throw new NotImplementedException();
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

            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(otherSimple.Identifier.Content, out Type type))
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

        public bool Equals(Type other) => IsBuiltin && BuiltinType == other;

        public bool Equals(RuntimeType other)
        {
            if (this.IsEnum)
            {
                for (int i = 0; i < this.@enum.Members.Length; i++)
                {
                    if (this.@enum.Members[i].ComputedValue.Type == other)
                    { return true; }
                }
            }

            if (!this.IsBuiltin) return false;
            return this.RuntimeType == other;
        }

        public static bool TryGetTypeParameters(CompiledType[]? definedParameters, CompiledType[]? passedParameters, [NotNullWhen(true)] out TypeArguments? typeParameters)
        {
            typeParameters = null;
            if (definedParameters is null || passedParameters is null) return false;
            if (definedParameters.Length != passedParameters.Length) return false;

            typeParameters = new TypeArguments();

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

        public override int GetHashCode()
        {
            if (IsBuiltin) return HashCode.Combine((byte)0, builtinType);
            if (IsEnum) return HashCode.Combine((byte)1, Enum);
            if (IsClass) return HashCode.Combine((byte)2, Class);
            if (IsStruct) return HashCode.Combine((byte)3, Struct);
            if (IsStackArray) return HashCode.Combine((byte)4, StackArrayOf, StackArraySize);
            if (IsFunction) return HashCode.Combine((byte)5, function);
            throw new NotImplementedException();
        }

        public static CompiledType CreateGeneric(string content) => new()
        {
            genericName = content,
        };

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
        public static CompiledType[] FromArray(CompiledType[]? types, TypeArguments? typeArguments)
        {
            if (types is null || types.Length == 0) return Array.Empty<CompiledType>();
            CompiledType[] result = new CompiledType[types.Length];
            for (int i = 0; i < types.Length; i++)
            { result[i] = new CompiledType(types[i], typeArguments); }
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
                TypeArguments saved = new(@class.CurrentTypeArguments);

                @class.SetTypeArguments(typeParameters);

                fieldOffsets = @class.FieldOffsets;

                @class.SetTypeArguments(saved);

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

        public bool AllGenericsDefined()
        {
            if (IsGeneric) return false;

            if (this.IsBuiltin) return true;

            if (this.IsEnum) return true;

            if (this.IsStackArray)
            { return this.StackArrayOf.AllGenericsDefined(); }

            if (this.IsFunction)
            {
                for (int i = 0; i < this.Function.Parameters.Length; i++)
                {
                    if (!this.function.Parameters[i].AllGenericsDefined())
                    { return false; }
                }
                return this.Function.ReturnType.AllGenericsDefined();
            }

            if (this.IsStruct)
            { return true; }

            if (this.IsClass)
            {
                if (this.Class.TemplateInfo == null)
                { return true; }
                return this.TypeParameters.Length > 0;
            }

            throw new NotImplementedException();
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

    public interface ICanBeSame
    {
        public bool IsSame(ICanBeSame? other);
    }
}
