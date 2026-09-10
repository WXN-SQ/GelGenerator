using GelGenerator;

internal static class CreateSample
{
    private static int Main(string[] args)
    {
        CcsCsvConverter.ConvertFile(args[0], args[1]);
        return 0;
    }
}
