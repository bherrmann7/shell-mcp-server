using System.Runtime.InteropServices;

namespace ShellMcpServer.Tests;

public class ShellCommandToolTests
{
    [Fact]
    public void ListDirectory_ReturnsOutput()
    {
        var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dir" : "ls";
        var result = ShellCommandTool.ExecuteShellCommand(command);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrEmpty(result.Output));
    }

    [Fact]
    public void Echo_ReturnsExpectedText()
    {
        var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "echo hello world"
            : "echo hello world";
        var result = ShellCommandTool.ExecuteShellCommand(command);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello world", result.Output);
    }

    [Fact]
    public void EmptyCommand_ReturnsError()
    {
        var result = ShellCommandTool.ExecuteShellCommand("");

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhitespaceCommand_ReturnsError()
    {
        var result = ShellCommandTool.ExecuteShellCommand("   ");

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
    }

    [Fact]
    public void InvalidWorkingDirectory_ReturnsError()
    {
        var result = ShellCommandTool.ExecuteShellCommand("echo hi", workingDirectory: "/nonexistent/path/that/does/not/exist");

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("Working directory does not exist", result.Error);
    }

    [Fact]
    public void WorkingDirectory_IsRespected()
    {
        var tempDir = Path.GetTempPath();
        var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cd" : "pwd";
        var result = ShellCommandTool.ExecuteShellCommand(command, workingDirectory: tempDir);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        // Resolve symlinks for comparison (e.g., macOS /tmp -> /private/tmp)
        Assert.Contains(Path.GetFileName(tempDir.TrimEnd(Path.DirectorySeparatorChar)), result.Output);
    }

    [Fact]
    public void FailingCommand_ReturnsNonZeroExitCode()
    {
        var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "cmd /c exit 1"
            : "exit 1";
        var result = ShellCommandTool.ExecuteShellCommand(command);

        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void InvalidTimeout_ReturnsError()
    {
        var result = ShellCommandTool.ExecuteShellCommand("echo hi", timeoutSeconds: 0);

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("Timeout must be greater than 0", result.Error);
    }

    [Fact]
    public void EnvironmentVariables_ArePassed()
    {
        var envVars = new Dictionary<string, string> { { "TEST_MCP_VAR", "mcp_test_value" } };
        var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "echo %TEST_MCP_VAR%"
            : "printenv TEST_MCP_VAR";
        var result = ShellCommandTool.ExecuteShellCommand(command, environmentVariables: envVars);

        Assert.True(result.Success);
        Assert.Contains("mcp_test_value", result.Output);
    }

    [Fact]
    public void Timeout_KillsLongRunningCommand()
    {
        var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "ping -n 30 127.0.0.1"
            : "sleep 30";
        var result = ShellCommandTool.ExecuteShellCommand(command, timeoutSeconds: 1);

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("timed out", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
