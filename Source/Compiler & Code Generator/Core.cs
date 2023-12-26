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
        public AddressingMode AddressingMode => BasepointerRelative ? AddressingMode.BasePointerRelative : AddressingMode.Absolute;

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

        public ValueAddress(CompiledVariable variable, bool basepointerRelative)
        {
            Address = variable.MemoryAddress;
            BasepointerRelative = basepointerRelative;
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
            result.Append(Address);
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

    readonly struct UndefinedOffset<T>
    {
        public readonly int InstructionIndex;
        public readonly bool IsAbsoluteAddress;

        public readonly Statement? Caller;
        public readonly T Called;

        public readonly string? CurrentFile;

        public UndefinedOffset(int callInstructionIndex, bool isAbsoluteAddress, Statement? caller, T called, string? file)
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
        public readonly string? SourceFile;

        public Reference(T source, string? sourceFile)
        {
            Source = source;
            SourceFile = sourceFile;
        }
    }

    public interface IReferenceable<T>
    {
        public void AddReference(T referencedBy, string? file);
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

        public static bool operator !=(FunctionType? a, FunctionType? b) => !(a == b);
        public static bool operator ==(FunctionType? a, FunctionType? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;

            return a.Equals(b);
        }
    }

    public delegate bool ComputeValue(StatementWithValue value, RuntimeType? expectedType, out DataItem computedValue);

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class CompiledType :
        IEquatable<CompiledType>,
        IEquatable<TypeInstance>,
        IEquatable<Type>,
        IEquatable<RuntimeType>,
        System.Numerics.IEqualityOperators<CompiledType?, CompiledType?, bool>,
        System.Numerics.IEqualityOperators<CompiledType?, TypeInstance?, bool>,
        System.Numerics.IEqualityOperators<CompiledType?, Type, bool>,
        System.Numerics.IEqualityOperators<CompiledType?, RuntimeType, bool>
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
                    Type.NotBuiltin => throw new UnreachableException(),

                    _ => throw new NotImplementedException($"Type conversion for {builtinType} is not implemented"),
                };

                if (@struct is not null) return @struct.Name.Content;
                if (@class is not null) return @class.Name.Content;
                if (@enum is not null) return @enum.Identifier.Content;
                if (function is not null) return function.ToString();

                throw new UnreachableException();
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
                if (IsStruct) return @struct.SizeOnStack;
                if (IsClass)
                {
                    TypeArguments saved = new(@class.CurrentTypeArguments);

                    @class.SetTypeArguments(typeParameters);

                    int size = @class.SizeOnHeap;

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
                if (IsStruct) return @struct.SizeOnStack;
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
                        _ => throw new UnreachableException(),
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
            ArgumentNullException.ThrowIfNull(other, nameof(other));

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

        public CompiledType(CompiledStruct @struct) : this()
        {
            this.@struct = @struct;
        }

        public CompiledType(FunctionType function) : this()
        {
            this.function = function;
        }

        public CompiledType(CompiledClass @class) : this()
        {
            this.@class = @class;
        }

        public CompiledType(CompiledClass @class, params CompiledType[][] typeParameters) : this()
        {
            this.@class = @class;
            List<CompiledType> typeParameters1 = new();
            for (int i = 0; i < typeParameters.Length; i++)
            { typeParameters1.AddRange(typeParameters[i]); }
            this.typeParameters = typeParameters1.ToArray();
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

        public CompiledType(string type, Func<string, CompiledType>? typeFinder) : this()
        {
            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(type, out this.builtinType))
            { return; }

            if (typeFinder == null) throw new InternalException($"Can't parse {type} to CompiledType");

            Set(typeFinder.Invoke(type));
        }

        public CompiledType(TypeInstance type, Func<string, CompiledType>? typeFinder, ComputeValue? constComputer = null) : this()
        {
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

            throw new UnreachableException();
        }

        /// <exception cref="InternalException"/>
        public CompiledType(TypeInstanceSimple type, Func<string, CompiledType>? typeFinder, ComputeValue? constComputer = null) : this()
        {
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

        public CompiledType(TypeInstanceFunction type, Func<string, CompiledType>? typeFinder, ComputeValue? constComputer = null) : this()
        {
            CompiledType returnType = new(type.FunctionReturnType, typeFinder, constComputer);
            CompiledType[] parameterTypes = CompiledType.FromArray(type.FunctionParameterTypes, typeFinder);

            function = new FunctionType(returnType, parameterTypes);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InternalException"/>
        public CompiledType(TypeInstanceStackArray type, Func<string, CompiledType>? typeFinder, ComputeValue? constComputer = null) : this()
        {
            ArgumentNullException.ThrowIfNull(constComputer, nameof(constComputer));

            stackArrayOf = new CompiledType(type.StackArrayOf, typeFinder, constComputer);
            stackArraySizeStatement = type.StackArraySize!;

            if (!constComputer.Invoke(type.StackArraySize!, RuntimeType.SInt32, out stackArraySize))
            { throw new CompilerException($"Failed to compute value", type.StackArraySize, null); }
        }

        public CompiledType(CompiledType other)
        {
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

        void Set(CompiledType other)
        {
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
                    Type.NotBuiltin => throw new UnreachableException(),
                    _ => throw new UnreachableException(),
                };
            }

            StringBuilder result = new();
            result.Append(Name);

            if (TypeParameters.Length > 0)
            { result.Append($"<{string.Join<CompiledType>(", ", TypeParameters)}>"); }
            else if (@class != null && @class.TemplateInfo is not null)
            { result.Append($"<{string.Join<Token>(", ", @class.TemplateInfo.TypeParameters)}>"); }

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

        public static bool Equals(CompiledType?[]? itemsA, TypeInstance?[]? itemsB)
        {
            if (itemsA is null && itemsB is null) return true;
            if (itemsA is null || itemsB is null) return false;

            if (itemsA.Length != itemsB.Length) return false;

            for (int i = 0; i < itemsA.Length; i++)
            {
                CompiledType? a = itemsA[i];
                TypeInstance? b = itemsB[i];

                if (a is null && b is null) continue;
                if (a is null || b is null) return false;

                if (!a.Equals(b)) return false;
            }

            return true;
        }

        public static bool Equals(CompiledType?[]? itemsA, CompiledType?[]? itemsB)
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

        /// <exception cref="NotImplementedException"/>
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
            if (IsClass)
            {
                TypeArguments saved = new(@class.CurrentTypeArguments);

                @class.SetTypeArguments(typeParameters);

                fieldOffsets = @class.FieldOffsets;

                @class.SetTypeArguments(saved);

                return true;
            }

            if (IsStruct)
            {
                fieldOffsets = @struct.FieldOffsets;
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

            if (IsStackArray) return StackArrayOf.AllGenericsDefined();

            if (IsFunction)
            {
                for (int i = 0; i < Function.Parameters.Length; i++)
                {
                    if (!function.Parameters[i].AllGenericsDefined())
                    { return false; }
                }
                return Function.ReturnType.AllGenericsDefined();
            }

            if (IsStruct) return true;

            if (IsClass)
            {
                if (Class.TemplateInfo == null) return true;
                return TypeParameters.Length > 0;
            }

            throw new NotImplementedException();
        }
    }

    public interface IAmInContext<T>
    {
        public T? Context { get; set; }
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
