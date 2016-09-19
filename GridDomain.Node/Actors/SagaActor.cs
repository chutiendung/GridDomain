﻿using System;
using System.Collections.Generic;
using System.Linq;
using Akka;
using Akka.Actor;
using Akka.Monitoring;
using Akka.Monitoring.Impl;
using Akka.Persistence;
using Automatonymous;
using CommonDomain;
using CommonDomain.Core;
using GridDomain.CQRS;
using GridDomain.CQRS.Messaging;
using GridDomain.EventSourcing;
using GridDomain.EventSourcing.Sagas;
using GridDomain.EventSourcing.Sagas.InstanceSagas;
using GridDomain.Node.AkkaMessaging;

namespace GridDomain.Node.Actors
{
    //TODO: add status info, e.g. was any errors during execution or recover

    /// <summary>
    ///     Name should be parse by AggregateActorName
    /// </summary>
    /// <typeparam name="TSaga"></typeparam>
    /// <typeparam name="TSagaState"></typeparam>
    public class SagaActor<TSaga, TSagaState> :
        ReceivePersistentActor where TSaga : class,ISagaInstance 
        where TSagaState : AggregateBase
    {
        private readonly ISagaProducer<TSaga> _producer;
        private readonly IPublisher _publisher;
        private TSaga _saga;
        private IAggregate _sagaData;
        public readonly Guid Id;
        public TSaga Saga => _saga ?? (_saga = _producer.Create(_sagaData));
        private readonly AggregateFactory _aggregateFactory = new AggregateFactory();
        private readonly HashSet<Type> _sagaStartMessageTypes;
        private readonly List<IActorRef> _recoverWaiters = new List<IActorRef>();

        private Guid GetSagaId(DomainEvent msg)
        {
            var type = msg.GetType();
            string fieldName;
         
            if (_sagaIdFields.TryGetValue(type, out fieldName))
                return (Guid) type.GetProperty(fieldName).GetValue(msg);

            return msg.SagaId;
        }

        public SagaActor(ISagaProducer<TSaga> producer,
                         IPublisher publisher)
        {
            _producer = producer;
            _publisher = publisher;
            _sagaStartMessageTypes = new HashSet<Type>(producer.KnownDataTypes.Where(t => typeof(DomainEvent).IsAssignableFrom(t)));
            _sagaIdFields = producer.Descriptor.AcceptMessages.ToDictionary(m => m.MessageType, m => m.CorrelationField);

            //id from name is used due to saga.Data can be not initialized before messages not belonging to current saga will be received
            Id = AggregateActorName.Parse<TSagaState>(PersistenceId).Id;
            _sagaData = _aggregateFactory.Build<TSagaState>(Id);


         
            Command<ShutdownRequest>(req =>
            {
                _monitor.IncrementMessagesReceived();
                Shutdown();
            });

            Command<CheckHealth>(s => Sender.Tell(new HealthStatus(s.Payload)));

            Command<NotifyOnRecoverComplete>(c =>
            {
                var waiter = c.Waiter ?? Sender;
                if (IsRecoveryFinished)
                {
                    waiter.Tell(RecoveryCompleted.Instance);
                }
                else _recoverWaiters.Add(waiter);
            });

            Command<DomainEvent>(msg =>
            {
                _monitor.IncrementMessagesReceived();
                if (_sagaStartMessageTypes.Contains(msg.GetType()))
                    _saga = _producer.Create(msg);

                ProcessSaga(msg);
            }, e => GetSagaId(e) == Id);

            Command<IFault>(fault =>
            {
                _monitor.IncrementMessagesReceived();
                ProcessSaga(fault);
            }, fault => fault.SagaId == Id);


            //recover messages will be provided only to right saga by using peristenceId
            Recover<SnapshotOffer>(offer => _sagaData = (IAggregate)offer.Snapshot);
            Recover<DomainEvent>(e => _sagaData.ApplyEvent(e));
            Recover<RecoveryCompleted>(message =>
            {
                Log.Debug("Recovery for actor {Id} is completed", PersistenceId);
                //notify all 
                foreach (var waiter in _recoverWaiters)
                    waiter.Tell(RecoveryCompleted.Instance);
                _recoverWaiters.Clear();
            });
        }

        protected override bool Receive(object message)
        {
            return base.Receive(message);
        }

        protected virtual void Shutdown()
        {
            //TODO: raise faults for all messages in stash
            Context.Stop(Self);
        }

        private void ProcessSaga(object message)
        {
            Saga.Transit(message);

            ProcessSagaStateChange();

            ProcessSagaCommands();
        }

        private void ProcessSagaCommands()
        {
            foreach (var msg in Saga.CommandsToDispatch)
                      _publisher.Publish(msg);

            Saga.ClearCommandsToDispatch();
        }

        private void ProcessSagaStateChange()
        {
            var stateChangeEvents = Saga.Data.GetUncommittedEvents().Cast<object>();

            PersistAll(stateChangeEvents, e => _publisher.Publish(e));

            Saga.Data.ClearUncommittedEvents();
        }

        private readonly ActorMonitor _monitor = new ActorMonitor(Context, typeof(TSaga).Name);
        private readonly Dictionary<Type, string> _sagaIdFields;

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

        public override string PersistenceId => Self.Path.Name;
    }
}