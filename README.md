# Shell MCP Server

A Model Context Protocol (MCP) server that provides secure cross-platform shell command execution capabilities for AI assistants and other MCP clients.

## Overview

This server exposes a single tool that allows MCP clients to execute shell commands safely with built-in security measures and timeout protection. Automatically uses the appropriate shell for each platform: bash on Unix/Linux/macOS and cmd.exe on Windows.

Commands run on the machine hosting the server, with that user's permissions — this is not a sandbox.

## Features

- **Cross-Platform**: Works on Windows (cmd.exe), macOS, and Linux (bash)
- **Secure Command Execution**: Commands are executed safely with platform-appropriate shells
- **Timeout Protection**: Configurable timeout (30 seconds by default); on timeout the command and any processes it started are killed
- **Execution Options**: Optional working directory and extra environment variables per command
- **Structured Response**: Returns success status, output, error messages, and exit codes
- **Built on .NET 10.0**: For broad compatibility across platforms

## Installation

### Prerequisites
- .NET 10.0 SDK
- Windows: cmd.exe (built-in)
- macOS/Linux: bash shell (typically pre-installed)

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

### Test
```bash
dotnet test ShellMcpServer.Tests
```

Prebuilt self-contained binaries for Windows x64, macOS x64/ARM64 and Linux x64 are produced by the GitHub Actions workflow as build artifacts.

## Usage

The server provides one MCP tool:

### ExecuteShellCommand

Executes a shell command and returns structured results. Automatically uses the appropriate shell for the platform (bash on Unix/Linux/macOS, cmd.exe on Windows). The tool description sent to clients names the host OS, architecture and shell so the client can use the right syntax.

**Parameters:**
- `command` (string, required): The shell command to execute
- `workingDirectory` (string, optional): Directory to run the command in; must already exist
- `timeoutSeconds` (int, optional, default 30): Maximum run time, from 1 to 2,147,483 seconds
- `environmentVariables` (object, optional): Extra environment variables as name/value pairs

**Returns:**
- `Success` (bool): Whether the command executed successfully (exit code 0)
- `Output` (string): Standard output from the command
- `Error` (string): Standard error from the command
- `ExitCode` (int): The exit code returned by the command

Invalid input, timeouts and internal errors return `Success = false` with `ExitCode = -1` and a message in `Error`.

## Security Features

- Input validation for the command, working directory and timeout
- Commands execute in isolated processes; standard input is closed, so commands can't read the MCP protocol stream
- Configurable timeout prevents resource exhaustion; the whole process tree is killed on timeout
- Platform-specific execution (temp files on Unix, direct execution on Windows)
- Automatic cleanup of temporary resources

## Technical Details

- Built with ModelContextProtocol library v1.2.0
- Uses stdio transport for MCP communication
- Redirects stdout to stderr to maintain MCP protocol compliance
- Content root is the app's own folder rather than the working directory, so clients that launch it from `/` (like Claude Desktop) don't cause the config watcher to scan the whole disk
- Platform detection using RuntimeInformation
- UTF-8 encoding without BOM for Unix script files
