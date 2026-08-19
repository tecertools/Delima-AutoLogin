namespace Delima.Provision;

internal static class Program
{
    public static int Main(string[] args)
    {
        var options = ProvisionOptions.Parse(args);
        var result = ProvisionEngine.Execute(options);
        return result.ExitCode;
    }
}
