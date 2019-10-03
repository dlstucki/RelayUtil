﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using Microsoft.Extensions.CommandLineUtils;

    class RelayCommands
    {
        public static void SetVerbose(CommandOption verboseOption)
        {
            bool verbose = verboseOption.HasValue();
            if (verbose)
            {
                RelayTraceSource.Instance.Switch.Level = SourceLevels.Verbose;
            }
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

        public static void TraceCommandHeader(string commandName)
        {
            RelayTraceSource.Instance.TraceEvent(TraceEventType.Information, (int)ConsoleColor.White, $"============================== {commandName} ==============================");
        }
    }
}