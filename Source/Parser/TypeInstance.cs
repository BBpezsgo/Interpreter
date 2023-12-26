using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LanguageCore.Parser
{
    using Compiler;
    using Statement;
    using Tokenizing;

    public abstract class TypeInstance : IEquatable<TypeInstance>, IPositioned
    {
        public abstract Position Position { get; }

        public static bool operator ==(TypeInstance? a, string? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            if (a is not TypeInstanceSimple a2) return false;
            if (a2.GenericTypes is not null) return false;
            return a2.Identifier.Content == b;
        }
        public static bool operator !=(TypeInstance? a, string? b) => !(a == b);

        public static bool operator ==(string? a, TypeInstance? b) => b == a;
        public static bool operator !=(string? a, TypeInstance? b) => !(b == a);

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is null) return false;
            if (obj is not TypeInstance other) return false;
            return this.Equals(other);
        }

        public abstract bool Equals(TypeInstance? other);

        public abstract override int GetHashCode();
        public abstract override string ToString();
        public virtual string ToString(TypeArguments typeArguments) => ToString();

        protected static bool TryGetAnalyzedType(CompiledType type, out TokenAnalyzedType analyzedType)
        {
            analyzedType = default;
            if (type.IsClass)
            {
                analyzedType = TokenAnalyzedType.Class;
                return true;
            }

            if (type.IsStruct)
            {
                analyzedType = TokenAnalyzedType.Struct;
                return true;
            }

            if (type.IsGeneric)
            {
                analyzedType = TokenAnalyzedType.TypeParameter;
                return true;
            }

            if (type.IsBuiltin)
            {
                analyzedType = TokenAnalyzedType.BuiltinType;
                return true;
            }

            if (type.IsFunction)
            {
                return TryGetAnalyzedType(type.Function.ReturnType, out analyzedType);
            }

            if (type.IsEnum)
            {
                analyzedType = TokenAnalyzedType.Enum;
                return true;
            }

            return false;
        }

        public abstract void SetAnalyzedType(CompiledType type);
    }

    public class TypeInstanceStackArray : TypeInstance, IEquatable<TypeInstanceStackArray?>
    {
        public readonly StatementWithValue? StackArraySize;
        public readonly TypeInstance StackArrayOf;

        public TypeInstanceStackArray(TypeInstance stackArrayOf, StatementWithValue? sizeValue) : base()
        {
            this.StackArrayOf = stackArrayOf;
            this.StackArraySize = sizeValue;
        }

        public override bool Equals(object? obj) => obj is TypeInstanceStackArray other && Equals(other);
        public override bool Equals(TypeInstance? other) => other is TypeInstanceStackArray other_ && Equals(other_);
        public bool Equals(TypeInstanceStackArray? other)
        {
            if (other is null) return false;
            if (!this.StackArrayOf.Equals(other.StackArrayOf)) return false;

            if (this.StackArraySize is null != other.StackArraySize is null) return false;

            return true;
        }

        public override int GetHashCode() => HashCode.Combine((byte)1, StackArrayOf, StackArraySize);

        public override Position Position => new(StackArrayOf, StackArraySize);

        public override void SetAnalyzedType(CompiledType type)
        {
            if (!type.IsStackArray) return;

            StackArrayOf.SetAnalyzedType(type.StackArrayOf);
        }

        public override string ToString() => $"{StackArrayOf}[{StackArraySize}]";
        public override string ToString(TypeArguments typeArguments) => $"{StackArrayOf.ToString(typeArguments)}[{StackArraySize}]";
    }

    public class TypeInstanceFunction : TypeInstance, IEquatable<TypeInstanceFunction?>
    {
        public readonly TypeInstance FunctionReturnType;
        public readonly TypeInstance[] FunctionParameterTypes;

        public TypeInstanceFunction(TypeInstance returnType, IEnumerable<TypeInstance> parameters) : base()
        {
            FunctionReturnType = returnType;
            FunctionParameterTypes = parameters.ToArray();
        }

        public override bool Equals(object? obj) => obj is TypeInstanceFunction other && Equals(other);
        public override bool Equals(TypeInstance? other) => other is TypeInstanceFunction other_ && Equals(other_);
        public bool Equals(TypeInstanceFunction? other)
        {
            if (other is null) return false;
            if (!this.FunctionReturnType.Equals(other.FunctionReturnType)) return false;
            if (this.FunctionParameterTypes.Length != other.FunctionParameterTypes.Length) return false;
            for (int i = 0; i < this.FunctionParameterTypes.Length; i++)
            {
                if (!this.FunctionParameterTypes[i].Equals(other.FunctionParameterTypes[i]))
                { return false; }
            }
            return true;
        }

        public override int GetHashCode() => HashCode.Combine((byte)2, FunctionReturnType, FunctionParameterTypes);

        public override Position Position
        {
            get
            {
                Position result = new(FunctionReturnType);
                result.Union(FunctionParameterTypes);
                return result;
            }
        }

        public override void SetAnalyzedType(CompiledType type)
        {
            if (!type.IsFunction) return;

            FunctionReturnType.SetAnalyzedType(type.Function.ReturnType);

            if (this.FunctionParameterTypes.Length == type.Function.Parameters.Length)
            {
                for (int i = 0; i < type.Function.Parameters.Length; i++)
                {
                    this.FunctionParameterTypes[i].SetAnalyzedType(type.Function.Parameters[i]);
                }
            }
        }

        public override string ToString() => $"{FunctionReturnType}({string.Join<TypeInstance>(", ", FunctionParameterTypes)})";
        public override string ToString(TypeArguments typeArguments)
        {
            StringBuilder result = new();
            result.Append(FunctionReturnType.ToString(typeArguments));
            result.Append('(');
            for (int i = 0; i < FunctionParameterTypes.Length; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(FunctionParameterTypes[i].ToString(typeArguments));
            }
            result.Append(')');
            return result.ToString();
        }
    }

    public class TypeInstanceSimple : TypeInstance, IEquatable<TypeInstanceSimple?>
    {
        public readonly Token Identifier;
        public readonly TypeInstance[]? GenericTypes;

        public TypeInstanceSimple(Token identifier, IEnumerable<TypeInstance>? genericTypes = null) : base()
        {
            this.Identifier = identifier;
            this.GenericTypes = genericTypes?.ToArray();
        }

        public override bool Equals(object? obj) => obj is TypeInstanceSimple other && Equals(other);
        public override bool Equals(TypeInstance? other) => other is TypeInstanceSimple other_ && Equals(other_);
        public bool Equals(TypeInstanceSimple? other)
        {
            if (other is null) return false;
            if (this.Identifier.Content != other.Identifier.Content) return false;

            if (this.GenericTypes is null) return other.GenericTypes is null;
            if (other.GenericTypes is null) return false;

            if (this.GenericTypes.Length != other.GenericTypes.Length) return false;
            for (int i = 0; i < this.GenericTypes.Length; i++)
            {
                if (!this.GenericTypes[i].Equals(other.GenericTypes[i]))
                { return false; }
            }
            return true;
        }

        public override int GetHashCode() => HashCode.Combine((byte)3, Identifier, GenericTypes);

        public override Position Position
        {
            get
            {
                Position result = new(Identifier);
                result.Union(GenericTypes);
                return result;
            }
        }

        public override void SetAnalyzedType(CompiledType type)
        {
            if (TryGetAnalyzedType(type, out TokenAnalyzedType analyzedType))
            { this.Identifier.AnalyzedType = analyzedType; }
        }

        public static TypeInstanceSimple CreateAnonymous(string name, Func<string, string?>? typeDefinitionReplacer)
        {
            string? definedType = typeDefinitionReplacer?.Invoke(name);
            if (definedType == null)
            { return new TypeInstanceSimple(Token.CreateAnonymous(name), null); }
            else
            { return new TypeInstanceSimple(Token.CreateAnonymous(definedType), null); }
        }

        public static TypeInstanceSimple CreateAnonymous(string name, IEnumerable<TypeInstance>? genericTypes, Func<string, string?>? typeDefinitionReplacer)
        {
            string? definedType = typeDefinitionReplacer?.Invoke(name);
            if (definedType == null)
            { return new TypeInstanceSimple(Token.CreateAnonymous(name), genericTypes); }
            else
            { return new TypeInstanceSimple(Token.CreateAnonymous(definedType), genericTypes); }
        }

        public static TypeInstanceSimple CreateAnonymous(string name, IEnumerable<Token>? genericTypes, Func<string, string?>? typeDefinitionReplacer)
        {
            TypeInstance[]? genericTypesConverted;
            if (genericTypes == null)
            { genericTypesConverted = null; }
            else
            {
                Token[] genericTypesA = genericTypes.ToArray();
                genericTypesConverted = new TypeInstance[genericTypesA.Length];
                for (int i = 0; i < genericTypesA.Length; i++)
                {
                    genericTypesConverted[i] = TypeInstanceSimple.CreateAnonymous(genericTypesA[i].Content, typeDefinitionReplacer);
                }
            }

            string? definedType = typeDefinitionReplacer?.Invoke(name);
            if (definedType == null)
            { return new TypeInstanceSimple(Token.CreateAnonymous(name), genericTypesConverted); }
            else
            { return new TypeInstanceSimple(Token.CreateAnonymous(definedType), genericTypesConverted); }
        }

        public override string ToString()
        {
            if (GenericTypes is null) return Identifier.Content;
            return $"{Identifier.Content}<{string.Join<TypeInstance>(", ", GenericTypes)}>";
        }
        public override string ToString(TypeArguments typeArguments)
        {
            string identifier = Identifier.Content;
            if (typeArguments.TryGetValue(Identifier.Content, out CompiledType? replaced))
            { identifier = replaced.ToString(); }

            if (GenericTypes is null)
            { return identifier; }

            StringBuilder result = new(identifier);
            result.Append('<');
            for (int i = 0; i < GenericTypes.Length; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(GenericTypes[i].ToString(typeArguments));
            }
            result.Append('>');
            return result.ToString();
        }
    }
}