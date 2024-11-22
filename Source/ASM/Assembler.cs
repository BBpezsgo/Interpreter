using System.IO;

namespace LanguageCore.Assembly;

[ExcludeFromCodeCoverage]
public static class Assembler
{
    public static void Assemble(string asmSourceCode, string outputFile)
    {
        string fileAsmTemp = outputFile + ".asm";
        string fileObjTemp = outputFile + ".obj";
        string fileExeFinal = outputFile;

        if (File.Exists(fileAsmTemp))
        { Output.LogWarning($"File \"{fileAsmTemp}\" will be overridden"); }

        if (File.Exists(fileObjTemp))
        { Output.LogWarning($"File \"{fileObjTemp}\" will be overridden"); }

        File.WriteAllText(fileAsmTemp, asmSourceCode);

        Nasm.Assemble(fileAsmTemp, fileObjTemp);

        GnuLinker.Link(fileObjTemp, fileExeFinal);
    }

    public static void AssembleRaw(string asmSourceCode, string outputFile, bool saveAsmFile = false, IEnumerable<emu8086.Symbols.Symbol>? symbols = null)
    {
        string outputFilename = Path.GetFileName(outputFile);

        string fileAsmTemp = outputFilename + ".asm";
        string fileBinTemp = outputFilename + ".bin";
        string fileBinFinal = outputFile + ".bin";

        if (File.Exists(fileAsmTemp))
        { Output.LogWarning($"File \"{fileAsmTemp}\" will be overridden"); }

        if (File.Exists(fileBinTemp))
        { Output.LogWarning($"File \"{fileBinTemp}\" will be overridden"); }

        try
        {
            File.WriteAllText(fileAsmTemp, asmSourceCode);

            if (saveAsmFile && File.Exists(fileAsmTemp))
            {
                Output.LogWarning($"File \"{fileBinFinal + ".~asm"}\" will be overridden");
                File.Copy(fileAsmTemp, fileBinFinal + ".~asm", true);
            }

            Nasm.AssembleRaw(fileAsmTemp, fileBinTemp);

            if (File.Exists(fileBinTemp))
            { File.Copy(fileBinTemp, fileBinFinal, true); }

            if (symbols is not null)
            {
                emu8086.Symbols _symbols = new(fileBinFinal, "4.08");
                _symbols.Entries.AddRange(symbols);
                File.WriteAllText(fileBinFinal + ".symbol", _symbols.Compile());
            }
        }
        finally
        {
            // if (File.Exists(fileAsmTemp))
            // { File.Delete(fileAsmTemp); }
            if (File.Exists(fileBinTemp))
            { File.Delete(fileBinTemp); }
        }
    }
}
