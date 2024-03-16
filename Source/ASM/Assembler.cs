using System.IO;

namespace LanguageCore.ASM;

public static class Assembler
{
    public static void Assemble(string asmSourceCode, string outputFile, bool saveAsmFile = false)
    {
        string outputFilename = Path.GetFileName(outputFile);

        string fileAsmTemp = outputFilename + ".asm";
        string fileObjTemp = outputFilename + ".obj";
        string fileExeTemp = outputFilename + ".exe";
        string fileExeFinal = outputFile + ".exe";

        if (File.Exists(fileAsmTemp))
        { Output.LogWarning($"File \"{fileAsmTemp}\" will be overridden"); }

        if (File.Exists(fileObjTemp))
        { Output.LogWarning($"File \"{fileObjTemp}\" will be overridden"); }

        if (File.Exists(fileExeTemp))
        { Output.LogWarning($"File \"{fileExeTemp}\" will be overridden"); }

        try
        {
            File.WriteAllText(fileAsmTemp, asmSourceCode);

            if (saveAsmFile && File.Exists(fileAsmTemp))
            {
                Output.LogWarning($"File \"{outputFile + ".asm"}\" will be overridden");
                File.Copy(fileAsmTemp, outputFile + ".asm", true);
            }

            Nasm.Assemble(fileAsmTemp, fileObjTemp);

            GolinkLinker.Link(fileObjTemp, fileExeTemp);

            if (File.Exists(fileExeTemp))
            { File.Copy(fileExeTemp, fileExeFinal, true); }
        }
        finally
        {
            // if (File.Exists(fileAsmTemp))
            // { File.Delete(fileAsmTemp); }
            if (File.Exists(fileObjTemp))
            { File.Delete(fileObjTemp); }
            if (File.Exists(fileExeTemp))
            { File.Delete(fileExeTemp); }
        }
    }

    public static void AssembleRaw(string asmSourceCode, string outputFile, bool saveAsmFile = false)
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
                Output.LogWarning($"File \"{outputFile + ".asm"}\" will be overridden");
                File.Copy(fileAsmTemp, outputFile + ".asm", true);
            }

            Nasm.AssembleRaw(fileAsmTemp, fileBinTemp);

            if (File.Exists(fileBinTemp))
            { File.Copy(fileBinTemp, fileBinFinal, true); }
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
