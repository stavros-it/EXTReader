using System.Diagnostics;
using System.Security.Principal;

namespace EXTReader.Services;

public static class AdminRightsService
{
    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static void RestartElevated()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine executable path.");

        var startInfo = new ProcessStartInfo(exePath)
        {
            Verb = "runas",
            UseShellExecute = true,
        };

        Process.Start(startInfo);
        Environment.Exit(0);
    }
}
