using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Redirect all stdout to stderr
Console.SetOut(Console.Error);

// Claude desktop launches this with cwd "/", and the default content root is cwd - so the
// appsettings reload watcher would watch the whole disk (and trip macOS's "access data from
// other apps" prompt). Anchor it to the app's own folder.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools(new[]
    {
        McpServerTool.Create(
            ShellCommandTool.ExecuteShellCommand,
            new McpServerToolCreateOptions
            {
                Name = "ExecuteShellCommand",
                Description = ShellCommandTool.BuildToolDescription()
            })
    });
builder.Build().Run();

public static class ShellCommandTool
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static readonly string ShellPath = IsWindows ? "cmd.exe" : "/bin/bash";

    private static readonly string ShellName = Path.GetFileName(ShellPath);

    // Keeps the timeout in milliseconds within int range for Process.WaitForExit
    private const int MaxTimeoutSeconds = int.MaxValue / 1000;

    public static string BuildToolDescription()
    {
        var os = RuntimeInformation.OSDescription.Trim();
        var arch = RuntimeInformation.OSArchitecture;
        return
            "Run shell commands directly on the user's own computer — their actual filesystem, " +
            "their installed tools, their environment. Use this when the user asks about their files, " +
            "their processes, their git repos, their installed software, or anything that requires " +
            "touching their real machine rather than a sandbox. " +
            $"This server is currently running on {os} ({arch}) and executes commands using {ShellName} ({ShellPath}); " +
            $"use shell syntax appropriate for {ShellName}. " +
            "Examples: ls ~, git status in their project, brew list, ps aux | grep node, reading their dotfiles, " +
            "checking disk usage. This is NOT a sandboxed environment — commands run with the user's " +
            "permissions and affect their real system.";
    }

    public static ShellCommandResult ExecuteShellCommand(
        string command,
        string? workingDirectory = null,
        int timeoutSeconds = 30,
        Dictionary<string, string>? environmentVariables = null)
    {
        try
        {
            // Validate the command to prevent security issues
            if (string.IsNullOrWhiteSpace(command))
            {
                return new ShellCommandResult
                {
                    Success = false,
                    Error = "Command cannot be empty",
                    ExitCode = -1
                };
            }

            // Validate working directory if provided
            if (!string.IsNullOrEmpty(workingDirectory) && !Directory.Exists(workingDirectory))
            {
                return new ShellCommandResult
                {
                    Success = false,
                    Error = $"Working directory does not exist: {workingDirectory}",
                    ExitCode = -1
                };
            }

            // Validate timeout
            if (timeoutSeconds <= 0 || timeoutSeconds > MaxTimeoutSeconds)
            {
                return new ShellCommandResult
                {
                    Success = false,
                    Error = $"Timeout must be between 1 and {MaxTimeoutSeconds} seconds",
                    ExitCode = -1
                };
            }

            // Use a more robust approach for passing the command
            // Write the command to a temporary file to avoid shell escaping issues (Unix only)
            string? tempScript = null;
            try
            {
                var processStartInfo = new ProcessStartInfo();
                
                if (IsWindows)
                {
                    processStartInfo.FileName = ShellPath;
                    processStartInfo.Arguments = $"/c \"{command}\"";
                }
                else
                {
                    tempScript = Path.GetTempFileName();
                    var utf8WithoutBom = new UTF8Encoding(false);
                    File.WriteAllText(tempScript, command, utf8WithoutBom);
                    processStartInfo.FileName = ShellPath;
                    processStartInfo.Arguments = tempScript;
                }
                
                // Set working directory if provided
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    processStartInfo.WorkingDirectory = workingDirectory;
                }
                
                // Redirect stdin so the child can't consume the MCP stdio JSON-RPC stream
                processStartInfo.RedirectStandardInput = true;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.UseShellExecute = false;
                processStartInfo.CreateNoWindow = true;

                // Add environment variables if provided
                if (environmentVariables != null)
                {
                    foreach (var kvp in environmentVariables)
                    {
                        processStartInfo.Environment[kvp.Key] = kvp.Value;
                    }
                }

                using var process = new Process { StartInfo = processStartInfo };
                
                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) => 
                {
                    if (e.Data != null) output.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) => 
                {
                    if (e.Data != null) error.AppendLine(e.Data);
                };

                process.Start();
                process.StandardInput.Close();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                // Add configurable timeout to prevent hanging
                if (!process.WaitForExit(TimeSpan.FromSeconds(timeoutSeconds)))
                {
                    try
                    {
                        // Kill the whole tree so children of the shell don't linger as orphans
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        // Process exited between the timed wait and Kill
                    }
                    return new ShellCommandResult
                    {
                        Success = false,
                        Error = $"Command timed out after {timeoutSeconds} seconds",
                        ExitCode = -1
                    };
                }

                // The timed wait doesn't wait for the async output readers to hit EOF; this does
                process.WaitForExit();

                return new ShellCommandResult
                {
                    Success = process.ExitCode == 0,
                    Output = output.ToString().TrimEnd('\n', '\r'),
                    Error = error.ToString().TrimEnd('\n', '\r'),
                    ExitCode = process.ExitCode
                };
            }
            finally
            {
                // Clean up temp file (only created on Unix platforms)
                if (tempScript != null)
                {
                    try
                    {
                        if (File.Exists(tempScript))
                            File.Delete(tempScript);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return new ShellCommandResult
            {
                Success = false,
                Error = $"Exception occurred: {ex.Message}",
                ExitCode = -1
            };
        }
    }

    // Helper class for structured response
    public class ShellCommandResult
    {
        public bool Success { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public int ExitCode { get; set; }
    }
}
