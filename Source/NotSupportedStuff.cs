#if UNITY
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

    [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string name)
        {

        }
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public class MemberNotNullWhenAttribute_ : Attribute
    {
        public MemberNotNullWhenAttribute_(bool returnValue, string memberName)
        {

        }
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

    public static class MemoryExtensions
    {
        public ref struct SpanSplitEnumerator<T> where T : IEquatable<T>
        {
            readonly ReadOnlySpan<T> _source;

            readonly T _separator;
            readonly ReadOnlySpan<T> _separatorBuffer;

            SpanSplitEnumeratorMode _splitMode;
            int _startCurrent;
            int _endCurrent;
            int _startNext;

            public readonly SpanSplitEnumerator<T> GetEnumerator() => this;

            public readonly ReadOnlySpan<T> Source => _source;

            public readonly Range Current => new(_startCurrent, _endCurrent);

            internal SpanSplitEnumerator(ReadOnlySpan<T> source, ReadOnlySpan<T> separator, bool treatAsSingleSeparator)
            {
                Debug.Assert(treatAsSingleSeparator, "Should only ever be called as true; exists to differentiate from separators overload");

                _startCurrent = default;
                _endCurrent = default;
                _startNext = default;

                _source = source;
                _separatorBuffer = separator;
                _separator = default!;
                _splitMode = separator.Length == 0 ?
                    SpanSplitEnumeratorMode.EmptySequence :
                    SpanSplitEnumeratorMode.Sequence;
            }

            internal SpanSplitEnumerator(ReadOnlySpan<T> source, T separator)
            {
                _startCurrent = default;
                _endCurrent = default;
                _startNext = default;

                _source = source;
                _separatorBuffer = default;
                _separator = separator;
                _splitMode = SpanSplitEnumeratorMode.SingleElement;
            }

            public bool MoveNext()
            {
                int separatorIndex, separatorLength;
                switch (_splitMode)
                {
                    case SpanSplitEnumeratorMode.None:
                        return false;

                    case SpanSplitEnumeratorMode.SingleElement:
                        separatorIndex = _source[_startNext..].IndexOf(_separator);
                        separatorLength = 1;
                        break;

                    case SpanSplitEnumeratorMode.Sequence:
                        separatorIndex = _source[_startNext..].IndexOf(_separatorBuffer);
                        separatorLength = _separatorBuffer.Length;
                        break;

                    case SpanSplitEnumeratorMode.EmptySequence:
                        separatorIndex = -1;
                        separatorLength = 1;
                        break;

                    default:
                        throw new UnreachableException();
                }

                _startCurrent = _startNext;
                if (separatorIndex >= 0)
                {
                    _endCurrent = _startCurrent + separatorIndex;
                    _startNext = _endCurrent + separatorLength;
                }
                else
                {
                    _startNext = _endCurrent = _source.Length;
                    _splitMode = SpanSplitEnumeratorMode.None;
                }

                return true;
            }
        }

        enum SpanSplitEnumeratorMode
        {
            None = 0,
            SingleElement,
            Sequence,
            EmptySequence,
        }

        public static SpanSplitEnumerator<T> Split<T>(this ReadOnlySpan<T> source, T separator) where T : IEquatable<T> => new(source, separator);
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [Flags]
    public enum DynamicallyAccessedMemberTypes
    {
        All = -1,
        None = 0,
        PublicParameterlessConstructor = 1,
        PublicConstructors = 3,
        NonPublicConstructors = 4,
        PublicMethods = 8,
        NonPublicMethods = 16,
        PublicFields = 32,
        NonPublicFields = 64,
        PublicNestedTypes = 128,
        NonPublicNestedTypes = 256,
        PublicProperties = 512,
        NonPublicProperties = 1024,
        PublicEvents = 2048,
        NonPublicEvents = 4096,
        Interfaces = 8192,
    }

    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class SetsRequiredMembersAttribute : Attribute
    {
        public SetsRequiredMembersAttribute() { }
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.GenericParameter | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue | AttributeTargets.Struct, Inherited = false)]
    public sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) { }
    }
}
#endif
