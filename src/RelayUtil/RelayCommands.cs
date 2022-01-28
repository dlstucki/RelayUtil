// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Security.Authentication;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Extensions.CommandLineUtils;

    class RelayCommands
    {
        public static CommandLineApplication CommonSetup(CommandLineApplication app)
        {
            app.HelpOption(CommandStrings.HelpTemplate);
            app.Option(CommandStrings.VerboseTemplate, CommandStrings.VerboseDescription, CommandOptionType.NoValue);

            // Check if verbose was specified anywhere on the command line:
            bool verbose = Environment.GetCommandLineArgs().Any(arg =>
                arg.Equals(CommandStrings.VerboseShort, StringComparison.CurrentCultureIgnoreCase) ||
                arg.Equals(CommandStrings.VerboseLong, StringComparison.CurrentCultureIgnoreCase));

            if (verbose)
            {
                RelayTraceSource.Instance.Switch.Level = SourceLevels.Verbose;
            }

            return app;
        }

        public static bool GetBoolOption(CommandOption boolOption, bool defaultValue)
        {
            if (!boolOption.HasValue())
            {
                return defaultValue;
            }

            return bool.Parse(boolOption.Value());
        }

        public static int GetIntOption(CommandOption intOption, int defaultValue)
        {
            if (!intOption.HasValue())
            {
                return defaultValue;
            }

            return int.Parse(intOption.Value());
        }

        public static string GetStringOption(CommandOption stringOption, string defaultValue)
        {
            return stringOption.Value() ?? defaultValue;
        }

        public static TEnum GetEnumOption<TEnum>(CommandOption enumOption, TEnum defaultValue)
            where TEnum : struct
        {
            if (enumOption.HasValue())
            {
                string stringValue = GetStringOption(enumOption, string.Empty);
                return (TEnum)Enum.Parse(typeof(TEnum), stringValue, ignoreCase: true);
            }

            return defaultValue;
        }

        public static string GetMessageBody(CommandOption valueOption, CommandOption lengthOption, string defaultValue)
        {
            string messageData = valueOption.HasValue() ? valueOption.Value() : defaultValue;
            if (lengthOption.HasValue())
            {
                if (string.IsNullOrEmpty(messageData))
                {
                    messageData = "1234567890";
                }

                int requestedLength = int.Parse(lengthOption.Value());
                var stringBuffer = new StringBuilder(requestedLength);

                int countNeeded;
                while ((countNeeded = requestedLength - stringBuffer.Length) > 0)
                {
                    stringBuffer.Append(messageData.Substring(0, Math.Min(countNeeded, messageData.Length)));
                }

                messageData = stringBuffer.ToString();
            }

            return messageData;
        }

        public static void ConfigureSecurityProtocol(CommandOption protocolOption)
        {
            if (protocolOption != null && protocolOption.HasValue())
            {
                ServicePointManager.SecurityProtocol = GetEnumOption(protocolOption, ServicePointManager.SecurityProtocol);
                SetServicePointManagerDefaultSslProtocols(GetEnumOption(protocolOption, SslProtocols.Default));
            }
        }

        public static void TraceMissingArgument(string argumentName)
        {
            RelayTraceSource.TraceError($"*** Error: {argumentName} is required ***");
        }

        public static void TraceCommandHeader(string commandName, TraceSource traceSource = null)
        {
            traceSource = traceSource ?? RelayTraceSource.Instance;
            traceSource.TraceEvent(TraceEventType.Warning, (int)ConsoleColor.White, $"========== {commandName} ==========");
        }

        internal static async Task RunTestAsync(string testName, Regex testNameRegex, IList<TestResult> testResults, Func<Task> testFunc)
        {
            if (testNameRegex != null && !testNameRegex.IsMatch(testName))
            {
                // Filter specified and test name doesn't match
                return;
            }

            TraceCommandHeader(testName);
            try
            {
                await testFunc();
                testResults.Add(new TestResult { Passed = true, TestName = testName });
            }
            catch (Exception exception)
            {
                RelayTraceSource.TraceException(exception, testName);
                testResults.Add(new TestResult { Passed = false, TestName = testName });
            }
        }

        internal static int ReportTestResults(List<TestResult> testResults)
        {
            IEnumerable<string> passedTests = testResults.Where(r => r.Passed).Select(r => r.TestName);
            IEnumerable<string> failedTests = testResults.Where(r => !r.Passed).Select(r => r.TestName);

            int passCount = 0;
            foreach (var passedTest in passedTests)
            {
                RelayTraceSource.TraceEvent(TraceEventType.Information, ConsoleColor.Green, $"PASSED: {passedTest}");
                passCount++;
            }

            if (passCount > 0)
            {
                RelayTraceSource.TraceEvent(TraceEventType.Information, ConsoleColor.Green, $"{passCount} tests passed.");
            }

            int failCount = 0;
            foreach (var failedTest in failedTests)
            {
                RelayTraceSource.TraceEvent(TraceEventType.Error, ConsoleColor.Red, $"FAILED: {failedTest}");
                failCount++;
            }

            if (failCount > 0)
            {
                RelayTraceSource.TraceEvent(TraceEventType.Error, ConsoleColor.Red, $"{failCount} tests failed");
            }

            return failCount;
        }

        static void SetServicePointManagerDefaultSslProtocols(SslProtocols sslProtocols)
        {
            FieldInfo s_defaultSslProtocols = typeof(ServicePointManager).GetField("s_defaultSslProtocols", BindingFlags.Static | BindingFlags.NonPublic);
            if (s_defaultSslProtocols != null)
            {
                s_defaultSslProtocols.SetValue(null, sslProtocols);
            }
            else
            {
                RelayTraceSource.TraceWarning("ServicePointManager.s_defaultSslProtocols field not found.");
            }
        }

        internal sealed class TestResult
        {
            public bool Passed { get; set; }

            public string TestName { get; set; }
        }
    }
}
