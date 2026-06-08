using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Generates Source Maps (v3) with Base64 VLQ encoding.
/// </summary>
public class SourceMapGenerator
{
    public string Generate(string generatedFileName, string sourceFileName, List<TypeScriptCodeBuilder.SourceMapping> mappings, string? sourceContent = null)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"version\": 3,");
        sb.Append($"\"file\": \"{generatedFileName}\",");
        sb.Append($"\"sourceRoot\": \"\",");
        sb.Append($"\"sources\": [\"{sourceFileName.Replace("\\", "/")}\"],");
        
        if (sourceContent != null)
        {
            sb.Append($"\"sourcesContent\": [\"{EscapeJson(sourceContent)}\"],");
        }
        
        sb.Append("\"names\": [],");
        sb.Append("\"mappings\": \"");
        sb.Append(EncodeMappings(mappings));
        sb.Append("\"");
        sb.Append("}");
        return sb.ToString();
    }

    private string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private string EncodeMappings(List<TypeScriptCodeBuilder.SourceMapping> mappings)
    {
        var sb = new StringBuilder();
        int prevGenLine = 1;
        int prevGenCol = 0;
        int prevSrcLine = 0;
        int prevSrcCol = 0;
        int prevSrcFile = 0;

        var sortedMappings = mappings.OrderBy(m => m.GeneratedLine).ThenBy(m => m.GeneratedColumn);

        foreach (var m in sortedMappings)
        {
            if (m.GeneratedLine > prevGenLine)
            {
                for (int i = 0; i < m.GeneratedLine - prevGenLine; i++)
                {
                    sb.Append(';');
                }
                prevGenLine = m.GeneratedLine;
                prevGenCol = 0;
            }
            else if (sb.Length > 0 && sb[sb.Length - 1] != ';')
            {
                sb.Append(',');
            }

            // Segment: [generatedCol, srcFile, srcLine, srcCol] — all values are 0-based deltas.
            sb.Append(Base64Vlq.Encode(m.GeneratedColumn - prevGenCol));
            sb.Append(Base64Vlq.Encode(0 - prevSrcFile)); // Single source file for now
            sb.Append(Base64Vlq.Encode(m.SourceLine - prevSrcLine));
            sb.Append(Base64Vlq.Encode(m.SourceColumn - prevSrcCol));

            prevGenCol = m.GeneratedColumn;
            prevSrcLine = m.SourceLine;
            prevSrcCol = m.SourceColumn;
        }

        return sb.ToString();
    }

    private static class Base64Vlq
    {
        private const string Base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        public static string Encode(int value)
        {
            var sb = new StringBuilder();
            int vlq = value < 0 ? ((-value) << 1) | 1 : value << 1;

            do
            {
                int digit = vlq & 0x1F;
                vlq >>= 5;
                if (vlq > 0) digit |= 0x20;
                sb.Append(Base64Chars[digit]);
            } while (vlq > 0);

            return sb.ToString();
        }
    }
}
