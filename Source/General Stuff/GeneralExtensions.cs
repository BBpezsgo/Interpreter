using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace LanguageCore
{
    public static partial class GeneralExtensions
    {
        public static Range<int> Extend(this Range<int> self, Range<int> range) => self.Extend(range.Start, range.End);
        public static Range<int> Extend(this Range<int> self, int start, int end) => new()
        {
            Start = Math.Min(self.Start, start),
            End = Math.Max(self.End, end),
        };
        public static bool Contains(this Range<int> self, int v) => v >= self.Start && v <= self.End;
        public static bool IsUnset(this Range<int> self) => self.Start == 0 && self.End == 0;
        public static string Escape(this char v)
        {
            switch (v)
            {
                case '\"': return "\\\"";
                case '\\': return @"\\";
                case '\0': return @"\0";
                case '\a': return @"\a";
                case '\b': return @"\b";
                case '\f': return @"\f";
                case '\n': return @"\n";
                case '\r': return @"\r";
                case '\t': return @"\t";
                case '\v': return @"\v";
                default:
                    if (v >= 0x20 && v <= 0x7e)
                    { return v.ToString(); }
                    else
                    { return @"\u" + ((int)v).ToString("x4"); }
            }
        }
        public static string Escape(this string v)
        {
            StringBuilder literal = new(v.Length);
            for (int i = 0; i < v.Length; i++)
            { literal.Append(v[i].Escape()); }
            return literal.ToString();
        }

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

    public static class SearchExtensions
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

        public static TElement? Find<TElement, TQuery>(this Stack<TElement> list, TQuery query) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Count; i++)
            { if (list[i].IsThis(query)) return list[i]; }
            return default;
        }


        public static bool TryFind<TElement, TQuery>(this TElement[] list, TQuery query, [MaybeNullWhen(false)] out TElement? result) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].IsThis(query))
                {
                    result = list[i];
                    return true;
                }
            }

            result = default;
            return false;
        }

        public static bool TryFind<TElement, TQuery>(this IEnumerable<TElement> list, TQuery query, [MaybeNullWhen(false)] out TElement? result) where TElement : ISearchable<TQuery>
        {
            foreach (TElement v in list)
            {
                if (v.IsThis(query))
                {
                    result = v;
                    return true;
                }
            }

            result = default;
            return false;
        }

        public static bool TryFind<TElement, TQuery>(this List<TElement> list, TQuery query, [MaybeNullWhen(false)] out TElement? result) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsThis(query))
                {
                    result = list[i];
                    return true;
                }
            }

            result = default;
            return false;
        }

        public static bool TryFind<TElement, TQuery>(this Stack<TElement> list, TQuery query, [MaybeNullWhen(false)] out TElement? result) where TElement : ISearchable<TQuery>
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsThis(query))
                {
                    result = list[i];
                    return true;
                }
            }

            result = default;
            return false;
        }
    }

    public interface ISearchable<T>
    {
        public bool IsThis(T query);
    }
}
