// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.CommandLineUtils;

namespace RelayUtil
{
    static class RelayCommandLineApplicationExtensions
    {
        public static CommandLineApplication RelayCommand(this CommandLineApplication application, string name, Action<CommandLineApplication> configure, bool throwOnUnexpectedArg = true)
        {
            Action<CommandLineApplication> outerConfigure = (app) =>
            {
                RelayCommands.CommonSetup(app);
                configure(app);
            };

            return application.Command(name, outerConfigure, throwOnUnexpectedArg);
        }

        public static CommandOption AddSecurityProtocolOption(this CommandLineApplication cmd)
        {
            return cmd.Option(
                "--security-protocol",
                $"Security Protocol (ssl3|tls|tls11|tls12|tls13)",
                CommandOptionType.SingleValue);
        }
    }
}
