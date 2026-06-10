using System.Reflection.Metadata;
using System.Text;

namespace libBitFieldAccessorGenerator;

public static class Generator
{
    // 共通出力メソッドに渡すパラメータ用の構造体
    private readonly record struct ByteMaskParams(int ByteIndex, string WriteMaskStr, string KeepMaskStr);
    private readonly record struct AccessorTemplateParams(
        string FieldType,
        int StartByte,
        int ByteCount,
        bool IsSingleByte,
        int BitOffsetInByte,      // 1バイト用
        string SingleWriteMask,   // 1バイト用
        string SingleKeepMask,    // 1バイト用
        IReadOnlyList<ByteMaskParams> MultiByteMasks, // 複数バイト用
        int RightShift,           // 複数バイト用
        IReadOnlyList<int> ByteShifts // 複数バイト用: 各b0, b1...のシフト量
    );

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
        var endianStr = isBigEndian ? "big endian" : "little endian";
        PrintSummery(sb, "", $"{totalBits} bits", endianStr, description);
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

        // 1. 各エンディアンのロジックでパラメータを計算
        AccessorTemplateParams p = isBigEndian
            ? CalculateBigEndianParams(bitIndex, fieldDefinition.width)
            : CalculateLittleEndianParams(bitIndex, fieldDefinition.width);

