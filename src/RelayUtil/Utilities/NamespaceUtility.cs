// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Microsoft.ServiceBus;

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
        static readonly Regex NamespacePrefixRegex = new Regex("(ns-sb2-|ns-sbeh-|ns-eh2-|ns-)", RegexOptions.IgnoreCase);
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

        public static async Task<int> GetEntityCountAsync(Uri namespaceUri, TokenProvider tokenProvider, string collectionName = "Relays")
        {
            if (namespaceUri.Scheme != Uri.UriSchemeHttps)
            {
                // Need https:// for HttpClient
                namespaceUri = new UriBuilder(namespaceUri) { Scheme = Uri.UriSchemeHttps }.Uri;
            }

            string webToken = await tokenProvider.GetWebTokenAsync(namespaceUri.AbsoluteUri, string.Empty, false, TimeSpan.FromMinutes(20));
            using (var httpClient = new HttpClient())
            {
                Uri resourceUri = new Uri(namespaceUri, $"/$Resources/{collectionName}/?$top=0&$inlinecount=allpages&api-version=2017-04");
                var httpRequest = new HttpRequestMessage();
                httpRequest.RequestUri = resourceUri;
                httpRequest.Headers.Add("X-Process-At", "ServiceBus");
                httpRequest.Headers.Add("ServiceBusAuthorization", webToken);
                using (var httpResponse = await httpClient.SendAsync(httpRequest))
                {
                    httpResponse.EnsureSuccessStatusCode();
                    string responseBody = await httpResponse.Content.ReadAsStringAsync();
                    var document = XDocument.Parse(responseBody);
                    XElement countElement = document.Descendants(XName.Get("count", "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect")).First();
                    return int.Parse(countElement.Value);
                }
            }
        }
    }
}
