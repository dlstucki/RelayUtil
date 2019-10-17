﻿// Copyright (c) Microsoft. All rights reserved.
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
        public static async Task<int> RunAsync(RelayConnectionStringBuilder connectionString, TraceSource traceSource)
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
                    traceSource.TraceEvent(TraceEventType.Information, (int)ConsoleColor.White, $"Creating HybridConnection '{connectionString.EntityPath}'");
                    createdHybridConnection = true;
                    await namespaceManager.CreateHybridConnectionAsync(new HybridConnectionDescription(connectionString.EntityPath));
                }

                listener = new HybridConnectionListener(connectionString.ToString());
                traceSource.TraceInformation($"Opening {listener}");
                await listener.OpenAsync(cts.Token);
                traceSource.TraceInformation("Listener Opened");

                Uri hybridHttpUri = new Uri($"https://{connectionString.Endpoint.GetComponents(UriComponents.HostAndPort, UriFormat.SafeUnescaped)}/{connectionString.EntityPath}");
                var token = await listener.TokenProvider.GetTokenAsync(hybridHttpUri.AbsoluteUri, TimeSpan.FromMinutes(20));
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    await PostLargeRequestSmallResponse(listener, token, client, traceSource);
                    await PostLargeRequestWithLargeResponse(listener, token, client, traceSource);
                    await GetLargeResponse(listener, token, client, traceSource);
                    await GetSmallResponse(listener, token, client, traceSource);

                    traceSource.TraceEvent(TraceEventType.Information, (int)ConsoleColor.Green, "All tests succeeded");
                }
            }
            catch (Exception exception)
            {
                traceSource.TraceError("FAILED");
                RelayCommands.LogException(exception);
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
                        traceSource.TraceInformation($"Closing {listener}");
                        await listener.CloseAsync(cleanupCancelSource.Token);
                    }

                    if (createdHybridConnection)
                    {
                        traceSource.TraceEvent(TraceEventType.Information, (int)ConsoleColor.White, $"Deleting HybridConnection '{connectionString.EntityPath}'");
                        await namespaceManager.DeleteHybridConnectionAsync(connectionString.EntityPath);
                    }
                }
                catch (Exception cleanupException)
                {
                    traceSource.TraceWarning($"Error during cleanup: {cleanupException.GetType()}: {cleanupException.Message}");
                }
                finally
                {
                    cleanupCancelSource.Dispose();
                }
            }

            return returnCode;
        }

        static async Task GetSmallResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client, TraceSource traceSource)
        {
            RelayCommands.TraceCommandHeader("Testing GET small response (over control connection)");
            listener.RequestHandler = async (context) =>
            {
                LogHttpRequest(context, traceSource);

                var responseBytes = Encoding.UTF8.GetBytes("small response");
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await context.Response.CloseAsync();
            };

            var httpRequest = new HttpRequestMessage();
            httpRequest.Headers.Add("ServiceBusAuthorization", token.TokenString);
            httpRequest.Method = HttpMethod.Get;
            using (HttpResponseMessage response = await client.SendAsync(httpRequest))
            {
                LogHttpResponse(response, traceSource);
                response.EnsureSuccessStatusCode();
                traceSource.TraceInformation("Success");
            }
        }

        static async Task GetLargeResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client, TraceSource traceSource)
        {
            RelayCommands.TraceCommandHeader("Testing GET large response (over rendezvous)");
            listener.RequestHandler = async (context) =>
            {
                LogHttpRequest(context, traceSource);
                var responseBytes = Encoding.UTF8.GetBytes(new string('b', 65 * 1024));
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await context.Response.CloseAsync();
            };

            var httpRequest = new HttpRequestMessage();
            httpRequest.Headers.Add("ServiceBusAuthorization", token.TokenString);
            httpRequest.Method = HttpMethod.Get;
            using (HttpResponseMessage response = await client.SendAsync(httpRequest))
            {
                LogHttpResponse(response, traceSource);
                response.EnsureSuccessStatusCode();
                traceSource.TraceInformation("Success");
            }
        }

        static async Task PostLargeRequestSmallResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client, TraceSource traceSource)
        {
            RelayCommands.TraceCommandHeader("Testing POST large request with small response (over rendezvous)");
            listener.RequestHandler = async (context) =>
            {
                LogHttpRequest(context, traceSource);
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
                LogHttpResponse(response, traceSource);
                response.EnsureSuccessStatusCode();
                traceSource.TraceInformation("Success");
            }
        }

        static async Task PostLargeRequestWithLargeResponse(HybridConnectionListener listener, SecurityToken token, HttpClient client, TraceSource traceSource)
        {
            RelayCommands.TraceCommandHeader("Testing POST large request with large response (over rendezvous)");
            listener.RequestHandler = async (context) =>
            {
                LogHttpRequest(context, traceSource);
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
                LogHttpResponse(response, traceSource);
                response.EnsureSuccessStatusCode();
                traceSource.TraceInformation("Success");
            }
        }

        internal static async Task VerifySendAsync(RelayConnectionStringBuilder connectionString, int number, string httpMethod, string requestData, TraceSource traceSource)
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

                    LogHttpRequest(httpRequest, client, traceSource);
                    using (HttpResponseMessage response = await client.SendAsync(httpRequest))
                    {
                        LogHttpResponse(response, traceSource);
                    }

                    traceSource.TraceInformation($"Elapsed:  {stopwatch.ElapsedMilliseconds} ms");
                }
            }
        }

        public static async Task<int> VerifyListenAsync(TraceSource traceSource, RelayConnectionStringBuilder connectionString, string responseBody, int responseChunkLength, HttpStatusCode statusCode, string statusDescription)
        {
            bool createdHybridConnection = false;
            var namespaceManager = new RelayNamespaceManager(connectionString.ToString());
            if (!await namespaceManager.HybridConnectionExistsAsync(connectionString.EntityPath))
            {
                traceSource.TraceEvent(TraceEventType.Information, (int)ConsoleColor.White, $"Creating HybridConnection '{connectionString.EntityPath}'");
                createdHybridConnection = true;
                await namespaceManager.CreateHybridConnectionAsync(new HybridConnectionDescription(connectionString.EntityPath));
                traceSource.TraceInformation("Created");
            }

            HybridConnectionListener listener = null;
            try
            {
                listener = new HybridConnectionListener(connectionString.ToString());
                listener.Connecting += (s, e) => traceSource.TraceWarning($"Listener attempting to connect. Last Error: {listener.LastError}");
                listener.Online += (s, e) => traceSource.TraceEvent(TraceEventType.Information, (int)ConsoleColor.Green, "Listener is online");
                EventHandler offlineHandler = (s, e) => traceSource.TraceError($"Listener is OFFLINE. Last Error: {listener.LastError}");
                listener.Offline += offlineHandler;

                var responseBytes = Encoding.UTF8.GetBytes(responseBody);
                listener.RequestHandler = async (context) =>
                {
                    try
                    {
                        var stopwatch = new Stopwatch();
                        LogHttpRequest(context, traceSource);
                        context.Response.StatusCode = statusCode;
                        if (statusDescription != null)
                        {
                            context.Response.StatusDescription = statusDescription;
                        }

                        context.Response.Headers[HttpResponseHeader.ContentType] = "text/html";
                        int bytesWritten = 0;
                        do
                        {
                            int countToWrite = Math.Min(responseBody.Length - bytesWritten, responseChunkLength);
                            stopwatch.Restart();
                            await context.Response.OutputStream.WriteAsync(responseBytes, bytesWritten, countToWrite);
                            bytesWritten += countToWrite;
                            traceSource.TraceEvent(TraceEventType.Verbose, 0, "Sent {0} bytes in {1} ms", countToWrite, stopwatch.ElapsedMilliseconds);
                        }
                        while (bytesWritten < responseBody.Length);

                        stopwatch.Restart();
                        await context.Response.CloseAsync();
                        traceSource.TraceEvent(TraceEventType.Verbose, 0, "Close completed in {0} ms", stopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception exception)
                    {
                        ColorConsole.WriteLine(ConsoleColor.Red, $"RequestHandler Error: {exception.GetType()}: {exception.Message}");
                    }
                };

                Console.WriteLine($"Opening {listener}");
                await listener.OpenAsync();
                traceSource.TraceInformation("Press <ENTER> to close the listener ");
                Console.ReadLine();

                traceSource.TraceInformation($"Closing {listener}");
                listener.Offline -= offlineHandler; // Avoid a spurious trace on expected shutdown.
                await listener.CloseAsync();
                traceSource.TraceInformation("Closed");
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
                        traceSource.TraceEvent(TraceEventType.Information, (int)ConsoleColor.White, $"Deleting HybridConnection '{connectionString.EntityPath}'");
                        await namespaceManager.DeleteHybridConnectionAsync(connectionString.EntityPath);
                        traceSource.TraceInformation("Deleted");
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

        public static void LogHttpRequest(HttpRequestMessage httpRequest, HttpClient httpClient, TraceSource traceSource)
        {
            string requestUri = $"{httpClient?.BaseAddress}{httpRequest.RequestUri}";
            traceSource.TraceInformation($"Request:  {httpRequest.Method} {requestUri} HTTP/{httpRequest.Version}");
            if (traceSource.Switch.ShouldTrace(TraceEventType.Verbose))
            {
                httpClient?.DefaultRequestHeaders.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, traceSource, TraceEventType.Information));
                httpRequest.Headers.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, traceSource, TraceEventType.Information));
                httpRequest.Content?.Headers.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, traceSource, TraceEventType.Information));
                if (httpRequest.Content != null)
                {
                    traceSource.TraceEvent(TraceEventType.Information, 0, httpRequest.Content?.ReadAsStringAsync().Result);
                }
                else
                {
                    traceSource.TraceEvent(TraceEventType.Information, 0, string.Empty);
                }
            }
        }

        internal static void LogHttpRequest(RelayedHttpListenerContext context, TraceSource traceSource)
        {
            string requestBody = new StreamReader(context.Request.InputStream).ReadToEnd();
            string output = $"Request:  {context.Request.HttpMethod} {context.Request.Url} ";
            if (traceSource.Switch.ShouldTrace(TraceEventType.Verbose) && !string.IsNullOrEmpty(requestBody))
            {
                output += Environment.NewLine + requestBody + Environment.NewLine;
            }

            output += $"({requestBody.Length} bytes, {context.TrackingContext.TrackingId})";
            traceSource.TraceInformation(output);
        }

        internal static void LogHttpResponse(HttpResponseMessage httpResponse, TraceSource traceSource)
        {
            var eventType = TraceEventType.Information;
            if ((int)httpResponse.StatusCode >= 500)
            {
                eventType = TraceEventType.Error;
            }
            else if ((int)httpResponse.StatusCode >= 400)
            {
                eventType = TraceEventType.Warning;
            }

            if (traceSource.Switch.ShouldTrace(TraceEventType.Verbose))
            {
                traceSource.TraceEvent(eventType, 0, $"Response: HTTP/{httpResponse.Version} {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}");
                httpResponse.Headers.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, traceSource, eventType));
                if (httpResponse.Content != null)
                {
                    httpResponse.Content?.Headers.ToList().ForEach((kvp) => LogHttpHeader(kvp.Key, kvp.Value, traceSource, eventType));
                    traceSource.TraceEvent(eventType, 0, $"{Environment.NewLine}{httpResponse.Content?.ReadAsStringAsync().GetAwaiter().GetResult()}");
                }
            }
            else
            {
                traceSource.TraceEvent(eventType, 0, $"Response: HTTP/{httpResponse.Version} {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase} ({httpResponse.Content?.ReadAsStreamAsync().Result.Length ?? 0} bytes)");
            }
        }

        public static void LogHttpHeader(string headerName, IEnumerable<string> headerValues, TraceSource traceSource, TraceEventType eventType)
        {
            traceSource.TraceEvent(eventType, 0, $"{headerName}: {string.Join(",", headerValues)}");
        }
    }
}
