// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil.WcfRelays
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Description;
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    class WcfRelayCommands
    {
        const string DefaultPath = "RelayUtilWcf";
            
        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.Command("wcf", (wcfCommand) =>
            {
                wcfCommand.Description = "Operations for WcfRelays (CRUD, Test)";
                wcfCommand.HelpOption(CommandStrings.HelpTemplate);
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
            wcfCommand.Command("create", (createCmd) =>
            {
                createCmd.Description = "Create a WcfRelay";
                createCmd.HelpOption(CommandStrings.HelpTemplate);

                var pathArgument = createCmd.Argument("path", "WcfRelay path");
                var connectionStringArgument = createCmd.Argument("connectionString", "Relay ConnectionString");

                var relayTypeOption = createCmd.Option(
                    "-t|--relaytype <relaytype>", "The RelayType (nettcp|http|netevent|netoneway)", CommandOptionType.SingleValue);
                var requireClientAuthOption = createCmd.Option(
                    CommandStrings.RequiresClientAuthTemplate, CommandStrings.RequiresClientAuthDescription, CommandOptionType.SingleValue);

                createCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(pathArgument.Value))
                    {
                        createCmd.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    wcfCommand.Out.WriteLine($"Creating WcfRelay '{pathArgument.Value}' in {connectionStringBuilder.Endpoints.First().Host}...");
                    var relayDescription = new RelayDescription(pathArgument.Value, GetRelayType(relayTypeOption));
                    relayDescription.RequiresClientAuthorization = GetRequiresClientAuthorization(requireClientAuthOption);
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                    await namespaceManager.CreateRelayAsync(relayDescription);
                    wcfCommand.Out.WriteLine($"Creating WcfRelay '{pathArgument.Value}' in {connectionStringBuilder.Endpoints.First().Host} succeeded");
                    return 0;
                });
            });
        }

        static void ConfigureWcfListCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.Command("list", (listCmd) =>
            {
                listCmd.Description = "List WcfRelay(s)";
                var connectionStringArgument = listCmd.Argument("connectionString", "Relay ConnectionString");
                listCmd.HelpOption(CommandStrings.HelpTemplate);

                listCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        listCmd.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    wcfCommand.Out.WriteLine($"Listing WcfRelays for {connectionStringBuilder.Endpoints.First().Host}");
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                    IEnumerable<RelayDescription> relays = await namespaceManager.GetRelaysAsync();
                    wcfCommand.Out.WriteLine($"{"Path",-38} {"ListenerCount",-15} {"RequiresClientAuth",-20} RelayType");
                    foreach (var relay in relays)
                    {
                        wcfCommand.Out.WriteLine($"{relay.Path,-38} {relay.ListenerCount,-15} {relay.RequiresClientAuthorization,-20} {relay.RelayType}");
                    }

                    return 0;
                });
            });
        }

        static void ConfigureWcfDeleteCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.Command("delete", (deleteCmd) =>
            {
                deleteCmd.Description = "Delete a WcfRelay";
                deleteCmd.HelpOption(CommandStrings.HelpTemplate);
                var pathArgument = deleteCmd.Argument("path", "WcfRelay path");
                var connectionStringArgument = deleteCmd.Argument("connectionString", "Relay ConnectionString");

                deleteCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(pathArgument.Value))
                    {
                        deleteCmd.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    wcfCommand.Out.WriteLine($"Deleting WcfRelay '{pathArgument.Value}' in {connectionStringBuilder.Endpoints.First().Host}...");
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                    await namespaceManager.DeleteRelayAsync(pathArgument.Value);
                    wcfCommand.Out.WriteLine($"Deleting WcfRelay '{pathArgument.Value}' in {connectionStringBuilder.Endpoints.First().Host} succeeded");
                    return 0;
                });
            });
        }

        static void ConfigureWcfListenCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.Command("listen", (listenCmd) =>
            {
                listenCmd.Description = "WcfRelay listen command";
                listenCmd.HelpOption(CommandStrings.HelpTemplate);
                var pathArgument = listenCmd.Argument("path", "WcfRelay path");
                var connectionStringArgument = listenCmd.Argument("connectionString", "Relay ConnectionString");

                var bindingOption = listenCmd.Option(
                    "-b|--binding <binding>",
                    "The Wcf Binding. (NetTcpRelayBinding|BasicHttpRelayBinding)",
                    CommandOptionType.SingleValue);

                var connectivityModeOption = listenCmd.Option(
                    CommandStrings.ConnectivityModeTemplate,
                    CommandStrings.ConnectivityModeDescription,
                    CommandOptionType.SingleValue);

                listenCmd.OnExecute(() =>
                {
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

                    return VerifyListen(wcfCommand.Out, connectionString, path, binding, GetConnectivityMode(connectivityModeOption));
                });
            });
        }

        static void ConfigureWcfSendCommand(CommandLineApplication wcfCommand)
        {
            wcfCommand.Command("send", (sendCmd) =>
            {
                sendCmd.Description = "WcfRelay send command";
                sendCmd.HelpOption(CommandStrings.HelpTemplate);
                var pathArgument = sendCmd.Argument("path", "WcfRelay path");
                var connectionStringArgument = sendCmd.Argument("connectionString", "Relay ConnectionString");

                var numberOption = sendCmd.Option(
                    "-n|--number <number>",
                    "The Number of messages to send",
                    CommandOptionType.SingleValue);

                var bindingOption = sendCmd.Option(
                    "-b|--binding <binding>",
                    "The Wcf Binding. (NetTcpRelayBinding|BasicHttpRelayBinding)",
                    CommandOptionType.SingleValue);

                var connectivityModeOption = sendCmd.Option(
                    CommandStrings.ConnectivityModeTemplate,
                    CommandStrings.ConnectivityModeDescription,
                    CommandOptionType.SingleValue);

                sendCmd.OnExecute(() =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    string path = pathArgument.Value ?? DefaultPath;
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        sendCmd.ShowHelp();
                        return 1;
                    }

                    int number = int.Parse(numberOption.Value() ?? "1");
                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    Binding binding = GetBinding(bindingOption);
                    ConnectivityMode connectivityMode = GetConnectivityMode(connectivityModeOption);
                    return VerifySend(wcfCommand.Out, connectionStringBuilder.Endpoints.First().Host, path, number, binding, connectivityMode, connectionStringBuilder.SharedAccessKeyName, connectionStringBuilder.SharedAccessKey);
                });
            });
        }

        public static int VerifyListen(TextWriter output, string connectionString, string path, Binding binding, ConnectivityMode connectivityMode)
        {
            output.WriteLine($"Open relay listener using {binding.GetType().Name}, ConnectivityMode.{connectivityMode}...");
            ServiceHost serviceHost = null;
            try
            {
                var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);                
                var tp = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionStringBuilder.SharedAccessKeyName, connectionStringBuilder.SharedAccessKey);

                string relayNamespace = connectionStringBuilder.Endpoints.First().Host;
                if (relayNamespace.Contains("."))
                {
                    relayNamespace = relayNamespace.Split('.')[0];
                }

                ServiceBusEnvironment.SystemConnectivity.Mode = connectivityMode;
                serviceHost = new ServiceHost(typeof(WcfEchoService));
                ServiceEndpoint endpoint = serviceHost.AddServiceEndpoint(typeof(IEcho), binding, ServiceBusEnvironment.CreateServiceUri(binding.Scheme, relayNamespace, path));
                endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tp));
                serviceHost.Open();
                output.WriteLine("Relay listener \"" + endpoint.Address.Uri + "\" is open");
                output.WriteLine("Press <ENTER> to close the listener ");
                Console.ReadLine();

                output.WriteLine("Closing Connection...");
                serviceHost.Close();
                output.WriteLine("Closed");
                return 0;
            }
            catch (Exception)
            {
                serviceHost?.Abort();
                throw;
            }
        }

        public static int VerifySend(TextWriter output, string relayNamespace, string path, int number, Binding binding, ConnectivityMode connectivityMode, string keyName, string keyValue)
        {
            output.WriteLine($"Send to relay service using {binding.GetType().Name}, ConnectivityMode.{connectivityMode}...");
            ChannelFactory<IEcho> channelFactory = null;
            IEcho channel = null;
            try
            {
                if (relayNamespace.Contains("."))
                {
                    relayNamespace = relayNamespace.Split('.')[0];
                }

                ServiceBusEnvironment.SystemConnectivity.Mode = connectivityMode;
                channelFactory = new ChannelFactory<IEcho>(binding, new EndpointAddress(ServiceBusEnvironment.CreateServiceUri(binding.Scheme, relayNamespace, path)));
                var tp = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, keyValue);
                channelFactory.Endpoint.EndpointBehaviors.Add(new TransportClientEndpointBehavior(tp));
                channel = channelFactory.CreateChannel();
                output.WriteLine($"Opening channel");
                ((IChannel)channel).Open();
                for (int i = 0; i < number; i++)
                {
                    string response = channel.Echo("Request from sender");
                    output.WriteLine($"Response: {response}");
                }

                output.WriteLine($"Closing channel");
                ((IChannel)channel).Close();
                channelFactory.Close();
                output.WriteLine($"Closed");

                return 0;
            }
            catch (Exception)
            {
                ((IChannel)channel)?.Abort();
                channelFactory?.Abort();
                throw;
            }
        }

        static Binding GetBinding(CommandOption bindingOption)
        {
            string bindingString = bindingOption.HasValue() ? bindingOption.Value() : "nettcprelaybinding";

            // Make a few friendly aliases
            switch (bindingString.ToLowerInvariant())
            {
                case "basichttp":
                case "basichttprelay":
                case "basichttprelaybinding":
                    return new BasicHttpRelayBinding();
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
                    return new WS2007HttpRelayBinding();
                default:
                    throw new ArgumentException("Unknown binding type: " + bindingString, "binding");

                // TODO: Needs more work:
                ////case "web":
                ////case "webhttp":
                ////case "webhttprelay":
                ////case "webhttprelaybinding":
                ////    return new WebHttpRelayBinding();
            }
        }

        static ConnectivityMode GetConnectivityMode(CommandOption connectivityModeOption)
        {
            string modeString = connectivityModeOption.HasValue() ? connectivityModeOption.Value() : "AutoDetect";

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
            string typeString = relayTypeOption.HasValue() ? relayTypeOption.Value() : "NetTcp";
            return (RelayType)Enum.Parse(typeof(RelayType), typeString, ignoreCase: true);
        }

        static bool GetRequiresClientAuthorization(CommandOption requireClientAuthOption)
        {
            if (requireClientAuthOption.HasValue())
            {
                return bool.Parse(requireClientAuthOption.Value());
            }

            return true;
        }

        [ServiceContract]
        public interface IEcho
        {
            [OperationContract]
            string Echo(string message);
        }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
        public class WcfEchoService : IEcho
        {
            public string Echo(string message)
            {
                Console.WriteLine($"Request:  {message}");
                return "Response from Listener";
            }
        }
    }
}
