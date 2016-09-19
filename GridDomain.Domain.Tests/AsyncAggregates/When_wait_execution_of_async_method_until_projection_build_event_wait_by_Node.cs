using System;
using GridDomain.Node;
using GridDomain.Node.AkkaMessaging.Waiting;
using GridDomain.Tests.CommandsExecution;
using GridDomain.Tests.SampleDomain;
using GridDomain.Tests.SampleDomain.Commands;
using NUnit.Framework;
using AggregateChangedEventNotification = GridDomain.Tests.SampleDomain.ProjectionBuilders.AggregateChangedEventNotification;

namespace GridDomain.Tests.AsyncAggregates
{
    [TestFixture]
    public class When_wait_execution_of_async_method_until_projection_build_event_wait_by_Node : SampleDomainCommandExecutionTests
    {
        public When_wait_execution_of_async_method_until_projection_build_event_wait_by_Node(bool inMemory=true) : base(inMemory)
        {


        }

        public When_wait_execution_of_async_method_until_projection_build_event_wait_by_Node():base(true)
        {
            
        }
        [Then]
        public void After_wait_aggregate_should_be_changed()
        {
            var syncCommand = new AsyncMethodCommand(42, Guid.NewGuid());
            var expectedMessage = ExpectedMessage.Once<AggregateChangedEventNotification>(e => e.AggregateId,
                syncCommand.AggregateId);

            GridNode.Execute(syncCommand, Timeout, expectedMessage);

            var aggregate = LoadAggregate<SampleAggregate>(syncCommand.AggregateId);
            Assert.AreEqual(syncCommand.Parameter.ToString(), aggregate.Value);
        }
    }
}