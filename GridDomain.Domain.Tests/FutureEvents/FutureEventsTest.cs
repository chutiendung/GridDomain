using System;
using GridDomain.Common;
using GridDomain.CQRS.Messaging;
using GridDomain.Node;
using GridDomain.Node.Configuration.Akka;
using GridDomain.Node.Configuration.Composition;
using GridDomain.Node.Configuration.Persistence;
using GridDomain.Scheduling.Quartz;
using GridDomain.Tests.Framework;
using GridDomain.Tests.FutureEvents.Infrastructure;
using Microsoft.Practices.Unity;
using NUnit.Framework;
using Quartz;

namespace GridDomain.Tests.FutureEvents
{
    public abstract class FutureEventsTest : ExtendedNodeCommandTest
    {
        protected IScheduler Scheduler;

        protected FutureEventsTest(bool inMemory) : base(inMemory)
        {

        }

        protected override IContainerConfiguration CreateConfiguration()
        {
            return new CustomContainerConfiguration(
                c => c.RegisterAggregate<TestAggregate, TestAggregatesCommandHandler>(),
                c => c.RegisterInstance(CreateQuartzConfig()));
        }

        protected override IMessageRouteMap CreateMap()
        {
            return new TestRouteMap();
        }


        protected override void Start()
        {
            base.Start();
            Scheduler = GridNode.Container.Resolve<IScheduler>();
            Scheduler.Clear();
        }

        protected virtual IQuartzConfig CreateQuartzConfig()
        {
            return InMemory ? (IQuartzConfig) new InMemoryQuartzConfig() : new PersistedQuartzConfig();
        }

        protected TestAggregate RaiseFutureEventInTime(DateTime scheduledTime)
        {
            var testCommand = new ScheduleEventInFutureCommand(scheduledTime, Guid.NewGuid(), "test value");

            ExecuteAndWaitFor<TestDomainEvent>(testCommand);

            return LoadAggregate<TestAggregate>(testCommand.AggregateId);
        }
    }
}