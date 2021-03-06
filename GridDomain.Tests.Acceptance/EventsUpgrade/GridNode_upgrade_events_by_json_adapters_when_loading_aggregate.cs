using System;
using System.Threading;
using System.Threading.Tasks;
using GridDomain.CQRS;
using GridDomain.EventSourcing.Adapters;
using GridDomain.Node;
using GridDomain.Tests.CommandsExecution;
using GridDomain.Tests.SampleDomain;
using GridDomain.Tests.SampleDomain.Commands;
using GridDomain.Tests.SampleDomain.Events;
using NUnit.Framework;

namespace GridDomain.Tests.Acceptance.EventsUpgrade
{
    [TestFixture]
    class GridNode_upgrade_events_by_json_adapters_when_loading_aggregate : SampleDomainCommandExecutionTests
    {
        protected override bool ClearDataOnStart { get; } = true;

        public GridNode_upgrade_events_by_json_adapters_when_loading_aggregate():base(false)
        {
            
        }
    
        class InIncreaseByInstanceAdapter : ObjectAdapter<string, string>
        {
            public override string Convert(string value)
            {
                return value + "01";
            }
        }


        class NullAdapter : ObjectAdapter<SampleAggregateCreatedEvent, SampleAggregateCreatedEvent>
        {
            public override SampleAggregateCreatedEvent Convert(SampleAggregateCreatedEvent value)
            {
                return new SampleAggregateCreatedEvent(value.Value + "01",
                                                       value.SourceId,
                                                       value.CreatedTime,
                                                       value.SagaId);
            }
        }

        [Test]
        public async Task Then_domain_events_should_be_upgraded_by_json_custom_adapter()
        {
            var cmd = new CreateSampleAggregateCommand(1, Guid.NewGuid());

            GridNode.ObjectAdapteresCatalog.Register(new InIncreaseByInstanceAdapter());
            GridNode.ObjectAdapteresCatalog.Register(new NullAdapter());

            var expect = Expect.Message<SampleAggregateCreatedEvent>();

            await GridNode.Execute(CommandPlan.New(cmd, TimeSpan.FromSeconds(10), expect));

            var aggregate = LoadAggregate<SampleAggregate>(cmd.AggregateId);

            Assert.AreEqual("101", aggregate.Value);
        }
    }
}