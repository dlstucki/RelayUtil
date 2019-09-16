// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;

    static class ColorConsole
    {
        static readonly object classLock = new object();

        public static void Write(ConsoleColor color, string text, params object[] formatArgs)
        {
            lock (classLock)
            {
                try
                {
                    Console.ForegroundColor = color;
                    Console.Write(text, formatArgs);
                }
                finally
                {
                    Console.ResetColor();
                }
            }
        }


        public static void WriteLine(ConsoleColor color, string text, params object[] formatArgs)
        {
            lock (classLock)
            {
                try
                {
                    Console.ForegroundColor = color;
                    Console.WriteLine(text, formatArgs);
                }
                finally
                {
                    Console.ResetColor();
                }
            }
        }
    }
}
