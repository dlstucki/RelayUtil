// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace RelayUtil.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    class NetworkUtility
    {
        static readonly int[] RelayPorts = new int[]
        {
            80, 443, 5671, 9350, 9351, 9352, 9353, 9354
        };

        public static async Task VerifyRelayPortsAsync(string hostName, TextWriter output)
        {
            output.Write($"Checking {hostName} ");
            try
            {
                IPHostEntry ipHostEntry = await Dns.GetHostEntryAsync(hostName);
                output.WriteLine($"({ipHostEntry.AddressList[0]})");
                var tasks = new List<Tuple<IPEndPoint, Task>>();
                foreach (int port in RelayPorts)
                {
                    var ipEndpoint = new IPEndPoint(ipHostEntry.AddressList[0], port);
                    tasks.Add(new Tuple<IPEndPoint, Task>(ipEndpoint, ProbeTcpPortAsync(ipEndpoint, port)));
                }

                foreach (Tuple<IPEndPoint, Task> item in tasks)
                {
                    IPEndPoint ipEndpoint = item.Item1;
                    Task task = item.Item2;
                    try
                    {
                        await task;
                        output.WriteLine($"{ipEndpoint} succeeded");
                    }
                    catch (Exception ex)
                    {
                        output.WriteLine($"ERROR: {ipEndpoint} FAILED {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                output.WriteLine("ERROR: Exception:" + e);
            }
        }

        public static async Task ProbeTcpPortAsync(IPEndPoint ipEndPoint, int port)
        {
            using (var socket = new Socket(ipEndPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                await Task.Factory.FromAsync(
                    (c, s) => socket.BeginConnect(ipEndPoint, c, s),
                    (a) => socket.EndConnect(a),
                    socket);
            }
        }
    }
}
