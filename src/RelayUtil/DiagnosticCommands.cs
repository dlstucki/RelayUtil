// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.ServiceBus;

    class DiagnosticCommands : RelayCommands
    {
        const string OutputFormat = "{0,-26}{1}";
        const string NamespaceOrConnectionStringArgumentName = "namespaceOrConnectionString";
        const string NamespaceOrConnectionStringArgumentDescription = "Relay Namespace or ConnectionString";

        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.RelayCommand("diag", (diagCommand) =>
            {
                diagCommand.Description = "Operations for diagnosing relay/hc issues (Analyze)";
                var namespaceOrConnectionStringArgument = diagCommand.Argument(NamespaceOrConnectionStringArgumentName, NamespaceOrConnectionStringArgumentDescription);

                CommandOption allOption = diagCommand.Option(
                    "-a|--all",
                    "Show all details",
                    CommandOptionType.NoValue);

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

                CommandOption instancePortsOption = diagCommand.Option(
                    "-ip|--instance-ports <instanceCount>",
                    "Probe Relay Instance Level Ports",
                    CommandOptionType.SingleValue);

                CommandOption osOption = diagCommand.Option(
                    "-o|--os",
                    "Display Platform/OS/.NET information",
                    CommandOptionType.NoValue);

                CommandOption protocolOption = diagCommand.AddSecurityProtocolOption();

                diagCommand.OnExecute(async () =>
                {
                    ConfigureSecurityProtocol(protocolOption);

                    bool defaultOptions = !allOption.HasValue() && !namespaceOption.HasValue() && !netStatOption.HasValue() &&
                        !portsOption.HasValue() && !instancePortsOption.HasValue() && !osOption.HasValue();

                    // Run netstat before we try to lookup the namespace to keep ourself out of the results
                    // NetStat output isn't part of the default run, must specify --netstat or --all
                    if (netStatOption.HasValue() || allOption.HasValue())
                    {
                        ExecuteNetStatCommand();
                    }

                    NamespaceDetails namespaceDetails = default;
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(namespaceOrConnectionStringArgument); // Might not be present
                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                        try
                        {
                            namespaceDetails = await NamespaceUtility.GetNamespaceDetailsAsync(connectionStringBuilder.Endpoints.First().Host);
                        }
                        catch (Exception e)
                        {
                            RelayTraceSource.TraceException(e, "Getting namespace details");
                        }
                    }

                    if (defaultOptions || osOption.HasValue() || allOption.HasValue())
                    {
                        await ExecutePlatformCommandAsync(namespaceDetails);
                    }

                    if (defaultOptions || namespaceOption.HasValue() || allOption.HasValue())
                    {
                        ExecuteNamespaceCommand(namespaceDetails);
                    }

                    if (defaultOptions || portsOption.HasValue() || allOption.HasValue() || instancePortsOption.HasValue())
                    {
                        int gatewayCount = GetIntOption(instancePortsOption, 1);
                        await ExecutePortsCommandAsync(namespaceDetails, gatewayCount);
                    }

                    return 0;
                });
            });
        }

        static async Task ExecutePortsCommandAsync(NamespaceDetails namespaceDetails, int gatewayCount)
        {
            TraceCommandHeader("Ports");
            if (string.IsNullOrEmpty(namespaceDetails.ServiceNamespace))
            {
                TraceMissingArgument(NamespaceOrConnectionStringArgumentDescription);
                return;
            }

            RelayTraceSource.TraceInfo(await NetworkUtility.VerifyRelayPortsAsync(namespaceDetails.ServiceNamespace));

            var tasks = new List<Task<string>>();
            for (int i = 0; i < gatewayCount; i++)
            {
                // Build the ILPIP DNS name and run it for G0 through G63
                var task = NetworkUtility.VerifyRelayPortsAsync(string.Format(namespaceDetails.GatewayDnsFormat, i));
                tasks.Add(task);
            }

            foreach (Task<string> task in tasks)
            {
                string result = await task;
                RelayTraceSource.TraceInfo(result);
            }
        }

        static void ExecuteNamespaceCommand(NamespaceDetails namespaceDetails)
        {
            TraceCommandHeader("Namespace Details");

            bool foundAny = false;
            void OutputLineIf(bool condition, string name, Func<string> valueSelector)
            {
                if (condition)
                {
                    RelayTraceSource.TraceInfo(OutputFormat, name + ":", valueSelector());
                    foundAny = true;
                }
            }

            OutputLineIf(!string.IsNullOrEmpty(namespaceDetails.ServiceNamespace), "ServiceNamespace", () => namespaceDetails.ServiceNamespace);
            OutputLineIf(namespaceDetails.AddressList?.Length > 0, "Address(VIP)", () => string.Join(",", (IEnumerable<IPAddress>)namespaceDetails.AddressList));
            OutputLineIf(!string.IsNullOrEmpty(namespaceDetails.Deployment), "Deployment", () => namespaceDetails.Deployment);
            OutputLineIf(!string.IsNullOrEmpty(namespaceDetails.HostName), "HostName", () => namespaceDetails.HostName);
            OutputLineIf(!string.IsNullOrEmpty(namespaceDetails.GatewayDnsFormat), "GatewayDnsFormat", () => namespaceDetails.GatewayDnsFormat);

            if (!foundAny)
            {
                TraceMissingArgument(NamespaceOrConnectionStringArgumentDescription);
            }
        }

        static void ExecuteNetStatCommand()
        {
            TraceCommandHeader("NetStat.exe");
            ExecuteProcess(
                "netstat.exe",
                "-ano -p tcp",
                TimeSpan.FromSeconds(30),
                (s) => RelayTraceSource.TraceInfo(s),
                (s) => RelayTraceSource.TraceError("ERROR: " + s),
                throwOnNonZero: true);
        }

        static async Task ExecutePlatformCommandAsync(NamespaceDetails namespaceDetails)
        {
            TraceCommandHeader("OS/Platform");
            RelayTraceSource.TraceInfo("OSVersion:", Environment.OSVersion);
            RelayTraceSource.TraceInfo(OutputFormat, "ProcessorCount:", Environment.ProcessorCount);
            RelayTraceSource.TraceInfo(OutputFormat, "Is64BitOperatingSystem:", Environment.Is64BitOperatingSystem);
            RelayTraceSource.TraceInfo(OutputFormat, "CLR Version:", Environment.Version);
            RelayTraceSource.TraceInfo(OutputFormat, "mscorlib AssemblyVersion:", typeof(object).Assembly.GetName().Version);
            RelayTraceSource.TraceInfo(OutputFormat, "mscorlib FileVersion:", FileVersionInfo.GetVersionInfo(typeof(object).Assembly.Location).FileVersion);

            if (!string.IsNullOrEmpty(namespaceDetails.ServiceNamespace))
            {
                await GetCloudServiceTimeAsync(namespaceDetails.ServiceNamespace);
            }

            var utcNow = DateTime.UtcNow;
            RelayTraceSource.TraceInfo(OutputFormat, "Machine Time(UTC):", utcNow.ToString(DateTimeFormatInfo.InvariantInfo.RFC1123Pattern));
            RelayTraceSource.TraceInfo(OutputFormat, "Machine Time(Local):", utcNow.ToLocalTime().ToString("ddd, dd MMM yyyy HH':'mm':'ss '('zzz')'")); // Like RFC1123Pattern but with zzz for timezone offset
        }

        static async Task GetCloudServiceTimeAsync(string serviceNamespace)
        {
            try
            {
                var webRequest = WebRequest.CreateHttp(new Uri($"https://{serviceNamespace}"));
                webRequest.Method = "GET";
                using (var response = await webRequest.GetResponseAsync())
                {
                    RelayTraceSource.TraceInfo(OutputFormat, "Azure Time:", response.Headers["Date"]); // RFC1123
                }
            }
            catch (Exception exception)
            {
                RelayTraceSource.TraceException(exception, "Getting current time from Relay cloud service");
            }
        }

        static int ExecuteProcess(string filePath, string args, TimeSpan timeout, Action<string> outputDataReceived, Action<string> errorDataReceived, bool throwOnNonZero)
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
            using (var outputCompletedEvent = new CountdownEvent(2))
            {
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        outputDataReceived?.Invoke(e.Data);
                    }
                    else
                    {
                        outputCompletedEvent.Signal();
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        errorDataReceived?.Invoke(e.Data);
                    }
                    else
                    {
                        outputCompletedEvent.Signal();
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit((int)timeout.TotalMilliseconds) || !outputCompletedEvent.Wait(timeout))
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
