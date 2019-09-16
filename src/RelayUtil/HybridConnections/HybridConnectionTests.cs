// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil.HybridConnections
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Relay;
    using Microsoft.Azure.Relay.Management;

    class HybridConnectionTests
    {
        public static async Task<int> RunAsync(RelayConnectionStringBuilder connectionString)
        {
            if (string.IsNullOrEmpty(connectionString.EntityPath))
            {
                connectionString.EntityPath = HybridConnectionCommands.DefaultPath;
            }

            var namespaceManager = new RelayNamespaceManager(connectionString.ToString());
            HybridConnectionListener listener = null;
            bool createdHybridConnection = false;
            int returnCode = 0;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(70));
            try
            {
                if (!await namespaceManager.HybridConnectionExistsAsync(connectionString.EntityPath))
                {
                    Console.WriteLine($"Creating HybridConnection '{connectionString.EntityPath}'");
                    createdHybridConnection = true;
                    await namespaceManager.CreateHybridConnectionAsync(new HybridConnectionDescription(connectionString.EntityPath));
                }

                listener = new HybridConnectionListener(connectionString.ToString());
                Console.WriteLine($"Opening Listener {listener}");
                await listener.OpenAsync(cts.Token);
                Console.WriteLine("Listener Opened");

                Uri hybridHttpUri = new Uri($"https://{connectionString.Endpoint.GetComponents(UriComponents.HostAndPort, UriFormat.SafeUnescaped)}/{connectionString.EntityPath}");
                var token = await listener.TokenProvider.GetTokenAsync(hybridHttpUri.AbsoluteUri, TimeSpan.FromMinutes(20));
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    await PostLargeRequestSmallResponse(listener, token, client);
                    await PostLargeRequestWithLargeResponse(listener, token, client);
                    await GetLargeResponse(listener, token, client);
                    await GetSmallResponse(listener, token, client);                    

                    ColorConsole.WriteLine(ConsoleColor.Green, "All tests succeeded");
                }
            }
            catch (Exception exception)
            {
                ColorConsole.WriteLine(ConsoleColor.Red, $"Exception: {exception}");
                ColorConsole.WriteLine(ConsoleColor.Red, $"FAILED");
                returnCode = exception.HResult;
            }
            finally
            {
                cts.Dispose();
                var cleanupCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                try
                {
                    if (listener != null)
                    {
                        Console.WriteLine("Closing Listener");
                        await listener.CloseAsync(cleanupCancelSource.Token);
                    }

                    if (createdHybridConnection)
                    {
                        Console.WriteLine($"Deleting HybridConnection '{connectionString.EntityPath}'");
                        await namespaceManager.DeleteHybridConnectionAsync(connectionString.EntityPath);
                    }
                }
                catch (Exception cleanupException)
                {
                    ColorConsole.WriteLine(ConsoleColor.Yellow, $"Error during cleanup: {cleanupException.GetType()}: {cleanupException.Message}");
                }
                finally
                {
                    cleanupCancelSource.Dispose();
                }
            }

            return returnCode;
        }

        static async Task GetSmallResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client)
        {
            ColorConsole.WriteLine(ConsoleColor.White, "==========\r\nTesting GET small response (over control connection)\r\n==========");
            listener.RequestHandler = async (context) =>
            {
                LogHttpRequest(context);

                var responseBytes = Encoding.UTF8.GetBytes("small response");
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await context.Response.CloseAsync();
            };

            var httpRequest = new HttpRequestMessage();
            httpRequest.Headers.Add("ServiceBusAuthorization", token.TokenString);
            httpRequest.Method = HttpMethod.Get;
            using (HttpResponseMessage response = await client.SendAsync(httpRequest))
            {
                LogHttpResponse(response);
                response.EnsureSuccessStatusCode();
                ColorConsole.WriteLine(ConsoleColor.White, "Success\r\n");
            }
        }

        static async Task GetLargeResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client)
        {
            ColorConsole.WriteLine(ConsoleColor.White, "==========\r\nTesting GET large response (over rendezvous)\r\n==========");
            listener.RequestHandler = async (context) =>
            {
                LogHttpRequest(context);
                var responseBytes = Encoding.UTF8.GetBytes(new string('b', 65 * 1024));
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await context.Response.CloseAsync();
            };

            var httpRequest = new HttpRequestMessage();
            httpRequest.Headers.Add("ServiceBusAuthorization", token.TokenString);
            httpRequest.Method = HttpMethod.Get;
            using (HttpResponseMessage response = await client.SendAsync(httpRequest))
            {
                LogHttpResponse(response);
                response.EnsureSuccessStatusCode();
                ColorConsole.WriteLine(ConsoleColor.White, "Success\r\n");
            }
        }

        static async Task PostLargeRequestSmallResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client)
        {
            ColorConsole.WriteLine(ConsoleColor.White, "==========\r\nTesting POST large request with small response (over rendezvous)\r\n==========");
            listener.RequestHandler = async (context) =>
            {
                LogHttpRequest(context);
                var responseBytes = Encoding.UTF8.GetBytes("small response");
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await context.Response.CloseAsync();
            };

            var httpRequest = new HttpRequestMessage();
            httpRequest.Headers.Add("ServiceBusAuthorization", token.TokenString);
            httpRequest.Method = HttpMethod.Post;
            httpRequest.Content = new StringContent(new string('a', 65 * 1024));
            using (HttpResponseMessage response = await client.SendAsync(httpRequest))
            {
                LogHttpResponse(response);
                response.EnsureSuccessStatusCode();
                ColorConsole.WriteLine(ConsoleColor.White, "Success\r\n");
            }
        }

        static async Task PostLargeRequestWithLargeResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client)
        {
            ColorConsole.WriteLine(ConsoleColor.White, "==========\r\nTesting POST large request with large response (over rendezvous)\r\n==========");
            listener.RequestHandler = async (context) =>
            {
                LogHttpRequest(context);
                var responseBytes = Encoding.UTF8.GetBytes(new string('b', 65 * 1024));
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await context.Response.CloseAsync();
            };

            var httpRequest = new HttpRequestMessage();
            httpRequest.Headers.Add("ServiceBusAuthorization", token.TokenString);
            httpRequest.Method = HttpMethod.Post;
            httpRequest.Content = new StringContent(new string('b', 65 * 1024));
            using (HttpResponseMessage response = await client.SendAsync(httpRequest))
            {
                LogHttpResponse(response);
                response.EnsureSuccessStatusCode();
                ColorConsole.WriteLine(ConsoleColor.White, "Success\r\n");
            }
        }

        static void LogHttpRequest(RelayedHttpListenerContext context)
        {
            Console.Write("Listener.RequestHandler invoked:");
            Console.WriteLine($"{context.Request.HttpMethod} {context.Request.Url} ({new StreamReader(context.Request.InputStream).ReadToEnd().Length} bytes)");
            Console.Write($"({context})");
        }

        public static void LogHttpResponse(HttpResponseMessage httpResponse, bool showBody = false)
        {
            var foregroundColor = Console.ForegroundColor;
            if (!httpResponse.IsSuccessStatusCode)
            {
                foregroundColor = ConsoleColor.Yellow;
            }

            ColorConsole.Write(foregroundColor, "Received Response:");
            ColorConsole.Write(foregroundColor, $"HTTP/{httpResponse.Version} {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase} ");
            ColorConsole.WriteLine(foregroundColor, $"({httpResponse.Content?.ReadAsStreamAsync().Result.Length ?? 0} bytes)");
        }
    }
}
