using System;
using System.Diagnostics;
using System.IO;

namespace LanguageCore.ASM
{
    public static class Assembler
    {
        public static void Assemble(string code, string outputFile)
        {
            const string masm = @"C:\masm32\bin\";
            
            string OutputFileAsm = outputFile + ".asm";
            string OutputFileObject = outputFile + ".obj";
            string OutputFileExe = outputFile + ".exe";

            File.WriteAllText(OutputFileAsm, code);
            System.Threading.Thread.Sleep(100);

            if (File.Exists(OutputFileAsm))
            {
                if (File.Exists(OutputFileObject))
                {
                    File.Delete(OutputFileObject);
                    System.Threading.Thread.Sleep(100);
                }
                Process? process = Process.Start(new ProcessStartInfo(masm + "ml", $"/c /Zd /Fo \"{OutputFileObject}\" /coff \"{OutputFileAsm}\""));
                process?.WaitForExit();
                Console.WriteLine();
                Console.WriteLine($"Exit code: {process?.ExitCode}");
                System.Threading.Thread.Sleep(200);
            }

            if (File.Exists(OutputFileObject))
            {
                if (File.Exists(OutputFileExe))
                {
                    File.Delete(OutputFileExe);
                    System.Threading.Thread.Sleep(100);
                }
                Process? process = Process.Start(new ProcessStartInfo(masm + "Link", $"/SUBSYSTEM:CONSOLE /OUT:\"{OutputFileExe}\" \"{OutputFileObject}\""));
                process?.WaitForExit();
                Console.WriteLine();
                Console.WriteLine($"Exit code: {process?.ExitCode}");
                System.Threading.Thread.Sleep(200);
            }
        }
    }
}
