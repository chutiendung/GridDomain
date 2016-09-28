using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka;
using Akka.Actor;
using Akka.Monitoring;
using Akka.Monitoring.Impl;
using Akka.Persistence;
using CommonDomain;
using CommonDomain.Core;
using GridDomain.CQRS;
using GridDomain.CQRS.Messaging;
using GridDomain.CQRS.Messaging.Akka;
using GridDomain.CQRS.Messaging.MessageRouting;
using GridDomain.EventSourcing;
using GridDomain.EventSourcing.Sagas.FutureEvents;
using GridDomain.Logging;
using GridDomain.Node.AkkaMessaging;
using GridDomain.Scheduling.Akka.Messages;

namespace GridDomain.Node.Actors
{

   
    //TODO: extract non-actor handler to reuse in tests for aggregate reaction for command
    /// <summary>
    ///     Name should be parse by AggregateActorName
    /// </summary>
    /// <typeparam name="TAggregate"></typeparam>
    public class AggregateActor<TAggregate> : ReceivePersistentActor where TAggregate : AggregateBase
    {
        private readonly IAggregateCommandsHandler<TAggregate> _handler;
        private readonly IPublisher _publisher;
        private readonly TypedMessageActor<ScheduleCommand> _schedulerActorRef;
        private readonly TypedMessageActor<Unschedule> _unscheduleActorRef;
        private readonly List<IActorRef> _recoverWaiters = new List<IActorRef>();
        public readonly Guid Id;

        public AggregateActor(IAggregateCommandsHandler<TAggregate> handler,
                              AggregateFactory factory,
                              TypedMessageActor<ScheduleCommand> schedulerActorRef,
                              TypedMessageActor<Unschedule> unscheduleActorRef,
                              IPublisher publisher)
        {
            _schedulerActorRef = schedulerActorRef;
            _unscheduleActorRef = unscheduleActorRef;
            _handler = handler;
            _publisher = publisher;
            PersistenceId = Self.Path.Name;
            Id = AggregateActorName.Parse<TAggregate>(Self.Path.Name).Id;
            Aggregate = factory.Build<TAggregate>(Id);
            _monitor = new ActorMonitor(Context,typeof(TAggregate).Name);
       
            //async aggregate method execution finished, aggregate already raised events
            //need process it in usual way
            Command<AsyncEventsReceived>(m =>
            {
                _monitor.IncrementMessagesReceived();
                if (m.Exception != null)
                {
                   _publisher.Publish(Fault.NewGeneric(m.Command, m.Exception, typeof(TAggregate),m.Command.SagaId));
                    return;
                }

                (Aggregate as Aggregate).FinishAsyncExecution(m.InvocationId);
                ProcessAggregateEvents(m.Command);
            });

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
            Command<ICommand>(cmd =>
            {
                _monitor.IncrementMessagesReceived();
                try
                {
                    Aggregate = _handler.Execute(Aggregate, cmd);
                }
                catch (Exception ex)
                {
                    _publisher.Publish(Fault.NewGeneric(cmd, ex, typeof(TAggregate),cmd.SagaId));
                    Log.Error(ex,"{Aggregate} raised an exception {Exception} while executing {Command}",Aggregate,ex,cmd);
                    return;
                }

                ProcessAggregateEvents(cmd);
            });
            Command<SaveSnapshotFailure>(f =>
            {
                Log.Error(f.Cause,
                          "Error while saving snapshot for {PersistenceId}, additional data: {Data}",
                          PersistenceId,
                          f.Metadata);
            });

            Recover<SnapshotOffer>(offer => Aggregate = (TAggregate) offer.Snapshot);
            Recover<DomainEvent>(e => ((IAggregate) Aggregate).ApplyEvent(e));
            Recover<RecoveryCompleted>(message =>
            {
                Log.Debug("Recovery for actor {Id} is completed", PersistenceId);
                //notify all 
                foreach(var waiter in _recoverWaiters)
                     waiter.Tell(RecoveryCompleted.Instance);
                _recoverWaiters.Clear();
            });
            

        }

        protected virtual void Shutdown()
        {
            Context.Stop(Self);
        }

        private void ProcessAggregateEvents(ICommand command)
        {

            var aggregate = (IAggregate) Aggregate;

            var uncommittedEvents = aggregate.GetUncommittedEvents();

            var events = uncommittedEvents.Cast<DomainEvent>();
            if (command.SagaId != Guid.Empty)
            {
                events = events.Select(e => e.CloneWithSaga(command.SagaId));
            }

            PersistAll(events, e =>
            {
                _publisher.Publish(e);

                //TODO: move scheduling event processing to some separate handler or aggregateActor extension. 
                // how to pass aggregate type in this case? 
                //direct call to method to not postpone process of event scheduling, 
                //case it can be interrupted by other messages in stash processing errors
                e.Match().With<FutureEventScheduledEvent>(Handle)
                         .With<FutureEventCanceledEvent>(Handle);

            });
            aggregate.ClearUncommittedEvents();

            SaveSnapshot(Aggregate);

            ProcessAsyncMethods(command);
        }

        private void ProcessAsyncMethods(ICommand command)
        {
            var extendedAggregate = Aggregate as Aggregate;
            if (extendedAggregate == null) return;

            //When aggregate notifies external world about async method execution start,
            //actor should schedule results to process it
            //command is included to safe access later, after async execution complete
            var cmd = command;
            foreach (var asyncMethod in extendedAggregate.AsyncUncomittedEvents)
                asyncMethod.ResultProducer.ContinueWith(t => new AsyncEventsReceived(t.IsFaulted ? null: t.Result, cmd, asyncMethod.InvocationId, t.Exception))
                                          .PipeTo(Self);

            extendedAggregate.AsyncUncomittedEvents.Clear();
        }

        public void Handle(FutureEventScheduledEvent message)
        {
            Guid scheduleId = message.Id;
            Guid aggregateId = message.Event.SourceId;

            var description = $"Aggregate {typeof(TAggregate).Name} id = {aggregateId} scheduled future event " +
                              $"{scheduleId} with payload type {message.Event.GetType().Name} on time {message.RaiseTime}\r\n" +
                              $"Future event: {message.ToPropsString()}";

            var scheduleKey = CreateScheduleKey(scheduleId, aggregateId, description);

            var scheduleEvent = new ScheduleCommand(new RaiseScheduledDomainEventCommand(message.Id, message.SourceId),
                                                    scheduleKey,
                                                    new ExecutionOptions(message.RaiseTime, message.Event.GetType()));

            _schedulerActorRef.Handle(scheduleEvent);
        }

        public static ScheduleKey CreateScheduleKey(Guid scheduleId, Guid aggregateId, string description)
        {
            return new ScheduleKey(scheduleId,
                                   $"{typeof(TAggregate).Name}_{aggregateId}_future_event_{scheduleId}",
                                   $"{typeof(TAggregate).Name}_futureEvents",
                                   "");
        }

        public void Handle(FutureEventCanceledEvent message)
        {
            var key = CreateScheduleKey(message.FutureEventId, message.SourceId, "");
            var unscheduleMessage = new Unschedule(key);
            _unscheduleActorRef.Handle(unscheduleMessage);
        }

        public TAggregate Aggregate { get; private set; }
        public override string PersistenceId { get; }

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