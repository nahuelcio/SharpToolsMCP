using System.Diagnostics;
using ModelContextProtocol;

namespace SharpTools.Tools.Mcp;

internal sealed record DotnetFormatRunResult(
    string TargetPath,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string CommandLine);

internal static class DotnetFormatRunner {
    public static async Task<DotnetFormatRunResult> RunAsync(
        string targetPath,
        string? diagnosticIds,
        IEnumerable<string>? includePaths,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(targetPath)) {
            throw new McpException("targetPath cannot be null or empty.");
        }

        if (!File.Exists(targetPath)) {
            throw new McpException($"Target path does not exist: {targetPath}");
        }

        var fullTargetPath = Path.GetFullPath(targetPath);
        var workingDirectory = Path.GetDirectoryName(fullTargetPath);
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory)) {
            throw new McpException($"Could not determine a valid working directory for '{fullTargetPath}'.");
        }

        var processStartInfo = new ProcessStartInfo {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        processStartInfo.ArgumentList.Add("format");

        if (!string.IsNullOrWhiteSpace(diagnosticIds)) {
            processStartInfo.ArgumentList.Add("analyzers");
        }

        processStartInfo.ArgumentList.Add(fullTargetPath);
        processStartInfo.ArgumentList.Add("--no-restore");

        if (!string.IsNullOrWhiteSpace(diagnosticIds)) {
            processStartInfo.ArgumentList.Add("--diagnostics");
            processStartInfo.ArgumentList.Add(diagnosticIds.Trim());
        }

        if (includePaths != null) {
            foreach (var includePath in includePaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase)) {
                processStartInfo.ArgumentList.Add("--include");
                processStartInfo.ArgumentList.Add(Path.GetFullPath(includePath));
            }
        }

        using var process = new Process {
            StartInfo = processStartInfo
        };

        try {
            if (!process.Start()) {
                throw new McpException("Failed to start `dotnet format` process.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var commandLine = BuildCommandLine(processStartInfo.ArgumentList);
            return new DotnetFormatRunResult(
                fullTargetPath,
                process.ExitCode,
                stdout,
                stderr,
                commandLine);
        } catch (OperationCanceledException) {
            if (!process.HasExited) {
                try {
                    process.Kill(entireProcessTree: true);
                } catch {
                    // Best effort kill on cancellation.
                }
            }
            throw;
        } catch (McpException) {
            throw;
        } catch (Exception ex) {
            throw new McpException($"Failed to execute `dotnet format`: {ex.Message}");
        }
    }

    private static string BuildCommandLine(IEnumerable<string> arguments) {
        static string Quote(string value) {
            return value.Contains(' ') ? $"\"{value}\"" : value;
        }

        var parts = new List<string> { "dotnet" };
        foreach (var arg in arguments) {
            parts.Add(Quote(arg));
        }
        return string.Join(" ", parts);
    }
}
