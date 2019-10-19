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
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    class WcfRelayCommands : RelayCommands
    {
        const string DefaultPath = "RelayUtilWcf";
        private const string BindingOptionTemplate = "-b|--binding <binding>";
        private const string BindingOptionDescription = "The Wcf Binding. (nettcp|basichttp|webhttp|wshttp)";

        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.RelayCommand("wcf", (wcfCommand) =>
            {
                wcfCommand.Description = "Operations for WcfRelays (CRUD, Test)";
                ConfigureWcfCreateCommand(wcfCommand);
                ConfigureWcfListCommand(wcfCommand);
                ConfigureWcfDeleteCommand(wcfCommand);
                ConfigureWcfListenCommand(wcfCommand);
                ConfigureWcfSendCommand(wcfCommand);

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
                var connectionStringArgument = listCmd.Argument("connectionString", "Relay ConnectionString");
                var protocolOption = listCmd.AddSecurityProtocolOption();

                listCmd.OnExecute(async () =>
                {
                    ConfigureSecurityProtocol(protocolOption);
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        listCmd.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    RelayTraceSource.TraceInfo($"Listing WcfRelays for {connectionStringBuilder.Endpoints.First().Host}");
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                    IEnumerable<RelayDescription> relays = await namespaceManager.GetRelaysAsync();
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

        static void ConfigureWcfListenCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.RelayCommand("listen", (listenCmd) =>
            {
                listenCmd.Description = "WcfRelay listen command";
                var pathArgument = listenCmd.Argument("path", "WcfRelay path");
                var connectionStringArgument = listenCmd.Argument("connectionString", "Relay ConnectionString");

                var bindingOption = listenCmd.Option(BindingOptionTemplate, BindingOptionDescription, CommandOptionType.SingleValue);
                var connectivityModeOption = listenCmd.Option(CommandStrings.ConnectivityModeTemplate, CommandStrings.ConnectivityModeDescription, CommandOptionType.SingleValue);
                var responseOption = listenCmd.Option("--response <response>", "Response to return", CommandOptionType.SingleValue);
                var protocolOption = listenCmd.AddSecurityProtocolOption();

                listenCmd.OnExecute(() =>
                {
                    ConfigureSecurityProtocol(protocolOption);

                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    string path = pathArgument.Value ?? DefaultPath;
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        listenCmd.ShowHelp();
                        return 1;
                    }

                    Binding binding = GetBinding(bindingOption);
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                    if (namespaceManager.RelayExists(path))
                    {
                        dynamic dynamicBinding = binding;
                        dynamicBinding.IsDynamic = false;
                    }

                    return VerifyListen(connectionString, path, binding, GetConnectivityMode(connectivityModeOption), responseOption.Value());
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
                var connectivityModeOption = sendCmd.Option(CommandStrings.ConnectivityModeTemplate, CommandStrings.ConnectivityModeDescription, CommandOptionType.SingleValue);
                var requestOption = sendCmd.Option(CommandStrings.RequestTemplate, CommandStrings.RequestDescription, CommandOptionType.SingleValue);
                var protocolOption = sendCmd.AddSecurityProtocolOption();

                sendCmd.OnExecute(() =>
                {
                    ConfigureSecurityProtocol(protocolOption);

                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    string path = pathArgument.Value ?? DefaultPath;
                    string request = GetStringOption(requestOption, "Test Message Data");
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        sendCmd.ShowHelp();
                        return 1;
                    }

                    int number = GetIntOption(numberOption, 1);
                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    Binding binding = GetBinding(bindingOption);
                    ConnectivityMode connectivityMode = GetConnectivityMode(connectivityModeOption);
                    return VerifySend(request, connectionStringBuilder.Endpoints.First().Host, path, number, binding, connectivityMode, connectionStringBuilder.SharedAccessKeyName, connectionStringBuilder.SharedAccessKey);
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

        public static int VerifyListen(string connectionString, string path, Binding binding, ConnectivityMode connectivityMode, string response)
        {
            RelayTraceSource.TraceInfo($"Open relay listener using {binding.GetType().Name}, ConnectivityMode.{connectivityMode}...");
            ServiceHost serviceHost = null;
            try
            {
                var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);                
                var tp = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionStringBuilder.SharedAccessKeyName, connectionStringBuilder.SharedAccessKey);

                string relayNamespace = connectionStringBuilder.Endpoints.First().Host;
                ServiceBusEnvironment.SystemConnectivity.Mode = connectivityMode;
                if (!(binding is WebHttpRelayBinding))
                {
                    serviceHost = new ServiceHost(new WcfEchoService(response));
                }
                else
                {
                    serviceHost = new WebServiceHost(new WcfEchoService(response));
                }

                ServiceEndpoint endpoint = serviceHost.AddServiceEndpoint(typeof(IEcho), binding, new Uri($"{binding.Scheme}://{relayNamespace}/{path}"));
                endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tp));
                serviceHost.Open();
                RelayTraceSource.TraceInfo("Relay listener \"" + endpoint.Address.Uri + "\" is open");
                RelayTraceSource.TraceInfo("Press <ENTER> to close the listener ");
                Console.ReadLine();

                RelayTraceSource.TraceInfo("Closing Connection...");
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

        public static int VerifySend(string request, string relayNamespace, string path, int number, Binding binding, ConnectivityMode connectivityMode, string keyName, string keyValue)
        {
            RelayTraceSource.TraceInfo($"Send to relay service using {binding.GetType().Name}, ConnectivityMode.{connectivityMode}...");
            ChannelFactory<IEchoClient> channelFactory = null;
            IEchoClient channel = null;
            try
            {
                ServiceBusEnvironment.SystemConnectivity.Mode = connectivityMode;
                Uri address = new Uri($"{binding.Scheme}://{relayNamespace}/{path}");
                if (!(binding is WebHttpRelayBinding))
                {
                    channelFactory = new ChannelFactory<IEchoClient>(binding, new EndpointAddress(address));
                }
                else
                {
                    channelFactory = new WebChannelFactory<IEchoClient>(binding, address);
                }

                var tp = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, keyValue);
                channelFactory.Endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tp));
                channel = channelFactory.CreateChannel();
                RelayTraceSource.TraceInfo($"Opening channel");
                var stopwatch = Stopwatch.StartNew();
                channel.Open();
                stopwatch.Stop();
                RelayTraceSource.TraceInfo($"Opened channel in {stopwatch.ElapsedMilliseconds} ms");

                for (int i = 0; i < number; i++)
                {
                    stopwatch.Restart();
                    string response = channel.Echo(DateTime.UtcNow, request);
                    RelayTraceSource.TraceInfo($"Response: {response} ({stopwatch.ElapsedMilliseconds} ms)");
                }

                RelayTraceSource.TraceInfo($"Closing channel");
                channel.Close();
                channelFactory.Close();
                RelayTraceSource.TraceInfo($"Closed");

                return 0;
            }
            catch (Exception)
            {
                channel?.Abort();
                channelFactory?.Abort();
                throw;
            }
        }

        static Binding GetBinding(CommandOption bindingOption)
        {
            string bindingString = GetStringOption(bindingOption, "nettcprelaybinding");

            // Make a few friendly aliases
            switch (bindingString.ToLowerInvariant())
            {
                case "basichttp":
                case "basichttprelay":
                case "basichttprelaybinding":
                    return new BasicHttpRelayBinding { UseDefaultWebProxy = true };
                case "tcp":
                case "nettcp":
                case "nettcprelay":
                case "nettcprelaybinding":
                    return new NetTcpRelayBinding();
                case "ws2007":
                case "wshttp":
                case "ws2007httprelay":
                case "wshttprelay":
                case "wshttprelaybinding":
                case "ws2007httprelaybinding":
                    return new WS2007HttpRelayBinding { UseDefaultWebProxy = true };
                case "web":
                case "webhttp":
                case "webhttprelay":
                case "webhttprelaybinding":
                    return new WebHttpRelayBinding(EndToEndWebHttpSecurityMode.Transport, RelayClientAuthenticationType.None) { UseDefaultWebProxy = true };
                default:
                    throw new ArgumentException("Unknown binding type: " + bindingString, "binding");
            }
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

        [ServiceContract]
        interface IEcho
        {
            /// <summary>
            /// Here's a sample HTTP request for WebHttpRelayBinding
            /// POST https://YOURRELAY.servicebus.windows.net/RelayUtilWcf/echo?start=2019-10-19T00:44:36.0328204Z HTTP/1.1
            /// Content-Type: application/json
            /// Content-Length: 20
            /// 
            /// "Test Message Data2"
            /// </summary>
            [OperationContract]
            [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, UriTemplate = "echo?start={start}")]
            string Echo(DateTime start, string message);

            /// <summary>
            /// Here's a sample HTTP request for WebHttpRelayBinding
            /// GET https://YOURRELAY.servicebus.windows.net/RelayUtilWcf/get?start=2019-10-19T00:44:36.0328204Z&message=hello%20http HTTP/1.1
            /// 
            /// </summary>
            [WebGet(ResponseFormat = WebMessageFormat.Json, UriTemplate ="get?start={start}&message={message}")]
            [OperationContract]
            string Get(DateTime start, string message);
        }

        interface IEchoClient : IEcho, IClientChannel { }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
        sealed class WcfEchoService : IEcho
        {
            readonly string response;

            public WcfEchoService(string response)
            {
                this.response = response;
            }

            public string Echo(DateTime start, string message)
            {
                string duration = string.Empty;
                if (start != default)
                {
                    duration = $"({(int)DateTime.UtcNow.Subtract(start).TotalMilliseconds}ms from start)";
                }

                RelayTraceSource.TraceInfo($"Echo Request: {message} {duration}");
                return this.response ?? message;
            }

            public string Get(DateTime start, string message)
            {
                string duration = string.Empty;
                if (start != default)
                {
                    duration = $"({(int)DateTime.UtcNow.Subtract(start).TotalMilliseconds}ms from start)";
                }

                RelayTraceSource.TraceInfo($"Get Request: {message} {duration}");
                return this.response ?? DateTime.UtcNow.ToString("o");
            }
        }
    }
}
