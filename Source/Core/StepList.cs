using System.Collections.Generic;

namespace ProgrammingLanguage.Core
{
    internal class StepList<T>
    {
        int Position = 0;
        readonly T[] Values = null;

        internal StepList(T[] values, int startIndex)
        {
            this.Values = values;
            this.Position = startIndex;
        }
        internal StepList(List<T> values, int startIndex) : this(values.ToArray(), startIndex) { }

        internal StepList(T[] values) : this(values, 0) { }
        internal StepList(List<T> values) : this(values.ToArray(), 0) { }

        internal T Next() => Values[Position++];
        internal T[] Next(int n)
        {
            T[] result = new T[n];
            for (int i = 0; i < n; i++) result[i] = Next();
            return result;
        }
        internal bool End() => Position >= Values.Length;
        internal void Reset() => Position = 0;
    }
}
