namespace LanguageCore;

public static class General
{
    public static int Min(params int[] values)
    {
        int n = values.Length;
        if (n == 0) return 0;

        int result = values[0];
        for (int i = 1; i < n; i++)
        {
            if (values[i] < result)
            {
                result = values[i];
            }
        }

        return result;
    }

    public static int Max(params int[] values)
    {
        int n = values.Length;
        if (n == 0) return 0;

        int result = values[0];
        for (int i = 1; i < n; i++)
        {
            if (values[i] > result)
            {
                result = values[i];
            }
        }

        return result;
    }

#if LANG_11
    public static T Max<T>(T a, T b) where T : IComparisonOperators<T, T, bool> => a > b ? a : b;
    public static T Min<T>(T a, T b) where T : IComparisonOperators<T, T, bool> => a < b ? a : b;
#endif
}
