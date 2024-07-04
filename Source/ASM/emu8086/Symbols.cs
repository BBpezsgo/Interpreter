namespace LanguageCore.ASM.emu8086;

/*
< THE SYMBOL TABLE >  test.com  --  emu8086 assembler version: 4.08 
===================================================================================================
Name                     	Offset    	Size      	Type      	Segment   
===================================================================================================
_MAIN                    	00100     	-1        	LABEL     	(NOSEG)   
T_F_PRINT_CHAR_P         	00113     	-1        	LABEL     	(NOSEG)   
===================================================================================================
[ 2024. 03. 17.  --  11:13:58 ] 
< END >
 */

[ExcludeFromCodeCoverage]
public class Symbols
{
    public readonly struct Symbol
    {
        public required string Name { get; init; }
        public required int Offset { get; init; }
        public required int Size { get; init; }
        public required string Type { get; init; }
        public string? Segment { get; init; }

        public void Compile(StringBuilder builder)
        {
            builder.Append(Name.PadRight(25, ' '));
            builder.Append('\t');

            builder.Append(Offset.ToString().PadLeft(5, '0').PadRight(10, ' '));
            builder.Append('\t');

            builder.Append(Size.ToString().PadRight(10, ' '));
            builder.Append('\t');

            builder.Append(Type.PadRight(10, ' '));
            builder.Append('\t');

            if (Segment is null)
            { builder.Append("(NOSEG)"); }
            else
            { builder.Append(Segment); }
        }
    }

    public readonly string BinaryFileName;
    public readonly string AssemblerVersion;
    public readonly List<Symbol> Entries;

    public Symbols(string binaryFileName, string assemblerVersion)
    {
        BinaryFileName = binaryFileName;
        AssemblerVersion = assemblerVersion;
        Entries = new List<Symbol>();
    }

    public string Compile()
    {
        StringBuilder result = new();

        result.Append("< THE SYMBOL TABLE >");
        result.Append("  ");
        result.Append(BinaryFileName);
        result.Append("  ");
        result.Append("--");
        result.Append("  ");
        result.Append("emu8086 assembler version: ");
        result.Append(AssemblerVersion);
        result.AppendLine();

        result.AppendLine("===================================================================================================");
        result.AppendLine("Name                     \tOffset    \tSize      \tType      \tSegment   ");
        result.AppendLine("===================================================================================================");

        foreach (Symbol item in Entries)
        {
            item.Compile(result);
        }

        result.AppendLine("===================================================================================================");

        DateTime now = DateTime.Now;
        result.AppendLine($"[ {now.Year}. {now.Month.ToString().PadLeft(2, '0')}. {now.Day.ToString().PadLeft(2, '0')}.  --  {now.Hour.ToString().PadLeft(2, '0')}:{now.Minute.ToString().PadLeft(2, '0')}:{now.Second.ToString().PadLeft(2, '0')} ]");
        result.AppendLine("< END >");

        return result.ToString();
    }
}
