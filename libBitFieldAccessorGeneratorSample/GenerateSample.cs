using libBitFieldAccessorGenerator;

namespace libBitFieldAccessorGeneratorSample;

public static class GenerateSample
{
    public static void TestAll()
    {
        Test(Test1BitLE(), "Test1BitLE");
        Test(Test1BitBE(), "Test1BitBE");
        Test(Test2BitLE(), "Test2BitLE");
        Test(Test2BitBE(), "Test2BitBE");
        Test(Test3BitLE(), "Test3BitLE");
        Test(Test3BitBE(), "Test3BitBE");
        Test(Test10BitLE(), "Test10BitLE");
        //var s = Test2Bit();
    }

    public static void Test(string s, string name)
    {
        Console.WriteLine($"========== {name} ==========");
        Console.WriteLine(s);
        Console.WriteLine();
        File.WriteAllText(name + ".cs", s);
    }

    public static string TestX()
    {
        return Generator.Generate(
                    "ns1.ns2",
                    "Test",
                    false,
                    [
                        ("B1", 1), // 0
            ("", 2), // 1
            ("B7", 7), // 3
            ("B8", 8), // 10
            ("W9", 9), // 18
            ("W15", 15), // 27
            ("W16", 16), // 42
            ("Dw17", 17), // 58
            ("Dw31", 31), // 75
            ("Dw32", 32), // 106
            ("Xyz", 6), // 138
                    ]);
    }

    public static string Test1BitLE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test1BitLE",
            false,
            [
                ("B10", 1), // 0
            ("B11", 1), // 1
            ("B12", 1), // 1
            ("B13", 1), // 1
            ("B14", 1), // 1
            ("B15", 1), // 1
            ("B16", 1), // 1
            ("B17", 1), // 1
            ]);
    }

    public static string Test1BitBE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test1BitBE",
            true,
            [
                ("B10", 1), // 0
            ("B11", 1), // 1
            ("B12", 1), // 1
            ("B13", 1), // 1
            ("B14", 1), // 1
            ("B15", 1), // 1
            ("B16", 1), // 1
            ("B17", 1), // 1
            ]);
    }

    public static string Test2BitLE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test2BitLE",
            false,
            [
                ("B200", 2),    // & 0000_0011
            ("B202", 2),    // & 0000_1100
            ("B204", 2),    // & 0011_0000
            ("B206", 2),    // & 1100_0000
            ("", 7), // 8
            ("B215", 2),    // & 1000_0000, 0000_0001
            ("", 7), // 17
            ]);
    }

    public static string Test2BitBE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test2BitBE",
            true,
            [
                ("B200", 2),    // & 1100_0000
            ("B202", 2),    // & 0011_0000
            ("B204", 2),    // & 0000_1100
            ("B206", 2),    // & 0000_0011
            ("", 7), // 8
            ("B215", 2),    // & 0000_0001, 1000_0000
            ("", 7), // 17
            ]);
    }

    public static string Test3BitLE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test3BitLE",
            false,
            [
                ("B300", 3),    // & 0000_0111
            ("B303", 3),    // & 0011_1000
            ("B306", 3),    // & 1100_0000, 0000_0001
            ("", 7), // 9
            ]);
    }

    public static string Test3BitBE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test3BitBE",
            true,
            [
                ("B300", 3),    // & 1110_0000
            ("B303", 3),    // & 0001_1100
            ("B306", 3),    // & 0000_0011, 1000_0000
            ("", 7), // 9
            ]);
    }

    public static string Test10BitLE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test10BitLE",
            false,
            [
                ("B100", 10),   // & 1111_1111, 0000_0011
            ("", 5),    // 10
            ("B115", 10),   // & 1000_0000, 1111_1111, 0000_0001
            ("", 7), // 25
            ]);
    }
}
