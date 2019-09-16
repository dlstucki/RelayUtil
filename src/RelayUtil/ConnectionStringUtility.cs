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
            if (!string.IsNullOrEmpty(connectionStringArgument.Value))
            {
                return connectionStringArgument.Value;
            }

            string environmentConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName);
            return environmentConnectionString;
        }
    }
}
