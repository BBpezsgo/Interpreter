using System;
using System.Collections.Generic;
using System.Globalization;

namespace LanguageCore
{
    public interface IDuplicatable<T> where T : notnull
    {
        public T Duplicate();
    }

    public static partial class Utils
    {
        public static string GetElapsedTime(double ms)
        {
            double result = ms;

            if (result <= 750)
            { return result.ToString("N3", CultureInfo.InvariantCulture) + " ms"; }
            result /= 1000;

            if (result <= 50)
            { return result.ToString("N2", CultureInfo.InvariantCulture) + " sec"; }
            result /= 60;

            if (result <= 50)
            { return result.ToString("N1", CultureInfo.InvariantCulture) + " min"; }
            result /= 60;

            if (result <= 20)
            { return result.ToString("N1", CultureInfo.InvariantCulture) + " hour"; }
            result /= 24;

            return result.ToString("N1", CultureInfo.InvariantCulture) + " day";
        }

        public static Dictionary<TKey, TValue> ConcatDictionary<TKey, TValue>(params IReadOnlyDictionary<TKey, TValue>?[] dictionaries) where TKey : notnull
        {
            Dictionary<TKey, TValue> result = new();
            for (int i = 0; i < dictionaries.Length; i++)
            {
                IReadOnlyDictionary<TKey, TValue>? dict = dictionaries[i];
                if (dict == null) continue;

                foreach (KeyValuePair<TKey, TValue> pair in dict)
                { result[pair.Key] = pair.Value; }
            }
            return result;
        }

        /// <exception cref="ArgumentException"/>
        public static void Map<TSourceKey, TSourceValue, TDestinationKey, TDestinationValue>(TSourceKey[] keys, TSourceValue[] values, Func<TSourceKey, TSourceValue, ValueTuple<TDestinationKey, TDestinationValue>> mapper, Dictionary<TDestinationKey, TDestinationValue> dictionary)
            where TDestinationKey : notnull
        {
            if (keys.Length != values.Length)
            { throw new ArgumentException($"There should be the same number of keys as values"); }

            for (int i = 0; i < keys.Length; i++)
            {
                ValueTuple<TDestinationKey, TDestinationValue> pair = mapper.Invoke(keys[i], values[i]);
                dictionary[pair.Item1] = pair.Item2;
            }
        }

        /// <exception cref="ArgumentException"/>
        public static Dictionary<TDestinationKey, TDestinationValue> Map<TSourceKey, TSourceValue, TDestinationKey, TDestinationValue>(TSourceKey[] keys, TSourceValue[] values, Func<TSourceKey, TSourceValue, ValueTuple<TDestinationKey, TDestinationValue>> mapper)
            where TDestinationKey : notnull
        {
            Dictionary<TDestinationKey, TDestinationValue> result = new();
            Utils.Map(keys, values, mapper, result);
            return result;
        }

        /// <exception cref="ArgumentException"/>
        public static void Map<TKey, TValue>(TKey[] keys, TValue[] values, Dictionary<TKey, TValue> dictionary)
            where TKey : notnull
        {
            if (keys.Length != values.Length)
            { throw new ArgumentException($"There should be the same number of keys as values"); }

            for (int i = 0; i < keys.Length; i++)
            { dictionary[keys[i]] = values[i]; }
        }

        /// <exception cref="ArgumentException"/>
        public static Dictionary<TKey, TValue> Map<TKey, TValue>(TKey[] keys, TValue[] values)
            where TKey : notnull
        {
            Dictionary<TKey, TValue> result = new();
            Utils.Map(keys, values, result);
            return result;
        }
    }
}
