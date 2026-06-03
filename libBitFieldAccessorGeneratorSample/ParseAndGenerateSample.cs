using libBitFieldAccessorGenerator;

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
        var (bitFieldAccessorClassName, isBigEndian, fields) = Parser.Parse(sr);
        var s = Generator.Generate(
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
