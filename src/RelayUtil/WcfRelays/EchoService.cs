// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil.WcfRelays
{
    using System;
    using System.ServiceModel;
    using System.Threading;
    using Microsoft.ServiceBus.Tracing;

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    sealed class EchoService : IEcho, ITestOneway, IDisposable
    {
        public static string DefaultResponse { get; set; }
        readonly string trackingId;

        public EchoService()
        {
            IContextChannel channel = OperationContext.Current?.Channel;
            this.trackingId = channel?.GetProperty<TrackingContext>()?.TrackingId;
            RelayTraceSource.TraceVerbose($"{nameof(EchoService)} instance created. TrackingId:{this.trackingId}");
        }

        public void Dispose()
        {
            RelayTraceSource.TraceVerbose($"{nameof(EchoService)} instance disposed. TrackingId:{this.trackingId}");
        }

        public string Echo(DateTime start, string message, TimeSpan delay)
        {
            string duration = string.Empty;
            if (start != default)
            {
                duration = $"({(int)DateTime.UtcNow.Subtract(start).TotalMilliseconds}ms from start)";
            }

            RelayTraceSource.TraceInfo($"Listener received request: Echo({message}) {duration}");
            RelayTraceSource.TraceVerbose($"Request MessageId:{OperationContext.Current.IncomingMessageHeaders.MessageId}, TrackingId:{this.trackingId}");

            if (delay != TimeSpan.Zero)
            {
                RelayTraceSource.TraceVerbose($"Request MessageId:{OperationContext.Current.IncomingMessageHeaders.MessageId} delaying for {delay}");
                Thread.Sleep(delay);
            }

            return DefaultResponse ?? message;
        }

        public string Get(DateTime start, string message, TimeSpan delay)
        {
            string duration = string.Empty;
            if (start != default)
            {
                duration = $"({(int)DateTime.UtcNow.Subtract(start).TotalMilliseconds}ms from start)";
            }

            RelayTraceSource.TraceInfo($"Listener received request: Get({message}) {duration}");
            RelayTraceSource.TraceVerbose($"Request MessageId:{OperationContext.Current.IncomingMessageHeaders.MessageId}, TrackingId:{this.trackingId}");

            if (delay != TimeSpan.Zero)
            {
                RelayTraceSource.TraceVerbose($"Request MessageId:{OperationContext.Current.IncomingMessageHeaders.MessageId} delaying for {delay}");
                Thread.Sleep(delay);
            }

            return DefaultResponse ?? DateTime.UtcNow.ToString("o");
        }

        void ITestOneway.Operation(DateTime start, string message)
        {
            string duration = string.Empty;
            if (start != default)
            {
                duration = $"({(int)DateTime.UtcNow.Subtract(start).TotalMilliseconds}ms from start)";
            }

            RelayTraceSource.TraceInfo($"Listener received request: ITestOneway.Operation({message}) {duration}");
            RelayTraceSource.TraceVerbose($"Request MessageId:{OperationContext.Current.IncomingMessageHeaders.MessageId}, TrackingId:{this.trackingId}");
        }
    }
}
