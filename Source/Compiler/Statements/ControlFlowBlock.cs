namespace LanguageCore.Compiler;

public readonly struct ControlFlowBlock
{
    public int? FlagAddress { get; }
    public Stack<int> PendingJumps { get; }
    public Stack<bool> Doings { get; }
    public ILocated Location { get; }

    public ControlFlowBlock(int? flagAddress, ILocated location)
    {
        FlagAddress = flagAddress;
        PendingJumps = new Stack<int>();
        Doings = new Stack<bool>();

        PendingJumps.Push(0);
        Doings.Push(false);
        Location = location;
    }
}
