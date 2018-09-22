﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using NAPS2.Scan;

namespace NAPS2.ClientServer
{
    public class ClientContextFactory
    {
        public ClientContext Create(ScanProxyConfig proxyConfig)
        {
            // TODO: Validate IP
            var uri = new Uri($"net.tcp://{proxyConfig.Ip}:{proxyConfig.Port}/NAPS2.Server");
            var callback = new ServerCallback();
            var instanceContext = new InstanceContext(callback);
            var channelFactory = new DuplexChannelFactory<IServerService>(instanceContext,
                new NetTcpBinding
                {
                    MaxReceivedMessageSize = 1024 * 1024 * 1024
                },
                new EndpointAddress(uri));
            var channel = channelFactory.CreateChannel();
            return new ClientContext { Service = channel, Callback = callback };
        }
    }
}
