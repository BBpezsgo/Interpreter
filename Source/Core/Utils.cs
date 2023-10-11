using System;
using System.Collections.Generic;
using LanguageCore.Tokenizing;

namespace LanguageCore
{
    public static partial class Utils
    {
        /// <exception cref="NotImplementedException"/>
        internal static void Map<TValue>(Token[] keys, TValue[] values, Dictionary<string, TValue> typeParameters)
        {
            if (keys.Length != values.Length)
            { throw new NotImplementedException($"There should be the same number of keys as values"); }

            for (int i = 0; i < keys.Length; i++)
            { typeParameters[keys[i].Content] = values[i]; }
        }
    }

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
