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

    public interface IModulusOperators<TSelf, TOher, TResult>
    {

    }

    public interface IUnaryPlusOperators<TSelf, TOher>
    {

    }

    public interface IUnaryNegationOperators<TSelf, TOher>
    {

    }

    public interface IShiftOperators<TSelf, TOher, TResult>
    {

    }

    public interface IBitwiseOperators<TSelf, TOher, TResult>
    {

    }

    public interface IAdditionOperators<TSelf, TOher, TResult>
    {
        
    }

    public interface ISubtractionOperators<TSelf, TOher, TResult>
    {
        
    }

    public interface IEqualityOperators<TSelf, TOher, TResult>
    {
        
    }

    public interface IMultiplyOperators<TSelf, TOher, TResult>
    {
        
    }

    public interface IDivisionOperators<TSelf, TOher, TResult>
    {
        
    }

    public interface IComparisonOperators<TSelf, TOher, TResult>
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