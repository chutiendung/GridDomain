﻿using System;
using System.Linq;
using System.Threading;
using Akka.Actor;
using CommonDomain.Core;
using GridDomain.CQRS;
using GridDomain.CQRS.Messaging.MessageRouting;
using GridDomain.EventSourcing.Sagas;
using GridDomain.Node.Actors;

namespace GridDomain.Node.AkkaMessaging.Routing
{
    public class ActorMessagesRouter : IMessagesRouter
    {
        private readonly IAggregateActorLocator _actorLocator;
        private readonly TypedMessageActor<CreateActorRouteMessage> _routingActorTypedMessageActor;
        private readonly TypedMessageActor<CreateHandlerRouteMessage> _routingTypedMessageActor;

        public ActorMessagesRouter(IActorRef routingActor, IAggregateActorLocator actorLocator)
        {
            _actorLocator = actorLocator;
            _routingTypedMessageActor = new TypedMessageActor<CreateHandlerRouteMessage>(routingActor);
            _routingActorTypedMessageActor = new TypedMessageActor<CreateActorRouteMessage>(routingActor);
        }

        public IRouteBuilder<TMessage> Route<TMessage>()
        {
            return new AkkaRouteBuilder<TMessage>(_routingTypedMessageActor, _routingActorTypedMessageActor,
                _actorLocator);
        }

        public void RegisterAggregate<TAggregate, TCommandHandler>()
            where TAggregate : AggregateBase
            where TCommandHandler : AggregateCommandsHandler<TAggregate>, new()
        {
            var messageRoutes = new TCommandHandler().GetRegisteredCommands().Select(c => new MessageRoute
            (
                c.Command,
                c.Property
            )).ToArray();

            var name = $"Aggregate_{typeof(TAggregate).Name}";
            var createActorRoute = CreateActorRouteMessage.ForAggregate<TAggregate>(name, messageRoutes);
            _routingActorTypedMessageActor.Handle(createActorRoute);
        }

        public void RegisterSaga(ISagaDescriptor sagaDescriptor)
        {
            var createActorRoute = CreateActorRouteMessage.ForSaga(sagaDescriptor);
            _routingActorTypedMessageActor.Handle(createActorRoute);
        }

        //TODO:replace with wait until event notifications
        public void WaitForRouteConfiguration()
        {
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
    }
}