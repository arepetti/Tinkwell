using System.Text;

namespace Tinkwell.Studio.Tests;

internal static class StubCli
{
    /// <summary>
    /// Creates a platform-appropriate executable script that prints the given lines
    /// to stdout, optionally prints something to stderr, and exits with the given code.
    /// Returns the path to the executable.
    /// </summary>
    public static string Create(string[] stdoutLines, int exitCode = 0, string? stderr = null, int delayMsPerLine = 0)
    {
        var dir = Path.Combine(Path.GetTempPath(), "tw-studio-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(dir, "stub.cmd");
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            // Switch cmd to UTF-8 so `type` emits the data files verbatim.
            sb.AppendLine("chcp 65001 > nul");
            for (var i=0; i < stdoutLines.Length; ++i)
            {
                if (delayMsPerLine > 0)
                {
                    var seconds = Math.Max(1, delayMsPerLine / 1000);
                    sb.AppendLine($"timeout /t {seconds} /nobreak > nul 2>&1");
                }
                // Stage each line in a separate UTF-8 file and emit it via `type`, which
                // copies the bytes as-is without cmd's echo quoting/escaping quirks (the
                // JSONL payloads contain %, &, <, >, ^, ", quotes, and non-ASCII).
                var dataPath = Path.Combine(dir, $"line-{i:D4}.txt");
                File.WriteAllText(dataPath, stdoutLines[i] + Environment.NewLine, new UTF8Encoding(false));
                sb.AppendLine($"type \"{dataPath}\"");
            }
            if (!string.IsNullOrEmpty(stderr))
            {
                var errPath = Path.Combine(dir, "stderr.txt");
                File.WriteAllText(errPath, stderr + Environment.NewLine, new UTF8Encoding(false));
                sb.AppendLine($"type \"{errPath}\" 1>&2");
            }
            sb.AppendLine($"exit /b {exitCode}");
            File.WriteAllText(path, sb.ToString());
            return path;
        }
        else
        {
            var path = Path.Combine(dir, "stub.sh");
            var sb = new StringBuilder();
            sb.AppendLine("#!/usr/bin/env bash");
            foreach (var line in stdoutLines)
            {
                if (delayMsPerLine > 0)
                    sb.AppendLine($"sleep {(delayMsPerLine / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                sb.AppendLine($"echo '{line.Replace("'", "'\\''")}'");
            }
            if (!string.IsNullOrEmpty(stderr))
                sb.AppendLine($"echo '{stderr.Replace("'", "'\\''")}' 1>&2");
            sb.AppendLine($"exit {exitCode}");
            File.WriteAllText(path, sb.ToString());
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            return path;
        }
    }

}
