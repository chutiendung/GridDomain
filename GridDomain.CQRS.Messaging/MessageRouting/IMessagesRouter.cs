using System.Collections.Generic;
using CommonDomain.Core;
using GridDomain.EventSourcing;
using GridDomain.EventSourcing.Sagas;
using GridDomain.EventSourcing.Sagas.InstanceSagas;

namespace GridDomain.CQRS.Messaging.MessageRouting
{
    public interface IMessagesRouter
    {
        void RegisterAggregate<TAggregate, TCommandHandler>()
            where TCommandHandler : AggregateCommandsHandler<TAggregate>, new()
            where TAggregate : AggregateBase;

        void RegisterAggregate(IAggregateCommandsHandlerDesriptor descriptor);

        void RegisterSaga(ISagaDescriptor sagaDescriptor, string name = null, ExecutionPolicy policy = null);

        void RegisterHandler<TMessage, THandler>(string correlationField, ExecutionPolicy policy = null) where THandler : IHandler<TMessage>; 

        void RegisterProjectionGroup<T>(T group, ExecutionPolicy policy = null) where T : IProjectionGroup;
    }
}
