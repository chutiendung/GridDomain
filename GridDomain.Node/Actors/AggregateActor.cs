using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka;
using Akka.Actor;
using Akka.Monitoring;
using Akka.Monitoring.Impl;
using CommonDomain.Core;
using CommonDomain.Persistence;
using GridDomain.CQRS;
using GridDomain.CQRS.Messaging;
using GridDomain.CQRS.Messaging.Akka;
using GridDomain.CQRS.Messaging.MessageRouting;
using GridDomain.EventSourcing;
using GridDomain.EventSourcing.Sagas.FutureEvents;
using GridDomain.Logging;
using GridDomain.Scheduling.Akka.Messages;
using Helios.Util;

namespace GridDomain.Node.Actors
{
    //TODO: extract non-actor handler to reuse in tests for aggregate reaction for command
    /// <summary>
    ///     Name should be parse by AggregateActorName
    /// </summary>
    /// <typeparam name="TAggregate"></typeparam>
    public class AggregateActor<TAggregate> : EventSourcedActor<TAggregate> where TAggregate : AggregateBase
    {
        protected readonly IAggregateCommandsHandler<TAggregate> _handler;
        private readonly TypedMessageActor<ScheduleCommand> _schedulerActorRef;
        private readonly TypedMessageActor<Unschedule> _unscheduleActorRef;

        public AggregateActor(IAggregateCommandsHandler<TAggregate> handler,
                              TypedMessageActor<ScheduleCommand> schedulerActorRef,
                              TypedMessageActor<Unschedule> unscheduleActorRef,
                              IPublisher publisher,
                              ISnapshotsPersistencePolicy snapshotsPersistencePolicy,
                              IConstructAggregates aggregateConstructor) : base(
                                  aggregateConstructor,
                                  snapshotsPersistencePolicy,
                                  publisher)
        {
            _schedulerActorRef = schedulerActorRef;
            _unscheduleActorRef = unscheduleActorRef;
            _handler = handler;

            //async aggregate method execution finished, aggregate already raised events
            //need process it in usual way
            Command<AsyncEventsReceived>(m =>
            {
                Monitor.IncrementMessagesReceived();
                if (m.Exception != null)
                {
                   Publisher.Publish(Fault.NewGeneric(m.Command, m.Exception, typeof(TAggregate),m.Command.SagaId));
                    return;
                }

                (State as Aggregate).FinishAsyncExecution(m.InvocationId);
                ProcessAggregateEvents(m.Command);
            });
           
            Command<ICommand>(cmd =>
            {
                Monitor.IncrementMessagesReceived();
                _log.Trace("{Aggregate} received a {@command}", State.Id, cmd);
                try
                {
                    State = _handler.Execute((TAggregate)State, cmd);
                }
                catch (Exception ex)
                {
                    Publisher.Publish(Fault.NewGeneric(cmd, ex, typeof(TAggregate),cmd.SagaId));
                    Log.Error(ex,"{Aggregate} raised an expection {@Exception} while executing {@Command}",State.Id,ex,cmd);
                    return;
                }

                ProcessAggregateEvents(cmd);
            });
        }

        private void ProcessAggregateEvents(ICommand command)
        {
            var events = State.GetUncommittedEvents().Cast<DomainEvent>().ToArray();

            if (command.SagaId != Guid.Empty)
            {
                events = events.Select(e => e.CloneWithSaga(command.SagaId)).ToArray();
            }

            PersistAll(events, e =>
            {
                //TODO: move scheduling event processing to some separate handler or aggregateActor extension. 
                //how to pass aggregate type in this case? 
                //direct call to method to not postpone process of event scheduling, 
                //case it can be interrupted by other messages in stash processing errors
                e.Match().With<FutureEventScheduledEvent>(Handle)
                         .With<FutureEventCanceledEvent>(Handle);

                Publisher.Publish(e);

                NotifyWatchers(new Persisted(e));
            });

            State.ClearUncommittedEvents();

            ProcessAsyncMethods(command);

            if(SnapshotsPolicy.ShouldSave(events))
                SaveSnapshot(State.GetSnapshot());
        }
        
        private void ProcessAsyncMethods(ICommand command)
        {
            var extendedAggregate = State as Aggregate;
            if (extendedAggregate == null) return;

            //When aggregate notifies external world about async method execution start,
            //actor should schedule results to process it
            //command is included to safe access later, after async execution complete
            var cmd = command;
            foreach (var asyncMethod in extendedAggregate.GetAsyncUncomittedEvents())
                asyncMethod.ResultProducer.ContinueWith(t => new AsyncEventsReceived(t.IsFaulted ? null: t.Result, cmd, asyncMethod.InvocationId, t.Exception))
                                          .PipeTo(Self);

            extendedAggregate.ClearAsyncUncomittedEvents();
        }

        public void Handle(FutureEventScheduledEvent message)
        {
            Guid scheduleId = message.Id;
            Guid aggregateId = message.Event.SourceId;

            var description = $"Aggregate {typeof(TAggregate).Name} id = {aggregateId} scheduled future event " +
                              $"{scheduleId} with payload type {message.Event.GetType().Name} on time {message.RaiseTime}\r\n" +
                              $"Future event: {message.ToPropsString()}";

            var scheduleKey = CreateScheduleKey(scheduleId, aggregateId, description);

            var scheduleEvent = new ScheduleCommand(new RaiseScheduledDomainEventCommand(message.Id, message.SourceId, Guid.NewGuid()),
                                                    scheduleKey,
                                                    new ExtendedExecutionOptions(message.RaiseTime, 
                                                                                 message.Event.GetType(), 
                                                                                 message.Event.SourceId,
                                                                                 nameof(DomainEvent.SourceId)));
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
    }
}