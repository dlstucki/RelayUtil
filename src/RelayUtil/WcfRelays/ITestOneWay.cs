// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil.WcfRelays
{
    using System;
    using System.ServiceModel;

    [ServiceContract(SessionMode = SessionMode.Allowed)]
    interface ITestOneway
    {
        [OperationContract(IsOneWay = true)]
        void Operation(DateTime start, string message);
    }

    interface ITestOnewayClient : ITestOneway, IClientChannel { }
}
