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
            string connectionString = string.Empty;
            if (!string.IsNullOrEmpty(connectionStringArgument.Value))
            {
                connectionString = connectionStringArgument.Value;
            }
            else
            {
                connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName);
            }

            if (!string.IsNullOrEmpty(connectionString) && connectionString.IndexOf("Endpoint=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                connectionString = "Endpoint=http://" + connectionString;
            }

            return connectionString;
        }
    }
}
