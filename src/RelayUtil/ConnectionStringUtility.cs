// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using Microsoft.Extensions.CommandLineUtils;

    class ConnectionStringUtility
    {
        const string ConnectionStringEnvironmentVariableName = "azure-relay-dotnet/connectionstring";

        internal static string ResolveConnectionString(CommandArgument connectionStringArgument)
        {
            string actualConnectionString = string.Empty;
            if (!string.IsNullOrEmpty(connectionStringArgument.Value))
            {
                actualConnectionString = connectionStringArgument.Value;
            }

            if (string.IsNullOrEmpty(actualConnectionString))
            {
                actualConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName);
            }

            return actualConnectionString;
        }
    }
}
