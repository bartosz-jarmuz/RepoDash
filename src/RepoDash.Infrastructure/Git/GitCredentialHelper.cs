using System;
using System.Diagnostics;
using System.Text;
using LibGit2Sharp;

namespace RepoDash.Infrastructure.Git;

internal static class GitCredentialHelper
{
    public static UsernamePasswordCredentials? TryGetCredentials(string repoPath, string url, string usernameFromUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                return null;

            var psi = new ProcessStartInfo("git", "credential fill")
            {
                WorkingDirectory = repoPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

            using var process = Process.Start(psi);
            if (process is null) return null;

            var input = new StringBuilder()
                .AppendLine($"protocol={uri.Scheme}")
                .AppendLine($"host={uri.Host}");

            var path = uri.AbsolutePath.Trim('/');
            if (!string.IsNullOrEmpty(path))
                input.AppendLine($"path={path}");

            if (!string.IsNullOrWhiteSpace(usernameFromUrl))
                input.AppendLine($"username={usernameFromUrl}");

            input.AppendLine();

            process.StandardInput.Write(input.ToString());
            process.StandardInput.Flush();
            process.StandardInput.Close();

            if (!process.WaitForExit(3000))
            {
                try { process.Kill(true); } catch { }
                return null;
            }

            if (process.ExitCode != 0) return null;

            var output = process.StandardOutput.ReadToEnd();
            var username = default(string?);
            var password = default(string?);

            foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("username=", StringComparison.Ordinal))
                {
                    username = line[9..];
                }
                else if (line.StartsWith("password=", StringComparison.Ordinal))
                {
                    password = line[9..];
                }
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return null;

            return new UsernamePasswordCredentials
            {
                Username = username,
                Password = password
            };
        }
        catch
        {
            return null;
        }
    }
}
