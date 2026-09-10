using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using GelGenerator;

internal static class TestGenerator
{
    private static int Main()
    {
        try { return Run(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().FullName);
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Run()
    {
        var variables = new List<VariableItem>
        {
            new VariableItem { Name = "angle_yaw", Expression = "gTelemetry.attitude.yaw", Type = "float32", OutputFormat = "十进制" },
            new VariableItem { Name = "ratio", Expression = "gRatio", Type = "float64", OutputFormat = "十进制" },
            new VariableItem { Name = "state", Expression = "gState", Type = "int8", OutputFormat = "十进制" },
            new VariableItem { Name = "count", Expression = "gCount", Type = "uint32", OutputFormat = "十进制" },
            new VariableItem { Name = "flags", Expression = "gFlags", Type = "uint8", OutputFormat = "十六进制" },
            new VariableItem { Name = "mode", Expression = "gModes[0].state", Type = "enum32", OutputFormat = "十进制" }
        };
        string gel = GelTemplate.Build("C:\\Data Folder\\log.csv", 10000, variables);
        AssertContains(gel, "string DL_CSV_PATH = \"C:\\\\Data Folder\\\\log.csv\";");
        AssertContains(gel, "sample,angle_yaw,ratio,state,count,flags,mode\\n");
        AssertContains(gel, "long double dl_v1;");
        AssertContains(gel, "unsigned long dl_v3;");
        AssertContains(gel, "long dl_v5;");
        AssertContains(gel, "long DL_SAMPLE_PERIOD_MS = 10000L;");
        AssertContains(gel, "long __dl_sample_count = 0L;");
        AssertContains(gel, "dl_v0 = 'SYM'::gTelemetry.attitude.yaw;");
        AssertContains(gel, "GEL_TextOut(\"%d,\",,,,,'GEL'::__dl_sample_count);");
        AssertContains(gel, "GEL_TextOut(\"%f,\",,,,,dl_v0);");
        AssertContains(gel, "GEL_TextOut(\"%f,\",,,,,dl_v1);");
        AssertContains(gel, "GEL_TextOut(\"%d,\",,,,,dl_v2);");
        AssertContains(gel, "GEL_TextOut(\"%u,\",,,,,dl_v3);");
        AssertContains(gel, "GEL_TextOut(\"%x,\",,,,,dl_v4);");
        AssertContains(gel, "GEL_TextOut(\"%d\\n\",,,,,dl_v5);");
        AssertNotContains(gel, "\", ,,,,,");
        GelTemplate.ValidateGeneratedScript(gel, variables.Count);
        TestTypeValidationAndMigration();
        TestCsvConversion();
        Console.WriteLine("Generator tests passed.");
        return 0;
    }

    private static void TestTypeValidationAndMigration()
    {
        VariableItem legacy = GelTemplate.MigrateLegacy(new VariableItem { Name = "u", Expression = "gU", Type = "hex" });
        if (legacy.Type != "uint16" || legacy.OutputFormat != "十六进制")
            throw new Exception("Legacy hex migration failed.");

        VariableItem verbose = GelTemplate.MigrateLegacy(new VariableItem { Type = "uint32（uint32_t/U32/Uint32/unsigned long）", OutputFormat = "十进制" });
        if (verbose.Type != "uint32") throw new Exception("Verbose type migration failed.");

        VariableItem scientific = GelTemplate.MigrateLegacy(new VariableItem { Type = "float32", OutputFormat = "科学计数法" });
        if (scientific.OutputFormat != "十进制") throw new Exception("Scientific format migration failed.");

        bool int64Rejected = false;
        try { GelTemplate.MigrateLegacy(new VariableItem { Type = "uint64（当前禁用：GEL 无 64 位整数格式）", OutputFormat = "十进制" }); }
        catch (InvalidOperationException) { int64Rejected = true; }
        if (!int64Rejected) throw new Exception("Legacy 64-bit integer must be rejected.");

        bool floatHexRejected = false;
        try { GelTemplate.ValidateTypeAndFormat("float32", "十六进制"); }
        catch (InvalidOperationException) { floatHexRejected = true; }
        if (!floatHexRejected) throw new Exception("Float hexadecimal format must be rejected.");

        bool legacyEnumRejected = false;
        try { GelTemplate.MigrateLegacy(new VariableItem { Type = "enum", OutputFormat = "十进制" }); }
        catch (InvalidOperationException) { legacyEnumRejected = true; }
        if (!legacyEnumRejected) throw new Exception("Ambiguous legacy enum must be rejected.");
    }

    private static void TestCsvConversion()
    {
        string csv =
            "C28xx_CPU1: GEL Output: sample,C28xx_CPU1 : angle_yaw,GEL Output: state\r\n" +
            "C28xx_CPU1: GEL Output: 0,C28xx_CPU1: GEL Output: 1.25,C28xx_CPU1 : 2\r\n" +
            "Cortex_M3_0: GEL Output: 1,Cortex_M3_0: 2.5,GEL Output: 3\r\n";
        List<List<string>> rows = CcsCsvConverter.ParseAndNormalize(csv);
        if (rows.Count != 3 || rows[0].Count != 3) throw new Exception("CSV normalized shape is incorrect.");
        if (rows[0][0] != "sample" || rows[0][1] != "angle_yaw" || rows[0][2] != "state")
            throw new Exception("CSV prefixes were not removed from headers.");
        if (rows[1][0] != "0" || rows[1][1] != "1.25" || rows[2][2] != "3")
            throw new Exception("CSV values were changed during cleanup.");

        string splitOutput =
            "C28xx_CPU1: GEL Output: sample,name,value\n" +
            "C28xx_CPU1: GEL Output: 0,\n" +
            "C28xx_CPU1: GEL Output: \"motor, yaw\",\n" +
            "C28xx_CPU1: GEL Output: -1.5e-3\n";
        List<List<string>> splitRows = CcsCsvConverter.ParseAndNormalize(splitOutput);
        if (splitRows.Count != 2 || splitRows[1][1] != "motor, yaw" || splitRows[1][2] != "-1.5e-3")
            throw new Exception("Split GEL output or quoted CSV field was not reconstructed correctly.");

        bool incompleteRejected = false;
        try { CcsCsvConverter.ParseAndNormalize("sample,a,b\n0,1"); }
        catch (InvalidDataException) { incompleteRejected = true; }
        if (!incompleteRejected) throw new Exception("Incomplete sample row was not rejected.");

        string path = Path.Combine(Path.GetTempPath(), "GelGeneratorTest-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            XlsxWriter.Write(path, rows);
            using (ZipArchive zip = ZipFile.OpenRead(path))
            {
                if (zip.GetEntry("xl/workbook.xml") == null || zip.GetEntry("xl/worksheets/sheet1.xml") == null)
                    throw new Exception("XLSX package is missing required parts.");
                using (var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml").Open()))
                {
                    string xml = reader.ReadToEnd();
                    AssertContains(xml, "<v>1.25</v>");
                    AssertContains(xml, "<v>3</v>");
                    AssertNotContains(xml, "C28xx_CPU1");
                    AssertNotContains(xml, "GEL Output");
                }
            }

            var precisionRows = new List<List<string>>
            {
                new List<string> { "safe", "signed64", "unsigned64", "float" },
                new List<string> { "999999999999999", "-9223372036854775808", "18446744073709551615", "1.25e-3" }
            };
            XlsxWriter.Write(path, precisionRows);
            using (ZipArchive zip = ZipFile.OpenRead(path))
            using (var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml").Open()))
            {
                string xml = reader.ReadToEnd();
                AssertContains(xml, "<v>999999999999999</v>");
                AssertContains(xml, "<t xml:space=\"preserve\">-9223372036854775808</t>");
                AssertContains(xml, "<t xml:space=\"preserve\">18446744073709551615</t>");
                AssertContains(xml, "<v>0.00125</v>");
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static void AssertNotContains(string text, string unexpected)
    {
        if (text.Contains(unexpected))
            throw new Exception("Unexpected text: " + unexpected);
    }

    private static void AssertContains(string text, string expected)
    {
        if (!text.Contains(expected))
            throw new Exception("Missing expected text: " + expected);
    }
}
