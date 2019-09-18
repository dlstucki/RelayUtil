// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Relay;
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.ServiceBus;
    using RelayUtil.Utilities;

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

                CommandOption namespaceOption = diagCommand.Option(
                    "-n|-ns|--namespace",
                    "Show namespace details",
                    CommandOptionType.NoValue);

                CommandOption netStatOption = diagCommand.Option(
                    "--netstat",
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

                    NamespaceDetails namespaceDetails = default;
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument); // Might not be present
                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        var connectionStringBuilder = connectionString != null ? new RelayConnectionStringBuilder(connectionString) : null;
                        namespaceDetails = await NamespaceUtility.GetNamespaceDetailsAsync(connectionStringBuilder.Endpoint.Host);
                    }

                    if (runAll || netStatOption.HasValue())
                    {
                        ExecuteNetStatCommand(diagCommand);
                    }

                    if (runAll || portsOption.HasValue())
                    {
                        await ExecutePortsCommandAsync(diagCommand, namespaceDetails);
                    }

                    if (runAll || osOption.HasValue())
                    {
                        await ExecutePlatformCommandAsync(diagCommand, namespaceDetails);
                    }

                    if (runAll || namespaceOption.HasValue())
                    {
                        ExecuteNamespaceCommand(diagCommand, namespaceDetails);
                    }

                    return 0;
                });
            });
        }

        static async Task ExecutePortsCommandAsync(CommandLineApplication app, NamespaceDetails namespaceDetails)
        {
            app.Out.WriteLine(CommandSeparatorLine);
            if (!string.IsNullOrEmpty(namespaceDetails.ServiceNamespace))
            {
                await NetworkUtility.VerifyRelayPortsAsync(namespaceDetails.ServiceNamespace, app.Out);

                // TODO: Build the ILPIP DNS name and run it for G0 through G63
                ////await NetworkUtility.VerifyRelayPortsAsync(namespaceDetails.GatewayDnsFormat, app.Out);
            }
        }

        static void ExecuteNamespaceCommand(CommandLineApplication app, NamespaceDetails namespaceDetails)
        {
            const string OutputFormat = "{0,-27}{1}";

            void OutputLineIf(bool condition, string name, string value)
            {
                if (condition)
                {
                    app.Out.WriteLine(OutputFormat, name + ":", value);
                }
            }

            app.Out.WriteLine(CommandSeparatorLine);
            OutputLineIf(!string.IsNullOrEmpty(namespaceDetails.ServiceNamespace), "ServiceNamespace", namespaceDetails.ServiceNamespace);
            OutputLineIf(namespaceDetails.AddressList?.Length > 0, "Address(VIP)", string.Join(",", (IEnumerable<IPAddress>)namespaceDetails.AddressList));
            OutputLineIf(!string.IsNullOrEmpty(namespaceDetails.Deployment), "Deployment", namespaceDetails.Deployment);
            OutputLineIf(!string.IsNullOrEmpty(namespaceDetails.HostName), "HostName", namespaceDetails.HostName);
            OutputLineIf(!string.IsNullOrEmpty(namespaceDetails.GatewayDnsFormat), "GatewayDnsFormat", namespaceDetails.GatewayDnsFormat);
        }

        static void ExecuteNetStatCommand(CommandLineApplication app)
        {
            app.Out.WriteLine(CommandSeparatorLine);
            ExecuteProcess(
                "netstat.exe",
                "-ano -p tcp",
                TimeSpan.FromSeconds(30),
                (s, e) => app.Out.WriteLine(e.Data),
                (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        app.Out.WriteLine("[netstat] ERROR: " + e.Data);
                    }
                },
                throwOnNonZero: true);
        }

        static async Task ExecutePlatformCommandAsync(CommandLineApplication app, NamespaceDetails namespaceDetails)
        {
            app.Out.WriteLine(CommandSeparatorLine);
            const string OutputFormat = "{0,-27}{1}";
            app.Out.WriteLine(OutputFormat, "OSVersion:", Environment.OSVersion);
            app.Out.WriteLine(OutputFormat, "ProcessorCount:", Environment.ProcessorCount);
            app.Out.WriteLine(OutputFormat, "Is64BitOperatingSystem:", Environment.Is64BitOperatingSystem);
            app.Out.WriteLine(OutputFormat, "CLR Version:", Environment.Version);
            app.Out.WriteLine(OutputFormat, "mscorlib Assembly Version:", typeof(object).Assembly.GetName().Version);
            app.Out.WriteLine(OutputFormat, "mscorlib File Version:", FileVersionInfo.GetVersionInfo(typeof(object).Assembly.Location).FileVersion);

            if (!string.IsNullOrEmpty(namespaceDetails.ServiceNamespace))
            {
                var webRequest = WebRequest.CreateHttp(new Uri($"https://{namespaceDetails.ServiceNamespace}"));
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
