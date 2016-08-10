﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Akka;
using Akka.Actor;
using Akka.Dispatch;
using Akka.DI.Core;
using Akka.Monitoring;
using Akka.Monitoring.Impl;
using GridDomain.CQRS;
using GridDomain.CQRS.Messaging;
using GridDomain.CQRS.Messaging.Akka;
using GridDomain.EventSourcing;
using GridDomain.Logging;
using GridDomain.Node.AkkaMessaging;
using GridDomain.Node.AkkaMessaging.Routing;
using GridDomain.Node.AkkaMessaging.Waiting;
using Quartz.Collection;

namespace GridDomain.Node.Actors
{
    public class GridNodeController : TypedActor
    {
        private readonly ISoloLogger _log = LogManager.GetLogger();
        private readonly IPublisher _messagePublisher;
        private readonly IMessageRouteMap _messageRouting;
        private readonly IActorSubscriber _subscriber;

        public GridNodeController(IPublisher transport,
                                  IActorSubscriber subscriber,
                                  IMessageRouteMap messageRouting)
        {
            _subscriber = subscriber;
            _messageRouting = messageRouting;
            _messagePublisher = transport;
            _monitor = new ActorMonitor(Context);
        }

        public void Handle(Start msg)
        {
            _monitor.IncrementMessagesReceived();
            LogManager.SetLoggerFactory(new DefaultLoggerFactory(new DefaultLoggerConfiguration()));

            var system = Context.System;
            var routingActor = system.ActorOf(system.DI().Props(msg.RoutingActorType),msg.RoutingActorType.Name);

            var actorMessagesRouter = new ActorMessagesRouter(routingActor, new DefaultAggregateActorLocator());
            _messageRouting.Register(actorMessagesRouter);

            //TODO: replace with message from router
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(3), Sender, new Started(), Self);
        }

        public void Handle(ICommand cmd)
        {
            _monitor.IncrementMessagesReceived();
            _messagePublisher.Publish(cmd);
        }

        public void Handle(CommandPlan commandWithConfirmation)
        {
            var waitActor = Context.System.ActorOf(Props.Create(() => new CommandWaiter(Sender, commandWithConfirmation.Command,commandWithConfirmation.ExpectedMessages)),"MessageWaiter_command_"+commandWithConfirmation.Command.Id);

            foreach(var expectedMessage in commandWithConfirmation.ExpectedMessages)
                    _subscriber.Subscribe(expectedMessage.MessageType, waitActor);

            Handle(commandWithConfirmation.Command);
        }
        
        public class Start
        {
            public Type RoutingActorType;
        }

        public class Started
        {
        }

        private readonly ActorMonitor _monitor;

        protected override void PreStart()
        {
            _monitor.IncrementActorStarted();
        }

        protected override void PostStop()
        {
            _monitor.IncrementActorStopped();
        }
        protected override void PreRestart(Exception reason, object message)
        {
            _monitor.IncrementActorRestarted();
        }
    }
}