        // 2. 共通の出力メソッドを呼び出す
        GenerateAccessorCode(sb, p);

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
                FieldType: fieldType, StartByte: startByte, ByteCount: byteCount, IsSingleByte: true,
                BitOffsetInByte: bitOffsetInByte,
                SingleWriteMask: MaskBinary8(bitWidth, bitOffsetInByte),
                SingleKeepMask: MaskBinary8Inverse(bitWidth, bitOffsetInByte),
                MultiByteMasks: Array.Empty<ByteMaskParams>(), RightShift: 0, ByteShifts: Array.Empty<int>()
            );
        }

        var masks = new List<ByteMaskParams>();
        var byteShifts = new List<int>();
        for (int i = 0; i < byteCount; i++)
        {
            int byteIndex = startByte + i;
            int localOffset = 0;
            int bitsFromThisByte = 8;

            if (i == 0)
            {
                int startOffset = bitIndex & 7;
                bitsFromThisByte = 8 - startOffset;
            }
            else if (i == byteCount - 1)
            {
                bitsFromThisByte = (bitIndex + bitWidth) & 7;
                if (bitsFromThisByte == 0) bitsFromThisByte = 8;
                localOffset = 8 - bitsFromThisByte;
            }

            int maskValue = ((1 << bitsFromThisByte) - 1) << localOffset;
            string keepMaskStr = "0b" + Convert.ToString((~maskValue) & 0xFF, 2).PadLeft(8, '0').Insert(4, "_") + "u";
            string writeMaskStr = "0b" + Convert.ToString(maskValue & 0xFF, 2).PadLeft(8, '0').Insert(4, "_") + "u";

            masks.Add(new ByteMaskParams(byteIndex, writeMaskStr, keepMaskStr));
            byteShifts.Add((byteCount - 1 - i) * 8);
        }

        int endOffset = (bitIndex + bitWidth) & 7;
        int rightShift = (endOffset == 0) ? 0 : (8 - endOffset);

        return new AccessorTemplateParams(
            FieldType: fieldType, StartByte: startByte, ByteCount: byteCount, IsSingleByte: false,
            BitOffsetInByte: 0, SingleWriteMask: "", SingleKeepMask: "",
            MultiByteMasks: masks, RightShift: rightShift, ByteShifts: byteShifts
        );
    }

    private static AccessorTemplateParams CalculateLittleEndianParams(int bitIndex, int bitWidth)
    {
        int startByte = bitIndex >> 3;
        int endByte = (bitIndex + bitWidth - 1) >> 3;
        int byteCount = endByte - startByte + 1;
        string fieldType = GetFieldType(bitWidth);

        if (byteCount == 1)
        {
            int bitOffsetInByte = bitIndex & 7;
            return new AccessorTemplateParams(
                FieldType: fieldType, StartByte: startByte, ByteCount: byteCount, IsSingleByte: true,
                BitOffsetInByte: bitOffsetInByte,
                SingleWriteMask: MaskBinary8(bitWidth, bitOffsetInByte),
                SingleKeepMask: MaskBinary8Inverse(bitWidth, bitOffsetInByte),
                MultiByteMasks: Array.Empty<ByteMaskParams>(), RightShift: 0, ByteShifts: Array.Empty<int>()
            );
        }

        var masks = new List<ByteMaskParams>();
        var byteShifts = new List<int>();
        for (int i = 0; i < byteCount; i++)
        {
            int byteIndex = startByte + i;
            int localOffset = 0;
            int bitsFromThisByte = 8;

            if (i == 0)
            {
                localOffset = bitIndex & 7;
                bitsFromThisByte = 8 - localOffset;
            }
            else if (i == byteCount - 1)
            {
                bitsFromThisByte = (bitIndex + bitWidth) & 7;
                if (bitsFromThisByte == 0) bitsFromThisByte = 8;
            }

            int maskValue = ((1 << bitsFromThisByte) - 1) << localOffset;
            string keepMaskStr = "0b" + Convert.ToString((~maskValue) & 0xFF, 2).PadLeft(8, '0').Insert(4, "_") + "u";
            string writeMaskStr = "0b" + Convert.ToString(maskValue & 0xFF, 2).PadLeft(8, '0').Insert(4, "_") + "u";

            masks.Add(new ByteMaskParams(byteIndex, writeMaskStr, keepMaskStr));
            byteShifts.Add(i * 8);
        }

        int rightShift = bitIndex & 7;

        return new AccessorTemplateParams(
            FieldType: fieldType, StartByte: startByte, ByteCount: byteCount, IsSingleByte: false,
            BitOffsetInByte: 0, SingleWriteMask: "", SingleKeepMask: "",
            MultiByteMasks: masks, RightShift: rightShift, ByteShifts: byteShifts
        );
    }

    #endregion

    #region 共通出力ロジック

    private static void GenerateAccessorCode(StringBuilder sb, AccessorTemplateParams p)
    {
        // --- 1バイトに収まる場合の共通出力 ---
        if (p.IsSingleByte)
        {
            // get
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            if (p.BitOffsetInByte == 0)
                sb.AppendLine($"            return ({p.FieldType})(_data[{p.StartByte}] & {p.SingleWriteMask});");
            else
                sb.AppendLine($"            return ({p.FieldType})((_data[{p.StartByte}] & {p.SingleWriteMask}) >> {p.BitOffsetInByte});");
            sb.AppendLine("        }");

            // set
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            if (p.BitOffsetInByte == 0)
                sb.AppendLine($"            _data[{p.StartByte}] = (byte)((_data[{p.StartByte}] & {p.SingleKeepMask}) | (value & {p.SingleWriteMask}));");
            else
                sb.AppendLine($"            _data[{p.StartByte}] = (byte)((_data[{p.StartByte}] & {p.SingleKeepMask}) | ((value << {p.BitOffsetInByte}) & {p.SingleWriteMask}));");
            sb.AppendLine("        }");
            return;
        }

        // --- 複数バイトにまたがる場合の共通出力 ---
        // getの生成
        sb.AppendLine("        get");
        sb.AppendLine("        {");
        for (int i = 0; i < p.ByteCount; i++)
        {
            sb.AppendLine($"            var b{i} = _data[{p.MultiByteMasks[i].ByteIndex}] & {p.MultiByteMasks[i].WriteMaskStr};");
        }
        sb.Append("            var x = ");
        for (int i = 0; i < p.ByteCount; i++)
        {
            int totalShift = p.ByteShifts[i];
            if (totalShift == 0) sb.Append($"b{i}");
            else sb.Append($"(b{i} << {totalShift})");

            if (i < p.ByteCount - 1) sb.Append(" | ");
        }
        sb.AppendLine(";");
        if (p.RightShift == 0) sb.AppendLine($"            return ({p.FieldType})x;");
        else sb.AppendLine($"            return ({p.FieldType})(x >> {p.RightShift});");
        sb.AppendLine("        }");

        // setの生成
        sb.AppendLine("        set");
        sb.AppendLine("        {");
        if (p.RightShift == 0) sb.AppendLine("            var x = value;");
        else sb.AppendLine($"            var x = value << {p.RightShift};");

        for (int i = 0; i < p.ByteCount; i++)
        {
            int totalShift = p.ByteShifts[i];
            if (totalShift == 0) sb.AppendLine($"            var b{i} = x;");
            else sb.AppendLine($"            var b{i} = x >> {totalShift};");
        }
        for (int i = 0; i < p.ByteCount; i++)
        {
            sb.AppendLine($"            _data[{p.MultiByteMasks[i].ByteIndex}] = (byte)((_data[{p.MultiByteMasks[i].ByteIndex}] & {p.MultiByteMasks[i].KeepMaskStr}) | (b{i} & {p.MultiByteMasks[i].WriteMaskStr}));");
        }
        sb.AppendLine("        }");
    }

    #endregion

    private static string MaskBinary8(int bitWidth, int shift)
    {
        var mask = (1 << bitWidth) - 1;
        mask <<= shift;
        string bin = Convert.ToString(mask & 0xFF, 2).PadLeft(8, '0');
        return "0b" + bin.Insert(4, "_") + "u";
    }

    static string ByteToString(byte b)
    {
        return "0b" + Convert.ToString(b, 2).PadLeft(8, '0').Insert(4, "_") + "u";
    }

    private static string MaskBinary8Inverse(int bitWidth, int shift)
    {
        int mask = ((1 << bitWidth) - 1) << shift;
        int inv = (~mask) & 0xFF;
        string bin = Convert.ToString(inv, 2).PadLeft(8, '0');
        return "0b" + bin.Insert(4, "_") + "u";
    }
}