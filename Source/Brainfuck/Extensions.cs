using System.Collections.Generic;

#nullable enable
 
namespace ProgrammingLanguage.Brainfuck
{
    internal interface ISearchable<T>
    {
        public bool IsThis(T query);
    }

    internal static class SearchExtensions
    {
        public static TElement? Find<TElement, TQuery>(this TElement[] list, TQuery query) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Length; i++)
            { if (list[i].IsThis(query)) return list[i]; }
            return default;
        }

        public static TElement? Find<TElement, TQuery>(this IEnumerable<TElement> list, TQuery query) where TElement : ISearchable<TQuery>
        {
            foreach (TElement v in list)
            { if (v.IsThis(query)) return v; }
            return default;
        }

        public static TElement? Find<TElement, TQuery>(this List<TElement> list, TQuery query) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Count; i++)
            { if (list[i].IsThis(query)) return list[i]; }
            return default;
        }

        public static TElement? Find<TElement, TQuery>(this ProgrammingLanguage.Core.Stack<TElement> list, TQuery query) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Count; i++)
            { if (list[i].IsThis(query)) return list[i]; }
            return default;
        }


        public static bool TryFind<TElement, TQuery>(this TElement[] list, TQuery query, out TElement result) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].IsThis(query))
                {
                    result = list[i];
                    return true;
                }
            }
#pragma warning disable CS8601 // Possible null reference assignment.
            result = default;
#pragma warning restore CS8601 // Possible null reference assignment.
            return false;
        }

        public static bool TryFind<TElement, TQuery>(this IEnumerable<TElement> list, TQuery query, out TElement result) where TElement : ISearchable<TQuery>
        {
            foreach (TElement v in list)
            {
                if (v.IsThis(query))
                {
                    result = v;
                    return true;
                }
            }
#pragma warning disable CS8601 // Possible null reference assignment.
            result = default;
#pragma warning restore CS8601 // Possible null reference assignment.
            return false;
        }

        public static bool TryFind<TElement, TQuery>(this List<TElement> list, TQuery query, out TElement result) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsThis(query))
                {
                    result = list[i];
                    return true;
                }
            }
#pragma warning disable CS8601 // Possible null reference assignment.
            result = default;
#pragma warning restore CS8601 // Possible null reference assignment.
            return false;
        }

        public static bool TryFind<TElement, TQuery>(this ProgrammingLanguage.Core.Stack<TElement> list, TQuery query, out TElement result) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsThis(query))
                {
                    result = list[i];
                    return true;
                }
            }
#pragma warning disable CS8601 // Possible null reference assignment.
            result = default;
#pragma warning restore CS8601 // Possible null reference assignment.
            return false;
        }
    }
    public static class Extensions
    {
        public static int Sum(this IEnumerable<int> v)
        {
            int result = 0;
            foreach (int item in v)
            {
                result += item;
            }
            return result;
        }
    }
}
