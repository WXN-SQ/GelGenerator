using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
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
        AssertContains(gel, "#define DL_CSV_PATH \"C:\\\\Data Folder\\\\log.csv\"");
        AssertNotContains(gel, "string DL_CSV_PATH");
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
        TestBridgeParsingAndTemplate();
        TestBridgeEndToEnd();
        TestBridgeReplay();
        TestBridgeConfigCompatibility();
        TestVofaCommandConfig();
        TestInputBridge();
        TestInputBridgeEndToEnd();
        TestCsvConversion();
        Console.WriteLine("Generator tests passed.");
        return 0;
    }

    private static void TestInputBridgeEndToEnd()
    {
        string path = Path.Combine(Path.GetTempPath(), "GelInputBridge-" + Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllText(path, "sample,x\n", Encoding.ASCII);
        var options = new BridgeOptions { CsvPath=path, Address=IPAddress.Loopback, Port=0, ControlPort=0, ReplayExisting=false,
            PeriodMs=10, ExpectedColumns=2, SelectedColumns=new[]{1}, HexColumns=new bool[2], WriteEnabled=true,
            WritableVariables=new[]{new VariableItem{Direction="input",Name="setpoint",Expression="gSetpoint",Type="uint32"}} };
        VofaBridgeService service=null; TcpClient vofa=null; TcpClient adapter=null; Thread fake=null; BridgeStatus latest=null;
        try {
            service=new VofaBridgeService(options, delegate(BridgeStatus s){latest=s;}); service.Start();
            adapter=new TcpClient(); adapter.Connect(IPAddress.Loopback, service.BoundControlPort);
            TcpClient captured=adapter;
            fake=new Thread(delegate(){byte[] status=Encoding.UTF8.GetBytes("STATUS,READY,C28xx_CPU1\n");captured.GetStream().Write(status,0,status.Length);var reader=new StreamReader(captured.GetStream(),Encoding.UTF8,false,1024,true);string request=reader.ReadLine();if(request!="SET,setpoint,4294967295")throw new Exception("Adapter request incorrect: "+request);byte[] reply=Encoding.ASCII.GetBytes("ACK,setpoint,4294967295\n");captured.GetStream().Write(reply,0,reply.Length);}); fake.Start();
            vofa=new TcpClient(); vofa.Connect(IPAddress.Loopback,service.BoundPort); vofa.GetStream().ReadTimeout=3000;
            byte[] command=Encoding.ASCII.GetBytes("plot0SET,setpoint,4294967295\n");vofa.GetStream().Write(command,0,command.Length);
            DateTime deadline=DateTime.UtcNow.AddSeconds(3);while((latest==null||latest.Writes<1)&&DateTime.UtcNow<deadline)Thread.Sleep(20);
            if(latest==null||latest.Writes!=1||latest.LastResponse!="ACK,setpoint,4294967295")throw new Exception("Single-connection command result was not recorded.");
            if(vofa.Available!=0)throw new Exception("Text ACK polluted the FireWater connection.");
            fake.Join(3000);
            if(latest==null || latest.TargetSessionStatus!="C28xx_CPU1，已就绪")throw new Exception("Adapter target-session status was not reported.");
        } finally { if(vofa!=null)vofa.Close();if(adapter!=null)adapter.Close();if(service!=null)service.Dispose();if(File.Exists(path))File.Delete(path); }
    }

    private static void TestInputBridge()
    {
        string value;
        if (!InputValueValidator.TryNormalize("uint32", "4294967295", out value) || value != "4294967295")
            throw new Exception("uint32 maximum was rejected or truncated.");
        if (InputValueValidator.TryNormalize("uint32", "4294967296", out value)) throw new Exception("uint32 overflow accepted.");
        if (!InputValueValidator.TryNormalize("int8", "-128", out value) || InputValueValidator.TryNormalize("int8", "128", out value))
            throw new Exception("int8 boundary validation failed.");
        if (InputValueValidator.TryNormalize("bool", "2", out value)) throw new Exception("invalid bool accepted.");
        if (InputValueValidator.TryNormalize("float32", "NaN", out value)) throw new Exception("NaN accepted.");
        if (!InputValueValidator.TryNormalize("uint32", "0xFFFFFFFF", out value) || value != "4294967295")
            throw new Exception("uint32 hexadecimal maximum failed.");
        if (InputValueValidator.TryNormalize("uint32", "0x100000000", out value)) throw new Exception("uint32 hexadecimal overflow accepted.");
        if (!InputValueValidator.TryNormalize("int8", "-0x80", out value) || value != "-128") throw new Exception("signed hexadecimal input failed.");
        if (InputValueValidator.TryNormalize("int8", "0xFF", out value)) throw new Exception("signed hexadecimal overflow accepted.");
        if (!InputValueValidator.TryNormalize("bool", "0x1", out value) || InputValueValidator.TryNormalize("bool", "0x2", out value))
            throw new Exception("hexadecimal bool validation failed.");
        if (InputValueValidator.TryNormalize("float32", "0x10", out value)) throw new Exception("hexadecimal float accepted.");
        var options = new BridgeOptions { ControlPort = 1348, CorePattern = "C28xx_CPU1", WritableVariables = new[] {
            new VariableItem { Direction="input", Name="yaw_ref", Expression="gPositionControlObj[0].positionRef", Type="float32" },
            new VariableItem { Direction="inout", Name="count", Expression="gCount", Type="uint32" }
        }};
        string legacy = CcsInputAdapterTemplate.Build("CCS 12.7.1", options);
        AssertContains(legacy, "activeDS.expression.evaluate"); AssertContains(legacy, "gPositionControlObj[0].positionRef");
        string modern = CcsInputAdapterTemplate.Build("CCS 20.x", options);
        AssertContains(modern, "session.expressions"); AssertContains(modern, "activeDS.expression"); AssertContains(modern, "require('net')");
        AssertContains(modern, "initScripting({inProcess:true})");
        AssertContains(modern, "scripting.listCores()");
        AssertContains(modern, "scripting.openSession(matches[0])");
        AssertContains(modern, "C28xx_CPU1");
        AssertContains(modern, "STATUS,READY");
        AssertContains(modern, "root.__gelGeneratorInputBridge");
        AssertContains(modern, "previous.stop()");
        AssertContains(modern, "setTimeout(function(){state.timer=null;connect();},1000)");
        AssertContains(modern, "var plain=item.expr+' = '+value");
        AssertContains(modern, "casted=item.expr+' = ('+item.cast+')('+value+')'");
        AssertContains(modern, "NO_DEBUG_SESSION");
        AssertContains(modern, "ASSIGN_FAILED");
        AssertContains(modern, "READBACK_FAILED");
        AssertNotContains(modern, "const socket");
        var allInput = new List<VariableItem>();
        string gel = GelTemplate.Build("C:\\x.csv", 100, allInput, "CCS 5.5");
        AssertContains(gel, "#define DL_CSV_PATH \"C:\\\\x.csv\"");
        AssertNotContains(gel, "string DL_CSV_PATH");
        AssertContains(gel, "CCS 5.5 GEL/JTAG"); AssertContains(gel, "GEL_TextOut(\"sample\\n\")");
        AssertContains(gel, "GEL_TextOut(\"%d\\n\",,,,,'GEL'::__dl_sample_count);");
        GelTemplate.ValidateGeneratedScript(gel, 0);
        string gelWithDialogs = GelTemplate.Build("C:\\x.csv", 100, allInput, "CCS 12.7.1", options.WritableVariables);
        AssertContains(gelWithDialogs, "menuitem \"Target Variable Input\"");
        AssertContains(gelWithDialogs, "dialog TargetInput_Set_1_yaw_ref(float value)");
        AssertContains(gelWithDialogs, "'SYM'::gPositionControlObj[0].positionRef = value;");
        AssertContains(gelWithDialogs, "dialog TargetInput_Set_2_count(unsigned long value)");
    }

    private static void TestBridgeReplay()
    {
        string path = Path.Combine(Path.GetTempPath(), "GelBridgeReplay-" + Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllText(path, "sample,a\n0,10\n1,11\n", new UTF8Encoding(false));
        var options = new BridgeOptions
        {
            CsvPath = path, Address = IPAddress.Loopback, Port = 0, ReplayExisting = true,
            PeriodMs = 10, ExpectedColumns = 2, SelectedColumns = new[] { 1 }, HexColumns = new bool[2]
        };
        VofaBridgeService service = null;
        TcpClient client = null;
        try
        {
            service = new VofaBridgeService(options, delegate { });
            service.Start();
            Thread.Sleep(200);
            client = new TcpClient();
            client.Connect(IPAddress.Loopback, service.BoundPort);
            client.GetStream().ReadTimeout = 3000;
            using (var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, true))
            {
                if (reader.ReadLine() != "0,10" || reader.ReadLine() != "1,11")
                    throw new Exception("Historical replay payload is incorrect.");
            }
        }
        finally
        {
            if (client != null) client.Close();
            if (service != null) service.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void TestBridgeConfigCompatibility()
    {
        string legacyJson = "{\"CsvPath\":\"C:\\\\x.csv\",\"PeriodValue\":100,\"PeriodUnit\":\"ms\",\"GelPath\":\"C:\\\\x.gel\",\"VofaCommandPort\":1348,\"Variables\":[{\"Name\":\"x\",\"Expression\":\"gX\",\"Type\":\"float32\",\"OutputFormat\":\"十进制\"}]}";
        GeneratorConfig legacy = new JavaScriptSerializer().Deserialize<GeneratorConfig>(legacyJson);
        if (legacy.VofaEnabled || legacy.VofaPort != 0 || legacy.Variables[0].SendToVofa)
            throw new Exception("Legacy config must keep VOFA bridge disabled.");

        var current = new GeneratorConfig
        {
            CsvPath = "x.csv", GelPath = "x.gel", PeriodValue = 100, PeriodUnit = "ms",
            VofaEnabled = true, VofaAddress = "127.0.0.1", VofaPort = 1347, CcsCorePattern = "C28xx_CPU1", VofaReplayExisting = true,
            GenerateVofaCommandConfig = true,
            Variables = new List<VariableItem> { new VariableItem { Name = "x", Expression = "gX", Type = "float32", OutputFormat = "十进制", SendToVofa = true } }
        };
        GeneratorConfig roundTrip = new JavaScriptSerializer().Deserialize<GeneratorConfig>(new JavaScriptSerializer().Serialize(current));
        if (!roundTrip.VofaEnabled || !roundTrip.GenerateVofaCommandConfig || roundTrip.VofaPort != 1347 || roundTrip.CcsCorePattern != "C28xx_CPU1" || !roundTrip.Variables[0].SendToVofa)
            throw new Exception("VOFA bridge config round-trip failed.");
    }

    private static void TestVofaCommandConfig()
    {
        var variables = new List<VariableItem>
        {
            new VariableItem { Direction="input", Name="workMode", Expression="gMode", Type="uint8", OutputFormat="十六进制" },
            new VariableItem { Direction="input", Name="offset", Expression="gOffset", Type="int16", OutputFormat="十进制" },
            new VariableItem { Direction="inout", Name="yawRef", Expression="gYaw", Type="uint32", OutputFormat="十进制" },
            new VariableItem { Direction="inout", Name="gain", Expression="gGain", Type="float32", OutputFormat="十进制" }
        };
        string json = VofaCommandConfigTemplate.Build("sample", variables);
        AssertContains(json, "\"type\":\"cmds\"");
        AssertContains(json, "input 输入变量");
        AssertContains(json, "inout 双向变量");
        AssertContains(json, "53 45 54 2C 77 6F 72 6B 4D 6F 64 65 2C 25 75 0A");
        AssertContains(json, "53 45 54 2C 6F 66 66 73 65 74 2C 25 64 0A");
        AssertContains(json, "53 45 54 2C 67 61 69 6E 2C 25 66 0A");
        AssertContains(json, "动态命令按十进制占位符发送");
        AssertNotContains(json, "25 58");
        AssertNotContains(json, "25 78");
        AssertContains(json, "\"hex_on\":false");
        if (!VofaCommandConfigTemplate.OutputPath("C:\\data\\logger.gel").EndsWith("logger_vofa.cmds.json"))
            throw new Exception("VOFA command config output path is incorrect.");
    }

    private static void TestBridgeParsingAndTemplate()
    {
        string frame, error;
        bool header, mismatch;
        bool ok = CcsCsvConverter.TryBuildFireWaterFrame(
            "C28xx_CPU1 : 0,C28xx_CPU1 : 3805.0,C28xx_CPU1 : 3956.0,C28xx_CPU1 : 29.734375,C28xx_CPU1 : 30.8984375",
            5, new[] { 1, 4 }, new bool[5], out frame, out header, out mismatch, out error);
        if (!ok || frame != "0,3805,30.8984375") throw new Exception("FireWater frame cleanup failed: " + error);

        ok = CcsCsvConverter.TryBuildFireWaterFrame("CPU-X : 1,CPU-X : ff", 2, new[] { 1 }, new[] { false, true }, out frame, out header, out mismatch, out error);
        if (!ok || frame != "1,255") throw new Exception("Hex FireWater conversion failed.");

        ok = CcsCsvConverter.TryBuildFireWaterFrame("sample,a,b", 3, new[] { 1 }, new bool[3], out frame, out header, out mismatch, out error);
        if (ok || !header || mismatch) throw new Exception("Bridge header recognition failed.");

        var options = new BridgeOptions
        {
            CsvPath = "C:\\Data Folder\\log.csv", Address = IPAddress.Loopback, Port = 1347,
            ReplayExisting = false, PeriodMs = 100, ExpectedColumns = 5,
            SelectedColumns = new[] { 1, 4 }, HexColumns = new bool[5], ChannelNames = new[] { "sample", "a", "d" }
        };
        string ps = BridgePowerShellTemplate.Build(options);
        AssertContains(ps, "$expectedColumns = 5");
        AssertContains(ps, "$selectedColumns = @(1,4)");
        AssertContains(ps, "127.0.0.1");
    }

    private static void TestBridgeEndToEnd()
    {
        string path = Path.Combine(Path.GetTempPath(), "GelBridgeTest-" + Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllText(path, "sample,a,b\n", new UTF8Encoding(false));
        var options = new BridgeOptions
        {
            CsvPath = path, Address = IPAddress.Loopback, Port = 0, ReplayExisting = false,
            PeriodMs = 10, ExpectedColumns = 3, SelectedColumns = new[] { 1, 2 }, HexColumns = new bool[3]
        };
        VofaBridgeService service = null;
        TcpClient client = null;
        try
        {
            service = new VofaBridgeService(options, delegate { });
            service.Start();
            client = new TcpClient();
            client.Connect(IPAddress.Loopback, service.BoundPort);
            client.GetStream().ReadTimeout = 3000;
            Thread.Sleep(300);
            File.AppendAllText(path, "C28xx_CPU1 : 0,C28xx_CPU1 : 1.25", Encoding.ASCII);
            Thread.Sleep(300);
            if (client.Available != 0) throw new Exception("Bridge sent an incomplete CSV line.");
            File.AppendAllText(path, ",C28xx_CPU1 : 2\n", Encoding.ASCII);
            using (var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, true))
            {
                string received = reader.ReadLine();
                if (received != "0,1.25,2") throw new Exception("Unexpected bridge payload: " + received);
            }
        }
        finally
        {
            if (client != null) client.Close();
            if (service != null) service.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
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

        VariableItem oldInput = GelTemplate.MigrateLegacy(new VariableItem { Direction = "input", Type = "uint8", OutputFormat = "无" });
        if (oldInput.OutputFormat != "十进制") throw new Exception("Legacy input format migration failed.");

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
