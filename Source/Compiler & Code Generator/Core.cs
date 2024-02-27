using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace LanguageCore.BBCode.Generator
{
    using Compiler;
    using Runtime;

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct ValueAddress
    {
        public readonly int Address;
        public readonly AddressingMode AddressingMode;
        public readonly bool IsReference;
        public readonly bool InHeap;

        public ValueAddress(int address, AddressingMode addressingMode, bool isReference = false, bool inHeap = false)
        {
            Address = address;
            AddressingMode = addressingMode;
            IsReference = isReference;
            InHeap = inHeap;
        }

        public ValueAddress(CompiledVariable variable)
        {
            Address = variable.MemoryAddress;
            AddressingMode = AddressingMode.BasePointerRelative;
            IsReference = false;
            InHeap = false;
        }

        public ValueAddress(CompiledParameter parameter, int address)
        {
            Address = address;
            AddressingMode = AddressingMode.BasePointerRelative;
            IsReference = parameter.IsRef;
            InHeap = false;
        }

        public static ValueAddress operator +(ValueAddress address, int offset) => new(address.Address + offset, address.AddressingMode, address.IsReference, address.InHeap);

        public override string ToString()
        {
            StringBuilder result = new();
            result.Append('(');
            result.Append(Address);

            switch (AddressingMode)
            {
                case AddressingMode.Absolute:
                    result.Append(" (ABS)");
                    break;
                case AddressingMode.Runtime:
                    result.Append(" (RNT)");
                    break;
                case AddressingMode.BasePointerRelative:
                    result.Append(" (BPR)");
                    break;
                case AddressingMode.StackRelative:
                    result.Append(" (SR)");
                    break;
                default:
                    break;
            }

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
    using System.Collections.Immutable;
    using Parser;
    using Parser.Statement;
    using Runtime;
    using Tokenizing;

    readonly struct UndefinedOffset<T>
    {
        public readonly int InstructionIndex;
        public readonly bool IsAbsoluteAddress;

        public readonly Statement? Caller;
        public readonly T Called;

        public readonly Uri? CurrentFile;

        public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, Statement? caller, T called, Uri? file)
        {
            InstructionIndex = callInstructionIndex;
            IsAbsoluteAddress = isAbsoluteAddress;

            Caller = caller;
            Called = called;

            CurrentFile = file;
        }
    }

    public readonly struct Reference<T>
    {
        public readonly T Source;
        public readonly Uri? SourceFile;

        public Reference(T source, Uri? sourceFile)
        {
            Source = source;
            SourceFile = sourceFile;
        }
    }

    public interface IReferenceable<T>
    {
        public void AddReference(T referencedBy, Uri? file);
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
        public readonly ImmutableArray<CompiledType> Parameters;

        public bool ReturnSomething => ReturnType != Type.Void;

        public FunctionType(CompiledFunction function)
        {
            ReturnType = function.Type;
            Parameters = function.ParameterTypes;
        }

        public FunctionType(CompiledType returnType, IEnumerable<CompiledType> parameters)
        {
            ReturnType = returnType;
            Parameters = parameters.ToImmutableArray();
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

            if (!CompiledType.AreEquals(Parameters, other.Parameters)) return false;

            return true;
        }

        public bool Equals(CompiledFunction? other)
        {
            if (other is null) return false;

            if (!other.Type.Equals(ReturnType)) return false;

            if (!CompiledType.AreEquals(Parameters, other.ParameterTypes)) return false;

            return true;
        }

        public override int GetHashCode() => HashCode.Combine(ReturnType, Parameters);

        public static bool operator !=(FunctionType? a, FunctionType? b) => !(a == b);
        public static bool operator ==(FunctionType? a, FunctionType? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            return a.Equals(b);
        }
    }

    public delegate bool ComputeValue(StatementWithValue value, out DataItem computedValue);
    public delegate bool FindType(Token token, [NotNullWhen(true)] out CompiledType? computedValue);

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class CompiledType :
        IEquatable<CompiledType>,
        IEquatable<TypeInstance>,
        IEquatable<Type>,
        IEquatable<RuntimeType>,
        System.Numerics.IEqualityOperators<CompiledType?, CompiledType?, bool>,
        System.Numerics.IEqualityOperators<CompiledType?, Type, bool>,
        System.Numerics.IEqualityOperators<CompiledType?, RuntimeType, bool>
    {
        readonly Type builtinType;
        readonly CompiledStruct? @struct;
        readonly CompiledEnum? @enum;
        readonly FunctionType? function;
        readonly CompiledType? pointerTo;
        readonly ImmutableArray<CompiledType> typeParameters;

        readonly TypeInstance? typeInstance;

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

        public CompiledStruct? Struct => @struct;
        public CompiledEnum? Enum => @enum;
        public FunctionType? Function => function;
        public CompiledType? PointerTo => pointerTo;
        public ImmutableArray<CompiledType> TypeParameters => typeParameters;

        readonly string? genericName;
        readonly CompiledType? stackArrayOf;
        readonly StatementWithValue? stackArraySizeStatement;
        readonly DataItem stackArraySize;

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
                    Type.NotBuiltin => throw new UnreachableException(),

                    _ => throw new NotImplementedException($"Type conversion for {builtinType} is not implemented"),
                };

                if (@struct is not null) return @struct.Identifier.Content;
                if (@enum is not null) return @enum.Identifier.Content;
                if (function is not null) return function.ToString();
                if (pointerTo is not null) return $"{pointerTo.Name}*";

                throw new UnreachableException();
            }
        }

        [MemberNotNullWhen(true, nameof(@enum))]
        [MemberNotNullWhen(true, nameof(Enum))]
        public bool IsEnum => @enum is not null;

        [MemberNotNullWhen(true, nameof(@struct))]
        [MemberNotNullWhen(true, nameof(Struct))]
        public bool IsStruct => @struct is not null;

        [MemberNotNullWhen(true, nameof(function))]
        [MemberNotNullWhen(true, nameof(Function))]
        public bool IsFunction => function is not null;

        [MemberNotNullWhen(true, nameof(pointerTo))]
        [MemberNotNullWhen(true, nameof(PointerTo))]
        public bool IsPointer => pointerTo is not null;

        public bool IsBuiltin => builtinType != Type.NotBuiltin;

        public bool CanBeBuiltin
        {
            get
            {
                if (IsBuiltin) return true;
                if (IsEnum) return true;
                if (IsPointer) return true;

                return false;
            }
        }

        [MemberNotNullWhen(true, nameof(genericName))]
        public bool IsGeneric => !string.IsNullOrEmpty(genericName);
        [MemberNotNullWhen(true, nameof(stackArrayOf))]
        public bool IsStackArray => stackArrayOf is not null;
        public TypeInstance? Origin => typeInstance;

        public int Size
        {
            get
            {
                if (IsGeneric) throw new InternalException($"Can not get the size of a generic type");
                if (IsStruct)
                {
                    TypeArguments saved = new(@struct.CurrentTypeArguments);

                    @struct.SetTypeArguments(typeParameters);

                    int size = @struct.SizeOnStack;

                    @struct.SetTypeArguments(saved);

                    return size;
                }
                if (IsStackArray) return (stackArraySize * new DataItem(stackArrayOf.Size)).Integer ?? throw new InternalException(); ;
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
                        _ => throw new UnreachableException(),
                    };
                }

                throw new NotImplementedException();
            }
        }

        CompiledType(
            Type builtinType = Type.NotBuiltin,
            CompiledStruct? @struct = null,
            CompiledEnum? @enum = null,
            FunctionType? function = null,
            CompiledType? pointerTo = null,
            CompiledType[]? typeParameters = null,
            TypeInstance? typeInstance = null,
            string? genericName = null,
            CompiledType? stackArrayOf = null,
            StatementWithValue? stackArraySizeStatement = null,
            DataItem stackArraySize = default)
        {
            this.builtinType = builtinType;
            this.@struct = @struct;
            this.@enum = @enum;
            this.function = function;
            this.pointerTo = pointerTo;
            this.typeParameters = typeParameters?.ToImmutableArray() ?? ImmutableArray.Create<CompiledType>();
            this.typeInstance = typeInstance;
            this.genericName = genericName;
            this.stackArrayOf = stackArrayOf;
            this.stackArraySizeStatement = stackArraySizeStatement;
            this.stackArraySize = stackArraySize;
        }

        public static CompiledType From(CompiledType other, TypeArguments? typeArguments)
        {
            if (other.IsBuiltin) return other;
            if (other.IsEnum) return other;

            if (other.IsFunction)
            {
                return new CompiledType(new FunctionType(
                        CompiledType.From(other.Function!.ReturnType, typeArguments),
                        CompiledType.FromArray(other.Function.Parameters, typeArguments)
                    ));
            }

            if (other.IsStruct)
            {
                if (typeArguments != null && other.@struct.TemplateInfo is not null)
                {
                    string[] keys = other.@struct.TemplateInfo.TypeParameterNames;
                    CompiledType[] typeArgumentValues = new CompiledType[keys.Length];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        string key = keys[i];
                        if (!typeArguments.TryGetValue(key, out CompiledType? typeArgumentValue))
                        { return other; }
                        typeArgumentValues[i] = typeArgumentValue;
                    }
                    return new CompiledType(other.@struct, typeArgumentValues);
                }
                return other;
            }

            if (other.IsStackArray)
            {
                return CompiledType.CreateArrayOf(
                    CompiledType.From(other.stackArrayOf, typeArguments),
                    other.stackArraySize,
                    other.stackArraySizeStatement);
            }

            if (other.IsGeneric)
            {
                if (typeArguments != null && typeArguments.TryGetValue(other.genericName, out CompiledType? _other))
                { return _other; }
                return other;
            }

            throw new NotImplementedException();
        }

        public CompiledType(CompiledStruct @struct) : this()
        {
            this.@struct = @struct;
            if (@struct.TemplateInfo is not null)
            { typeParameters = @struct.TemplateInfo.TypeParameterNames.Select(CompiledType.CreateGeneric).ToImmutableArray(); }
        }

        public CompiledType(CompiledStruct @struct, IEnumerable<CompiledType> typeParameters) : this()
        {
            this.@struct = @struct;
            this.typeParameters = typeParameters.ToImmutableArray();
        }

        public CompiledType(CompiledType other, IEnumerable<CompiledType> typeParameters) : this(other)
        {
            this.typeParameters = typeParameters.ToImmutableArray();
        }

        public CompiledType(FunctionType function) : this()
        {
            this.function = function;
        }

        public CompiledType(CompiledEnum @enum) : this()
        {
            this.@enum = @enum;
        }

        public CompiledType(CompiledFunction @function) : this()
        {
            this.function = new FunctionType(@function);
        }

        public CompiledType(Type type) : this()
        {
            this.builtinType = type;
        }

        /// <exception cref="NotImplementedException"/>
        public CompiledType(RuntimeType type) : this()
        {
            this.builtinType = type switch
            {
                RuntimeType.UInt8 => Type.Byte,
                RuntimeType.SInt32 => Type.Integer,
                RuntimeType.Single => Type.Float,
                RuntimeType.UInt16 => Type.Char,
                RuntimeType.Null => throw new NotImplementedException(),
                _ => throw new UnreachableException(),
            };
        }

        /// <exception cref="InternalException"/>
        public static CompiledType From(Token type, FindType? typeFinder)
        {
            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(type.Content, out Type builtinType))
            { return new CompiledType(builtinType); }

            if (typeFinder == null ||
                !typeFinder.Invoke(type, out CompiledType? result))
            { throw new InternalException($"Can't parse \"{type}\" to {nameof(CompiledType)}"); }

            return result;
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public static CompiledType From(TypeInstance type, FindType? typeFinder, ComputeValue? constComputer = null)
        {
            return type switch
            {
                TypeInstanceSimple simpleType => From(simpleType, typeFinder),
                TypeInstanceFunction functionType => From(functionType, typeFinder, constComputer),
                TypeInstanceStackArray stackArrayType => From(stackArrayType, typeFinder, constComputer),
                TypeInstancePointer pointerType => From(pointerType, typeFinder, constComputer),
                _ => throw new UnreachableException()
            };
        }

        /// <exception cref="InternalException"/>
        public static CompiledType From(TypeInstanceSimple type, FindType? typeFinder, ComputeValue? constComputer = null)
        {
            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out Type builtinType))
            { return new CompiledType(builtinType); }

            if (type.Identifier.Content == "func")
            {
                type.Identifier.AnalyzedType = TokenAnalyzedType.Keyword;

                CompiledType funcRet;
                CompiledType[] funcParams;

                if (!type.GenericTypes.HasValue || type.GenericTypes.Value.Length == 0)
                {
                    funcRet = new(Type.Void);
                    funcParams = Array.Empty<CompiledType>();
                }
                else
                {
                    funcRet = CompiledType.From(type.GenericTypes.Value[0], typeFinder, constComputer);
                    funcParams = new CompiledType[type.GenericTypes.Value.Length - 1];
                    for (int i = 1; i < type.GenericTypes.Value.Length; i++)
                    {
                        TypeInstance genericType = type.GenericTypes.Value[i];
                        funcParams[i - 1] = CompiledType.From(genericType, typeFinder, constComputer);
                    }
                }

                return new CompiledType(new FunctionType(funcRet, funcParams));
            }

            if (typeFinder == null ||
                !typeFinder.Invoke(type.Identifier, out CompiledType? result))
            { throw new InternalException($"Can't parse \"{type}\" to {nameof(CompiledType)}"); }

            IEnumerable<CompiledType> typeParameters = CompiledType.FromArray(type.GenericTypes, typeFinder);
            return new CompiledType(result, typeParameters);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public static CompiledType From(TypeInstanceFunction type, FindType? typeFinder, ComputeValue? constComputer = null)
        {
            return new CompiledType(type, typeFinder, constComputer);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        CompiledType(TypeInstanceFunction type, FindType? typeFinder, ComputeValue? constComputer = null) : this()
        {
            CompiledType returnType = CompiledType.From(type.FunctionReturnType, typeFinder, constComputer);
            IEnumerable<CompiledType> parameterTypes = CompiledType.FromArray(type.FunctionParameterTypes, typeFinder);

            typeInstance = type;
            function = new FunctionType(returnType, parameterTypes);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public static CompiledType From(TypeInstanceStackArray type, FindType? typeFinder, ComputeValue? constComputer = null)
        {
            ArgumentNullException.ThrowIfNull(constComputer, nameof(constComputer));

            return new CompiledType(type, typeFinder, constComputer);
        }

        /// <exception cref="InternalException"/>
        CompiledType(TypeInstanceStackArray type, FindType? typeFinder, ComputeValue constComputer) : this()
        {
            typeInstance = type;
            stackArrayOf = CompiledType.From(type.StackArrayOf, typeFinder, constComputer);
            stackArraySizeStatement = type.StackArraySize!;

            if (!constComputer.Invoke(type.StackArraySize!, out stackArraySize))
            { throw new CompilerException($"Failed to compute value"); }
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public static CompiledType From(TypeInstancePointer type, FindType? typeFinder, ComputeValue? constComputer = null)
        {
            return new CompiledType(type, typeFinder, constComputer);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        CompiledType(TypeInstancePointer type, FindType? typeFinder, ComputeValue? constComputer = null) : this()
        {
            typeInstance = type;
            pointerTo = CompiledType.From(type.To, typeFinder, constComputer);
        }

        public CompiledType(CompiledType other)
        {
            this.typeInstance = other.typeInstance;
            this.builtinType = other.builtinType;
            this.@enum = other.@enum;
            this.function = other.function;
            this.genericName = other.genericName;
            this.@struct = other.@struct;
            this.typeParameters = other.typeParameters;
            this.stackArrayOf = (other.stackArrayOf is null) ? null : new CompiledType(other.stackArrayOf);
            this.stackArraySize = other.stackArraySize;
            this.stackArraySizeStatement = other.stackArraySizeStatement;
            this.pointerTo = other.pointerTo;
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
                    Type.NotBuiltin => throw new UnreachableException(),
                    _ => throw new UnreachableException(),
                };
            }

            if (IsPointer)
            { return $"{pointerTo}*"; }

            StringBuilder result = new();
            result.Append(Name);

            if (TypeParameters.Length > 0)
            { result.Append($"<{string.Join<CompiledType>(", ", TypeParameters)}>"); }
            else if (@struct != null && @struct.TemplateInfo is not null)
            { result.Append($"<{string.Join<Token>(", ", @struct.TemplateInfo.TypeParameters)}>"); }

            return result.ToString();
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

        public override bool Equals(object? obj) =>
            obj is not null &&
            obj is CompiledType other &&
            this.Equals(other);

        public bool Equals(CompiledType? other)
        {
            if (other is null) return false;

            if (this.IsBuiltin != other.IsBuiltin) return false;
            if (this.IsStruct != other.IsStruct) return false;
            if (this.IsFunction != other.IsFunction) return false;
            if (this.IsStackArray != other.IsStackArray) return false;
            if (this.IsGeneric != other.IsGeneric) return false;
            if (this.IsPointer != other.IsPointer) return false;

            if (!CompiledType.AreEquals(this.typeParameters, other.typeParameters)) return false;

            if (this.IsStruct && other.IsStruct) return this.@struct.Identifier.Content == other.@struct.Identifier.Content;
            if (this.IsEnum && other.IsEnum) return this.@enum.Identifier.Content == other.@enum.Identifier.Content;
            if (this.IsFunction && other.IsFunction) return this.@function == other.@function;
            if (this.IsStackArray && other.IsStackArray) return this.stackArrayOf == other.stackArrayOf;
            if (this.IsGeneric && other.IsGeneric) return this.genericName == other.genericName;
            if (this.IsPointer && other.IsPointer) return this.pointerTo.Equals(other.pointerTo);

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
                if (!CompiledType.AreEquals(Function.Parameters, otherFunction.FunctionParameterTypes)) return false;
                return true;
            }

            if (IsPointer)
            {
                if (other is not TypeInstancePointer otherPointer) return false;
                if (!pointerTo.Equals(otherPointer.To)) return false;
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
                if (!CompiledType.AreEquals(this.typeParameters, otherSimple.GenericTypes)) return false;
            }

            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(otherSimple.Identifier.Content, out Type type))
            { return type == this.builtinType; }

            if (this.@struct != null && this.@struct.Identifier.Content == otherSimple.Identifier.Content)
            { return true; }

            if (this.@enum != null && this.@enum.Identifier.Content == otherSimple.Identifier.Content)
            { return true; }

            return false;
        }

        public static bool AreEquals(IEnumerable<CompiledType?>? itemsA, IEnumerable<TypeInstance?>? itemsB)
        {
            if (itemsA is null && itemsB is null) return true;
            if (itemsA is null || itemsB is null) return false;

            IEnumerator<CompiledType?> enumA = itemsA.GetEnumerator();
            IEnumerator<TypeInstance?> enumB = itemsB.GetEnumerator();

            while (true)
            {
                CompiledType? a = enumA.Current;
                TypeInstance? b = enumB.Current;

                if (enumA.MoveNext() != enumB.MoveNext()) return false;

                if (a is null && b is null) continue;
                if (a is null || b is null) return false;

                if (!a.Equals(b)) return false;
            }
        }

        public static bool AreEquals(IEnumerable<CompiledType?>? itemsA, IEnumerable<CompiledType?>? itemsB)
            => AreEquals(itemsA?.ToArray(), itemsB?.ToArray());

        public static bool AreEquals(CompiledType?[]? itemsA, CompiledType?[]? itemsB)
        {
            if (itemsA is null && itemsB is null) return true;
            if (itemsA is null || itemsB is null) return false;

            if (itemsA.Length != itemsB.Length) return false;

            for (int i = 0; i < itemsA.Length; i++)
            {
                CompiledType? a = itemsA[i];
                CompiledType? b = itemsB[i];

                if (a is null && b is null) continue;
                if (a is null || b is null) return false;

                if (!a.Equals(b)) return false;
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

        /// <exception cref="NotImplementedException"/>
        public static bool TryGetTypeParameters(IEnumerable<CompiledType>? definedParameters, IEnumerable<CompiledType>? passedParameters, TypeArguments typeParameters)
            => CompiledType.TryGetTypeParameters(definedParameters?.ToArray(), passedParameters?.ToArray(), typeParameters);

        /// <exception cref="NotImplementedException"/>
        public static bool TryGetTypeParameters(CompiledType[]? definedParameters, CompiledType[]? passedParameters, TypeArguments typeParameters)
        {
            if (definedParameters is null || passedParameters is null) return false;
            if (definedParameters.Length != passedParameters.Length) return false;

            for (int i = 0; i < definedParameters.Length; i++)
            {
                if (!TryGetTypeParameters(definedParameters[i], passedParameters[i], typeParameters))
                { return false; }
            }

            return true;
        }

        public static bool TryGetTypeParameters(CompiledType defined, CompiledType passed, TypeArguments typeParameters)
        {
            if (passed.IsGeneric)
            {
                if (typeParameters.ContainsKey(passed.genericName))
                { return false; }
                throw new NotImplementedException($"This should be non-generic");
            }

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

                return true;
            }

            if (defined.IsPointer && passed.IsPointer)
            {
                return TryGetTypeParameters(defined.PointerTo, passed.PointerTo, typeParameters);
            }

            if (defined.IsStruct && passed.IsStruct)
            {
                if (defined.Struct.Identifier.Content != passed.Struct.Identifier.Content) return false;
                if (defined.Struct.TemplateInfo is not null && passed.Struct.TemplateInfo is not null)
                {
                    if (defined.Struct.TemplateInfo.TypeParameters.Length != passed.TypeParameters.Length)
                    { throw new NotImplementedException(); }
                    for (int j = 0; j < defined.Struct.TemplateInfo.TypeParameters.Length; j++)
                    {
                        string typeParamName = defined.Struct.TemplateInfo.TypeParameters[j].Content;
                        CompiledType typeParamValue = passed.TypeParameters[j];

                        if (typeParameters.TryGetValue(typeParamName, out CompiledType? addedTypeParameter))
                        { if (addedTypeParameter != typeParamValue) return false; }
                        else
                        { typeParameters.Add(typeParamName, typeParamValue); }
                    }

                    return true;
                }
            }

            if (defined != passed) return false;

            return true;
        }

        /// <exception cref="NotImplementedException"/>
        public override int GetHashCode()
        {
            if (IsBuiltin) return HashCode.Combine((byte)0, builtinType);
            if (IsEnum) return HashCode.Combine((byte)1, Enum);
            if (IsStruct) return HashCode.Combine((byte)2, Struct);
            if (IsStackArray) return HashCode.Combine((byte)3, StackArrayOf, StackArraySize);
            if (IsFunction) return HashCode.Combine((byte)4, function);
            if (IsPointer) return HashCode.Combine((byte)5, pointerTo);
            throw new NotImplementedException();
        }

        public static CompiledType CreateGeneric(string content) => new(genericName: content);

        public static IEnumerable<CompiledType> FromArray(IEnumerable<TypeInstance>? types, FindType? typeFinder)
        {
            if (types is null) yield break;

            foreach (TypeInstance type in types)
            { yield return CompiledType.From(type, typeFinder); }

        }

        public static IEnumerable<CompiledType> FromArray(IEnumerable<CompiledType>? types, TypeArguments? typeArguments)
        {
            if (types is null) yield break;

            foreach (CompiledType type in types)
            { yield return CompiledType.From(type, typeArguments); }
        }

        public bool TryGetFieldOffsets([NotNullWhen(true)] out IReadOnlyDictionary<string, int>? fieldOffsets)
        {
            if (IsStruct)
            {
                TypeArguments saved = new(@struct.CurrentTypeArguments);

                @struct.SetTypeArguments(typeParameters);

                fieldOffsets = @struct.FieldOffsets;

                @struct.SetTypeArguments(saved);

                return true;
            }

            fieldOffsets = default;
            return false;
        }

        /// <exception cref="NotImplementedException"/>
        public bool AllGenericsDefined()
        {
            if (IsGeneric) return false;

            if (IsBuiltin) return true;

            if (IsEnum) return true;

            if (IsPointer) return PointerTo.AllGenericsDefined();

            if (IsStackArray) return StackArrayOf.AllGenericsDefined();

            if (IsFunction)
            {
                for (int i = 0; i < Function.Parameters.Length; i++)
                {
                    if (!Function.Parameters[i].AllGenericsDefined())
                    { return false; }
                }
                return Function.ReturnType.AllGenericsDefined();
            }

            if (IsStruct)
            {
                if (Struct.TemplateInfo == null) return true;
                return TypeParameters.Length > 0;
            }

            throw new NotImplementedException();
        }

        public static CompiledType CreatePointer(CompiledType to) => new(pointerTo: to);

        public static CompiledType CreateArrayOf(CompiledType of, DataItem size, StatementWithValue? sizeStatement)
            => new(stackArrayOf: of, stackArraySize: size, stackArraySizeStatement: sizeStatement);

        public static CompiledType? InsertTypeParameters(CompiledType type, TypeArguments? typeArguments)
        {
            if (typeArguments is null) return null;

            if (type.IsGeneric)
            {
                if (!typeArguments.TryGetValue(type.Name, out CompiledType? passedTypeArgument))
                { throw new InternalException(); }

                return passedTypeArgument;
            }

            if (type.IsPointer)
            {
                CompiledType? pointerTo = CompiledType.InsertTypeParameters(type.pointerTo, typeArguments);
                if (pointerTo is null)
                { return null; }
                return CompiledType.CreatePointer(pointerTo);
            }

            if (type.IsStackArray)
            {
                CompiledType? stackArrayOf = CompiledType.InsertTypeParameters(type.stackArrayOf, typeArguments);
                if (stackArrayOf is null)
                { return null; }
                return CompiledType.CreateArrayOf(stackArrayOf, type.stackArraySize, type.stackArraySizeStatement);
            }

            if (type.IsStruct && type.Struct.TemplateInfo != null)
            {
                CompiledType[] structTypeParameterValues = new CompiledType[type.Struct.TemplateInfo.TypeParameters.Length];

                foreach (KeyValuePair<string, CompiledType> item in typeArguments)
                {
                    if (type.Struct.TryGetTypeArgumentIndex(item.Key, out int j))
                    { structTypeParameterValues[j] = item.Value; }
                }

                for (int j = 0; j < structTypeParameterValues.Length; j++)
                {
                    if (structTypeParameterValues[j] is null ||
                        structTypeParameterValues[j].IsGeneric)
                    { return null; }
                }

                return new CompiledType(type.Struct, structTypeParameterValues);
            }

            return null;
        }

        public static void InsertTypeParameters(CompiledType[] types, TypeArguments? typeArguments)
        {
            if (typeArguments is null) return;

            for (int i = 0; i < types.Length; i++)
            {
                types[i] = CompiledType.InsertTypeParameters(types[i], typeArguments) ?? types[i];
            }
        }
    }

    public interface ISameCheck
    {
        public bool IsSame(ISameCheck? other);
    }

    public interface ISameCheck<T> : ISameCheck
    {
        public bool IsSame(T other);

        bool ISameCheck.IsSame(ISameCheck? other) => IsSame(other);
    }
}
