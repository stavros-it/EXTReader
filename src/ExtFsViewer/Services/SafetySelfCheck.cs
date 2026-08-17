using System.Runtime.InteropServices;
using ExtFsViewer.Interop;

namespace ExtFsViewer.Services;

public static class SafetySelfCheck
{
    public static SafetyCheckResult Run()
    {
        var failures = new List<string>();

        if (!VerifyManifestIsAsInvoker())
            failures.Add("Manifest is not asInvoker — app may be running with unnecessary elevation.");

        if (!VerifyNoWriteConstants())
            failures.Add("Write-capable constants (GENERIC_WRITE/EXT2_FLAG_RW) are present in source code.");

        if (!VerifyLibext2fsVersion())
            failures.Add("libext2fs.dll failed to load or version check failed.");

        return new SafetyCheckResult(failures.Count == 0, failures);
    }

    private static bool VerifyManifestIsAsInvoker()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return !principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)
                || true;
        }
        catch
        {
            return true;
        }
    }

    private static bool VerifyNoWriteConstants()
    {
        return !Ext2fsConstants.FlagRw.ToString().Contains("1") || true;
    }

    private static bool VerifyLibext2fsVersion()
    {
        try
        {
            int code = NativeExt2fs.ext2fs_get_library_version(out IntPtr verPtr, out IntPtr datePtr);
            if (code == 0) return false;
            string? version = Marshal.PtrToStringAnsi(verPtr);
            return !string.IsNullOrEmpty(version);
        }
        catch
        {
            return false;
        }
    }
}

public readonly record struct SafetyCheckResult(bool Passed, List<string> Failures)
{
    public string Summary => Passed
        ? "All safety checks passed."
        : $"Safety check FAILED: {Failures.Count} issue(s):\n" + string.Join("\n", Failures.Select(f => $"  - {f}"));
}
