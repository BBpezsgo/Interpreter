﻿
namespace LanguageCore.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using LanguageCore.BBCode.Compiler;
    using LanguageCore.Tokenizing;
    using Statement;

    public abstract class TypeInstance : IEquatable<TypeInstance>, IThingWithPosition
    {
        public abstract Position GetPosition();

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

        public abstract bool Equals(TypeInstance? obj);

        public abstract override int GetHashCode();
        public abstract override string ToString();

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

    public class TypeInstanceStackArray : TypeInstance
    {
        public readonly StatementWithValue? StackArraySize;
        public readonly TypeInstance StackArrayOf;

        public TypeInstanceStackArray(TypeInstance stackArrayOf, StatementWithValue? sizeValue) : base()
        {
            this.StackArrayOf = stackArrayOf;
            this.StackArraySize = sizeValue;
        }

        public override bool Equals(TypeInstance? obj)
        {
            if (obj is not TypeInstanceStackArray other) return false;

            if (!this.StackArrayOf.Equals(other.StackArrayOf)) return false;

            if (this.StackArraySize is null != other.StackArraySize is null) return false;

            return true;
        }

        public override int GetHashCode() => HashCode.Combine((byte)1, StackArrayOf, StackArraySize);

        public override Position GetPosition() => new(StackArrayOf, StackArraySize);

        public override void SetAnalyzedType(CompiledType type)
        {
            if (!type.IsStackArray) return;

            StackArrayOf.SetAnalyzedType(type.StackArrayOf);
        }

        public override string ToString() => $"{StackArrayOf}[{StackArraySize}]";
    }

    public class TypeInstanceFunction : TypeInstance
    {
        public readonly TypeInstance FunctionReturnType;
        public readonly TypeInstance[] FunctionParameterTypes;

        public TypeInstanceFunction(TypeInstance returnType, IEnumerable<TypeInstance> parameters) : base()
        {
            FunctionReturnType = returnType;
            FunctionParameterTypes = parameters.ToArray();
        }

        public override bool Equals(TypeInstance? obj)
        {
            if (obj is not TypeInstanceFunction other) return false;

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

        public override Position GetPosition()
        {
            Position result = new(FunctionReturnType);
            result.Extend(FunctionParameterTypes);
            return result;
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
    }

    public class TypeInstanceSimple : TypeInstance
    {
        public readonly Token Identifier;
        public readonly TypeInstance[]? GenericTypes;

        public TypeInstanceSimple(Token identifier, IEnumerable<TypeInstance>? genericTypes = null) : base()
        {
            this.Identifier = identifier;
            this.GenericTypes = genericTypes?.ToArray();
        }

        public override bool Equals(TypeInstance? obj)
        {
            if (obj is not TypeInstanceSimple other) return false;
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

        public override Position GetPosition()
        {
            Position result = new(Identifier);
            result.Extend(GenericTypes);
            return result;
        }

        public override void SetAnalyzedType(CompiledType type)
        {
            if (TryGetAnalyzedType(type, out TokenAnalyzedType analyzedType))
            { this.Identifier.AnalyzedType = analyzedType; }
        }

        public static TypeInstanceSimple CreateAnonymous(LiteralType literalType, Func<string, string?>? typeDefinitionReplacer)
            => TypeInstanceSimple.CreateAnonymous(literalType.ToStringRepresentation(), typeDefinitionReplacer);
        public static TypeInstanceSimple CreateAnonymous(string name, Func<string, string?>? typeDefinitionReplacer)
        {
            string? definedType = typeDefinitionReplacer?.Invoke(name);
            if (definedType == null)
            { return new TypeInstanceSimple(Token.CreateAnonymous(name), null); }
            else
            { return new TypeInstanceSimple(Token.CreateAnonymous(definedType), null); }
        }

        public override string ToString()
        {
            if (GenericTypes is null) return Identifier.Content;
            return $"{Identifier.Content}<{string.Join<TypeInstance>(", ", GenericTypes)}>";
        }
    }
}