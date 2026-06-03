namespace libBitFieldAccessorGenerator;

[Serializable]
public class BitFieldDefinitionErrorException : Exception
{
    public BitFieldDefinitionErrorException()
    {
    }

    public BitFieldDefinitionErrorException(string? message) : base(message)
    {
    }

    public BitFieldDefinitionErrorException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}