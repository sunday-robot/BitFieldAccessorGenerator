using libBitFieldAccessorGenerator;

namespace BitFieldAccessorGenerator;

internal class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Usage();
            return;
        }

        var nameSpace = args[0];
        foreach (var bitFieldDefintionFilePath in args.Skip(1))
            Generate(nameSpace, bitFieldDefintionFilePath);
    }

    private static void Usage()
    {
        Console.WriteLine("Usage: BitFieldAccessorGenerator <name space> <bit field definition file> ...");
    }

    static void Generate(string nameSpace, string bitFieldDefintionFilePath)
    {
        var sr = new StreamReader(bitFieldDefintionFilePath);
        var (bitFieldAccessorClassName, isBigEndian, description, fields) = Parser.Parse(sr);
        var s = Generator.Generate(nameSpace, bitFieldAccessorClassName, isBigEndian, description, fields);
        File.WriteAllText(bitFieldAccessorClassName + ".cs", s);
    }
}
