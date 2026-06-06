namespace libBitFieldAccessorGenerator;

public static class Validator
{
    public static void Validate(IReadOnlyList<(int width, string name, string description)> fieldDefinitions)
    {
        var totalBitWidth = 0;
        foreach ((var width, var _, var _) in fieldDefinitions)
        {
            if (width <= 0 || width > 32)
                throw new BitFieldDefinitionErrorException($"ビット幅 {width} は1～32の整数でなければなりません。");
            totalBitWidth += width;
        }
        if (totalBitWidth % 8 != 0)
            throw new BitFieldDefinitionErrorException($"合計ビット幅 {totalBitWidth} は8の倍数でなければなりません。");
    }
}
