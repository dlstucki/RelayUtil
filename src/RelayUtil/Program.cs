﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Extensions.CommandLineUtils;
    using RelayUtil.WcfRelays;

    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = nameof(RelayUtil);
            app.Description = "Azure Relay Utility Commands";
            app.HelpOption(CommandStrings.HelpTemplate);
            app.Option("-v|--verbose", "Verbose output", CommandOptionType.NoValue);

            try
            {
                app.OnExecute(() =>
                {
                    app.ShowHelp();
                    return 0;
                });

                DiagnosticCommands.ConfigureCommands(app);
                WcfRelayCommands.ConfigureCommands(app);
                HybridConnectionCommands.ConfigureCommands(app);

                return app.Execute(args);
            }
            catch (Exception exception)
            {
                LogException(app.Out, exception);
                return exception.HResult;
            }
        }

        static void LogException(TextWriter output, Exception exception)
        {
            bool verbose = Environment.GetCommandLineArgs().Any(arg => arg.Equals("-v", StringComparison.CurrentCultureIgnoreCase) || arg.Equals("--verbose", StringComparison.CurrentCultureIgnoreCase));
            if (exception is AggregateException aggregateException)
            {
                exception = aggregateException.GetBaseException();
            }
            
            if (verbose)
            {
                output.WriteLine(exception);
            }
            else
            {
                output.WriteLine($"{exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}