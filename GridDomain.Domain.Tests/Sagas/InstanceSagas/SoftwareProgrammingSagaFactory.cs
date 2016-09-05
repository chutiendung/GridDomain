using System;
using GridDomain.EventSourcing;
using GridDomain.EventSourcing.Sagas;
using GridDomain.EventSourcing.Sagas.InstanceSagas;
using GridDomain.Tests.Sagas.SoftwareProgrammingDomain.Events;
using GridDomain.Tests.Sagas.StateSagas.SampleSaga;

namespace GridDomain.Tests.Sagas.InstanceSagas
{
    class SoftwareProgrammingSagaFactory:
             ISagaFactory<ISagaInstance<SoftwareProgrammingSaga, SoftwareProgrammingSagaData>, SagaDataAggregate<SoftwareProgrammingSagaData>>,
             ISagaFactory<ISagaInstance<SoftwareProgrammingSaga, SoftwareProgrammingSagaData>, GotTiredEvent>,
             ISagaFactory<ISagaInstance<SoftwareProgrammingSaga, SoftwareProgrammingSagaData>, SleptWellEvent>
    {
        public ISagaInstance<SoftwareProgrammingSaga, SoftwareProgrammingSagaData> Create(SagaDataAggregate<SoftwareProgrammingSagaData> message)
        {
           return SagaInstance.New(new SoftwareProgrammingSaga(), message);
        }

        public ISagaInstance<SoftwareProgrammingSaga, SoftwareProgrammingSagaData> Create(GotTiredEvent message)
        {
            var saga = new SoftwareProgrammingSaga();
            var data = new SagaDataAggregate<SoftwareProgrammingSagaData>(message.SagaId,
                                                                          new SoftwareProgrammingSagaData(saga.Coding.Name));
            return SagaInstance.New(saga, data);
        }

        public ISagaInstance<SoftwareProgrammingSaga, SoftwareProgrammingSagaData> Create(SleptWellEvent message)
        {
            var saga = new SoftwareProgrammingSaga();
            var data = new SagaDataAggregate<SoftwareProgrammingSagaData>(message.SagaId,
                                                                          new SoftwareProgrammingSagaData(saga.Sleeping.Name));
            return SagaInstance.New(saga, data);
        }
    }

    class SoftwareProgrammingSagaFactory_with_custom_routes :
           ISagaFactory<ISagaInstance<CustomRoutesSoftwareProgrammingSaga, SoftwareProgrammingSagaData>, SagaDataAggregate<SoftwareProgrammingSagaData>>,
           ISagaFactory<ISagaInstance<CustomRoutesSoftwareProgrammingSaga, SoftwareProgrammingSagaData>, GotTiredEvent>,
           ISagaFactory<ISagaInstance<CustomRoutesSoftwareProgrammingSaga, SoftwareProgrammingSagaData>, SleptWellEvent>
    {
        public ISagaInstance<CustomRoutesSoftwareProgrammingSaga, SoftwareProgrammingSagaData> Create(SagaDataAggregate<SoftwareProgrammingSagaData> message)
        {
            return SagaInstance.New(new CustomRoutesSoftwareProgrammingSaga(), message);
        }

        public ISagaInstance<CustomRoutesSoftwareProgrammingSaga, SoftwareProgrammingSagaData> Create(GotTiredEvent message)
        {
            var saga = new CustomRoutesSoftwareProgrammingSaga();
            var data = new SagaDataAggregate<SoftwareProgrammingSagaData>(message.SagaId,
                                                                          new SoftwareProgrammingSagaData(saga.Coding.Name));
            return SagaInstance.New(saga, data);
        }

        public ISagaInstance<CustomRoutesSoftwareProgrammingSaga, SoftwareProgrammingSagaData> Create(SleptWellEvent message)
        {
            var saga = new CustomRoutesSoftwareProgrammingSaga();
            var data = new SagaDataAggregate<SoftwareProgrammingSagaData>(message.SagaId,
                                                                          new SoftwareProgrammingSagaData(saga.Sleeping.Name));
            return SagaInstance.New(saga, data);
        }
    }
}