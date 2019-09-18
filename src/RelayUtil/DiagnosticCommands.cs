// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Relay;
    using Microsoft.Extensions.CommandLineUtils;

    class DiagnosticCommands
    {
        const string NamespaceOrConnectionStringArgumentName = "namespaceOrConnectionString";
        const string NamespaceOrConnectionStringArgumentDescription = "Relay Namespace or ConnectionString";

        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.Command("diag", (diagCommand) =>
            {
                // TODO
                diagCommand.Description = "Operations for diagnosing relay/hc issues (Analyze)";
                diagCommand.HelpOption(CommandStrings.HelpTemplate);
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

                diagCommand.OnExecute(async () =>
                {
                    bool defaultOptions = !diagCommand.Options.Any(o => o.HasValue());

                    // Run netstat before we try to lookup the namespace to keep ourself out of the results
                    // NetStat output isn't part of the default run, must specify --netstat or --all
                    if (netStatOption.HasValue() || allOption.HasValue())
                    {                        
                        ExecuteNetStatCommand(diagCommand.Out);
                    }

                    NamespaceDetails namespaceDetails = default;
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(namespaceOrConnectionStringArgument); // Might not be present
                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        var connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
                        try
                        {
                            namespaceDetails = await NamespaceUtility.GetNamespaceDetailsAsync(connectionStringBuilder.Endpoint.Host);
                        }
                        catch (Exception e)
                        {
                            diagCommand.Out.WriteLine($"Error getting namespace details. {e.GetType()}: {e.Message}");
                        }
                    }

                    if (defaultOptions || osOption.HasValue() || allOption.HasValue())
                    {
                        await ExecutePlatformCommandAsync(diagCommand.Out, namespaceDetails);
                    }

                    if (defaultOptions || namespaceOption.HasValue() || allOption.HasValue())
                    {
                        ExecuteNamespaceCommand(diagCommand.Out, namespaceDetails);
                    }

                    if (defaultOptions || portsOption.HasValue() || allOption.HasValue() || instancePortsOption.HasValue())
                    {
                        int gatewayCount = 1;
                        if (instancePortsOption.HasValue())
                        {
                            gatewayCount = int.Parse(instancePortsOption.Value());
                        }

                        await ExecutePortsCommandAsync(diagCommand.Out, namespaceDetails, gatewayCount);
                    }

                    return 0;
                });
            });
        }

        static async Task ExecutePortsCommandAsync(TextWriter output, NamespaceDetails namespaceDetails, int gatewayCount)
        {
            PrintCommandHeader(output, "Ports");
            if (string.IsNullOrEmpty(namespaceDetails.ServiceNamespace))
            {
                output.WriteLine($"{NamespaceOrConnectionStringArgumentDescription} is required");
                return;
            }

            output.Write(await NetworkUtility.VerifyRelayPortsAsync(namespaceDetails.ServiceNamespace, output));

            var tasks = new List<Task<string>>();
            for (int i = 0; i < gatewayCount; i++)
            {
                // Build the ILPIP DNS name and run it for G0 through G63
                var task = NetworkUtility.VerifyRelayPortsAsync(string.Format(namespaceDetails.GatewayDnsFormat, i), output);
                tasks.Add(task);
            }

            foreach (Task<string> task in tasks)
            {
                string result = await task;
                output.Write(result);
            }
        }

        static void ExecuteNamespaceCommand(TextWriter output, NamespaceDetails namespaceDetails)
        {
            PrintCommandHeader(output, "Namespace Details");
            const string OutputFormat = "{0,-26}{1}";

            bool foundAny = false;
            void OutputLineIf(bool condition, string name, Func<string> valueSelector)
            {
                if (condition)
                {
                    output.WriteLine(OutputFormat, name + ":", valueSelector());
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
                output.WriteLine($"{NamespaceOrConnectionStringArgumentDescription} is required");
            }
        }

        static void ExecuteNetStatCommand(TextWriter output)
        {
            PrintCommandHeader(output, "NetStat.exe");
            ExecuteProcess(
                "netstat.exe",
                "-ano -p tcp",
                TimeSpan.FromSeconds(30),
                (s) => output.WriteLine(s),
                (s) => output.WriteLine("ERROR: " + s),
                throwOnNonZero: true);
        }

        static async Task ExecutePlatformCommandAsync(TextWriter output, NamespaceDetails namespaceDetails)
        {
            PrintCommandHeader(output, "OS/Platform");
            const string OutputFormat = "{0,-26}{1}";
            output.WriteLine(OutputFormat, "OSVersion:", Environment.OSVersion);
            output.WriteLine(OutputFormat, "ProcessorCount:", Environment.ProcessorCount);
            output.WriteLine(OutputFormat, "Is64BitOperatingSystem:", Environment.Is64BitOperatingSystem);
            output.WriteLine(OutputFormat, "CLR Version:", Environment.Version);
            output.WriteLine(OutputFormat, "mscorlib AssemblyVersion:", typeof(object).Assembly.GetName().Version);
            output.WriteLine(OutputFormat, "mscorlib FileVersion:", FileVersionInfo.GetVersionInfo(typeof(object).Assembly.Location).FileVersion);

            if (!string.IsNullOrEmpty(namespaceDetails.ServiceNamespace))
            {
                var webRequest = WebRequest.CreateHttp(new Uri($"https://{namespaceDetails.ServiceNamespace}"));
                webRequest.Method = "GET";
                using (var response = await webRequest.GetResponseAsync())
                {
                    output.WriteLine(OutputFormat, "Azure Time:", response.Headers["Date"]); // RFC1123
                }
            }

            var utcNow = DateTime.UtcNow;
            output.WriteLine(OutputFormat, "Machine Time(UTC):", utcNow.ToString(DateTimeFormatInfo.InvariantInfo.RFC1123Pattern));
            output.WriteLine(OutputFormat, "Machine Time(Local):", utcNow.ToLocalTime().ToString("ddd, dd MMM yyyy HH':'mm':'ss '('zzz')'")); // Like RFC1123Pattern but with zzz for timezone offset
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

        static void PrintCommandHeader(TextWriter output, string commandName)
        {            
            output.WriteLine($"{output.NewLine}****************************** {commandName} ******************************");
        }
    }
}
