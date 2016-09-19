using System;
using GridDomain.Node;
using GridDomain.Node.AkkaMessaging.Waiting;
using GridDomain.Tests.SampleDomain;
using GridDomain.Tests.SampleDomain.Commands;
using GridDomain.Tests.SampleDomain.Events;
using NUnit.Framework;

namespace GridDomain.Tests.CommandsExecution
{
    [TestFixture]
    public class InMemory_When_SyncExecute_until_aggregate_event_wait_by_Node : SampleDomainCommandExecutionTests
    {

        public InMemory_When_SyncExecute_until_aggregate_event_wait_by_Node():base(true)
        {
            
        }
        public InMemory_When_SyncExecute_until_aggregate_event_wait_by_Node(bool inMemory=true) : base(inMemory)
        {


        }

        [Then]
        public void SyncExecute_until_aggregate_event_wait_by_Node()
        {
            var syncCommand = new LongOperationCommand(1000, Guid.NewGuid());
            GridNode.Execute(syncCommand,
                Timeout,
                ExpectedMessage.Once<SampleAggregateChangedEvent>(nameof(SampleAggregateChangedEvent.SourceId),syncCommand.AggregateId));

            var aggregate = LoadAggregate<SampleAggregate>(syncCommand.AggregateId);
            Assert.AreEqual(syncCommand.Parameter.ToString(), aggregate.Value);
        }


    }
}