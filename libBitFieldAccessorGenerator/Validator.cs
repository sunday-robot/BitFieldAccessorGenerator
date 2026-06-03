namespace libBitFieldAccessorGenerator;

public static class Validator
{
    public static void Validate(IReadOnlyList<(string bitFieldName, int bitWidth)> fieldDefinitions)
    {
        var totalBitWidth = 0;
        foreach ((var _, var bitWidth) in fieldDefinitions)
        {
            if (bitWidth <= 0 || bitWidth > 32)
                throw new BitFieldDefinitionErrorException($"ビット幅 {bitWidth} は1～32の整数でなければなりません。");
            totalBitWidth += bitWidth;
        }
        if (totalBitWidth % 8 != 0)
            throw new BitFieldDefinitionErrorException($"合計ビット幅 {totalBitWidth} は8の倍数でなければなりません。");
    }
}
