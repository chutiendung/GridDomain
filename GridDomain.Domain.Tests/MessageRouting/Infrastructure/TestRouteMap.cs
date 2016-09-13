﻿using GridDomain.CQRS.Messaging;
using GridDomain.CQRS.Messaging.MessageRouting;

namespace GridDomain.Tests.MessageRouting.Infrastructure
{
    public class TestRouteMap : IMessageRouteMap
    {
        public void Register(IMessagesRouter router)
        {
            router.RegisterAggregate(TestAggregatesCommandHandler.Descriptor);
            router.RegisterSaga(SampleSaga.Descriptor);
        }
    }
}