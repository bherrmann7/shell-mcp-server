# Bash MCP Server

A Model Context Protocol (MCP) server that provides secure bash command execution capabilities for AI assistants and other MCP clients.

## Overview

This server exposes a single tool that allows MCP clients to execute bash commands safely with built-in security measures and timeout protection.

## Features

- **Secure Command Execution**: Commands are executed through temporary script files to avoid shell escaping issues
- **Timeout Protection**: 30-second timeout prevents hanging processes
- **Structured Response**: Returns success status, output, error messages, and exit codes
- **Cross-Platform**: Built on .NET 9.0 for broad compatibility

## Installation

### Prerequisites
- .NET 9.0 SDK
- Bash shell (typically available on macOS and Linux)

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

### ExecuteBashCommand

Executes a bash command and returns structured results.

**Parameters:**
- `command` (string): The bash command to execute

**Returns:**
- `Success` (bool): Whether the command executed successfully (exit code 0)
- `Output` (string): Standard output from the command
- `Error` (string): Standard error from the command  
- `ExitCode` (int): The exit code returned by the command

## Security Features

- Input validation prevents empty commands
- Commands execute in isolated processes
- 30-second timeout prevents resource exhaustion
- Temporary script files are automatically cleaned up
- No shell expansion or interpretation of command arguments

## Technical Details

- Built with ModelContextProtocol library v0.3.0-preview.4
- Uses stdio transport for MCP communication
- Redirects stdout to stderr to maintain MCP protocol compliance
- UTF-8 encoding without BOM for script files