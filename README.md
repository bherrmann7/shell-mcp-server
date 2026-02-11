# Shell MCP Server

A Model Context Protocol (MCP) server that provides secure cross-platform shell command execution capabilities for AI assistants and other MCP clients.

## Overview

This server exposes a single tool that allows MCP clients to execute shell commands safely with built-in security measures and timeout protection. Automatically uses the appropriate shell for each platform: bash on Unix/Linux/macOS and cmd.exe on Windows.

## Features

- **Cross-Platform**: Works on Windows (cmd.exe), macOS, and Linux (bash)
- **Secure Command Execution**: Commands are executed safely with platform-appropriate shells
- **Timeout Protection**: 30-second timeout prevents hanging processes
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

## Usage

The server provides one MCP tool:

### ExecuteShellCommand

Executes a shell command and returns structured results. Automatically uses the appropriate shell for the platform (bash on Unix/Linux/macOS, cmd.exe on Windows).

**Parameters:**
- `command` (string): The shell command to execute

**Returns:**
- `Success` (bool): Whether the command executed successfully (exit code 0)
- `Output` (string): Standard output from the command
- `Error` (string): Standard error from the command  
- `ExitCode` (int): The exit code returned by the command

## Security Features

- Input validation prevents empty commands
- Commands execute in isolated processes
- 30-second timeout prevents resource exhaustion
- Platform-specific execution (temp files on Unix, direct execution on Windows)
- Automatic cleanup of temporary resources

## Technical Details

- Built with ModelContextProtocol library v0.8.0-preview.1
- Uses stdio transport for MCP communication
- Redirects stdout to stderr to maintain MCP protocol compliance
- Platform detection using RuntimeInformation
- UTF-8 encoding without BOM for Unix script files
