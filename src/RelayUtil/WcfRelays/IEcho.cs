// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil.WcfRelays
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Web;

    [ServiceContract(SessionMode = SessionMode.Allowed)]
    interface IEcho
    {
        /// <summary>
        /// Here's a sample HTTP request for WebHttpRelayBinding
        /// POST https://YOURRELAY.servicebus.windows.net/RelayUtilWcf/echo?start=2019-10-19T00:44:36.0328204Z HTTP/1.1
        /// Content-Type: application/json
        /// Content-Length: 20
        /// 
        /// "Test Message Data2"
        /// </summary>
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, UriTemplate = "echo?start={start}&delay={delay}")]
        string Echo(DateTime start, string message, TimeSpan delay);

        /// <summary>
        /// Here's a sample HTTP request for WebHttpRelayBinding
        /// GET https://YOURRELAY.servicebus.windows.net/RelayUtilWcf/get?start=2019-10-19T00:44:36.0328204Z&message=hello%20http HTTP/1.1
        /// 
        /// </summary>
        [WebGet(ResponseFormat = WebMessageFormat.Json, UriTemplate = "get?start={start}&message={message}&delay={delay}")]
        [OperationContract]
        string Get(DateTime start, string message, TimeSpan delay);
    }

    interface IEchoClient : IEcho, IClientChannel { }
}
