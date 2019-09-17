// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Relay;
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.ServiceBus;
    using RelayUtil.Diagnostics;

    class DiagnosticCommands
    {
        static readonly string CommandSeparatorLine = new string('*', 80);

        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.Command("diag", (diagCommand) =>
            {
                // TODO
                diagCommand.Description = "Operations for diagnosing relay/hc issues (Analyze)";
                diagCommand.HelpOption(CommandStrings.HelpTemplate);
                var connectionStringArgument = diagCommand.Argument("connectionString", "Relay Namespace ConnectionString");

                CommandOption netStatOption = diagCommand.Option(
                    "-ns|--netstat",
                    "Show netstat output",
                    CommandOptionType.NoValue);

                CommandOption portsOption = diagCommand.Option(
                    "-p|--ports",
                    "Probe Relay Ports",
                    CommandOptionType.NoValue);

                CommandOption osOption = diagCommand.Option(
                    "-o|--os",
                    "Display Platform/OS/.NET information",
                    CommandOptionType.NoValue);

                diagCommand.OnExecute(async () =>
                {
                    bool runAll = !diagCommand.Options.Any(o => o.HasValue());
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument); // Might not be present

                    if (runAll || netStatOption.HasValue())
                    {
                        ExecuteNetStatCommand(diagCommand);
                    }

                    if (runAll || portsOption.HasValue())
                    {
                        await ExecutePortsCommandAsync(diagCommand, connectionString);
                    }

                    if (runAll || osOption.HasValue())
                    {
                        await ExecutePlatformCommandAsync(diagCommand, connectionString);
                    }

                    return 0;
                });
            });
        }

        static async Task ExecutePortsCommandAsync(CommandLineApplication app, string connectionString)
        {
            app.Out.WriteLine(CommandSeparatorLine);
            if (!string.IsNullOrEmpty(connectionString))
            {
                var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                await NetworkUtility.VerifyRelayPortsAsync(connectionStringBuilder.Endpoints.First().Host, app.Out);

                // TODO: Build the ILPIP DNS name and run it for G0 through G63
                ////await NetworkUtility.VerifyRelayPortsAsync("g0-prod-by3-003-sb.servicebus.windows.net", app.Out);
            }
        }

        static void ExecuteNetStatCommand(CommandLineApplication app)
        {
            app.Out.WriteLine(CommandSeparatorLine);
            ExecuteProcess(
                "netstat.exe",
                "-ano",
                TimeSpan.FromSeconds(30),
                (s, e) =>
                {
                    // Skip UDP stuff
                    if (!string.IsNullOrEmpty(e.Data) && !e.Data.Contains(" UDP "))
                    {
                        app.Out.WriteLine("[netstat] " + e.Data);
                    }
                },
                (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        app.Out.WriteLine("[netstat] ERROR: " + e.Data);
                    }
                },
                throwOnNonZero: true);
        }

        static async Task ExecutePlatformCommandAsync(CommandLineApplication app, string connectionString)
        {
            app.Out.WriteLine(CommandSeparatorLine);
            const string OutputFormat = "{0,-27}{1}";
            app.Out.WriteLine(OutputFormat, "OSVersion:", Environment.OSVersion);
            app.Out.WriteLine(OutputFormat, "ProcessorCount:", Environment.ProcessorCount);
            app.Out.WriteLine(OutputFormat, "Is64BitOperatingSystem:", Environment.Is64BitOperatingSystem);
            app.Out.WriteLine(OutputFormat, "CLR Version:", Environment.Version);
            app.Out.WriteLine(OutputFormat, "mscorlib Assembly Version:", typeof(object).Assembly.GetName().Version);
            app.Out.WriteLine(OutputFormat, "mscorlib File Version:", FileVersionInfo.GetVersionInfo(typeof(object).Assembly.Location).FileVersion);

            if (connectionString != null)
            {
                var connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
                var webRequest = WebRequest.CreateHttp(new Uri($"https://{connectionStringBuilder.Endpoint.Host}"));
                webRequest.Method = "GET";
                using (var response = await webRequest.GetResponseAsync())
                {
                    app.Out.WriteLine(OutputFormat, "Azure Time:", response.Headers["Date"]); // RFC1123
                }
            }

            var utcNow = DateTime.UtcNow;
            app.Out.WriteLine(OutputFormat, "Machine Time(UTC):", utcNow.ToString(DateTimeFormatInfo.InvariantInfo.RFC1123Pattern));
            app.Out.WriteLine(OutputFormat, "Machine Time(Local):", utcNow.ToLocalTime().ToString("ddd, dd MMM yyyy HH':'mm':'ss '('zzz')'")); // Like RFC1123Pattern but with zzz for timezone offset
        }

        static int ExecuteProcess(string filePath, string args, TimeSpan timeout, DataReceivedEventHandler outputDataReceived, DataReceivedEventHandler errorDataReceived, bool throwOnNonZero)
        {
            var processStartInfo = new ProcessStartInfo();
            processStartInfo.FileName = filePath;
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            if (!string.IsNullOrEmpty(args))
            {
                processStartInfo.Arguments = args;
            }

            using (var process = Process.Start(processStartInfo))
            {
                if (outputDataReceived != null)
                {
                    process.OutputDataReceived += outputDataReceived;
                    process.BeginOutputReadLine();
                }

                if (errorDataReceived != null)
                {
                    process.ErrorDataReceived += errorDataReceived;
                    process.BeginErrorReadLine();
                }

                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    throw new TimeoutException($"Executing '{processStartInfo.FileName} {args}' did not complete within the expected timeout of {timeout}");
                }

                if (process.ExitCode != 0 && throwOnNonZero)
                {
                    string errorMessage = $"Executing '{processStartInfo.FileName} {processStartInfo.Arguments}' failed with  exit code: {process.ExitCode}";
                    throw new ApplicationException(errorMessage);
                }

                return process.ExitCode;
            }
        }
    }
}
