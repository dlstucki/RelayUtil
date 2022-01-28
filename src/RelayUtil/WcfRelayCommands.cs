// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil.WcfRelays
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Security.Authentication;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Description;
    using System.ServiceModel.Web;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    using Microsoft.ServiceBus.Tracing;
    using TokenProvider = Microsoft.ServiceBus.TokenProvider;

    class WcfRelayCommands : RelayCommands
    {
        const string DefaultPath = "RelayUtilWcf";
        private const string BindingOptionTemplate = "-b|--binding <binding>";
        private const string BindingOptionDescription = "The Wcf Binding. (nettcp|basichttp|webhttp|wshttp|netoneway|netevent)";

        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.RelayCommand("wcf", (wcfCommand) =>
            {
                wcfCommand.Description = "Operations for WcfRelays (CRUD, Test)";
                ConfigureWcfCreateCommand(wcfCommand);
                ConfigureWcfListCommand(wcfCommand);
                ConfigureWcfDeleteCommand(wcfCommand);
                ConfigureWcfCountCommand(wcfCommand);
                ConfigureWcfListenCommand(wcfCommand);
                ConfigureWcfSendCommand(wcfCommand);
                ConfigureWcfTestCommand(wcfCommand);

                wcfCommand.OnExecute(() =>
                {
                    wcfCommand.ShowHelp();
                    return 0;
                });
            });
        }

        static void ConfigureWcfCreateCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.RelayCommand("create", (createCmd) =>
            {
                createCmd.Description = "Create a WcfRelay";

                var pathArgument = createCmd.Argument("path", "WcfRelay path");
                var connectionStringArgument = createCmd.Argument("connectionString", "Relay ConnectionString");

                var relayTypeOption = createCmd.Option(
                    "-t|--relaytype <relaytype>", "The RelayType (nettcp|http)", CommandOptionType.SingleValue);
                var requireClientAuthOption = createCmd.Option(
                    CommandStrings.RequiresClientAuthTemplate, CommandStrings.RequiresClientAuthDescription, CommandOptionType.SingleValue);
                var protocolOption = createCmd.AddSecurityProtocolOption();

                createCmd.OnExecute(async () =>
                {
                    ConfigureSecurityProtocol(protocolOption);
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(pathArgument.Value))
                    {
                        TraceMissingArgument(string.IsNullOrEmpty(connectionString) ? connectionStringArgument.Name : pathArgument.Name);
                        createCmd.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    RelayTraceSource.TraceInfo($"Creating WcfRelay '{pathArgument.Value}' in {connectionStringBuilder.Endpoints.First().Host}...");
                    var relayDescription = new RelayDescription(pathArgument.Value, GetRelayType(relayTypeOption));
                    relayDescription.RequiresClientAuthorization = GetBoolOption(requireClientAuthOption, true);
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                    await namespaceManager.CreateRelayAsync(relayDescription);
                    RelayTraceSource.TraceInfo($"Creating WcfRelay '{pathArgument.Value}' in {connectionStringBuilder.Endpoints.First().Host} succeeded");
                    return 0;
                });
            });
        }

        static void ConfigureWcfListCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.RelayCommand("list", (listCmd) =>
            {
                listCmd.Description = "List WcfRelay(s)";
                var pathArgument = listCmd.Argument("path", "Optional WcfRelay path");
                var connectionStringArgument = listCmd.Argument("connectionString", "Relay ConnectionString");
                var protocolOption = listCmd.AddSecurityProtocolOption();

                listCmd.OnExecute(async () =>
                {
                    ConfigureSecurityProtocol(protocolOption);
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        TraceMissingArgument(connectionStringArgument.Name);
                        listCmd.ShowHelp();
                        return 1;
                    }

                    string path = pathArgument.Value;

                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                    IEnumerable<RelayDescription> relays;
                    if (string.IsNullOrEmpty(path))
                    {
                        RelayTraceSource.TraceInfo($"Listing WcfRelays for {connectionStringBuilder.Endpoints.First().Host}");
                        relays = await namespaceManager.GetRelaysAsync();
                    }
                    else
                    {
                        RelayTraceSource.TraceInfo($"Getting WcfRelay {connectionStringBuilder.Endpoints.First().Host}/{path}");
                        relays = new[] { await namespaceManager.GetRelayAsync(path) };
                    }

                    RelayTraceSource.TraceInfo($"{"Path",-38} {"ListenerCount",-15} {"RequiresClientAuth",-20} RelayType");
                    foreach (var relay in relays)
                    {
                        RelayTraceSource.TraceInfo($"{relay.Path,-38} {relay.ListenerCount,-15} {relay.RequiresClientAuthorization,-20} {relay.RelayType}");
                    }

                    return 0;
                });
            });
        }

        static void ConfigureWcfDeleteCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.RelayCommand("delete", (deleteCmd) =>
            {
                deleteCmd.Description = "Delete a WcfRelay";
                var pathArgument = deleteCmd.Argument("path", "WcfRelay path");
                var connectionStringArgument = deleteCmd.Argument("connectionString", "Relay ConnectionString");
                var protocolOption = deleteCmd.AddSecurityProtocolOption();

                deleteCmd.OnExecute(async () =>
                {
                    ConfigureSecurityProtocol(protocolOption);
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(pathArgument.Value))
                    {
                        TraceMissingArgument(string.IsNullOrEmpty(connectionString) ? connectionStringArgument.Name : pathArgument.Name);
                        deleteCmd.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    RelayTraceSource.TraceInfo($"Deleting WcfRelay '{pathArgument.Value}' in {connectionStringBuilder.Endpoints.First().Host}...");
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                    await namespaceManager.DeleteRelayAsync(pathArgument.Value);
                    RelayTraceSource.TraceInfo($"Deleting WcfRelay '{pathArgument.Value}' in {connectionStringBuilder.Endpoints.First().Host} succeeded");
                    return 0;
                });
            });
        }

        static void ConfigureWcfCountCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.RelayCommand("count", (countCommand) =>
            {
                countCommand.Description = "Get WCF Relay Count";

                var connectionStringArgument = countCommand.Argument("connectionString", "Relay ConnectionString");

                countCommand.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        TraceMissingArgument(connectionStringArgument.Name);
                        countCommand.ShowHelp();
                        return 1;
                    }

                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                    Uri namespaceUri = namespaceManager.Address;
                    string namespaceHost = namespaceUri.Host;
                    var tokenProvider = namespaceManager.Settings.TokenProvider;

                    RelayTraceSource.TraceVerbose($"Getting WcfRelay count for '{namespaceUri}");

                    int count = await NamespaceUtility.GetEntityCountAsync(namespaceUri, tokenProvider, "Relays");
                    RelayTraceSource.TraceInfo(string.Format($"{{0,-{namespaceHost.Length}}}  {{1}}", "Namespace", "WcfRelayCount"));
                    RelayTraceSource.TraceInfo(string.Format($"{{0,-{namespaceHost.Length}}}  {{1}}", namespaceHost, count));

                    return 0;
                });
            });
        }

        static void ConfigureWcfListenCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.RelayCommand("listen", (listenCmd) =>
            {
                listenCmd.Description = "WcfRelay listen command";
                var pathArgument = listenCmd.Argument("path", "WcfRelay path");
                var connectionStringArgument = listenCmd.Argument("connectionString", "Relay ConnectionString");

                var bindingOption = listenCmd.Option(BindingOptionTemplate, BindingOptionDescription, CommandOptionType.SingleValue);
                var noClientAuthOption = listenCmd.Option("--no-client-auth", "Skip client authentication", CommandOptionType.NoValue);
                var connectivityModeOption = listenCmd.Option(CommandStrings.ConnectivityModeTemplate, CommandStrings.ConnectivityModeDescription, CommandOptionType.SingleValue);
                var responseOption = listenCmd.Option("--response <response>", "Response to return", CommandOptionType.SingleValue);
                var protocolOption = listenCmd.AddSecurityProtocolOption();
                var maxConcurrentSessionsOption = listenCmd.Option("--max-concurrent-sessions <count>", "Max Concurrent Sessions", CommandOptionType.SingleValue);
                var maxConcurrentInstancesOption = listenCmd.Option("--max-concurrent-instances <count>", "Max Concurrent Instances", CommandOptionType.SingleValue);

                listenCmd.OnExecute(() =>
                {
                    ConfigureSecurityProtocol(protocolOption);

                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    string path = pathArgument.Value ?? DefaultPath;
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        TraceMissingArgument(connectionStringArgument.Name);
                        listenCmd.ShowHelp();
                        return 1;
                    }

                    Binding binding = GetBinding(bindingOption, noClientAuthOption, null, null, null);
                    try
                    {
                        var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                        namespaceManager.Settings.OperationTimeout = TimeSpan.FromSeconds(5);
                        if (namespaceManager.RelayExists(path))
                        {
                            dynamic dynamicBinding = binding;
                            dynamicBinding.IsDynamic = false;
                        }
                    }
                    catch (Exception exception)
                    {
                        RelayTraceSource.TraceException(exception, "Error calling RelayExists");
                    }

                    var throttlingBehavior = GetThrottlingBehavior(maxConcurrentSessionsOption, maxConcurrentInstancesOption);
                    return VerifyListen(connectionString, path, binding, GetConnectivityMode(connectivityModeOption), responseOption.Value(), throttlingBehavior);
                });
            });
        }

        static void ConfigureWcfSendCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.RelayCommand("send", (sendCmd) =>
            {
                sendCmd.Description = "WcfRelay send command";
                var pathArgument = sendCmd.Argument("path", "WcfRelay path");
                var connectionStringArgument = sendCmd.Argument("connectionString", "Relay ConnectionString");

                var numberOption = sendCmd.Option(CommandStrings.NumberTemplate, CommandStrings.NumberDescription, CommandOptionType.SingleValue);
                var bindingOption = sendCmd.Option(BindingOptionTemplate, BindingOptionDescription, CommandOptionType.SingleValue);
                var noClientAuthOption = sendCmd.Option("--no-client-auth", "Skip client authentication", CommandOptionType.NoValue);
                var connectivityModeOption = sendCmd.Option(CommandStrings.ConnectivityModeTemplate, CommandStrings.ConnectivityModeDescription, CommandOptionType.SingleValue);
                var requestOption = sendCmd.Option(CommandStrings.RequestTemplate, CommandStrings.RequestDescription, CommandOptionType.SingleValue);
                var requestDelayOption = sendCmd.Option("-rd|--request-delay <delay>", "A TimeSpan indicating how long the listener should delay before responding to request", CommandOptionType.SingleValue);
                var openTimeoutOption = sendCmd.Option("-ot|--open-timeout <timeout>", "A TimeSpan for configuring the Binding.OpenTimeout", CommandOptionType.SingleValue);
                var sendTimeoutOption = sendCmd.Option("-st|--send-timeout <timeout>", "A TimeSpan for configuring the Binding.SendTimeout", CommandOptionType.SingleValue);
                var readTimeoutOption = sendCmd.Option("-rt|--receive-timeout <timeout>", "A TimeSpan for configuring the Binding.ReceiveTimeout", CommandOptionType.SingleValue);
                var protocolOption = sendCmd.AddSecurityProtocolOption();

                sendCmd.OnExecute(() =>
                {
                    ConfigureSecurityProtocol(protocolOption);

                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    string path = pathArgument.Value ?? DefaultPath;
                    string request = GetStringOption(requestOption, "Test Message Data");
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        TraceMissingArgument(connectionStringArgument.Name);
                        sendCmd.ShowHelp();
                        return 1;
                    }

                    int number = GetIntOption(numberOption, 1);
                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    Binding binding = GetBinding(bindingOption, noClientAuthOption, openTimeoutOption, sendTimeoutOption, readTimeoutOption);
                    ConnectivityMode connectivityMode = GetConnectivityMode(connectivityModeOption);
                    TimeSpan requestDelay = requestDelayOption.HasValue() ? TimeSpan.Parse(requestDelayOption.Value()) : TimeSpan.Zero;
                    return VerifySend(request, connectionStringBuilder, path, number, binding, noClientAuthOption.HasValue(), connectivityMode, requestDelay);
                });
            });
        }

        static void ConfigureWcfTestCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.RelayCommand("test", (testCmd) =>
            {
                testCmd.Description = "WcfRelay tests";
                var connectionStringArgument = testCmd.Argument("connectionString", "Relay ConnectionString");
                var numberOption = testCmd.Option(CommandStrings.NumberTemplate, CommandStrings.NumberDescription, CommandOptionType.SingleValue);
                var testNameOption = testCmd.Option("-t|--tests", "A regex to pick which tests to run", CommandOptionType.SingleValue);

                testCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        TraceMissingArgument(connectionStringArgument.Name);
                        testCmd.ShowHelp();
                        return 1;
                    }

                    int number = GetIntOption(numberOption, 1);
                    return await RunWcfTestsAsync(connectionString, number, testNameOption.Value());
                });
            });
        }


        static void SetServicePointManagerDefaultSslProtocols(SslProtocols sslProtocols)
        {
            FieldInfo s_defaultSslProtocols = typeof(ServicePointManager).GetField("s_defaultSslProtocols", BindingFlags.Static | BindingFlags.NonPublic);
            if (s_defaultSslProtocols != null)
            {
                s_defaultSslProtocols.SetValue(null, sslProtocols);
            }
            else
            {
                RelayTraceSource.TraceWarning("ServicePointManager.s_defaultSslProtocols field not found.");
            }
        }

        public static int VerifyListen(string connectionString, string path, Binding binding, ConnectivityMode connectivityMode, string response, ServiceThrottlingBehavior throttlingBehavior)
        {
            RelayTraceSource.TraceInfo($"Open relay listener using {binding.GetType().Name}, ConnectivityMode.{connectivityMode}...");
            ServiceHost serviceHost = null;
            try
            {
                var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);                
                var tp = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionStringBuilder.SharedAccessKeyName, connectionStringBuilder.SharedAccessKey);

                string relayNamespace = connectionStringBuilder.Endpoints.First().Host;
                ServiceBusEnvironment.SystemConnectivity.Mode = connectivityMode;
                EchoService.DefaultResponse = response;
                if (!(binding is WebHttpRelayBinding))
                {
                    serviceHost = new ServiceHost(typeof(EchoService));
                }
                else
                {
                    serviceHost = new WebServiceHost(typeof(EchoService));
                }

                Type contractType = IsOneWay(binding) ? typeof(ITestOneway) : typeof(IEcho);
                ServiceEndpoint endpoint = serviceHost.AddServiceEndpoint(contractType, binding, new Uri($"{binding.Scheme}://{relayNamespace}/{path}"));
                var listenerActivityId = Guid.NewGuid();
                RelayTraceSource.TraceVerbose($"Listener ActivityId:{listenerActivityId}");
                endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tp) { ActivityId = listenerActivityId });
                serviceHost.Description.Behaviors.Add(throttlingBehavior);

                // Trace status changes
                var connectionStatus = new ConnectionStatusBehavior();
                connectionStatus.Connecting += (s, e) => RelayTraceSource.TraceException(connectionStatus.LastError, TraceEventType.Warning, "Relay listener Re-Connecting");
                connectionStatus.Online += (s, e) => RelayTraceSource.Instance.TraceEvent(TraceEventType.Information, (int)ConsoleColor.Green, "Relay Listener is online");
                EventHandler offlineHandler = (s, e) => RelayTraceSource.TraceException(connectionStatus.LastError, "Relay Listener is OFFLINE");
                connectionStatus.Offline += offlineHandler;
                endpoint.EndpointBehaviors.Add(connectionStatus);
                serviceHost.Faulted += (s, e) => RelayTraceSource.TraceException(connectionStatus.LastError, "Relay listener ServiceHost Faulted");

                serviceHost.Open();
                RelayTraceSource.TraceInfo("Relay listener \"" + endpoint.Address.Uri + "\" is open");
                RelayTraceSource.TraceInfo("Press <ENTER> to close the listener ");
                Console.ReadLine();

                RelayTraceSource.TraceInfo("Closing Connection...");
                connectionStatus.Offline -= offlineHandler; // Avoid a spurious trace on expected shutdown.
                serviceHost.Close();
                RelayTraceSource.TraceInfo("Closed");
                return 0;
            }
            catch (Exception)
            {
                serviceHost?.Abort();
                throw;
            }
        }

        public static int VerifySend(
            string request, ServiceBusConnectionStringBuilder connectionString, string path, int number, Binding binding, bool noClientAuth, ConnectivityMode connectivityMode, TimeSpan requestDelay)
        {
            RelayTraceSource.TraceInfo($"Send to relay listener using {binding.GetType().Name}, ConnectivityMode.{connectivityMode}...");
            string relayNamespace = connectionString.Endpoints.First().Host;
            string keyName = connectionString.SharedAccessKeyName;
            string keyValue = connectionString.SharedAccessKey;

            if (IsOneWay(binding))
            {
                return VerifySendCore<ITestOnewayClient>(request, relayNamespace, path, number, binding, noClientAuth, connectivityMode, keyName, keyValue, requestDelay);
            }
            else
            {
                return VerifySendCore<IEchoClient>(request, relayNamespace, path, number, binding, noClientAuth, connectivityMode, keyName, keyValue, requestDelay);
            }
        }

        static int VerifySendCore<TChannel>(
            string request, string relayNamespace, string path, int number, Binding binding, bool noClientAuth, ConnectivityMode connectivityMode, string keyName, string keyValue, TimeSpan requestDelay)
            where TChannel : class, IClientChannel
        {
            ChannelFactory<TChannel> channelFactory = null;
            TChannel channel = null;
            try
            {
                ServiceBusEnvironment.SystemConnectivity.Mode = connectivityMode;
                Uri address = new Uri($"{binding.Scheme}://{relayNamespace}/{path}");
                if (binding is WebHttpRelayBinding)
                {
                    channelFactory = new WebChannelFactory<TChannel>(binding, address);
                }
                else
                {
                    channelFactory = new ChannelFactory<TChannel>(binding, new EndpointAddress(address));
                }

                var tp = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, keyValue);
                if (!noClientAuth)
                {
                    channelFactory.Endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tp));
                }

                RelayTraceSource.TraceVerbose("Sender opening channel factory");
                var stopwatch = new Stopwatch();
                stopwatch.Restart();
                channelFactory.Open();
                RelayTraceSource.TraceVerbose($"Sender opened channel factory in {stopwatch.ElapsedMilliseconds} ms");

                channel = channelFactory.CreateChannel();
                RelayTraceSource.TraceInfo("Sender opening channel");
                using (new OperationContextScope(channel))
                {
                    Guid trackingId = Guid.NewGuid();
                    RelayTraceSource.TraceVerbose($"Channel TrackingId:{trackingId}");
                    if (binding.MessageVersion.Addressing != AddressingVersion.None)
                    {
                        OperationContext.Current.OutgoingMessageHeaders.MessageId = new UniqueId(trackingId);
                    }

                    stopwatch.Restart();
                    channel.Open();
                    RelayTraceSource.TraceVerbose($"Sender opened channel in {stopwatch.ElapsedMilliseconds} ms");                    
                    RelayTraceSource.TraceVerbose($"Channel SessionId:{channel.SessionId}");
                }

                for (int i = 0; i < number; i++)
                {
                    using (new OperationContextScope(channel))
                    {
                        var messageId = Guid.NewGuid();
                        RelayTraceSource.TraceVerbose($"Sending MessageId:{messageId}");
                        if (binding.MessageVersion.Addressing != AddressingVersion.None)
                        {
                            OperationContext.Current.OutgoingMessageHeaders.MessageId = new UniqueId(messageId);
                        }

                        stopwatch.Restart();
                        if (channel is IEchoClient echoChannel)
                        {
                            string response = echoChannel.Echo(DateTime.UtcNow, request, requestDelay);
                            RelayTraceSource.TraceInfo($"Sender received response: {response} ({stopwatch.ElapsedMilliseconds} ms)");
                        }
                        else if (channel is ITestOnewayClient onewayClient)
                        {
                            onewayClient.Operation(DateTime.UtcNow, request);
                            RelayTraceSource.TraceInfo($"Sender sent oneway request: {request} ({stopwatch.ElapsedMilliseconds} ms)");
                        }
                        else
                        {
                            throw new NotSupportedException($"Contract {typeof(TChannel)} is not supported");
                        }
                    }
                }

                RelayTraceSource.TraceInfo("Sender closing channel");
                stopwatch.Restart();
                channel.Close();
                RelayTraceSource.TraceVerbose($"Sender closed channel in {stopwatch.ElapsedMilliseconds} ms");
                channel = null;

                RelayTraceSource.TraceVerbose("Sender closing channel factory");
                stopwatch.Restart();
                channelFactory.Close();
                RelayTraceSource.TraceVerbose($"Sender closed channel factory in {stopwatch.ElapsedMilliseconds} ms");
                channelFactory = null;

                return 0;
            }
            finally
            {
                channel?.Abort();
                channelFactory?.Abort();
            }
        }

        public static async Task<int> RunWcfTestsAsync(string connectionString, int numberOfRequests, string testNamePattern)
        {
            var originalLevel = RelayTraceSource.Instance.Switch.Level;
            if (originalLevel == SourceLevels.Information)
            {
                // If the -v parameter wasn't specified, limit output to warning here;
                RelayTraceSource.Instance.Switch.Level = SourceLevels.Warning;
            }

            Regex testNameRegex = string.IsNullOrEmpty(testNamePattern) ? null : new Regex(testNamePattern, RegexOptions.IgnoreCase);

            var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
            var testResults = new List<TestResult>();

            connectionStringBuilder.EntityPath = "NetTcpRelayBinding_AutoConnectivity_Dynamic";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new NetTcpRelayBinding(), ConnectivityMode.AutoDetect, default, numberOfRequests));

            connectionStringBuilder.EntityPath = "NetTcpRelayBinding_HttpsConnectivity_Dynamic";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new NetTcpRelayBinding(), ConnectivityMode.Https, default, numberOfRequests));

            connectionStringBuilder.EntityPath = "NetTcpRelayBinding_TcpConnectivity_Persistent";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new NetTcpRelayBinding { IsDynamic = false }, ConnectivityMode.Tcp, RelayType.NetTcp, numberOfRequests));

            connectionStringBuilder.EntityPath = "NetTcpRelayBinding_HttpsConnectivity_Persistent";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new NetTcpRelayBinding { IsDynamic = false }, ConnectivityMode.Https, RelayType.NetTcp, numberOfRequests));

            connectionStringBuilder.EntityPath = "BasicHttpRelayBinding_AutoConnectivity_Dynamic";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new BasicHttpRelayBinding(), ConnectivityMode.AutoDetect, null, numberOfRequests));

            connectionStringBuilder.EntityPath = "BasicHttpRelayBinding_TcpConnectivity_Dynamic";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new BasicHttpRelayBinding(), ConnectivityMode.Tcp, null, numberOfRequests));

            connectionStringBuilder.EntityPath = "BasicHttpRelayBinding_HttpsConnectivity_Persistent";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new BasicHttpRelayBinding { IsDynamic = false }, ConnectivityMode.Https, RelayType.Http, numberOfRequests));

            connectionStringBuilder.EntityPath = "WSHttpRelayBinding_AutoConnectivity_Dynamic";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new WS2007HttpRelayBinding(), ConnectivityMode.AutoDetect, null, numberOfRequests));

            connectionStringBuilder.EntityPath = "WSHttpRelayBinding_TcpConnectivity_Dynamic";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new WS2007HttpRelayBinding(), ConnectivityMode.Tcp, null, numberOfRequests));

            connectionStringBuilder.EntityPath = "WSHttpRelayBinding_HttpsConnectivity_Dynamic";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new WS2007HttpRelayBinding(), ConnectivityMode.Https, null, numberOfRequests));

            connectionStringBuilder.EntityPath = "NetOnewayRelayBinding_AutoConnectivity";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new NetOnewayRelayBinding(), ConnectivityMode.AutoDetect, null, numberOfRequests));

            connectionStringBuilder.EntityPath = "NetOnewayRelayBinding_HttpsConnectivity";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new NetOnewayRelayBinding(), ConnectivityMode.Https, null, numberOfRequests));

            connectionStringBuilder.EntityPath = "NetEventRelayBinding_TcpConnectivity";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new NetEventRelayBinding(), ConnectivityMode.Tcp, null, numberOfRequests));

            connectionStringBuilder.EntityPath = "NetEventRelayBinding_HttpsConnectivity";
            await RunTestAsync(
                connectionStringBuilder.EntityPath,
                testNameRegex,
                testResults,
                () => RunBindingScenarioAsync(connectionStringBuilder, new NetEventRelayBinding(), ConnectivityMode.Https, null, numberOfRequests));

            RelayTraceSource.Instance.Switch.Level = originalLevel;
            return ReportTestResults(testResults);
        }

        static async Task<int> RunBindingScenarioAsync(ServiceBusConnectionStringBuilder connectionString, Binding binding, ConnectivityMode connectivityMode, RelayType? createRelayOfType, int numberOfRequests)
        {
            Uri baseAddress = connectionString.Endpoints.First();
            string relayNamespace = baseAddress.Host;
            string path = connectionString.EntityPath;
            var tp = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionString.SharedAccessKeyName, connectionString.SharedAccessKey);

            ServiceHost serviceHost = null;
            bool createdRelay = false;
            try
            {
                if (createRelayOfType.HasValue)
                {
                    RelayTraceSource.TraceEvent(TraceEventType.Information, ConsoleColor.White, $"Creating WcfRelay '{path}'");
                    var relayDescription = new RelayDescription(path, createRelayOfType.Value);
                    var namespaceManager = new NamespaceManager(connectionString.Endpoints.First(), tp);
                    namespaceManager.Settings.OperationTimeout = TimeSpan.FromSeconds(20);
                    await namespaceManager.CreateRelayAsync(relayDescription);
                    createdRelay = true;
                    RelayTraceSource.TraceVerbose("Creating WcfRelay succeeded");
                }

                ServiceBusEnvironment.SystemConnectivity.Mode = connectivityMode;
                EchoService.DefaultResponse = "ResponsePayload";
                if (!(binding is WebHttpRelayBinding))
                {
                    serviceHost = new ServiceHost(typeof(EchoService));
                }
                else
                {
                    serviceHost = new WebServiceHost(typeof(EchoService));
                }

                Type contractType = IsOneWay(binding) ? typeof(ITestOneway) : typeof(IEcho);
                ServiceEndpoint endpoint = serviceHost.AddServiceEndpoint(contractType, binding, new Uri($"{binding.Scheme}://{relayNamespace}/{path}"));
                endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tp));

                // Trace status changes
                var connectionStatus = new ConnectionStatusBehavior();
                connectionStatus.Connecting += (s, e) => RelayTraceSource.TraceException(connectionStatus.LastError, TraceEventType.Warning, "Relay listener Re-Connecting");
                connectionStatus.Online += (s, e) => RelayTraceSource.Instance.TraceEvent(TraceEventType.Information, (int)ConsoleColor.Green, "Relay Listener is online");
                EventHandler offlineHandler = (s, e) => RelayTraceSource.TraceEvent(TraceEventType.Information, ConsoleColor.Yellow, "Relay Listener is OFFLINE");
                connectionStatus.Offline += offlineHandler;
                endpoint.EndpointBehaviors.Add(connectionStatus);
                serviceHost.Faulted += (s, e) => RelayTraceSource.TraceException(connectionStatus.LastError, TraceEventType.Warning, "Relay listener ServiceHost Faulted");

                RelayTraceSource.TraceInfo($"Opening relay listener \"{endpoint.Address.Uri}\"");
                serviceHost.Open();
                RelayTraceSource.TraceVerbose($"Opening relay listener succeeded");

                VerifySend("RequestPayload", connectionString, path, numberOfRequests, binding, false, connectivityMode, TimeSpan.Zero);

                RelayTraceSource.TraceInfo("Closing relay listener");
                connectionStatus.Offline -= offlineHandler; // Avoid a spurious trace on expected shutdown.
                serviceHost.Close();
                RelayTraceSource.TraceVerbose("Closing relay listener succeeded");
                return 0;
            }
            catch (Exception)
            {
                serviceHost?.Abort();
                throw;
            }
            finally
            {
                if (createdRelay)
                {
                    try
                    {
                        RelayTraceSource.TraceEvent(TraceEventType.Information, ConsoleColor.White, $"Deleting WcfRelay '{path}'");
                        var namespaceManager = new NamespaceManager(baseAddress, tp);
                        await namespaceManager.DeleteRelayAsync(path);
                        RelayTraceSource.TraceVerbose($"Deleting WcfRelay '{path}' succeeded");
                    }
                    catch (Exception cleanupException)
                    {
                        RelayTraceSource.TraceWarning($"Error during cleanup: {cleanupException.GetType()}: {cleanupException.Message}");
                    }
                }
            }
        }

        static Binding GetBinding(CommandOption bindingOption, CommandOption noClientAuthOption, CommandOption openTimeoutOption, CommandOption sendTimeoutOption, CommandOption readTimeoutOption)
        {
            string bindingString = GetStringOption(bindingOption, "nettcprelaybinding");
            Binding binding;

            // Make a few friendly aliases
            switch (bindingString.ToLowerInvariant())
            {
                case "basichttp":
                case "basichttprelay":
                case "basichttprelaybinding":
                    var basicHttpRelayBinding = new BasicHttpRelayBinding { UseDefaultWebProxy = true };
                    if (noClientAuthOption.HasValue())
                    {
                        basicHttpRelayBinding.Security.RelayClientAuthenticationType = RelayClientAuthenticationType.None;
                        basicHttpRelayBinding.Security.Mode = EndToEndBasicHttpSecurityMode.None;
                    }

                    binding = basicHttpRelayBinding;
                    break;
                case "event":
                case "netevent":
                case "neteventrelay":
                case "neteventrelaybinding":
                    var eventRelayBinding = new NetEventRelayBinding();
                    if (noClientAuthOption.HasValue())
                    {
                        eventRelayBinding.Security.RelayClientAuthenticationType = RelayClientAuthenticationType.None;
                        eventRelayBinding.Security.Mode = EndToEndSecurityMode.None;
                    }

                    binding = eventRelayBinding;
                    break;
                case "oneway":
                case "netone":
                case "netoneway":
                case "netonewayrelay":
                case "netonewayrelaybinding":
                    var netOnewayRelayBinding = new NetOnewayRelayBinding();
                    if (noClientAuthOption.HasValue())
                    {
                        netOnewayRelayBinding.Security.RelayClientAuthenticationType = RelayClientAuthenticationType.None;
                        netOnewayRelayBinding.Security.Mode = EndToEndSecurityMode.None;
                    }

                    binding = netOnewayRelayBinding;
                    break;
                case "tcp":
                case "nettcp":
                case "nettcprelay":
                case "nettcprelaybinding":
                    var netTcpRelayBinding = new NetTcpRelayBinding();
                    if (noClientAuthOption.HasValue())
                    {
                        netTcpRelayBinding.Security.RelayClientAuthenticationType = RelayClientAuthenticationType.None;
                        netTcpRelayBinding.Security.Mode = EndToEndSecurityMode.None;
                    }

                    binding = netTcpRelayBinding;
                    break;
                case "ws2007":
                case "wshttp":
                case "ws2007httprelay":
                case "wshttprelay":
                case "wshttprelaybinding":
                case "ws2007httprelaybinding":
                    var ws2007HttpRelayBinding = new WS2007HttpRelayBinding { UseDefaultWebProxy = true };
                    if (noClientAuthOption.HasValue())
                    {
                        ws2007HttpRelayBinding.Security.RelayClientAuthenticationType = RelayClientAuthenticationType.None;
                        ws2007HttpRelayBinding.Security.Mode = EndToEndSecurityMode.None;
                    }

                    binding = ws2007HttpRelayBinding;
                    break;
                case "web":
                case "webhttp":
                case "webhttprelay":
                case "webhttprelaybinding":                    
                    var webHttpRelayBinding = new WebHttpRelayBinding() { UseDefaultWebProxy = true };
                    if (noClientAuthOption.HasValue())
                    {
                        webHttpRelayBinding.Security.RelayClientAuthenticationType = RelayClientAuthenticationType.None;
                        webHttpRelayBinding.Security.Mode = EndToEndWebHttpSecurityMode.None;
                    }

                    binding = webHttpRelayBinding;
                    break;
                default:
                    throw new ArgumentException("Unknown binding type: " + bindingString, "binding");
            }

            if (openTimeoutOption?.HasValue() == true)
            {
                binding.OpenTimeout = TimeSpan.Parse(openTimeoutOption.Value());
            }

            if (sendTimeoutOption?.HasValue() == true)
            {
                binding.SendTimeout = TimeSpan.Parse(sendTimeoutOption.Value());
            }

            if (readTimeoutOption?.HasValue() == true)
            {
                binding.ReceiveTimeout = TimeSpan.Parse(readTimeoutOption.Value());
            }

            return binding;
        }

        static ConnectivityMode GetConnectivityMode(CommandOption connectivityModeOption)
        {
            string modeString = GetStringOption(connectivityModeOption, "autodetect");

            // Make a few friendly aliases
            switch (modeString.ToLowerInvariant())
            {
                case "h":
                    modeString = "https";
                    break;
                case "t":
                    modeString = "tcp";
                    break;
                case "a":
                case "auto":
                    modeString = "autodetect";
                    break;
            }

            return (ConnectivityMode)Enum.Parse(typeof(ConnectivityMode), modeString, ignoreCase: true);
        }

        static RelayType GetRelayType(CommandOption relayTypeOption)
        {
            string typeString = GetStringOption(relayTypeOption, "NetTcp");
            return (RelayType)Enum.Parse(typeof(RelayType), typeString, ignoreCase: true);
        }

        static bool IsOneWay(Binding binding)
        {
            return binding is NetOnewayRelayBinding || binding is NetEventRelayBinding;
        }

        static ServiceThrottlingBehavior GetThrottlingBehavior(CommandOption maxConcurrentSessionsOption, CommandOption maxConcurrentInstancesOption)
        {
            var throttlingBehavior = new ServiceThrottlingBehavior();
            if (maxConcurrentSessionsOption.HasValue())
            {
                throttlingBehavior.MaxConcurrentSessions = GetIntOption(maxConcurrentSessionsOption, throttlingBehavior.MaxConcurrentSessions);
            }

            if (maxConcurrentInstancesOption.HasValue())
            {
                throttlingBehavior.MaxConcurrentInstances = GetIntOption(maxConcurrentInstancesOption, throttlingBehavior.MaxConcurrentInstances);
            }

            return throttlingBehavior;
        }
    }
}
