namespace LanguageCore;

public struct EndlessCheck
{
    int Iteration;
    readonly int Max;

    public EndlessCheck(int max = 1000)
    {
        Iteration = 0;
        Max = max;
    }

    public bool Step()
    {
        Iteration++;
        int max = Max == 0 ? 1000 : Max;
        if (Iteration == max)
        {
            return true;
        }
        else if (Iteration > max)
        {
            throw new EndlessLoopException();
        }
        return false;
    }
}
