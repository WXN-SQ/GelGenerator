using System.Net;
using System.IO;
using System.Text;
using GelGenerator;

internal static class CreateBridgeSample
{
    private static int Main(string[] args)
    {
        var options = new BridgeOptions
        {
            CsvPath = @"C:\Data Folder\log.csv",
            Address = IPAddress.Loopback,
            Port = 1347,
            ReplayExisting = false,
            PeriodMs = 100,
            ExpectedColumns = 5,
            SelectedColumns = new[] { 1, 2, 3, 4 },
            HexColumns = new bool[5],
            ChannelNames = new[] { "sample", "x1", "y1", "x2", "y2" }
        };
        File.WriteAllText(args[0], BridgePowerShellTemplate.Build(options), new UTF8Encoding(true));
        return 0;
    }
}
