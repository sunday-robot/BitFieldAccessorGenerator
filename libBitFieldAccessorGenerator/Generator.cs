using System.Reflection.Metadata;
using System.Text;

namespace libBitFieldAccessorGenerator;

public static class Generator
{
    public static string Generate(string namespaceName, string className, bool isBigEndian, string description, IReadOnlyList<(int width, string name, string description)> fieldDefinitions)
    {
        Validator.Validate(fieldDefinitions);

        var sb = new StringBuilder();

        GenerateNameSpace(sb, namespaceName);
        GenerateClassSummary(sb, isBigEndian, description, fieldDefinitions);
        GenerateClassHeader(sb, className);

        int bitIndex = 0;
        foreach (var fieldDefinition in fieldDefinitions)
        {
            GenerateField(sb, isBigEndian, bitIndex, fieldDefinition);
            bitIndex += fieldDefinition.width;
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    static void GenerateNameSpace(StringBuilder sb, string namespaceName)
    {
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
    }

    static void GenerateClassSummary(StringBuilder sb, bool isBigEndian, string description, IReadOnlyList<(int width, string name, string description)> fieldDefinitions)
    {
        var totalBits = fieldDefinitions.Sum(f => f.width);
        if (isBigEndian)
            PrintSummery(sb, "", $"{totalBits} bits", "big endian", description);
        else
            PrintSummery(sb, "", $"{totalBits} bits", "little endian", description);
    }

    static void GenerateClassHeader(StringBuilder sb, string className)
    {
        sb.AppendLine($"public sealed class {className}(byte[] data)");
        sb.AppendLine("{");
        sb.AppendLine("    readonly byte[] _data = data;");
    }

    static void GenerateField(StringBuilder sb, bool isBigEndian, int bitIndex, (int width, string name, string description) fieldDefinition)
    {
        sb.AppendLine();

        if (fieldDefinition.name.Length == 0)
        {
            GenerateReserverdFieldComment(sb, bitIndex, fieldDefinition);
            return;
        }

        GenerateFieldSummary(sb, bitIndex, fieldDefinition);
        sb.AppendLine($"    public {GetFieldType(fieldDefinition.width)} {fieldDefinition.name}");
        sb.AppendLine("    {");
        GenerateGetter(sb, isBigEndian, bitIndex, fieldDefinition);
        GenerateSetter(sb, isBigEndian, bitIndex, fieldDefinition);
        sb.AppendLine("    }");
    }

    static void GenerateReserverdFieldComment(StringBuilder sb, int bitIndex, (int width, string name, string description) fieldDefinition)
    {
        sb.AppendLine($"    // reserved, offset:{bitIndex / 8}.{bitIndex % 8}, bitWidth:{fieldDefinition.width}");
    }

    static void GenerateFieldSummary(StringBuilder sb, int bitIndex, (int width, string name, string description) fieldDefinition)
    {
        PrintSummery(sb, "    ", $"offset:{bitIndex / 8}.{bitIndex % 8}", $"bitWidth:{fieldDefinition.width}", fieldDefinition.description);
    }

    static string GetFieldType(int bitWidth)
    {
        return bitWidth switch
        {
            <= 8 => "byte",
            <= 16 => "ushort",
            _ => "uint",
        };
    }

    static void PrintSummery(StringBuilder sb, string indent, params string[] summaries)
    {
        sb.AppendLine($"{indent}/// <summary>");
        foreach (var summary in summaries)
            if (summary.Length != 0)
                sb.AppendLine($"{indent}/// {summary}");
        sb.AppendLine($"{indent}/// </summary>");
    }

    private static string MaskBinary8(int bitWidth, int shift)
    {
        int mask = ((1 << bitWidth) - 1) << shift;
        string bin = Convert.ToString(mask & 0xFF, 2).PadLeft(8, '0');
        return "0b" + bin.Insert(4, "_") + "u";
    }

    private static string MaskBinary8Inverse(int bitWidth, int shift)
    {
        int mask = ((1 << bitWidth) - 1) << shift;
        int inv = (~mask) & 0xFF;
        string bin = Convert.ToString(inv, 2).PadLeft(8, '0');
        return "0b" + bin.Insert(4, "_") + "u";
    }

    private static void GenerateGetter(
        StringBuilder sb,
        bool isBigEndian,
        int bitIndex,
        (int width, string name, string description) fieldDefinition)
    {
        int bitWidth = fieldDefinition.width;

        int startByte = bitIndex >> 3;
        int endByte = (bitIndex + bitWidth - 1) >> 3;
        int byteCount = endByte - startByte + 1;

        string type = bitWidth switch
        {
            <= 8 => "byte",
            <= 16 => "ushort",
            _ => "uint",
        };

        sb.AppendLine("        get");
        sb.AppendLine("        {");

        // 1バイトに収まる場合
        if (byteCount == 1)
        {
            int bitOffsetInByte = bitIndex & 7;
            if (isBigEndian)
            {
                bitOffsetInByte = 8 - bitWidth - bitOffsetInByte;
            }

            string mask = MaskBinary8(bitWidth, bitOffsetInByte);

            if (bitOffsetInByte == 0)
            {
                sb.AppendLine($"            return ({type})(_data[{startByte}] & {mask});");
            }
            else
            {
                sb.AppendLine($"            return ({type})((_data[{startByte}] & {mask}) >> {bitOffsetInByte});");
            }
            sb.AppendLine("        }");
            return;
        }

        // 2〜5バイトにまたがる場合（ローカル変数結合方式）
        var localVars = new List<string>();

        for (int i = 0; i < byteCount; i++)
        {
            int byteIndex = startByte + i;
            int localOffset = 0;
            int bitsFromThisByte = 8;

            if (i == 0)
            {
                if (!isBigEndian)
                {
                    localOffset = bitIndex & 7;
                    bitsFromThisByte = 8 - localOffset;
                }
                else
                {
                    localOffset = 0;
                    int startOffset = bitIndex & 7;
                    bitsFromThisByte = 8 - startOffset;
                }
            }
            else if (i == byteCount - 1)
            {
                if (!isBigEndian)
                {
                    localOffset = 0;
                    bitsFromThisByte = (bitIndex + bitWidth) & 7;
                    if (bitsFromThisByte == 0) bitsFromThisByte = 8;
                }
                else
                {
                    bitsFromThisByte = (bitIndex + bitWidth) & 7;
                    if (bitsFromThisByte == 0) bitsFromThisByte = 8;
                    localOffset = 8 - bitsFromThisByte;
                }
            }

            int maskValue = ((1 << bitsFromThisByte) - 1) << localOffset;
            string maskStr = "0b" + Convert.ToString(maskValue, 2).PadLeft(8, '0').Insert(4, "_") + "u";

            string varName = $"b{i}";
            localVars.Add(varName);
            sb.AppendLine($"            var {varName} = _data[{byteIndex}] & {maskStr};");
        }

        sb.Append("            var x = ");
        for (int i = 0; i < byteCount; i++)
        {
            string varName = localVars[i];
            int byteWeight = isBigEndian ? (byteCount - 1 - i) : i;
            int totalShift = byteWeight * 8;

            if (totalShift == 0)
                sb.Append(varName);
            else
                sb.Append($"({varName} << {totalShift})");

            if (i < byteCount - 1)
                sb.Append(" | ");
        }
        sb.AppendLine(";");

        int finalRightShift;
        if (!isBigEndian)
        {
            finalRightShift = bitIndex & 7;
        }
        else
        {
            int endOffset = (bitIndex + bitWidth) & 7;
            finalRightShift = (endOffset == 0) ? 0 : (8 - endOffset);
        }

        if (finalRightShift == 0)
            sb.AppendLine($"            return ({type})x;");
        else
            sb.AppendLine($"            return ({type})(x >> {finalRightShift});");

        sb.AppendLine("        }");
    }

    private static void GenerateSetter(
        StringBuilder sb,
        bool isBigEndian,
        int bitIndex,
        (int width, string name, string description) fieldDefinition)
    {
        int bitWidth = fieldDefinition.width;

        int startByte = bitIndex >> 3;
        int endByte = (bitIndex + bitWidth - 1) >> 3;
        int byteCount = endByte - startByte + 1;

        sb.AppendLine("        set");
        sb.AppendLine("        {");

        // 1 byte → 直接マスク方式
        if (byteCount == 1)
        {
            int bitOffsetInByte = bitIndex & 7;
            if (isBigEndian)
            {
                bitOffsetInByte = 8 - bitWidth - bitOffsetInByte;
            }

            string writeMask = MaskBinary8(bitWidth, bitOffsetInByte);
            string keepMask = MaskBinary8Inverse(bitWidth, bitOffsetInByte);

            if (bitOffsetInByte == 0)
            {
                sb.AppendLine($"            _data[{startByte}] = (byte)((_data[{startByte}] & {keepMask}) | (value & {writeMask}));");
            }
            else
            {
                sb.AppendLine($"            _data[{startByte}] = (byte)((_data[{startByte}] & {keepMask}) | ((value << {bitOffsetInByte}) & {writeMask}));");
            }

            sb.AppendLine("        }");
            return;
        }

        // 2〜5 bytes → ローカル変数分配方式
        int initialLeftShift;
        if (!isBigEndian)
        {
            initialLeftShift = bitIndex & 7;
        }
        else
        {
            int endOffset = (bitIndex + bitWidth) & 7;
            initialLeftShift = (endOffset == 0) ? 0 : (8 - endOffset);
        }

        if (initialLeftShift == 0)
            sb.AppendLine("            var x = value;");
        else
            sb.AppendLine($"            var x = value << {initialLeftShift};");

        for (int i = 0; i < byteCount; i++)
        {
            int byteWeight = isBigEndian ? (byteCount - 1 - i) : i;
            int totalShift = byteWeight * 8;

            if (totalShift == 0)
                sb.AppendLine($"            var b{i} = x;");
            else
                sb.AppendLine($"            var b{i} = x >> {totalShift};");
        }

        for (int i = 0; i < byteCount; i++)
        {
            int byteIndex = startByte + i;
            int localOffset = 0;
            int bitsFromThisByte = 8;

            if (i == 0)
            {
                if (!isBigEndian)
                {
                    localOffset = bitIndex & 7;
                    bitsFromThisByte = 8 - localOffset;
                }
                else
                {
                    localOffset = 0;
                    int startOffset = bitIndex & 7;
                    bitsFromThisByte = 8 - startOffset;
                }
            }
            else if (i == byteCount - 1)
            {
                if (!isBigEndian)
                {
                    localOffset = 0;
                    bitsFromThisByte = (bitIndex + bitWidth) & 7;
                    if (bitsFromThisByte == 0) bitsFromThisByte = 8;
                }
                else
                {
                    bitsFromThisByte = (bitIndex + bitWidth) & 7;
                    if (bitsFromThisByte == 0) bitsFromThisByte = 8;
                    localOffset = 8 - bitsFromThisByte;
                }
            }

            int maskValue = ((1 << bitsFromThisByte) - 1) << localOffset;
            string keepMaskStr = "0b" + Convert.ToString((~maskValue) & 0xFF, 2).PadLeft(8, '0').Insert(4, "_") + "u";
            string writeMaskStr = "0b" + Convert.ToString(maskValue & 0xFF, 2).PadLeft(8, '0').Insert(4, "_") + "u";

            sb.AppendLine($"            _data[{byteIndex}] = (byte)((_data[{byteIndex}] & {keepMaskStr}) | (b{i} & {writeMaskStr}));");
        }

        sb.AppendLine("        }");
    }
}
