using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Redirect all stdout to stderr
Console.SetOut(Console.Error);

var builder = Host.CreateApplicationBuilder(args);        
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();
builder.Build().Run();

[McpServerToolType]
public static class ShellCommandTool
{
    [McpServerTool, Description("Execute a shell command and return the result on the user's local machine. Uses bash on Unix/Linux/macOS and cmd.exe on Windows.")]
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
            if (timeoutSeconds <= 0)
            {
                return new ShellCommandResult
                {
                    Success = false,
                    Error = "Timeout must be greater than 0",
                    ExitCode = -1
                };
            }

            // Use a more robust approach for passing the command
            // Write the command to a temporary file to avoid shell escaping issues
            var tempScript = Path.GetTempFileName();
            try
            {
                var processStartInfo = new ProcessStartInfo();
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // On Windows, use cmd.exe to execute the command directly
                    processStartInfo.FileName = "cmd.exe";
                    processStartInfo.Arguments = $"/c \"{command}\"";
                }
                else
                {
                    // On Unix/Linux/macOS, write command to temp file and execute with bash
                    var utf8WithoutBom = new UTF8Encoding(false);
                    File.WriteAllText(tempScript, command, utf8WithoutBom);
                    processStartInfo.FileName = "/bin/bash";
                    processStartInfo.Arguments = tempScript;
                }
                
                // Set working directory if provided
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    processStartInfo.WorkingDirectory = workingDirectory;
                }
                
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
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                // Add configurable timeout to prevent hanging
                int timeoutMs = timeoutSeconds * 1000;
                if (!process.WaitForExit(timeoutMs))
                {
                    process.Kill();
                    return new ShellCommandResult
                    {
                        Success = false,
                        Error = $"Command timed out after {timeoutSeconds} seconds",
                        ExitCode = -1
                    };
                }

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
                // Clean up temp file (only needed on Unix platforms)
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
