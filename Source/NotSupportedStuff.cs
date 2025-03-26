global using MemberNotNullWhenAttribute = System.Runtime.CompilerServices.MemberNotNullWhenAttribute_;

[assembly: SuppressMessage("Design", "CS8604")]
[assembly: SuppressMessage("Design", "CS8632")]

namespace System.Runtime.InteropServices
{
    public static class CollectionsMarshal
    {
        public static Span<T> AsSpan<T>(List<T> values) => values.ToArray();
    }
}

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit : Attribute
    {
        public IsExternalInit()
        {

        }
    }

    public class RequiredMemberAttribute : Attribute
    {
        public RequiredMemberAttribute()
        {

        }
    }

    public class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string name)
        {

        }
    }

    public class MemberNotNullWhenAttribute_ : Attribute
    {
        public MemberNotNullWhenAttribute_(bool returnValue, string memberName)
        {

        }
    }
}

namespace System.Numerics
{
    public interface INumberBase<TSelf>
    {

    }

    public interface IMinMaxValue<TSelf>
    {

    }

    public interface IModulusOperators<TSelf, TOther, TResult>
    {

    }

    public interface IUnaryPlusOperators<TSelf, TOther>
    {

    }

    public interface IUnaryNegationOperators<TSelf, TOther>
    {

    }

    public interface IShiftOperators<TSelf, TOther, TResult>
    {

    }

    public interface IBitwiseOperators<TSelf, TOther, TResult>
    {

    }

    public interface IAdditionOperators<TSelf, TOther, TResult>
    {

    }

    public interface ISubtractionOperators<TSelf, TOther, TResult>
    {

    }

    public interface IEqualityOperators<TSelf, TOther, TResult>
    {

    }

    public interface IMultiplyOperators<TSelf, TOther, TResult>
    {

    }

    public interface IDivisionOperators<TSelf, TOther, TResult>
    {

    }

    public interface IComparisonOperators<TSelf, TOther, TResult>
    {

    }
}

namespace System
{
    [Serializable]
    public class UnreachableException : Exception
    {
        public UnreachableException() { }
        public UnreachableException(string message) : base(message) { }
        public UnreachableException(string message, Exception inner) : base(message, inner) { }
        protected UnreachableException(
            Runtime.Serialization.SerializationInfo info,
            Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class SetsRequiredMembersAttribute : Attribute
    {
        public SetsRequiredMembersAttribute() { }
    }
}