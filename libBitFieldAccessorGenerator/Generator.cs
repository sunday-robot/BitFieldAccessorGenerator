using System.Text;

namespace libBitFieldAccessorGenerator;

public static class Generator
{
    private readonly record struct AccessorTemplateParams(
        string FieldType,
        int StartByte,
        int ByteCount,
        bool IsBigEndian,
        int ShiftAmount,
        byte StartWriteMask,
        byte EndWriteMask
    );

    public static string Generate(string namespaceName, string className, bool isBigEndian, string description, IReadOnlyList<(int width, string name, string description)> fieldDefinitions)
    {
        Validator.Validate(fieldDefinitions);

        var sb = new StringBuilder();

        GenerateNameSpace(sb, namespaceName);
        var totalBits = fieldDefinitions.Sum(f => f.width);
        GenerateClassSummaryComment(sb, isBigEndian, description, totalBits);
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

    static void GenerateClassSummaryComment(StringBuilder sb, bool isBigEndian, string description, int totalBits)
    {
        var endianStr = isBigEndian ? "big endian" : "little endian";
        GenerateSummaryComment(sb, "", $"{totalBits} bits", endianStr, description);
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

        GenerateFieldSummaryComment(sb, bitIndex, fieldDefinition);
        sb.AppendLine($"    public {GetFieldType(fieldDefinition.width)} {fieldDefinition.name}");
        sb.AppendLine("    {");

        AccessorTemplateParams p = isBigEndian
            ? CalculateBigEndianParams(bitIndex, fieldDefinition.width)
            : CalculateLittleEndianParams(bitIndex, fieldDefinition.width);

        GenerateAccessorCode(sb, p);

        sb.AppendLine("    }");
    }

    static void GenerateReserverdFieldComment(StringBuilder sb, int bitIndex, (int width, string name, string description) fieldDefinition)
    {
        sb.AppendLine($"    // reserved, offset:{bitIndex / 8}.{bitIndex % 8}, bitWidth:{fieldDefinition.width}");
    }

    static void GenerateFieldSummaryComment(StringBuilder sb, int bitIndex, (int width, string name, string description) fieldDefinition)
    {
        GenerateSummaryComment(sb, "    ", $"offset:{bitIndex / 8}.{bitIndex % 8}", $"bitWidth:{fieldDefinition.width}", fieldDefinition.description);
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

    static void GenerateSummaryComment(StringBuilder sb, string indent, params string[] summaries)
    {
        sb.AppendLine($"{indent}/// <summary>");
        foreach (var summary in summaries)
            if (summary.Length != 0)
                sb.AppendLine($"{indent}/// {summary}");
        sb.AppendLine($"{indent}/// </summary>");
    }

    #region パラメータ計算ロジック (エンディアン個別)

    private static AccessorTemplateParams CalculateBigEndianParams(int bitIndex, int bitWidth)
    {
        int startByte = bitIndex >> 3;
        int endByte = (bitIndex + bitWidth - 1) >> 3;
        int byteCount = endByte - startByte + 1;
        string fieldType = GetFieldType(bitWidth);

        if (byteCount == 1)
        {
            int bitOffsetInByte = 8 - bitWidth - (bitIndex & 7);
            return new AccessorTemplateParams(
                FieldType: fieldType, StartByte: startByte, ByteCount: byteCount, IsBigEndian: true,
                ShiftAmount: bitOffsetInByte,
                StartWriteMask: CalculateMask8(bitWidth, bitOffsetInByte), EndWriteMask: 0
            );
        }

        int startOffset = bitIndex & 7;
        int bitsFromStartByte = 8 - startOffset;
        int startMaskVal = (1 << bitsFromStartByte) - 1;

        int bitsFromEndByte = (bitIndex + bitWidth) & 7;
        if (bitsFromEndByte == 0) bitsFromEndByte = 8;
        int endLocalOffset = 8 - bitsFromEndByte;
        int endMaskVal = ((1 << bitsFromEndByte) - 1) << endLocalOffset;

        int rightShift = (bitsFromEndByte == 8) ? 0 : (8 - bitsFromEndByte);

        return new AccessorTemplateParams(
            FieldType: fieldType, StartByte: startByte, ByteCount: byteCount, IsBigEndian: true,
            ShiftAmount: rightShift,
            StartWriteMask: (byte)startMaskVal,
            EndWriteMask: (byte)(endMaskVal & 0xFF)
        );
    }

    private static AccessorTemplateParams CalculateLittleEndianParams(int bitIndex, int bitWidth)
    {
        int startByte = bitIndex >> 3;
        int endByte = (bitIndex + bitWidth - 1) >> 3;
        int byteCount = endByte - startByte + 1;
        string fieldType = GetFieldType(bitWidth);

        int startOffset = bitIndex & 7;

        if (byteCount == 1)
        {
            return new AccessorTemplateParams(
                FieldType: fieldType, StartByte: startByte, ByteCount: byteCount, IsBigEndian: false,
                ShiftAmount: startOffset,
                StartWriteMask: CalculateMask8(bitWidth, startOffset), EndWriteMask: 0
            );
        }

        int bitsFromStartByte = 8 - startOffset;
        int startMaskVal = ((1 << bitsFromStartByte) - 1) << startOffset;

        int bitsFromEndByte = (bitIndex + bitWidth) & 7;
        if (bitsFromEndByte == 0) bitsFromEndByte = 8;
        int endMaskVal = (1 << bitsFromEndByte) - 1;

        return new AccessorTemplateParams(
            FieldType: fieldType, StartByte: startByte, ByteCount: byteCount, IsBigEndian: false,
            ShiftAmount: startOffset,
            StartWriteMask: (byte)(startMaskVal & 0xFF),
            EndWriteMask: (byte)endMaskVal
        );
    }

    #endregion

    #region 共通出力ロジック

    private static void GenerateAccessorCode(StringBuilder sb, AccessorTemplateParams p)
    {
        string startMaskStr = ToBinaryString(p.StartWriteMask);
        string endMaskStr = ToBinaryString(p.EndWriteMask);
        string invStartMaskStr = ToBinaryString((byte)~p.StartWriteMask);
        string invEndMaskStr = ToBinaryString((byte)~p.EndWriteMask);

        // --- 1バイトに収まる場合の共通出力 ---
        if (p.ByteCount == 1)
        {
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            if (p.StartWriteMask == 0xFF)
                sb.AppendLine($"            return _data[{p.StartByte}];");
            else if (p.ShiftAmount == 0)
                sb.AppendLine($"            return ({p.FieldType})(_data[{p.StartByte}] & {startMaskStr});");
            else
                sb.AppendLine($"            return ({p.FieldType})((_data[{p.StartByte}] & {startMaskStr}) >> {p.ShiftAmount});");
            sb.AppendLine("        }");

            sb.AppendLine("        set");
            sb.AppendLine("        {");
            if (p.StartWriteMask == 0xFF)
                sb.AppendLine($"            _data[{p.StartByte}] = value;");
            else if (p.ShiftAmount == 0)
                sb.AppendLine($"            _data[{p.StartByte}] = (byte)((_data[{p.StartByte}] & {invStartMaskStr}) | (value & {startMaskStr}));");
            else
                sb.AppendLine($"            _data[{p.StartByte}] = (byte)((_data[{p.StartByte}] & {invStartMaskStr}) | ((value << {p.ShiftAmount}) & {startMaskStr}));");
            sb.AppendLine("        }");
            return;
        }

        // --- 複数バイトにまたがる場合の共通出力 ---
        // getの生成
        sb.AppendLine("        get");
        sb.AppendLine("        {");
        for (int i = 0; i < p.ByteCount; i++)
        {
            int currentByte = p.StartByte + i;
            if (i == 0 && p.StartWriteMask != 0xFF)
                sb.AppendLine($"            var b{i} = _data[{currentByte}] & {startMaskStr};");
            else if (i == p.ByteCount - 1 && p.EndWriteMask != 0xFF)
                sb.AppendLine($"            var b{i} = _data[{currentByte}] & {endMaskStr};");
            else
                sb.AppendLine($"            var b{i} = _data[{currentByte}];");
        }

        sb.Append("            var x = ");
        for (int i = 0; i < p.ByteCount; i++)
        {
            int shift = p.IsBigEndian
                ? (p.ByteCount - 1 - i) * 8
                : i * 8;

            if (shift == 0) sb.Append($"b{i}");
            else sb.Append($"(b{i} << {shift})");

            if (i < p.ByteCount - 1) sb.Append(" | ");
        }
        sb.AppendLine(";");

        if (p.ShiftAmount != 0) sb.AppendLine($"            return ({p.FieldType})(x >> {p.ShiftAmount});");
        else sb.AppendLine($"            return ({p.FieldType})x;");
        sb.AppendLine("        }");

        // setの生成
        sb.AppendLine("        set");
        sb.AppendLine("        {");
        if (p.ShiftAmount != 0) sb.AppendLine($"            var x = value << {p.ShiftAmount};");
        else sb.AppendLine("            var x = value;");

        for (int i = 0; i < p.ByteCount; i++)
        {
            int shift = p.IsBigEndian
                ? (p.ByteCount - 1 - i) * 8
                : i * 8;

            if (shift == 0) sb.AppendLine($"            var b{i} = x;");
            else sb.AppendLine($"            var b{i} = x >> {shift};");
        }

        for (int i = 0; i < p.ByteCount; i++)
        {
            int currentByte = p.StartByte + i;
            if (i == 0)
            {
                if (p.StartWriteMask == 0xFF)
                    sb.AppendLine($"            _data[{currentByte}] = (byte)b{i};");
                else
                    sb.AppendLine($"            _data[{currentByte}] = (byte)((_data[{currentByte}] & {invStartMaskStr}) | (b{i} & {startMaskStr}));");
            }
            else if (i == p.ByteCount - 1)
            {
                if (p.EndWriteMask == 0xFF)
                    sb.AppendLine($"            _data[{currentByte}] = (byte)b{i};");
                else
                    sb.AppendLine($"            _data[{currentByte}] = (byte)((_data[{currentByte}] & {invEndMaskStr}) | (b{i} & {endMaskStr}));");
            }
            else
            {
                sb.AppendLine($"            _data[{currentByte}] = (byte)b{i};");
            }
        }
        sb.AppendLine("        }");
    }

    #endregion

    private static byte CalculateMask8(int bitWidth, int shift)
    {
        var mask = (1 << bitWidth) - 1;
        mask <<= shift;
        return (byte)(mask & 0xFF);
    }

    private static string ToBinaryString(byte value)
    {
        string bin = Convert.ToString(value, 2).PadLeft(8, '0');
        return "0b" + bin.Insert(4, "_") + "u";
    }
}
