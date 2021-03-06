using System;
using GridDomain.Tests.Framework.Configuration;
using GridDomain.Tests.SampleDomain;
using GridDomain.Tests.Tools;
using GridDomain.Tests.Tools.Repositories;
using GridDomain.Tools.Repositories.AggregateRepositories;
using GridDomain.Tools.Repositories.EventRepositories;
using NUnit.Framework;

namespace GridDomain.Tests.Acceptance.Tools
{
    [TestFixture]
    class Given_persisted_aggreate_It_can_be_loaded_and_saved
    {
        private SampleAggregate _aggregate;
        private Guid _aggregateId;
        private string _agregateValue;

        [OneTimeSetUp]
        public void Given_persisted_aggreate()
        {
            _aggregateId = Guid.NewGuid();
            _agregateValue = "initial";
            var aggregate = new SampleAggregate(_aggregateId, _agregateValue);

            using (var repo = new AggregateRepository(ActorSystemEventRepository.New(new AutoTestAkkaConfiguration())))
            {
                repo.Save(aggregate);
            }

            using (var repo = new AggregateRepository(ActorSystemEventRepository.New(new AutoTestAkkaConfiguration())))
            {
                _aggregate = repo.LoadAggregate<SampleAggregate>(aggregate.Id);
            }
        }

        [Then]
        public void Aggregate_has_correct_id()
        {
            Assert.AreEqual(_aggregateId, _aggregate.Id);
        }


        [Then]
        public void Aggregate_has_state_from_changed_event()
        {
            Assert.AreEqual(_agregateValue, _aggregate.Value);
        }

    }
}