using System.IO;
using System.Text;
using GelGenerator;

internal static class CreateAdapterSample
{
    private static int Main(string[] args)
    {
        var options = new BridgeOptions
        {
            ControlPort = 1349,
            CorePattern = "C28xx_CPU1",
            WritableVariables = new[]
            {
                new VariableItem { Direction = "input", Name = "workMode", Expression = "WorkModeSwitchObj.Current_Mode", Type = "uint8", OutputFormat = "无" }
            }
        };
        File.WriteAllText(args[0], CcsInputAdapterTemplate.Build("CCS 20.x", options), new UTF8Encoding(false));
        return 0;
    }
}
