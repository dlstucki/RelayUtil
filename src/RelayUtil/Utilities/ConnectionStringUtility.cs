// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Text.RegularExpressions;
    using Microsoft.Extensions.CommandLineUtils;

    class ConnectionStringUtility
    {
        const string DotNetTestsConnectionStringEnvironmentVariableName = "azure-relay-dotnet/connectionstring";
        const string JavaTestsConnectionStringEnvironmentVariableName = "RELAY_CONNECTION_STRING";
        
        static readonly Regex ConnectionStringRegex = new Regex(@"[\w]*Endpoint=(http|https|sb)://", RegexOptions.IgnoreCase);

        internal static string ResolveConnectionString(CommandArgument connectionStringArgument)
        {
            string connectionString = string.Empty;
            if (!string.IsNullOrEmpty(connectionStringArgument.Value))
            {
                connectionString = connectionStringArgument.Value;
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = Environment.GetEnvironmentVariable(DotNetTestsConnectionStringEnvironmentVariableName);
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = Environment.GetEnvironmentVariable(JavaTestsConnectionStringEnvironmentVariableName);
            }

            if (!string.IsNullOrEmpty(connectionString) && !IsConnectionString(connectionString))
            {
                connectionString = "Endpoint=http://" + connectionString;
            }

            return connectionString;
        }

        internal static bool IsConnectionString(string connectionString)
        {
            return !string.IsNullOrEmpty(connectionString) && ConnectionStringRegex.IsMatch(connectionString);
        }
    }
}
