using System;
using System.Threading.Tasks;
using GridDomain.CQRS;
using GridDomain.EventSourcing.Sagas.FutureEvents;
using GridDomain.Node;
using GridDomain.Node.AkkaMessaging.Waiting;
using GridDomain.Tests.FutureEvents.Infrastructure;
using NUnit.Framework;

namespace GridDomain.Tests.FutureEvents
{
    [TestFixture]
    public class Given_aggregate_When_raising_several_future_events : FutureEventsTest
    {
        private FutureEventOccuredEvent _eventA;
        private FutureEventOccuredEvent _eventB;
        private Guid _aggregateId;

        [OneTimeSetUp]
        public async Task FutureDomainEvent_envelops_has_unique_id()
        {
            _aggregateId = Guid.NewGuid();
            var testCommandA = new ScheduleEventInFutureCommand(DateTime.Now.AddSeconds(1), _aggregateId, "test value A");
            var testCommandB = new ScheduleEventInFutureCommand(DateTime.Now.AddSeconds(2), _aggregateId, "test value B");

            _eventA = await GridNode.Execute(CommandPlan.New(testCommandA, Timeout, Expect.Message<FutureEventOccuredEvent>(e => e.SourceId, testCommandA.AggregateId)));
            _eventB = await GridNode.Execute(CommandPlan.New(testCommandB, Timeout, Expect.Message<FutureEventOccuredEvent>(e => e.SourceId, testCommandB.AggregateId)));
        }

        protected override TimeSpan Timeout => TimeSpan.FromSeconds(3);

        [Then]
        public void Envelop_ids_are_different()
        {
            Assert.AreNotEqual(_eventA.FutureEventId, _eventB.FutureEventId);
        }

        [Then]
        public void Envelop_id_not_equal_to_aggregate_id()
        {
            Assert.True(_eventA.Id != _aggregateId && _aggregateId !=  _eventB.Id);
        }

        public Given_aggregate_When_raising_several_future_events(bool inMemory) : base(inMemory)
        {
        }
        public Given_aggregate_When_raising_several_future_events() : base(true)
        {
        }
    }
}