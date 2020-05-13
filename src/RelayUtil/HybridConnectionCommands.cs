// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Relay;
    using Microsoft.Azure.Relay.Management;
    using Microsoft.Extensions.CommandLineUtils;
    using RelayUtil.HybridConnections;

    class HybridConnectionCommands : RelayCommands
    {
        internal const string DefaultPath = "RelayUtilHc";

        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.RelayCommand("hc", (hcCommand) =>
            {
                hcCommand.Description = "Operations for HybridConnections (CRUD, Test)";
                ConfigureCreateCommand(hcCommand);
                ConfigureListCommand(hcCommand);
                ConfigureDeleteCommand(hcCommand);
                ConfigureListenCommand(hcCommand);
                ConfigureSendCommand(hcCommand);
                ConfigureTestCommand(hcCommand);

                hcCommand.OnExecute(() =>
                {
                    hcCommand.ShowHelp();
                    return 0;
                });
            });
        }

        static void ConfigureCreateCommand(CommandLineApplication hcCommand)
        {
            hcCommand.RelayCommand("create", (createCmd) =>
            {
                createCmd.Description = "Create a HybridConnection";

                var pathArgument = createCmd.Argument("path", "HybridConnection path");
                var connectionStringArgument = createCmd.Argument("connectionString", "Relay ConnectionString");

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

                    var connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
                    RelayTraceSource.TraceInfo($"Creating HybridConnection '{pathArgument.Value}' in {connectionStringBuilder.Endpoint.Host}...");
                    var hcDescription = new HybridConnectionDescription(pathArgument.Value);
                    hcDescription.RequiresClientAuthorization = GetBoolOption(requireClientAuthOption, true);
                    var namespaceManager = new RelayNamespaceManager(connectionString);
                    await namespaceManager.CreateHybridConnectionAsync(hcDescription);
                    RelayTraceSource.TraceInfo($"Creating HybridConnection '{pathArgument.Value}' in {connectionStringBuilder.Endpoint.Host} succeeded");
                    return 0;
                });
            });
        }

        static void ConfigureListCommand(CommandLineApplication hcCommand)
        {
            hcCommand.RelayCommand("list", (listCmd) =>
            {
                listCmd.Description = "List HybridConnection(s)";
                var connectionStringArgument = listCmd.Argument("connectionString", "Relay ConnectionString");

                listCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        listCmd.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
                    RelayTraceSource.TraceInfo($"Listing HybridConnections for {connectionStringBuilder.Endpoint.Host}");
                    RelayTraceSource.TraceInfo($"{"Path",-38} {"ListenerCount",-15} {"RequiresClientAuth",-20}");
                    var namespaceManager = new RelayNamespaceManager(connectionString);
                    IEnumerable<HybridConnectionDescription> hybridConnections = await namespaceManager.GetHybridConnectionsAsync();
                    foreach (var hybridConnection in hybridConnections)
                    {
                        RelayTraceSource.TraceInfo($"{hybridConnection.Path,-38} {hybridConnection.ListenerCount,-15} {hybridConnection.RequiresClientAuthorization}");
                    }

                    return 0;
                });
            });
        }

        static void ConfigureDeleteCommand(CommandLineApplication hcCommand)
        {
            hcCommand.RelayCommand("delete", (deleteCmd) =>
            {
                deleteCmd.Description = "Delete a HybridConnection";
                var pathArgument = deleteCmd.Argument("path", "HybridConnection path");
                var connectionStringArgument = deleteCmd.Argument("connectionString", "Relay ConnectionString");

                deleteCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(pathArgument.Value))
                    {
                        deleteCmd.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
                    RelayTraceSource.TraceInfo($"Deleting HybridConnection '{pathArgument.Value}' in {connectionStringBuilder.Endpoint.Host}...");
                    var namespaceManager = new RelayNamespaceManager(connectionString);
                    await namespaceManager.DeleteHybridConnectionAsync(pathArgument.Value);
                    RelayTraceSource.TraceInfo($"Deleting HybridConnection '{pathArgument.Value}' in {connectionStringBuilder.Endpoint.Host} succeeded");
                    return 0;
                });
            });
        }

        static void ConfigureListenCommand(CommandLineApplication hcCommand)
        {
            hcCommand.RelayCommand("listen", (listenCmd) =>
            {
                listenCmd.Description = "HybridConnection listen command";
                var pathArgument = listenCmd.Argument("path", "HybridConnection path");
                var connectionStringArgument = listenCmd.Argument("connectionString", "Relay ConnectionString");

                var responseOption = listenCmd.Option("--response <response>", "Response to return", CommandOptionType.SingleValue);
                var responseLengthOption = listenCmd.Option("--response-length <responseLength>", "Length of response to return", CommandOptionType.SingleValue);
                var responseChunkLengthOption = listenCmd.Option("--response-chunk-length <responseLength>", "Length of response to return", CommandOptionType.SingleValue);
                var statusCodeOption = listenCmd.Option("--status-code <statusCode>", "The HTTP Status Code to return (200|201|401|404|etc.)", CommandOptionType.SingleValue);
                var statusDescriptionOption = listenCmd.Option("--status-description <statusDescription>", "The HTTP Status Description to return", CommandOptionType.SingleValue);

                listenCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        listenCmd.ShowHelp();
                        return 1;
                    }

                    string response = GetMessageBody(responseOption, responseLengthOption, "<html><head><title>Azure Relay HybridConnection</title></head><body>Response Body from Listener</body></html>");
                    var connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
                    connectionStringBuilder.EntityPath = pathArgument.Value ?? connectionStringBuilder.EntityPath ?? DefaultPath;
                    var statusCode = (HttpStatusCode)GetIntOption(statusCodeOption, 200);
                    int responseChunkLength = GetIntOption(responseChunkLengthOption, response.Length);
                    return await HybridConnectionTests.VerifyListenAsync(RelayTraceSource.Instance, connectionStringBuilder, response, responseChunkLength, statusCode, statusDescriptionOption.Value());
                });
            });
        }

        static void ConfigureSendCommand(CommandLineApplication hcCommand)
        {
            hcCommand.RelayCommand("send", (sendCmd) =>
            {
                sendCmd.Description = "HybridConnection send command";
                var pathArgument = sendCmd.Argument("path", "HybridConnection path");
                var connectionStringArgument = sendCmd.Argument("connectionString", "Relay ConnectionString");

                var numberOption = sendCmd.Option(CommandStrings.NumberTemplate, CommandStrings.NumberDescription, CommandOptionType.SingleValue);
                var methodOption = sendCmd.Option("-m|--method <method>", "The HTTP Method (GET|POST|PUT|DELETE|WEBSOCKET)", CommandOptionType.SingleValue);
                var requestOption = sendCmd.Option(CommandStrings.RequestTemplate, CommandStrings.RequestDescription, CommandOptionType.SingleValue);
                var requestLengthOption = sendCmd.Option(CommandStrings.RequestLengthTemplate, CommandStrings.RequestLengthDescription, CommandOptionType.SingleValue);

                sendCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        sendCmd.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
                    connectionStringBuilder.EntityPath = pathArgument.Value ?? connectionStringBuilder.EntityPath ?? DefaultPath;

                    int number = GetIntOption(numberOption, 1);
                    string method = GetStringOption(methodOption, "GET");
                    string requestContent = GetMessageBody(requestOption, requestLengthOption, null);
                    await HybridConnectionTests.VerifySendAsync(connectionStringBuilder, number, method, requestContent, RelayTraceSource.Instance);
                    return 0;
                });
            });
        }

        static void ConfigureTestCommand(CommandLineApplication hcCommand)
        {
            hcCommand.RelayCommand("test", (testCmd) =>
            {
                testCmd.Description = "HybridConnection tests";
                var connectionStringArgument = testCmd.Argument("connectionString", "Relay ConnectionString");

                testCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        testCmd.ShowHelp();
                        return 1;
                    }

                    return await HybridConnectionTests.RunAsync(new RelayConnectionStringBuilder(connectionString), RelayTraceSource.Instance);
                });
            });
        }
    }
}
