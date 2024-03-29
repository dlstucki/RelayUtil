﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Microsoft.Extensions.CommandLineUtils;
    using RelayUtil.Utilities;
    using RelayUtil.WcfRelays;

    class Program
    {
        static int Main(string[] args)
        {
            // Unpack other DLLs we need.
            SupportFiles.UnpackResourcesIfNeeded();

            // Don't add any new DLL dependencies in this Main method. Put them inside MainCore or lower and use the SupportFiles approach.
            return MainCore(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int MainCore(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = nameof(RelayUtil);
            app.Description = "Azure Relay Utility Commands";
            RelayCommands.CommonSetup(app);

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
                SharedAccessSignatureCommands.ConfigureCommands(app);

                return app.Execute(args);
            }
            catch (Exception exception)
            {
                RelayTraceSource.TraceException(exception);
                return exception.HResult;
            }
        }
    }
}
