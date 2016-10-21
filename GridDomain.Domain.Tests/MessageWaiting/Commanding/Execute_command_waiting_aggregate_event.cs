using System;
using GridDomain.Common;
using GridDomain.CQRS;
using GridDomain.Tests.CommandsExecution;
using GridDomain.Tests.SampleDomain;
using GridDomain.Tests.SampleDomain.Commands;
using GridDomain.Tests.SampleDomain.Events;
using GridDomain.Tests.SampleDomain.ProjectionBuilders;
using NUnit.Framework;

namespace GridDomain.Tests.MessageWaiting.Commanding
{
    [TestFixture]
    public class Execute_command_waiting_aggregate_event : SampleDomainCommandExecutionTests
    {
        [Test]
        public void CommandWaiter_Should_wait_until_aggregate_event()
        {
            var cmd = new LongOperationCommand(1000, Guid.NewGuid());

            var waiter = GridNode.NewCommandWaiter()
                                   .Expect<SampleAggregateChangedEvent>(e => e.SourceId == cmd.AggregateId)
                                   .Create(Timeout)
                                 .Execute(cmd);

            if (!waiter.Wait(Timeout))
                throw new TimeoutException();

            //to finish persistence
            var aggregate = LoadAggregate<SampleAggregate>(cmd.AggregateId);
            Assert.AreEqual(cmd.Parameter.ToString(), aggregate.Value);
        }

        [Test]
        public void MessageWaiter_after_cmd_execute_should_waits_until_aggregate_event()
        {
            var cmd = new LongOperationCommand(1000, Guid.NewGuid());

            var waiter = GridNode.NewWaiter()
                                    .Expect<SampleAggregateChangedEvent>(e => e.SourceId == cmd.AggregateId)
                                 .Create(Timeout);

            GridNode.Execute(cmd);

            if (!waiter.Wait(Timeout))
                throw new TimeoutException();

            //to finish persistence
            var aggregate = LoadAggregate<SampleAggregate>(cmd.AggregateId);
            Assert.AreEqual(cmd.Parameter.ToString(), aggregate.Value);
        }

        [Then]
        public void After_wait_ends_aggregate_should_be_changed()
        {
            var cmd = new LongOperationCommand(1000, Guid.NewGuid());

            GridNode.NewCommandWaiter()
                         .Expect<SampleAggregateChangedEvent>(e => e.SourceId == cmd.AggregateId)
                         .Create(Timeout)
                       .Execute(cmd)
                       .Wait();

            var aggregate = LoadAggregate<SampleAggregate>(cmd.AggregateId);
            Assert.AreEqual(cmd.Parameter.ToString(), aggregate.Value);
        }

        [Then]
        public void Wait_for_timeout_command_throws_excpetion()
        {
            var cmd = new LongOperationCommand(100000, Guid.NewGuid());

            GridNode.NewCommandWaiter()
                         .Expect<SampleAggregateChangedEvent>(e => e.SourceId == cmd.AggregateId)
                         .Create(TimeSpan.FromMilliseconds(100))
                       .Execute(cmd)
                       .Wait();

            var aggregate = LoadAggregate<SampleAggregate>(cmd.AggregateId);
            Assert.AreEqual(cmd.Parameter.ToString(), aggregate.Value);
        }


        [Then]
        public void After_wait_of_async_command_aggregate_should_be_changed()
        {
            var syncCommand = new AsyncMethodCommand(42, Guid.NewGuid());

            GridNode.NewCommandWaiter()
                    .Expect<AggregateChangedEventNotification>(e => e.AggregateId == syncCommand.AggregateId)
                    .Create(Timeout)
                    .Execute(syncCommand)
                    .Wait();

            var aggregate = LoadAggregate<SampleAggregate>(syncCommand.AggregateId);
            Assert.AreEqual(syncCommand.Parameter.ToString(), aggregate.Value);
        }


        [Then]
        public void CommandWaiter_will_wait_for_all_of_expected_message()
        {
            var syncCommand = new LongOperationCommand(1000, Guid.NewGuid());
            try
            {
                GridNode.NewCommandWaiter()
                        .Expect<SampleAggregateChangedEvent>(e => e.SourceId == syncCommand.AggregateId)
                        .And<SampleAggregateCreatedEvent>(e => e.SourceId == syncCommand.AggregateId)
                        .Create(Timeout)
                        .Execute(syncCommand)
                        .Wait();
            }
            catch (AggregateException e)
            {
                Assert.IsInstanceOf<TimeoutException>(e.UnwrapSingle());
            }
        }
    }
}