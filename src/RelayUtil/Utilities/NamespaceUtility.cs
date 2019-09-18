// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    struct NamespaceDetails
    {
        public string ServiceNamespace { get; set; }
        public string HostName { get; internal set; }
        public string Suffix { get; set; }
        public string Deployment { get; set; }
        public IPAddress[] AddressList { get; internal set; }
        public string GatewayDnsFormat { get; internal set; }
        public string[] Aliases { get; internal set; }
    }

    static class NamespaceUtility
    {
        internal const string DefaultSuffix = "servicebus.windows.net";
        internal const int MaxGatewayInstanceCount = 64;
        static readonly Regex NamespacePrefixRegex = new Regex("(ns-sb2-|ns-sbeh-|ns-eh2-)", RegexOptions.IgnoreCase);
        static readonly Regex DeploymentRegex = new Regex(@"^[\w]*(PROD|PPE|INT|BVT)[\w]*-[\w]{3,}-\d{3,}$", RegexOptions.IgnoreCase);

        internal static async Task<NamespaceDetails> GetNamespaceDetailsAsync(string serviceNamespace)
        {
            serviceNamespace = serviceNamespace.Trim();

            var details = new NamespaceDetails();
            Match match = DeploymentRegex.Match(serviceNamespace);
            if (match != null && match.Success && !serviceNamespace.Contains("."))
            {
                details.Deployment = match.Value.ToUpperInvariant();
            }
            else
            {
                string[] namespaceAndSuffix = serviceNamespace.Split(new[] { '.' }, 2);
                if (namespaceAndSuffix.Length == 1)
                {
                    serviceNamespace = $"{serviceNamespace}.{DefaultSuffix}";
                    details.Suffix = DefaultSuffix;
                }
                else
                {
                    details.Suffix = namespaceAndSuffix[1];
                }

                details.ServiceNamespace = serviceNamespace;

                IPHostEntry dnsEntry = await Dns.GetHostEntryAsync(serviceNamespace).ConfigureAwait(false);
                details.HostName = dnsEntry.HostName;

                // Get the part up to the first '.' e.g. ns-sb2-prod-by-003, then remove 'ns-sb2' or similar prefixes
                string deployment = dnsEntry.HostName.Split('.')[0];
                deployment = NamespacePrefixRegex.Replace(deployment, string.Empty);
                details.Deployment = deployment.ToUpperInvariant();

                details.AddressList = dnsEntry.AddressList;
                details.GatewayDnsFormat = $"g{{0}}-{deployment}-sb.{details.Suffix}";
                details.Aliases = dnsEntry.Aliases;
            }

            return details;
        }
    }
}
