using System.Globalization;

namespace LanguageCore
{
    public interface IDuplicatable<T>
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
    }
}
