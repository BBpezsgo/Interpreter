namespace LanguageCore;

struct EndlessCheck
{
    int iteration;

    public void Step()
    {
        if (++iteration > 1000)
        {
            throw new EndlessLoopException();
        }
    }
}
