using System.Runtime.InteropServices;
using EXTReader.Interop;

namespace EXTReader.Services;

public static class SafetySelfCheck
{
    public static SafetyCheckResult Run()
    {
        var failures = new List<string>();

        if (!VerifyLibext2fsVersion())
            failures.Add("libext2fs.dll failed to load or version check failed.");

        if (!VerifyReadOnlyFlags())
            failures.Add("Read-only flag configuration is incorrect (EXT2_FLAG_RW bit must not be set in ReadOnlyFlags).");

        return new SafetyCheckResult(failures.Count == 0, failures);
    }

    private static bool VerifyReadOnlyFlags()
    {
        return (Ext2fsConstants.ReadOnlyFlags & Ext2fsConstants.FlagRw) == 0;
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
