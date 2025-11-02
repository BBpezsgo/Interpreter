using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LanguageCore.Native.Generator;

#if NET
[SupportedOSPlatform("linux")]
#endif
public readonly struct NativeFunction : IDisposable
{
    readonly nint Ptr;
    readonly nuint Size;

    public T AsDelegate<T>() where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Ptr);

    NativeFunction(nint ptr, nuint size)
    {
        Ptr = ptr;
        Size = size;
    }

    public static NativeFunction Allocate(byte[] code)
    {
        int pageSize = Environment.SystemPageSize;
        ulong allocSize = (ulong)((code.Length + pageSize - 1) / pageSize * pageSize);
        nuint allocSizePtr = checked((nuint)allocSize);

        nint mem = LibC.mmap(0, allocSizePtr,
                            LibC.PROT_WRITE,
                            LibC.MAP_PRIVATE | LibC.MAP_ANONYMOUS,
                            -1, 0);

        if (mem == (nint)(-1))
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"mmap failed, errno={err}");
        }

        Marshal.Copy(code, 0, mem, code.Length);

        if (LibC.mprotect(mem, allocSizePtr, LibC.PROT_EXEC) != 0)
        {
            int err = Marshal.GetLastWin32Error();
            LibC.munmap(mem, allocSizePtr);
            throw new InvalidOperationException($"mprotect failed, errno={err}");
        }

        return new NativeFunction(mem, allocSizePtr);
    }

    public void Dispose()
    {
        LibC.munmap(Ptr, Size);
    }
}

#if NET
[SupportedOSPlatform("linux")]
#endif
public static partial class LibC
{
    public const int PROT_NONE = 0x0;
    public const int PROT_READ = 0x1;
    public const int PROT_WRITE = 0x2;
    public const int PROT_EXEC = 0x4;

    public const int MAP_PRIVATE = 0x02;
    public const int MAP_ANONYMOUS = 0x20;

#if NET7_0_OR_GREATER

    [LibraryImport("libc", SetLastError = true)]
    public static partial nint mmap(nint addr, nuint length, int prot, int flags, int fd, nint offset);

    [LibraryImport("libc", SetLastError = true)]
    public static partial int mprotect(nint addr, nuint len, int prot);

    [LibraryImport("libc", SetLastError = true)]
    public static partial int munmap(nint addr, nuint length);

#else

    [DllImport("libc", SetLastError = true)]
    public static extern nint mmap(nint addr, nuint length, int prot, int flags, int fd, nint offset);

    [DllImport("libc", SetLastError = true)]
    public static extern int mprotect(nint addr, nuint len, int prot);

    [DllImport("libc", SetLastError = true)]
    public static extern int munmap(nint addr, nuint length);

#endif
}

static class Assembler
{
    public static byte[] Assemble(string assembly, DiagnosticsCollection diagnostics)
    {
        string sourceFile = Path.GetTempFileName();
        File.WriteAllText(sourceFile, assembly);

        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "nasm",
                Arguments = $"-f bin \"{sourceFile}\" -o /dev/stdout",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        process.WaitForExit();

        File.Delete(sourceFile);

        foreach (string line in process.StandardError.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string prefix = $"{sourceFile}:";
            if (line.StartsWith(prefix))
            {
                string rest = line[prefix.Length..];
                int i = rest.IndexOf(':');
                if (i != -1 && int.TryParse(rest[..i], out int lineNumber))
                {
                    rest = rest[(i + 1)..].TrimStart();

                    const string errorPrefix = "error:";
                    const string warningPrefix = "warning:";

                    if (rest.StartsWith(errorPrefix))
                    {
                        rest = rest[errorPrefix.Length..].TrimStart();
                        diagnostics.Add(new Diagnostic(DiagnosticsLevel.Error, $"{rest}\n{assembly.Split('\n')[lineNumber - 1]}", Position.UnknownPosition, null, true, ImmutableArray<Diagnostic>.Empty));
                        continue;
                    }

                    if (rest.StartsWith(warningPrefix))
                    {
                        rest = rest[warningPrefix.Length..].TrimStart();
                        diagnostics.Add(new Diagnostic(DiagnosticsLevel.Warning, $"{rest}\n{assembly.Split('\n')[lineNumber - 1]}", Position.UnknownPosition, null, false, ImmutableArray<Diagnostic>.Empty));
                        continue;
                    }
                }
            }
            throw new NotImplementedException();
        }

        List<byte> codeList = new();
        byte[] chunk = new byte[512];
        while (true)
        {
            int l = process.StandardOutput.BaseStream.Read(chunk);
            codeList.AddRange(chunk[..l]);
            if (l < chunk.Length) break;
        }
        return codeList.ToArray();
    }
}
