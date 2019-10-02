// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil.HybridConnections
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
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
                Console.WriteLine($"Opening {listener}");
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
                        Console.WriteLine($"Closing {listener}");
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
            ColorConsole.WriteLine(ConsoleColor.White, "========== Testing GET small response (over control connection) ==========");
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
                ColorConsole.WriteLine(ConsoleColor.White, "Success");
            }
        }

        static async Task GetLargeResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client)
        {
            ColorConsole.WriteLine(ConsoleColor.White, "========== Testing GET large response (over rendezvous) ==========");
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
                ColorConsole.WriteLine(ConsoleColor.White, "Success");
            }
        }

        static async Task PostLargeRequestSmallResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client)
        {
            ColorConsole.WriteLine(ConsoleColor.White, "========== Testing POST large request with small response (over rendezvous) ==========");
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
                ColorConsole.WriteLine(ConsoleColor.White, "Success");
            }
        }

        static async Task PostLargeRequestWithLargeResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client)
        {
            ColorConsole.WriteLine(ConsoleColor.White, "========== Testing POST large request with large response (over rendezvous) ==========");
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
                ColorConsole.WriteLine(ConsoleColor.White, "Success");
            }
        }

        internal static async Task VerifySendAsync(RelayConnectionStringBuilder connectionString, int number, string httpMethod, string requestData, bool verbose)
        {
            Uri hybridHttpUri = new Uri($"https://{connectionString.Endpoint.GetComponents(UriComponents.HostAndPort, UriFormat.SafeUnescaped)}/{connectionString.EntityPath}");
            var tokenProvider = GetTokenProvider(connectionString);
            string token = null;
            if (tokenProvider != null)
            {
                token = (await tokenProvider.GetTokenAsync(hybridHttpUri.AbsoluteUri, TimeSpan.FromMinutes(20))).TokenString;
            }

            var stopwatch = new Stopwatch();
            using (var client = new HttpClient { BaseAddress = hybridHttpUri })
            {
                client.DefaultRequestHeaders.ExpectContinue = false;

                for (int i = 0; i < number; i++)
                {
                    stopwatch.Restart();
                    var httpRequest = new HttpRequestMessage();
                    if (token != null)
                    {
                        httpRequest.Headers.Add("ServiceBusAuthorization", token);
                    }

                    httpRequest.Method = new HttpMethod(httpMethod);
                    if (requestData != null)
                    {
                        httpRequest.Content = new StringContent(requestData);
                    }

                    LogHttpRequest(httpRequest, client, verbose);
                    using (HttpResponseMessage response = await client.SendAsync(httpRequest))
                    {
                        LogHttpResponse(response, verbose);
                    }

                    Console.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds} ms");
                }
            }
        }

        public static async Task<int> VerifyListenAsync(TextWriter output, RelayConnectionStringBuilder connectionString, string responseBody, HttpStatusCode statusCode, string statusDescription, bool verbose)
        {
            bool createdHybridConnection = false;
            var namespaceManager = new RelayNamespaceManager(connectionString.ToString());
            if (!await namespaceManager.HybridConnectionExistsAsync(connectionString.EntityPath))
            {
                output.WriteLine($"Creating HybridConnection {connectionString.EntityPath}...");
                createdHybridConnection = true;
                await namespaceManager.CreateHybridConnectionAsync(new HybridConnectionDescription(connectionString.EntityPath));
                output.WriteLine("Created");
            }

            HybridConnectionListener listener = null;
            try
            {
                listener = new HybridConnectionListener(connectionString.ToString());
                listener.Connecting += (s, e) => ColorConsole.WriteLine(ConsoleColor.Yellow, $"Listener attempting to connect. Last Error: {listener.LastError}");
                listener.Online += (s, e) => ColorConsole.WriteLine(ConsoleColor.Green, "Listener is online");
                EventHandler offlineHandler = (s, e) => ColorConsole.WriteLine(ConsoleColor.Red, $"Listener is OFFLINE. Last Error: {listener.LastError}");
                listener.Offline += offlineHandler;

                var responseBytes = Encoding.UTF8.GetBytes(responseBody);
                listener.RequestHandler = async (context) =>
                {
                    try
                    {
                        LogHttpRequest(context, verbose);
                        context.Response.StatusCode = statusCode;
                        if (statusDescription != null)
                        {
                            context.Response.StatusDescription = statusDescription;
                        }

                        context.Response.Headers[HttpResponseHeader.ContentType] = "text/html";
                        await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        await context.Response.CloseAsync();
                    }
                    catch (Exception exception)
                    {
                        ColorConsole.WriteLine(ConsoleColor.Red, $"RequestHandler Error: {exception.GetType()}: {exception.Message}");
                    }
                };

                Console.WriteLine($"Opening {listener}");
                await listener.OpenAsync();
                output.WriteLine("Press <ENTER> to close the listener ");
                Console.ReadLine();

                output.WriteLine($"Closing {listener}");
                listener.Offline -= offlineHandler; // Avoid a spurious trace on expected shutdown.
                await listener.CloseAsync();
                output.WriteLine("Closed");
                return 0;
            }
            catch (Exception)
            {
                listener?.CloseAsync();
                throw;
            }
            finally
            {
                if (createdHybridConnection)
                {
                    try
                    {
                        output.WriteLine($"Deleting HybridConnection {connectionString.EntityPath}...");
                        await namespaceManager.DeleteHybridConnectionAsync(connectionString.EntityPath);
                        output.WriteLine($"Deleted");
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        internal static TokenProvider GetTokenProvider(RelayConnectionStringBuilder connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString.SharedAccessKeyName) || !string.IsNullOrEmpty(connectionString.SharedAccessKey))
            {
                // At least one was specified
                if (string.IsNullOrEmpty(connectionString.SharedAccessKeyName) || string.IsNullOrEmpty(connectionString.SharedAccessKey))
                {
                    // Only one was specified!
                    throw new ArgumentException(nameof(connectionString), "SharedAccessKey and SharedAccessKeyName must both be specified.");
                }

                return TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionString.SharedAccessKeyName, connectionString.SharedAccessKey);
            }
            else if (!string.IsNullOrEmpty(connectionString.SharedAccessSignature))
            {
                return TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionString.SharedAccessSignature);
            }

            return null;
        }

        public static void LogHttpRequest(HttpRequestMessage httpRequest, HttpClient httpClient, bool verbose = false)
        {
            ConsoleColor color = Console.ForegroundColor;
            string requestUri = $"{httpClient?.BaseAddress}{httpRequest.RequestUri}";
            ColorConsole.WriteLine(color, $"Request: {httpRequest.Method} {requestUri} HTTP/{httpRequest.Version}");
            if (verbose)
            {
                httpClient?.DefaultRequestHeaders.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, color));
                httpRequest.Headers.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, color));
                httpRequest.Content?.Headers.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, color));
                if (httpRequest.Content != null)
                {
                    ColorConsole.WriteLine(color, httpRequest.Content?.ReadAsStringAsync().Result);
                }
                else
                {
                    ColorConsole.WriteLine(color, string.Empty);
                }
            }
        }

        internal static void LogHttpRequest(RelayedHttpListenerContext context, bool verbose = false)
        {
            Console.Write("Request:  ");
            Console.Write($"{context.Request.HttpMethod} {context.Request.Url}");
            if (verbose)
            {
                Console.WriteLine();
                Console.WriteLine($"{new StreamReader(context.Request.InputStream).ReadToEnd()}");
            }
            else
            {
                Console.WriteLine($" ({new StreamReader(context.Request.InputStream).ReadToEnd().Length} bytes, { context.TrackingContext.TrackingId})");
            }            
        }

        internal static void LogHttpResponse(HttpResponseMessage httpResponse, bool verbose = false)
        {
            var foregroundColor = Console.ForegroundColor;
            if ((int)httpResponse.StatusCode >= 500)
            {
                foregroundColor = ConsoleColor.Red;
            }
            else if ((int)httpResponse.StatusCode >= 400)
            {
                foregroundColor = ConsoleColor.Yellow;
            }

            ColorConsole.Write(foregroundColor, $"Response: HTTP/{httpResponse.Version} {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}");
            if (verbose)
            {
                ColorConsole.WriteLine(foregroundColor, string.Empty);
                httpResponse.Headers.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, foregroundColor));
                if (httpResponse.Content != null)
                {
                    httpResponse.Content?.Headers.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, foregroundColor));
                    ColorConsole.WriteLine(foregroundColor, $"{Environment.NewLine}{httpResponse.Content?.ReadAsStringAsync().GetAwaiter().GetResult()}");
                }
            }
            else
            {
                ColorConsole.WriteLine(foregroundColor, $" ({httpResponse.Content?.ReadAsStreamAsync().Result.Length ?? 0} bytes)");
            }
        }

        public static void LogHttpHeader(string headerName, IEnumerable<string> headerValues, ConsoleColor color)
        {
            ColorConsole.WriteLine(color, $"{headerName}: {string.Join(",", headerValues)}");
        }
    }
}
