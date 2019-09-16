// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Linq;
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.ServiceBus;
    using RelayUtil.Diagnostics;

    class DiagnosticCommands
    {
        internal static void ConfigureCommands(CommandLineApplication app)
        {
            app.Command("diag", (diagCommand) =>
            {
                // TODO
                diagCommand.Description = "Operations for diagnosing relay/hc issues (Analyze)";
                diagCommand.HelpOption(CommandStrings.HelpTemplate);
                var connectionStringArgument = diagCommand.Argument("connectionString", "Relay Namespace ConnectionString");

                diagCommand.OnExecute(async () =>
                {
                    string connectionString = ConnectionStringUtility.ResolveConnectionString(connectionStringArgument);
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        diagCommand.ShowHelp();
                        return 1;
                    }

                    var connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
                    string result = await NetworkUtility.VerifyRelayPortsAsync(connectionStringBuilder.Endpoints.First().Host);
                    Console.WriteLine(result);
                    return 0;
                });
            });
        }
    }
}
