using System;
using System.Linq;
using GridDomain.CQRS;
using GridDomain.CQRS.Messaging;
using GridDomain.Node.AkkaMessaging.Waiting;
using GridDomain.Tests.CommandsExecution;
using GridDomain.Tests.SampleDomain;
using GridDomain.Tests.SampleDomain.Commands;
using GridDomain.Tests.SampleDomain.Events;
using NUnit.Framework;

namespace GridDomain.Tests.MessageWaiting.Commanding
{
    [TestFixture]
    public class CommandWaiter_results_tests : SampleDomainCommandExecutionTests
    {
        private IWaitResults _results;

        protected override IMessageRouteMap CreateMap()
        {
            var faultyHandlerMap =
                new CustomRouteMap(
                    r => r.RegisterAggregate(SampleAggregatesCommandHandler.Descriptor));

            return new CompositeRouteMap(faultyHandlerMap);
        }

        [OneTimeSetUp]
        public void When_expect_more_than_one_messages()
        {
            var cmd = new CreateAndChangeSampleAggregateCommand(100, Guid.NewGuid());

            _results = GridNode.NewCommandWaiter(Timeout)
                               .Expect<SampleAggregateChangedEvent>(e => e.SourceId == cmd.AggregateId)
                               .And<SampleAggregateCreatedEvent>(e => e.SourceId == cmd.AggregateId)
                               .Create()
                               .Execute(cmd).Result;
        }

        [Then]
        public void Then_recieve_something()
        {
            Assert.NotNull(_results);
        }

        [Then]
        public void Then_recieve_non_empty_collection()
        {
            CollectionAssert.IsNotEmpty(_results.All);
        }

        [Then]
        public void Then_recieved_collection_of_expected_messages()
        {
            Assert.IsTrue(_results.Message<SampleAggregateChangedEvent>() != null && 
                          _results.Message<SampleAggregateCreatedEvent>() != null);
        }

        [Then]
        public void Then_recieve_only_expected_messages()
        {
            Assert.IsTrue(_results.All.Count == 2);
        }
    }
}