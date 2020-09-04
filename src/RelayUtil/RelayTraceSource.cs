// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Diagnostics;

    static class RelayTraceSource
    {
        public static readonly TraceSource Instance = InitializeTraceSource();

        static TraceSource InitializeTraceSource()
        {
            var traceSource = new TraceSource("RelayUtil", SourceLevels.Information);
            traceSource.Listeners.Add(new ColorConsoleTraceListener());
            return traceSource;
        }

        public static void TraceInfo(string message)
        {
            Instance.TraceInformation(message);
        }

        public static void TraceInfo(string message, params object[] args)
        {
            Instance.TraceInformation(message, args);
        }

        public static void TraceWarning(string message)
        {
            Instance.TraceEvent(TraceEventType.Warning, 0, message);
        }


        public static void TraceWarning(this TraceSource traceSource, string message)
        {
            traceSource.TraceEvent(TraceEventType.Warning, 0, message);
        }

        public static void TraceError(this TraceSource traceSource, string message)
        {
            traceSource.TraceEvent(TraceEventType.Error, 0, message);
        }

        public static void TraceVerbose(this TraceSource traceSource, string message)
        {
            traceSource.TraceEvent(TraceEventType.Verbose, 0, message);
        }

        public static void TraceError(string message)
        {
            Instance.TraceEvent(TraceEventType.Error, 0, message);
        }

        public static void TraceVerbose(string message)
        {
            Instance.TraceEvent(TraceEventType.Verbose, 0, message);
        }

        public static void TraceException(Exception exception, string operation = "")
        {
            TraceException(exception, TraceEventType.Error, operation);
        }

        public static void TraceException(Exception exception, TraceEventType eventType, string operation)
        {
            operation = !string.IsNullOrEmpty(operation) ? operation + ": " : string.Empty;
            if (exception is AggregateException aggregateException)
            {
                exception = aggregateException.GetBaseException();
            }

            Instance.TraceEvent(eventType, 0, $"*** {operation}{exception?.ToString().Split('\r')[0]} ***");
            TraceVerbose($"{operation}{exception}");
        }

        class ColorConsoleTraceListener : ConsoleTraceListener
        {
            public ColorConsoleTraceListener()
            {
            }

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
            {
                var color = GetColor(eventType, id);
                ColorConsole.WriteLine(color, eventCache.DateTime.ToString("[HH:mm:ss.fff]"));
            }

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
            {
                var color = GetColor(eventType, id);
                ColorConsole.WriteLine(color, eventCache.DateTime.ToString("[HH:mm:ss.fff] ") + format, args);
            }

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
            {
                var color = GetColor(eventType, id);
                ColorConsole.WriteLine(color, eventCache.DateTime.ToString("[HH:mm:ss.fff] ") + message);
            }

            static ConsoleColor GetColor(TraceEventType eventType, int id)
            {
                switch (eventType)
                {
                    case TraceEventType.Critical:
                    case TraceEventType.Error:
                        return ConsoleColor.Red;
                    case TraceEventType.Warning:
                        return ConsoleColor.Yellow;
                    case TraceEventType.Verbose:
                        return ConsoleColor.DarkGray;
                    case TraceEventType.Start:
                        return ConsoleColor.White;
                    case TraceEventType.Stop:
                        return ConsoleColor.Green;
                    case TraceEventType.Information:
                    default:
                        if (id != 0)
                        {
                            return (ConsoleColor)id;
                        }

                        return Console.ForegroundColor;
                }
            }
        }
    }
}
