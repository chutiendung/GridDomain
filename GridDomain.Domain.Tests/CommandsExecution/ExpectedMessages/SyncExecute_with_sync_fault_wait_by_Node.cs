using System;
using System.Threading.Tasks;
using GridDomain.CQRS;
using GridDomain.Tests.SampleDomain;
using GridDomain.Tests.SampleDomain.Commands;
using GridDomain.Tests.SampleDomain.Events;
using NUnit.Framework;

namespace GridDomain.Tests.CommandsExecution.ExpectedMessages
{
    [TestFixture]
    public class SyncExecute_with_sync_fault_wait_by_Node : SampleDomainCommandExecutionTests
    {

        public SyncExecute_with_sync_fault_wait_by_Node() : base(true)
        {

        }
        public SyncExecute_with_sync_fault_wait_by_Node(bool inMemory) : base(inMemory)
        {


        }

        [Then]
        public async Task SyncExecute_throws_exception_from_aggregate_on_fault_wait_by_Node()
        {
            var syncCommand = new AlwaysFaultCommand(Guid.NewGuid());
            var expectedMessage = Expect.Message<SampleAggregateChangedEvent>(e => e.SourceId,syncCommand.AggregateId);

            await  GridNode.Execute(CommandPlan.New(syncCommand, Timeout, expectedMessage))
                           .ShouldThrow<SampleAggregateException>();
        }

    }
}