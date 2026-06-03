using System.Text;

namespace libBitAccessorGenerator;

public static class Generator
{
    static void PrintSummery(StringBuilder sb, string indent, params string[] summaries)
    {
        sb.AppendLine($"{indent}/// <summary>");
        foreach (var summary in summaries)
            sb.AppendLine($"{indent}/// {summary}");
        sb.AppendLine($"{indent}/// </summary>");
    }

    public static string Generate(string namespaceName, string className, bool isBigEndian, IReadOnlyList<(string bitFieldName, int bitWidth)> fieldDefinitions)
    {
        Validator.Validate(fieldDefinitions);

        var sb = new StringBuilder();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        var totalBits = fieldDefinitions.Sum(f => f.bitWidth);
        if (isBigEndian)
            PrintSummery(sb, "", $"{totalBits} bits", "big endian");
        else
            PrintSummery(sb, "", $"{totalBits} bits", "little endian");
        sb.AppendLine($"public sealed class {className}(byte[] data)");
        sb.AppendLine("{");
        sb.AppendLine("    readonly byte[] _data = data;");

        int bitIndex = 0;
        foreach (var fieldDefinition in fieldDefinitions)
        {
            GenerateField(sb, isBigEndian, bitIndex, fieldDefinition);
            bitIndex += fieldDefinition.bitWidth;
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    static void GenerateField(StringBuilder sb, bool isBigEndian, int bitIndex, (string bitFieldName, int bitWidth) fieldDefinition)
    {
        sb.AppendLine();
        if (fieldDefinition.bitFieldName.Length == 0)
        {
            sb.AppendLine($"    // reserved, bitWidth:{fieldDefinition.bitWidth}");
            return;
        }

        PrintSummery(sb, "    ", $"bitWidth:{fieldDefinition.bitWidth}");
        sb.AppendLine($"    public uint {fieldDefinition.bitFieldName}");
        sb.AppendLine("    {");
        GenerateGetter(sb, isBigEndian, bitIndex, fieldDefinition);
        GenerateSetter(sb, isBigEndian, bitIndex, fieldDefinition);
        sb.AppendLine("    }");
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
        (string bitFieldName, int bitWidth) fieldDefinition)
    {
        int bitWidth = fieldDefinition.bitWidth;

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
        (string bitFieldName, int bitWidth) fieldDefinition)
    {
        int bitWidth = fieldDefinition.bitWidth;

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
