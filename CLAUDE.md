# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A stdio MCP server (.NET 10, `ModelContextProtocol` SDK 1.2.0) exposing one tool, `ExecuteShellCommand`, that runs commands on the user's real machine: `/bin/bash` on Unix/macOS, `cmd.exe` on Windows.

## Commands

```bash
dotnet build                                   # build the server
dotnet run                                     # run it (speaks MCP JSON-RPC over stdin/stdout)
dotnet test ShellMcpServer.Tests               # run all tests
dotnet test ShellMcpServer.Tests --filter "FullyQualifiedName~Timeout_KillsLongRunningCommand"   # single test
```

There is no .sln. The test project lives inside the server's folder, so `ShellMcpServer.csproj` excludes `ShellMcpServer.Tests/**` from compilation. Keep that exclusion when touching the csproj. `Directory.Build.props` is intentionally empty to stop MSBuild importing props from parent directories.

CI (`.github/workflows/build.yml`) builds and tests on ubuntu, windows and macos, then publishes self-contained single-file binaries per platform. Tests must pass on all three, so each test picks a Windows or Unix command via `RuntimeInformation`.

## Architecture

Everything is in `ShellMcpServer.cs`: top-level statements that build the host, plus the static `ShellCommandTool` class the tests call directly.

- **stdout is reserved for MCP.** `Console.SetOut(Console.Error)` runs first so stray writes don't corrupt the JSON-RPC stream. For the same reason, child processes get a redirected stdin that is closed immediately; if a child inherited the server's stdin, it could read protocol messages.
- **Content root is `AppContext.BaseDirectory`, not cwd.** Claude Desktop launches the server with cwd `/`, and the config reload watcher would otherwise watch the whole disk and trigger macOS privacy prompts.
- **The tool is registered explicitly** with `McpServerTool.Create(...)`, not attributes, because its description is built at runtime (`BuildToolDescription`) to name the host OS, architecture and shell.
- **How a command is run:** on Unix the command is written to a temp file and run as `bash <file>`, which avoids escaping problems; the file is deleted afterwards. On Windows it's passed as `cmd.exe /c "<command>"`, and no temp file is created.
- **Process lifecycle:** after the timed `WaitForExit(TimeSpan)` returns, a second `WaitForExit()` with no argument waits for the async output readers to finish, so output isn't cut off. On timeout the whole process tree is killed. `timeoutSeconds` is limited to `int.MaxValue / 1000`.
- The tool returns `ShellCommandResult` (`Success`, `Output`, `Error`, `ExitCode`). Validation failures, timeouts and exceptions all come back as `Success = false`, `ExitCode = -1` rather than as thrown errors.

## Repo conventions

- Commit and push directly to `main`; no feature branches or PRs.
- `README.md` documents the tool's parameters and behavior; update it when those change.
