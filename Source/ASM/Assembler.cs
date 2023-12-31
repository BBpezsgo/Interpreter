using System.IO;

namespace LanguageCore.ASM
{
    public static class Assembler
    {
        public static void Assemble(string asmSourceCode, string outputFile)
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

                Nasm.Assemble(fileAsmTemp, fileObjTemp);

                Linker.Link(fileObjTemp, fileExeTemp);

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
    }
}
