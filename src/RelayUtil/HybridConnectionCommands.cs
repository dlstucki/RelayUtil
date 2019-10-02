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

    static class HybridConnectionCommands
    {
        internal const string DefaultPath = "RelayUtilHc";

        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.Command("hc", (hcCommand) =>
            {
                hcCommand.Description = "Operations for HybridConnections (CRUD, Test)";
                hcCommand.HelpOption(CommandStrings.HelpTemplate);
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
            hcCommand.Command("create", (createCmd) =>
            {
                createCmd.Description = "Create a HybridConnection";
                createCmd.HelpOption(CommandStrings.HelpTemplate);

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
                    hcCommand.Out.WriteLine($"Creating HybridConnection '{pathArgument.Value}' in {connectionStringBuilder.Endpoint.Host}...");
                    var hcDescription = new HybridConnectionDescription(pathArgument.Value);
                    hcDescription.RequiresClientAuthorization = GetRequiresClientAuthorization(requireClientAuthOption);
                    var namespaceManager = new RelayNamespaceManager(connectionString);
                    await namespaceManager.CreateHybridConnectionAsync(hcDescription);
                    hcCommand.Out.WriteLine($"Creating HybridConnection '{pathArgument.Value}' in {connectionStringBuilder.Endpoint.Host} succeeded");
                    return 0;
                });
            });
        }

        static void ConfigureListCommand(CommandLineApplication hcCommand)
        {
            hcCommand.Command("list", (listCmd) =>
            {
                listCmd.Description = "List HybridConnection(s)";
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

                    var connectionStringBuilder = new RelayConnectionStringBuilder(connectionString);
                    hcCommand.Out.WriteLine($"Listing HybridConnections for {connectionStringBuilder.Endpoint.Host}");
                    hcCommand.Out.WriteLine($"{"Path",-38} {"ListenerCount",-15} {"RequiresClientAuth",-20}");
                    var namespaceManager = new RelayNamespaceManager(connectionString);
                    IEnumerable<HybridConnectionDescription> hybridConnections = await namespaceManager.GetHybridConnectionsAsync();
                    foreach (var hybridConnection in hybridConnections)
                    {
                        hcCommand.Out.WriteLine($"{hybridConnection.Path,-38} {hybridConnection.ListenerCount,-15} {hybridConnection.RequiresClientAuthorization}");
                    }

                    return 0;
                });
            });
        }

        static void ConfigureDeleteCommand(CommandLineApplication hcCommand)
        {
            hcCommand.Command("delete", (deleteCmd) =>
            {
                deleteCmd.Description = "Delete a HybridConnection";
                deleteCmd.HelpOption(CommandStrings.HelpTemplate);
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
                    hcCommand.Out.WriteLine($"Deleting HybridConnection '{pathArgument.Value}' in {connectionStringBuilder.Endpoint.Host}...");
                    var namespaceManager = new RelayNamespaceManager(connectionString);
                    await namespaceManager.DeleteHybridConnectionAsync(pathArgument.Value);
                    hcCommand.Out.WriteLine($"Deleting HybridConnection '{pathArgument.Value}' in {connectionStringBuilder.Endpoint.Host} succeeded");
                    return 0;
                });
            });
        }

        static void ConfigureListenCommand(CommandLineApplication hcCommand)
        {
            hcCommand.Command("listen", (listenCmd) =>
            {
                listenCmd.Description = "HybridConnection listen command";
                listenCmd.HelpOption(CommandStrings.HelpTemplate);
                var pathArgument = listenCmd.Argument("path", "HybridConnection path");
                var connectionStringArgument = listenCmd.Argument("connectionString", "Relay ConnectionString");

                var responseOption = listenCmd.Option("--response <response>", "Response to return", CommandOptionType.SingleValue);
                var responseLengthOption = listenCmd.Option("--response-length <responseLength>", "Length of response to return", CommandOptionType.SingleValue);
                var statusCodeOption = listenCmd.Option("--status-code <statusCode>", "The HTTP Status Code to return (200|201|401|404|etc.)", CommandOptionType.SingleValue);
                var statusDescriptionOption = listenCmd.Option("--status-description <statusDescription>", "The HTTP Status Description to return", CommandOptionType.SingleValue);
                var verboseOption = listenCmd.Option(CommandStrings.VerboseTemplate, CommandStrings.VerboseDescription, CommandOptionType.NoValue);

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
                    bool verbose = verboseOption.HasValue() ? true : false;
                    var statusCode = (HttpStatusCode)int.Parse(statusCodeOption.Value() ?? "200");
                    return await HybridConnectionTests.VerifyListenAsync(hcCommand.Out, connectionStringBuilder, response, statusCode, statusDescriptionOption.Value(), verbose);
                });
            });
        }

        static void ConfigureSendCommand(CommandLineApplication hcCommand)
        {
            hcCommand.Command("send", (sendCmd) =>
            {
                sendCmd.Description = "HybridConnection send command";
                sendCmd.HelpOption(CommandStrings.HelpTemplate);

                var pathArgument = sendCmd.Argument("path", "HybridConnection path");
                var connectionStringArgument = sendCmd.Argument("connectionString", "Relay ConnectionString");

                var numberOption = sendCmd.Option(CommandStrings.NumberTemplate, CommandStrings.NumberDescription, CommandOptionType.SingleValue);
                var methodOption = sendCmd.Option("-m|--method <method>", "The HTTP Method (GET|POST|PUT|DELETE)", CommandOptionType.SingleValue);
                var requestOption = sendCmd.Option(CommandStrings.RequestTemplate, CommandStrings.RequestDescription, CommandOptionType.SingleValue);
                var requestLengthOption = sendCmd.Option(CommandStrings.RequestLengthTemplate, CommandStrings.RequestLengthDescription, CommandOptionType.SingleValue);
                var verboseOption = sendCmd.Option(CommandStrings.VerboseTemplate, CommandStrings.VerboseDescription, CommandOptionType.NoValue);

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

                    int number = int.Parse(numberOption.Value() ?? "1");
                    string method = methodOption.Value() ?? "GET";
                    string requestContent = GetMessageBody(requestOption, requestLengthOption, null);
                    bool verbose = verboseOption.HasValue() ? true : false;
                    await HybridConnectionTests.VerifySendAsync(connectionStringBuilder, number, method, requestContent, verbose);
                    return 0;
                });
            });
        }

        static void ConfigureTestCommand(CommandLineApplication hcCommand)
        {
            hcCommand.Command("test", (testCmd) =>
            {
                testCmd.Description = "HybridConnection tests";
                testCmd.HelpOption(CommandStrings.HelpTemplate);
                var connectionStringArgument = testCmd.Argument("connectionString", "Relay ConnectionString");

                testCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        testCmd.ShowHelp();
                        return 1;
                    }

                    return await HybridConnectionTests.RunAsync(new RelayConnectionStringBuilder(connectionString));
                });
            });
        }

        static bool GetRequiresClientAuthorization(CommandOption requireClientAuthOption)
        {
            if (requireClientAuthOption.HasValue())
            {
                return bool.Parse(requireClientAuthOption.Value());
            }

            return true;
        }

        static string GetMessageBody(CommandOption valueOption, CommandOption lengthOption, string defaultValue)
        {
            string requestData = valueOption.HasValue() ? valueOption.Value() : defaultValue;
            if (lengthOption.HasValue())
            {
                if (string.IsNullOrEmpty(requestData))
                {
                    requestData = "1234567890";
                }

                int requestedLength = int.Parse(lengthOption.Value());
                var stringBuffer = new StringBuilder(requestedLength);

                int countNeeded;
                while ((countNeeded = requestedLength - stringBuffer.Length) > 0)
                {
                    stringBuffer.Append(requestData.Substring(0, Math.Min(countNeeded, requestData.Length)));
                }

                requestData = stringBuffer.ToString();
            }

            return requestData;
        }
    }
}
