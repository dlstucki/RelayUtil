// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
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

                listenCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    string path = pathArgument.Value ?? DefaultPath;
                    string response = responseOption?.Value() ?? "<html><head><title>Azure Relay HybridConnection</title></head><body>Response Body from Listener</body></html>";
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        listenCmd.ShowHelp();
                        return 1;
                    }

                    return await VerifyListenAsync(hcCommand.Out, new RelayConnectionStringBuilder(connectionString), path, response);
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

        public static async Task<int> VerifyListenAsync(TextWriter output, RelayConnectionStringBuilder connectionString, string path, string response)
        {
            bool createdHybridConnection = false;
            if (string.IsNullOrEmpty(connectionString.EntityPath))
            {
                connectionString.EntityPath = path ?? DefaultPath;

                var namespaceManager = new RelayNamespaceManager(connectionString.ToString());
                if (!await namespaceManager.HybridConnectionExistsAsync(connectionString.EntityPath))
                {
                    output.WriteLine($"Creating HybridConnection {connectionString.EntityPath}...");
                    createdHybridConnection = true;
                    await namespaceManager.CreateHybridConnectionAsync(new HybridConnectionDescription(connectionString.EntityPath));
                    output.WriteLine("Created");
                }
            }

            HybridConnectionListener listener = null;
            try
            {
                listener = new HybridConnectionListener(connectionString.ToString());
                listener.Connecting += (s, e) => ColorConsole.WriteLine(ConsoleColor.Yellow, $"Listener attempting to connect. Last Error: {listener.LastError}");
                listener.Online += (s, e) => ColorConsole.WriteLine(ConsoleColor.Green, "Listener is online");
                EventHandler offlineHandler = (s, e) => ColorConsole.WriteLine(ConsoleColor.Red, $"Listener is OFFLINE. Last Error: {listener.LastError}");
                listener.Offline += offlineHandler;

                var responseBytes = Encoding.UTF8.GetBytes(response);
                listener.RequestHandler = async (context) =>
                {
                    try
                    {
                        HybridConnectionTests.LogHttpRequest(context);
                        context.Response.Headers[HttpResponseHeader.ContentType] = "text/html";
                        await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        await context.Response.CloseAsync();
                    }
                    catch (Exception exception)
                    {
                        ColorConsole.WriteLine(ConsoleColor.Red, $"RequestHandler Error: {exception.GetType()}: {exception.Message}");
                    }
                };

                Console.WriteLine($"Opening {listener}");
                await listener.OpenAsync();
                output.WriteLine("Press <ENTER> to close the listener ");
                Console.ReadLine();

                output.WriteLine($"Closing {listener}");
                listener.Offline -= offlineHandler; // Avoid a spurious trace on expected shutdown.
                await listener.CloseAsync();
                output.WriteLine("Closed");
                return 0;
            }
            catch (Exception)
            {
                listener?.CloseAsync();
                throw;
            }
            finally
            {
                if (createdHybridConnection)
                {
                    try
                    {
                        output.WriteLine($"Deleting HybridConnection {connectionString.EntityPath}...");
                        var namespaceManager = new RelayNamespaceManager(connectionString.ToString());
                        await namespaceManager.DeleteHybridConnectionAsync(connectionString.EntityPath);
                        output.WriteLine($"Deleted");
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        static int VerifySend(string connectionString, string path, int number)
        {
            throw new NotImplementedException();
        }

        static bool GetRequiresClientAuthorization(CommandOption requireClientAuthOption)
        {
            if (requireClientAuthOption.HasValue())
            {
                return bool.Parse(requireClientAuthOption.Value());
            }

            return true;
        }

    }
}
