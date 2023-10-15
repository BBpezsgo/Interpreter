using System.Collections.Generic;
using System.Text;

namespace LanguageCore.ASM
{
    public struct AssemblyHeader
    {
        public string MasmPath;
        public List<string> Libraries;
    }

    public class AssemblyCode
    {
        readonly StringBuilder Builder;
        const string EOL = "\r\n";

        public AssemblyCode()
        {
            Builder = new StringBuilder();
        }

        public void WriteHeader(AssemblyHeader header)
        {
            this.AppendLine(".386");
            this.AppendLine(".model flat, stdcall");
            this.AppendLine("option casemap:none");
            this.AppendLine();

            for (int i = 0; i < header.Libraries.Count; i++)
            {
                string library = header.Libraries[i];
                this.AppendLine($"include {header.MasmPath}include\\{library}.inc");
            }

            for (int i = 0; i < header.Libraries.Count; i++)
            {
                string library = header.Libraries[i];
                this.AppendLine($"includelib {header.MasmPath}lib\\{library}.lib");
            }

            this.AppendLine();
        }

        public void AppendLine() => Builder.Append(EOL);

        public void AppendLine(string line)
        {
            Builder.Append(line);
            Builder.Append(EOL);
        }

        public override string ToString() => Builder.ToString();
    }
}
