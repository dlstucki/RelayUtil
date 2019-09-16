// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Relay;
    using Microsoft.Azure.Relay.Management;
    using Microsoft.Extensions.CommandLineUtils;
    using RelayUtil.HybridConnections;

    static class HybridConnectionCommands
    {
        internal const string DefaultPath = "RelayUtilHcListener";

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

                //var clientAuthRequiredOption = createCmd.Option(
                //     "-car|--client-auth-required <clientAuthRequired>",
                //     "Whether Client Authentication is required",
                //     CommandOptionType.SingleValue);

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
                    var hcDescription = new HybridConnectionDescription(pathArgument.Value) { RequiresClientAuthorization = true };
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
                    var namespaceManager = new RelayNamespaceManager(connectionString);
                    IEnumerable<HybridConnectionDescription> hybridConnections = await namespaceManager.GetHybridConnectionsAsync();
                    foreach (var hybridConnection in hybridConnections)
                    {
                        hcCommand.Out.WriteLine($"Path:{hybridConnection.Path}\tListenerCount:{hybridConnection.ListenerCount}\tRequiresClientAuthorization:{hybridConnection.RequiresClientAuthorization}");
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

                listenCmd.OnExecute(() =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    string path = pathArgument.Value ?? DefaultPath;
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        listenCmd.ShowHelp();
                        return 1;
                    }

                    return VerifyListen(connectionString, path);
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

                var numberOption = sendCmd.Option(
                    "-n|--number <number>",
                    "The Number of messages to send",
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
                    return VerifySend(connectionString, path, number);
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

        public static int VerifyListen(string connectionString, string path)
        {
            throw new NotImplementedException();
        }

        public static int VerifySend(string connectionString, string path, int number)
        {
            throw new NotImplementedException();
        }
    }
}
