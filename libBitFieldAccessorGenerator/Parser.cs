namespace libBitFieldAccessorGenerator;

public static class Parser
{
    public static (string bitFieldAccessorClassName, bool isBigEndian, string description, List<(int width, string name, string description)> fields)
        Parse(TextReader tr)
    {
        var headerLine = ReadLine(tr)
            ?? throw new FormatException("クラス名行が存在しません。");
        var (className, isBigEndian, description) = ParseHeader(headerLine);
        var fields = new List<(int width, string name, string description)>();
        string? line;
        while ((line = ReadLine(tr)) != null)
            fields.Add(ParseField(line));
        return (className, isBigEndian, description, fields);
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

    static (string className, bool isBigEndian, string description) ParseHeader(string line)
    {
        string className;
        string endian;
        string description;

        var parts = SplitCsv(line);
        switch (parts.Length)
        {
            case 2:
                className = parts[0];
                endian = parts[1];
                description = "";
                break;
            case 3:
                className = parts[0];
                endian = parts[1];
                description = parts[2];
                break;
            default:
                throw new FormatException($"クラス名行の形式が不正です: {line}");
        }

        bool isBigEndian = endian.ToLowerInvariant() switch
        {
            "bigendian" => true,
            "littleendian" => false,
            _ => throw new FormatException($"エンディアン指定が不正です: {parts[1]}")
        };

        return (className, isBigEndian, description);
    }

    static (int width, string name, string description) ParseField(string line)
    {
        int width;
        string name;
        string description;

        var parts = SplitCsv(line);
        switch (parts.Length)
        {
            case 1:
                width = int.Parse(parts[0]);
                name = "";
                description = "";
                break;
            case 2:
                width = int.Parse(parts[0]);
                name = parts[1];
                description = "";
                break;
            case 3:
                width = int.Parse(parts[0]);
                name = parts[1];
                description = parts[2];
                break;
            default:
                throw new FormatException($"フィールド行の形式が不正です: {line}");
        }

        return (width, name, description);
    }

    static string[] SplitCsv(string line)
    {
        var columns = line.Split(',');
        for (int i = 0; i < columns.Length; i++)
        {
            columns[i] = columns[i].Trim();
        }
        return columns;
    }
}
