namespace libBitFieldAccessorGeneratorSample;

public class ParseAndGenerateSample
{
    public static void TestAll()
    {
        Test("Test");
    }

    public static void Test(string name)
    {
        var sr = new StreamReader(name + ".txt");
        var (bitFieldAccessorClassName, isBigEndian, fields) = libBitAccessorGenerator.Parser.Parse(sr);
        var s = libBitAccessorGenerator.Generator.Generate(
            "ns1.ns2",
            bitFieldAccessorClassName,
            isBigEndian,
            fields);
        Console.WriteLine($"========== {name} ==========");
        Console.WriteLine(s);
        Console.WriteLine();
        File.WriteAllText(name + ".cs", s);
    }
}
