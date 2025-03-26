namespace LanguageCore;

public static class General
{
    public static int Max(int a, int b) => a > b ? a : b;
    public static int Min(int a, int b) => a < b ? a : b;

    public static SinglePosition Max(SinglePosition a, SinglePosition b) => a > b ? a : b;
    public static SinglePosition Min(SinglePosition a, SinglePosition b) => a < b ? a : b;
}
