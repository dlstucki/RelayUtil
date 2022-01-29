// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
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

        public static async Task<string> VerifyRelayPortsAsync(string hostName, IEnumerable<int> extraPorts = null)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Checking {hostName} ");
            try
            {
                IPHostEntry ipHostEntry = await Dns.GetHostEntryAsync(hostName);
                var tasks = new List<Task<string>>();
                IEnumerable<int> ports = extraPorts != null ? RelayPorts.Union(extraPorts) : RelayPorts;
                foreach (var address in ipHostEntry.AddressList)
                {
                    foreach (int port in ports)
                    {
                        var ipEndpoint = new IPEndPoint(address, port);
                        tasks.Add(ProbeTcpPortAsync(ipEndpoint));
                    }
                }

                foreach (Task<string> task in tasks)
                {
                    string result = await task;
                    stringBuilder.AppendLine(result);
                }
            }
            catch (Exception e)
            {
                stringBuilder.AppendLine("ERROR: Exception:" + e);
            }

            return stringBuilder.ToString();
        }

        public static async Task<string> ProbeTcpPortAsync(IPEndPoint ipEndPoint)
        {
            using (var socket = new Socket(ipEndPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await Task.Factory.FromAsync(
                        (c, s) => socket.BeginConnect(ipEndPoint, c, s),
                        (a) => socket.EndConnect(a),
                        socket);
                    stopwatch.Stop();
                    return $"{ipEndPoint} succeeded in {stopwatch.ElapsedMilliseconds} ms";
                }
                catch (Exception ex)
                {
                    return $"{ipEndPoint} FAILED in {stopwatch.ElapsedMilliseconds} ms. {ex.GetType().Name}: {ex.Message}";
                }
            }
        }
    }
}
