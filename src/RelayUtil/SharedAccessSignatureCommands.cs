// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.ServiceBus;

    class SharedAccessSignatureCommands : RelayCommands
    {
        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.RelayCommand("sas", (sasCommand) =>
            {
                sasCommand.Description = "Operations for Shared Access Signatures";
                ConfigureCreateCommand(sasCommand);

                sasCommand.OnExecute(() =>
                {
                    sasCommand.ShowHelp();
                    return 0;
                });
            });
        }

        static void ConfigureCreateCommand(CommandLineApplication sasCommand)
        {
            sasCommand.RelayCommand("create", (createCmd) =>
            {
                createCmd.Description = "Create a Sas Token";

                var pathArgument = createCmd.Argument("path", "Entity path");
                var connectionStringArgument = createCmd.Argument("connectionString", "The ConnectionString to use");

                createCmd.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        TraceMissingArgument(connectionStringArgument.Name);
                        createCmd.ShowHelp();
                        return 1;
                    }
                    
                    var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);

                    var audience = new UriBuilder(namespaceManager.Address);
                    if (!string.IsNullOrEmpty(pathArgument.Value))
                    {
                        audience.Path = pathArgument.Value;
                    }

                    string token = await namespaceManager.Settings.TokenProvider.GetWebTokenAsync(audience.Uri.AbsoluteUri, string.Empty, true, TimeSpan.FromMinutes(20));
                    RelayTraceSource.TraceInfo($"Token:\r\n{token}");
                    return 0;
                });
            });
        }
    }
}
