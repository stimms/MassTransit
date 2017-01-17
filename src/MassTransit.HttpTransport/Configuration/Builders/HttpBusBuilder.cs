﻿// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.HttpTransport.Builders
{
    using System;
    using Clients;
    using GreenPipes;
    using MassTransit.Builders;
    using MassTransit.Pipeline;
    using MassTransit.Pipeline.Filters;
    using MassTransit.Pipeline.Observables;
    using MassTransit.Pipeline.Pipes;
    using Specifications;
    using Transport;
    using Transports;
    using Transports.InMemory;


    public class HttpBusBuilder :
        BusBuilder
    {
        readonly HttpReceiveEndpointSpecification _busEndpointSpecification;
        readonly BusHostCollection<HttpHost> _hosts;
        readonly IInMemoryEndpointConfiguration _configuration;

        public HttpBusBuilder(BusHostCollection<HttpHost> hosts, IInMemoryEndpointConfiguration configuration)
            : base(hosts, configuration)
        {
            _hosts = hosts;
            _configuration = configuration;

            var endpointSpecification = configuration.CreateConfiguration(ConsumePipe);

            _busEndpointSpecification = new HttpReceiveEndpointSpecification(_hosts[0], "", endpointSpecification);

            foreach (var host in hosts.Hosts)
            {
                var factory = new HttpReceiveEndpointFactory(this, host, configuration);

                host.ReceiveEndpointFactory = factory;
            }
        }

        public BusHostCollection<HttpHost> Hosts => _hosts;

        public override IPublishEndpointProvider PublishEndpointProvider => _busEndpointSpecification.PublishEndpointProvider;

        public override ISendEndpointProvider SendEndpointProvider => _busEndpointSpecification.SendEndpointProvider;

        protected override void PreBuild()
        {
            _busEndpointSpecification.Apply(this);
        }

        protected override Uri GetInputAddress()
        {
            //TODO: Is this the best approach?
            var addy = _busEndpointSpecification.InputAddress;
            var urb = new UriBuilder(addy);
            urb.Scheme = "reply";
            return urb.Uri;
        }

        protected override ISendTransportProvider CreateSendTransportProvider()
        {
            var receivePipe = CreateReceivePipe();

            var endpointBuilder = new HttpReceiveEndpointBuilder(this, _hosts[0], _configuration);

            return new HttpSendTransportProvider(_hosts, receivePipe, new ReceiveObservable(), _busEndpointSpecification.Configuration,
                _busEndpointSpecification.InputAddress, endpointBuilder.MessageSerializer, _hosts[0]);
        }

        protected IReceivePipe CreateReceivePipe()
        {
            //            AddRescueFilter(builder);

            var endpointBuilder = new HttpReceiveEndpointBuilder(this, _hosts[0], _configuration);

            IPipe<ReceiveContext> receivePipe = Pipe.New<ReceiveContext>(x =>
            {
                x.UseFilter(new DeserializeFilter(endpointBuilder.MessageDeserializer, ConsumePipe));
            });

            return new ReceivePipe(receivePipe, ConsumePipe);
        }
    }
}