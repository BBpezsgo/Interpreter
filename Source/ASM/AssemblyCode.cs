﻿namespace LanguageCore.ASM;

[ExcludeFromCodeCoverage]
public class AssemblyCode
{
    public static readonly string[] ReservedWords = new string[] {
        "$",
        "DF",
        "GROUP",
        "ORG",
        "*",
        "DGROUP",
        "GT",
        "%OUT",
        "+",
        "DOSSEG",
        "HIGH",
        "PAGE",
        "_",
        "DQ",
        "IF",
        "PARA",
        ".",
        "DS",
        "IF1",
        "PROC",
        "/",
        "DT",
        "IF2",
        "PTR",
        "=",
        "DUP",
        "IFB",
        "PUBLIC",
        "?",
        "DW",
        "IFDEF",
        "PURGE",
        "[  ]",
        "DWORD",
        "IFGIF",
        "QWORD",
        ".186",
        "ELSE",
        "IFDE",
        ".RADIX",
        ".286",
        "END",
        "IFIDN",
        "RECORD",
        ".286P",
        "ENDIF",
        "IFNB",
        "REPT",
        ".287",
        "ENDM",
        "IFNDEF",
        ".SALL",
        ".386",
        "ENDP",
        "INCLUDE",
        "SEG",
        ".386P",
        "ENDS",
        "INCLUDELIB",
        "SEGMENT",
        ".387",
        "EQ",
        "IRP",
        ".SEQ",
        ".8086",
        "EQU",
        "IRPC",
        ".SFCOND",
        ".8087",
        ".ERR",
        "LABEL",
        "SHL",
        "ALIGN",
        ".ERR1",
        ".LALL",
        "SHORT",
        ".ALPHA",
        ".ERR2",
        "LARGE",
        "SHR",
        "AND",
        ".ERRB",
        "LE",
        "SIZE",
        "ASSUME",
        ".ERRDEF",
        "LENGTH",
        "SMALL",
        "AT",
        ".ERRDIF",
        ".LFCOND",
        "STACK",
        "BYTE",
        ".ERRE",
        ".LIST",
        "@STACK",
        ".CODE",
        ".ERRIDN",
        "LOCAL",
        ".STACK",
        "@CODE",
        ".ERRNB",
        "LOW",
        "STRUC",
        "@CODESIZE",
        ".ERRNDEF",
        "LT",
        "SUBTTL",
        "COMM",
        ".ERRNZ",
        "MACRO",
        "TBYTE",
        "COMMENT",
        "EVEN",
        "MASK",
        ".TFCOND",
        ".CONST",
        "EXITM",
        "MEDIUM",
        "THIS",
        ".CREF",
        "EXTRN",
        "MOD",
        "TITLE",
        "@CURSEG",
        "FAR",
        ".MODEL",
        "TYPE",
        "@DATA",
        "@FARDATA",
        "NAME",
        ".TYPE",
        ".DATA",
        ".FARDATA",
        "NE",
        "WIDTH",
        "@DATA?",
        "@FARDATA?",
        "NEAR",
        "WORD",
        ".DATA?",
        ".FARDATA?",
        "NOT",
        "@WORDSIZE",
                    "@DATASIZE",
        "@FILENAME",
        "NOTHING",
        ".XALL",
        "DB",
        "FWORD",
        "OFFSET",
        ".XCREP",
        "DD",
        "GE",
        "OR",
        ".XLIST",
        "XOR",
    };
    public const string ValidIdentifierCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public readonly TextSectionBuilder CodeBuilder;
    public readonly DataSectionBuilder DataBuilder;

    const string EOL = "\r\n";

    public AssemblyCode()
    {
        CodeBuilder = new TextSectionBuilder()
        {
            Indent = SectionBuilder.IndentIncrement,
        };
        DataBuilder = new DataSectionBuilder()
        {
            Indent = SectionBuilder.IndentIncrement,
        };
    }

    public static string GenerateLabel(string? prefix, int length, Func<string, bool>? isValidCallback)
    {
        StringBuilder result = new(prefix, length + (prefix?.Length ?? 0));

        int endlessSafe = 128;
        while (result.Length < length)
        {
            char newChar = ValidIdentifierCharacters[System.Random.Shared.Next(0, ValidIdentifierCharacters.Length)];
            while (AssemblyCode.IsReserved(result.ToString() + newChar) ||
                   isValidCallback == null ||
                   isValidCallback.Invoke(result.ToString() + newChar))
            {
                newChar = ValidIdentifierCharacters[System.Random.Shared.Next(0, ValidIdentifierCharacters.Length)];
                if (endlessSafe-- < 0) throw new EndlessLoopException();
            }
            result.Append(newChar);
        }

        return result.ToString();
    }

    public static bool IsReserved(string word)
    {
        for (int i = 0; i < ReservedWords.Length; i++)
        {
            if (string.Equals(ReservedWords[i], word, StringComparison.OrdinalIgnoreCase))
            { return true; }
        }
        return false;
    }

    public string Make(bool is16Bits)
    {
        StringBuilder builder = new();

        builder.Append(";" + EOL);
        builder.Append("; WARNING: Generated by BB's compiler!" + EOL);
        builder.Append(";" + EOL);
        builder.Append(EOL);

        if (is16Bits)
        {
            builder.Append("BITS 16" + EOL);
            builder.Append("ORG 100h" + EOL);
            builder.Append(EOL);
        }

        if (CodeBuilder.Imports.Count > 0)
        {
            builder.Append(";" + EOL);
            builder.Append("; External functions" + EOL);
            builder.Append(";" + EOL);
            foreach (string import in CodeBuilder.Imports)
            {
                builder.Append($"extern {import}" + EOL);
            }
            builder.Append(EOL);
        }

        if (!is16Bits)
        {
            builder.Append(";" + EOL);
            builder.Append("; Global functions" + EOL);
            builder.Append(";" + EOL);
            builder.Append("global _main" + EOL);
            builder.Append(EOL);
        }

        builder.Append(";" + EOL);
        builder.Append("; Code Section" + EOL);
        builder.Append(";" + EOL);
        if (!is16Bits)
        { builder.Append("section .text" + EOL); }
        builder.Append(EOL);
        builder.Append("_main:" + EOL);

        builder.Append(CodeBuilder.Builder);
        builder.Append(EOL);

        if (DataBuilder.Builder.Length > 0)
        {
            builder.Append(";" + EOL);
            builder.Append("; Data Section" + EOL);
            builder.Append(";" + EOL);
            builder.Append("section .rodata" + EOL);
            builder.Append(DataBuilder.Builder);
            builder.Append(EOL);
        }

        return builder.ToString();
    }
}
