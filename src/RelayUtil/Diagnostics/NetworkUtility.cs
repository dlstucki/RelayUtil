// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace RelayUtil.Diagnostics
{
    using System;
    using System.Collections.Generic;
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

        public static async Task<string> VerifyRelayPortsAsync(string hostName)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("*****************************************************************************************");
            stringBuilder.Append($"Checking {hostName} ");
            try
            {
                IPHostEntry ipHostEntry = await Dns.GetHostEntryAsync(hostName);
                stringBuilder.AppendLine($"({ipHostEntry.AddressList[0]})");
                var tasks = new List<Tuple<int, Task>>();
                foreach (int port in RelayPorts)
                {
                    tasks.Add(new Tuple<int, Task>(port, ProbeTcpPortAsync(ipHostEntry, port)));
                }

                foreach (Tuple<int, Task> item in tasks)
                {
                    int port = item.Item1;
                    Task task = item.Item2;
                    try
                    {
                        await task;
                        stringBuilder.AppendLine($"Port: {port} succeeded");
                    }
                    catch (Exception ex)
                    {
                        stringBuilder.AppendLine($"ERROR: Port: {port} FAILED {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                stringBuilder.AppendLine("\r\nERROR: Exception:" + e);
            }

            return stringBuilder.ToString();
        }

        public static async Task ProbeTcpPortAsync(IPHostEntry ipHostEntry, int port)
        {
            var myLocalEndPoint = new IPEndPoint(ipHostEntry.AddressList[0], port);
            using (var socket = new Socket(myLocalEndPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                await Task.Factory.FromAsync(
                    (c, s) => socket.BeginConnect(myLocalEndPoint, c, s),
                    (a) => socket.EndConnect(a),
                    socket);
            }
        }
    }
}
