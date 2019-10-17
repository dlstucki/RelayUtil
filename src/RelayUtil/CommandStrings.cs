// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    static class CommandStrings
    {
        public const string HelpTemplate = "-?|-h|--help";
        public const string NamespaceTemplate = "-ns|--namespace <namespace>";
        public const string ConnectivityModeTemplate = "-cm|--connectivity-mode <mode>";
        public const string ConnectivityModeDescription = "The ConnectivityMode (auto|tcp|https)";
        public const string RequiresClientAuthTemplate = "-rca|--requires-client-auth <requiresClientAuth>";
        public const string RequiresClientAuthDescription = "Whether client authorization is required (true|false)";
        public const string NumberTemplate = "-n|--number <number>";
        public const string NumberDescription = "The Number of messages to send";
        public const string RequestTemplate = "--request <request>";
        public const string RequestDescription = "The request to send";
        public const string RequestLengthTemplate = "--request-length <requestLength>";
        public const string RequestLengthDescription = "The length of request to send";
        public const string VerboseShort = "-v";
        public const string VerboseLong = "--verbose";
        public const string VerboseTemplate = VerboseShort + "|" + VerboseLong;
        public const string VerboseDescription = "Show verbose output";
    }
}
