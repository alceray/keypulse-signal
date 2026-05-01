using System.Diagnostics;
using System.Text;
using Serilog;

namespace KeyPulse.Helpers;

public static class PowershellScripts
{
    private const int TimeoutMs = 10_000;

    public static string? GetDeviceName(string deviceId)
    {
        var escapedDeviceId = deviceId.Replace(@"\", @"\\");
        var script = $$"""
            Get-PnpDevice -PresentOnly | Where-Object {
                $_.InstanceId -match '{{escapedDeviceId}}'
            } | ForEach-Object {
                $properties = Get-PnpDeviceProperty -InstanceId $_.InstanceId
                ($properties | Where-Object { $_.KeyName -eq 'DEVPKEY_Device_BusReportedDeviceDesc' }).Data
            }
            """;

        try
        {
            var output = RunPowerShellExternal(script);
            var firstLine = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

            if (!string.IsNullOrEmpty(firstLine))
                return firstLine;

            Log.Debug("PowerShell device name lookup returned no result for DeviceId={DeviceId}", deviceId);
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "External PowerShell device name lookup failed for DeviceId={DeviceId}", deviceId);
            return null;
        }
    }

    private static string RunPowerShellExternal(string script)
    {
        // Suppress the progress stream so PowerShell does not write CLIXML progress records to stderr.
        const string preamble = "$ProgressPreference = 'SilentlyContinue'\n";

        // Encode as UTF-16LE so the script reaches PowerShell verbatim with no quoting issues.
        var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(preamble + script));

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            Log.Warning("Failed to start powershell.exe for device name lookup");
            return string.Empty;
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(TimeoutMs))
        {
            process.Kill();
            Log.Warning("powershell.exe timed out during device name lookup for and was killed");
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(error))
            Log.Warning("PowerShell stderr: {PowerShellError}", error.Trim());

        return output;
    }
}
