using System;
using System.Collections.Generic;
using ProgrammingLanguage.BBCode;

namespace ProgrammingLanguage
{
    public static class Utils
    {
        internal static double GetGoodNumber(double val) => Math.Round(val * 100) / 100;

        internal static string GetElapsedTime(double ms)
        {
            var val = ms;

            if (val > 750)
            {
                val /= 1000;
            }
            else
            {
                return GetGoodNumber(val).ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms";
            }

            if (val > 50)
            {
                val /= 50;
            }
            else
            {
                return GetGoodNumber(val).ToString(System.Globalization.CultureInfo.InvariantCulture) + " sec";
            }

            return GetGoodNumber(val).ToString(System.Globalization.CultureInfo.InvariantCulture) + " min";
        }

        /// <exception cref="NotImplementedException"/>
        internal static void Map<TKey, TValue>(TKey[] keys, TValue[] values, Dictionary<TKey, TValue> typeParameters)
        {
            if (keys.Length != values.Length)
            { throw new NotImplementedException($"There should be the same number of keys as values"); }

            for (int i = 0; i < keys.Length; i++)
            { typeParameters[keys[i]] = values[i]; }
        }
        /// <exception cref="NotImplementedException"/>
        internal static void Map<TValue>(BBCode.Token[] keys, TValue[] values, Dictionary<string, TValue> typeParameters)
        {
            if (keys.Length != values.Length)
            { throw new NotImplementedException($"There should be the same number of keys as values"); }

            for (int i = 0; i < keys.Length; i++)
            { typeParameters[keys[i].Content] = values[i]; }
        }
    }

    namespace Core
    {
        public partial struct Range<T>
        {
            public static Range<SinglePosition> Create(params Token[] tokens)
            {
                if (tokens.Length == 0) throw new ArgumentException($"Array 'tokens' length is 0");

                Range<SinglePosition> result = new()
                {
                    Start = tokens[0].Position.Start,
                    End = tokens[0].Position.End
                };

                for (int i = 1; i < tokens.Length; i++)
                {
                    Token token = tokens[i];
                    result = result.Extend(token.Position);
                }

                return result;
            }

        }
    }
}
