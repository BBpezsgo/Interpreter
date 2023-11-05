using System;
using System.Collections.Generic;
using LanguageCore.Tokenizing;

namespace LanguageCore
{
    public static partial class Utils
    {
        /// <exception cref="ArgumentException"/>
        public static void Map<TValue>(Token[] keys, TValue[] values, Dictionary<string, TValue> dictionary)
        {
            if (keys.Length != values.Length)
            { throw new ArgumentException($"There should be the same number of keys as values"); }

            for (int i = 0; i < keys.Length; i++)
            { dictionary[keys[i].Content] = values[i]; }
        }
    }
}
