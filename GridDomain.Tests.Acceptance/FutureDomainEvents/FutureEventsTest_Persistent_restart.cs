using System;
using System.Threading;
using GridDomain.EventSourcing.Sagas.FutureEvents;
using GridDomain.Node;
using GridDomain.Node.Configuration.Akka;
using GridDomain.Node.Configuration.Composition;
using GridDomain.Node.Configuration.Persistence;
using GridDomain.Scheduling.Quartz;
using GridDomain.Tests.Framework.Configuration;
using GridDomain.Tests.FutureEvents;
using GridDomain.Tests.FutureEvents.Infrastructure;
using GridDomain.Tools.Repositories.AggregateRepositories;
using GridDomain.Tools.Repositories.EventRepositories;
using Microsoft.Practices.Unity;
using NUnit.Framework;

namespace GridDomain.Tests.Acceptance.FutureDomainEvents
{
    [TestFixture]
    public class FutureEventsTest_Persistent_restart : FutureEventsTest
    {

        public FutureEventsTest_Persistent_restart(): base(false)
        {
        }

        protected override TimeSpan Timeout => TimeSpan.FromSeconds(10);

        protected override GridDomainNode CreateGridDomainNode(AkkaConfiguration akkaConf)
        {
            return new GridDomainNode(CreateConfiguration(), CreateMap(), () => AkkaCfg.CreateSystem());
        }

        [Then]
        public void It_fires_after_node_restart()
        {

            var cmd = new ScheduleEventInFutureCommand(DateTime.Now.AddSeconds(10),
                                                        Guid.NewGuid(),
                                                       "test value");

           GridNode.NewCommandWaiter()
                    .Expect<FutureEventScheduledEvent>(e => e.Event.SourceId == cmd.AggregateId)
                    .Create()
                    .Execute(cmd)
                    .Wait(Timeout);

            GridNode.Stop();
            GridNode.Start(new LocalDbConfiguration());

            var waiter = GridNode.NewWaiter()
                                 .Expect<FutureEventOccuredEvent>(e => e.SourceId == cmd.AggregateId)
                                 .Create();

            waiter.Wait(Timeout);

            var repo = new AggregateRepository(new ActorSystemEventRepository(GridNode.System),GridNode.EventsAdaptersCatalog);
            var aggregate = repo.LoadAggregate<TestAggregate>(cmd.AggregateId);
            Assert.LessOrEqual(aggregate.ProcessedTime - cmd.RaiseTime, TimeSpan.FromSeconds(2));
        }
    }
}