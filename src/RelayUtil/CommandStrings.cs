// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    static class CommandStrings
    {
        public const string HelpTemplate = "-?|-h|--help";
        public const string NamespaceTemplate = "-ns|--namespace <namespace>";
        public const string ConnectionStringTemplate = "-cs|--connection-string <string>";
        public const string ConnectivityModeTemplate = "-cm|--connectivity-mode <mode>";
        public const string ConnectivityModeDescription = "The ConnectivityMode (auto|tcp|https)";
        public const string RequiresClientAuthTemplate = "-rca|--requires-client-auth <requiresClientAuth>";
        public const string RequiresClientAuthDescription = "Whether client authorization is required (true|false)";
    }
}
