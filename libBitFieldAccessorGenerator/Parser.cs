namespace libBitFieldAccessorGenerator;

public static class Parser
{
    public static (string bitFieldAccessorClassName, bool isBigEndian, List<(string bitFieldName, int bitWidth)> fields)
        Parse(TextReader tr)
    {
        var headerLine = ReadLine(tr)
            ?? throw new FormatException("クラス名行が存在しません。");
        var (className, isBigEndian) = ParseHeader(headerLine);
        var fields = new List<(string bitFieldName, int bitWidth)>();
        string? line;
        while ((line = ReadLine(tr)) != null)
            fields.Add(ParseField(line));

        return (className, isBigEndian, fields);
    }

    static string? ReadLine(TextReader tr)
    {
        while (true)
        {
            var raw = tr.ReadLine();
            if (raw == null)
                return null;
            var line = raw.Split('#')[0].Trim();
            if (line.Length == 0)
                continue;
            return line;
        }
    }

    static (string className, bool isBigEndian) ParseHeader(string line)
    {
        var parts = SplitCsv(line);

        string className = parts[0];
        string endian = parts[1].ToLowerInvariant();

        bool isBigEndian = endian switch
        {
            "bigendian" => true,
            "littleendian" => false,
            _ => throw new FormatException($"エンディアン指定が不正です: {parts[1]}")
        };

        return (className, isBigEndian);
    }

    static (string bitFieldName, int bitWidth) ParseField(string line)
    {
        var parts = SplitCsv(line);

        string name = parts[0]; // 空なら reserved
        int width = int.Parse(parts[1]);

        return (name, width);
    }

    static string[] SplitCsv(string line)
    {
        var parts = line.Split(',');
        if (parts.Length != 2)
            throw new FormatException($"CSV形式が不正です: {line}");

        return
        [
            parts[0].Trim(),
            parts[1].Trim()
        ];
    }
}
