using System;
using System.IO;

namespace LanguageCore.ASM
{
    [Serializable]
    public class ProcessException : Exception
    {
        readonly string processName;
        readonly int exitCode;
        readonly string stdOutput;
        readonly string stdError;

        public override string Message => $"Process \"{processName}\" exited with code {exitCode}";
        public string StandardOutput => stdOutput;
        public string StandardError => stdError;

        public ProcessException(string processName, int exitCode, string stdOutput, string stdError) : base()
        {
            this.processName = processName;
            this.exitCode = exitCode;
            this.stdOutput = stdOutput;
            this.stdError = stdError;
        }
        protected ProcessException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
            this.exitCode = info.GetInt32("exitCode");
            this.stdOutput = info.GetString("stdOutput") ?? string.Empty;
            this.stdError = info.GetString("stdError") ?? string.Empty;
            this.processName = info.GetString("processName") ?? string.Empty;
        }
    }

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
