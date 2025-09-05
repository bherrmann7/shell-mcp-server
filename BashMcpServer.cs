using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
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
public static class BashCommandTool
{
    [McpServerTool, Description("Execute a bash command and return the result.")]
    public static BashCommandResult ExecuteBashCommand(string command)
    {
        try
        {
            // Validate the command to prevent security issues
            if (string.IsNullOrWhiteSpace(command))
            {
                return new BashCommandResult
                {
                    Success = false,
                    Error = "Command cannot be empty",
                    ExitCode = -1
                };
            }

            // Use a more robust approach for passing the command
            // Write the command to a temporary file to avoid shell escaping issues
            var tempScript = Path.GetTempFileName();
            try
            {
                // Write command to temp file with UTF-8 encoding WITHOUT BOM
                var utf8WithoutBom = new UTF8Encoding(false);
                File.WriteAllText(tempScript, command, utf8WithoutBom);
                
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = tempScript, // Execute the script file directly
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

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
                
                // Add timeout to prevent hanging
                if (!process.WaitForExit(30000)) // 30 second timeout
                {
                    process.Kill();
                    return new BashCommandResult
                    {
                        Success = false,
                        Error = "Command timed out after 30 seconds",
                        ExitCode = -1
                    };
                }

                return new BashCommandResult
                {
                    Success = process.ExitCode == 0,
                    Output = output.ToString().TrimEnd('\n', '\r'),
                    Error = error.ToString().TrimEnd('\n', '\r'),
                    ExitCode = process.ExitCode
                };
            }
            finally
            {
                // Clean up temp file
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
        catch (Exception ex)
        {
            return new BashCommandResult
            {
                Success = false,
                Error = $"Exception occurred: {ex.Message}",
                ExitCode = -1
            };
        }
    }

    // Helper class for structured response
    public class BashCommandResult
    {
        public bool Success { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public int ExitCode { get; set; }
    }
}
