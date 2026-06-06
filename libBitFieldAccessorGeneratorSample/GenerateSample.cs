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
                    "テスト",
                    [
                        (1, "B1", ""), // 0
                        (2, "", ""), // 1
                        (7, "B7", ""), // 3
                        (8, "B8", ""), // 10
                        (9, "W9", ""), // 18
                        (15, "W15", ""), // 27
                        (16, "W16", ""), // 42
                        (17, "Dw17", ""), // 58
                        (31, "Dw31", ""), // 75
                        (32, "Dw32", ""), // 106
                        (6, "Xyz", ""), // 138
                    ]);
    }

    public static string Test1BitLE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test1BitLE",
            false,
            "テスト",
            [
                (1, "B10", ""), // 0
                (1, "B11", ""), // 1
                (1, "B12", ""), // 1
                (1, "B13", ""), // 1
                (1, "B14", ""), // 1
                (1, "B15", ""), // 1
                (1, "B16", ""), // 1
                (1, "B17", ""), // 1
            ]);
    }

    public static string Test1BitBE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test1BitBE",
            true,
           "テスト",
            [
                (1, "B10", ""), // 0
                (1, "B11", ""), // 1
                (1, "B12", ""), // 1
                (1, "B13", ""), // 1
                (1, "B14", ""), // 1
                (1, "B15", ""), // 1
                (1, "B16", ""), // 1
                (1, "B17", ""), // 1
            ]);
    }

    public static string Test2BitLE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test2BitLE",
            false,
            "テスト",
            [
                (2, "B200", ""),    // & 0000_0011
                (2, "B202", ""),    // & 0000_1100
                (2, "B204", ""),    // & 0011_0000
                (2, "B206", ""),    // & 1100_0000
                (7, "", ""), // 8
                (2, "B215", ""),    // & 1000_0000, 0000_0001
                (7, "", ""), // 17
            ]);
    }

    public static string Test2BitBE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test2BitBE",
            true,
            "テスト",
            [
                (2, "B200", ""),    // & 1100_0000
                (2, "B202", ""),    // & 0011_0000
                (2, "B204", ""),    // & 0000_1100
                (2, "B206", ""),    // & 0000_0011
                (7, "", ""), // 8
                (2, "B215", ""),    // & 0000_0001, 1000_0000
                (7, "", ""), // 17
            ]);
    }

    public static string Test3BitLE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test3BitLE",
            false,
            "テスト",
            [
                (3, "B300", ""),    // & 0000_0111
                (3, "B303", ""),    // & 0011_1000
                (3, "B306", ""),    // & 1100_0000, 0000_0001
                (7, "", ""), // 9
            ]);
    }

    public static string Test3BitBE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test3BitBE",
            true,
            "テスト",
            [
                (3, "B300", ""),    // & 1110_0000
                (3, "B303", ""),    // & 0001_1100
                (3, "B306", ""),    // & 0000_0011, 1000_0000
                (7, "", ""), // 9
            ]);
    }

    public static string Test10BitLE()
    {
        return Generator.Generate(
            "ns1.ns2",
            "Test10BitLE",
            false,
            "テスト",
            [
                (10, "B100", ""),   // & 1111_1111, 0000_0011
                (5, "", ""),    // 10
                (10, "B115", ""),   // & 1000_0000, 1111_1111, 0000_0001
                (7, "", ""), // 25
            ]);
    }
}
