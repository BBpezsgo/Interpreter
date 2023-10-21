using System;
using System.Collections.Generic;

namespace LanguageCore
{
    public static partial class Utils
    {
        public static double GetGoodNumber(double val) => Math.Round(val * 100) / 100;

        public static string GetElapsedTime(double ms)
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
        public static void Map<TKey, TValue>(TKey[] keys, TValue[] values, Dictionary<TKey, TValue> typeParameters) where TKey : notnull
        {
            if (keys.Length != values.Length)
            { throw new NotImplementedException($"There should be the same number of keys as values"); }

            for (int i = 0; i < keys.Length; i++)
            { typeParameters[keys[i]] = values[i]; }
        }

        public static int GCF(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }
        public static int LCM(int a, int b)
        {
            return (a / GCF(a, b)) * b;
        }

        public static (int, int, int) Split(int v)
        {
            double _v = (double)v;
            double a = Math.Sqrt(_v);
            int resultA = (int)Math.Floor(a);
            int resultB = (int)Math.Ceiling(a);
            return (resultA, resultB, v - (resultA * resultB));
        }
    }
}
