using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CommonDomain.Core;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;

namespace GridDomain.CQRS.Messaging.MessageRouting
{
    public class AggregateCommandsHandler<TAggregate> : IAggregateCommandsHandler<TAggregate>,
                                                        IAggregateCommandsHandlerDesriptor,
                                                        ICommandAggregateLocator<TAggregate>
                                                        where TAggregate : AggregateBase
    {
        private readonly IDictionary<Type, AggregateCommandHandler<TAggregate>> _commandHandlers =
                                                     new Dictionary<Type, AggregateCommandHandler<TAggregate>>();

        private readonly IUnityContainer _unityContainer;


        public AggregateCommandsHandler(IUnityContainer unityContainer = null) 
        {
            _unityContainer = unityContainer;

        }

        public TAggregate Execute(TAggregate aggregate, ICommand command)
        {
            return GetHandler(command).Execute(aggregate, command);
        }

        public Guid GetAggregateId(ICommand command)
        {
            return GetHandler(command).GetId(command);
        }

        private AggregateCommandHandler<TAggregate> GetHandler(ICommand cmd)
        {
            AggregateCommandHandler<TAggregate> aggregateCommandHandler; //
            var commandType = cmd.GetType();
            if (!_commandHandlers.TryGetValue(commandType, out aggregateCommandHandler))
                throw new CannotFindAggregateCommandHandlerExeption(typeof (TAggregate), commandType);

            return aggregateCommandHandler;
        }

        private void Map<TCommand>(AggregateCommandHandler<TAggregate> handler)
        {
            _commandHandlers[typeof (TCommand)] = handler;
        }

        public void Map<TCommand>(Expression<Func<TCommand, Guid>> idLocator,
            Action<TCommand, TAggregate> commandExecutor) where TCommand : ICommand
        {
            Map<TCommand>(AggregateCommandHandler<TAggregate>.New(idLocator, commandExecutor, _unityContainer));
        }

        protected void Map<TCommand>(Expression<Func<TCommand, Guid>> idLocator,
            Func<TCommand, TAggregate> commandExecutor) where TCommand : ICommand
        {
            Map<TCommand>(AggregateCommandHandler<TAggregate>.New(idLocator, commandExecutor, _unityContainer));
        }

        public IReadOnlyCollection<AggregateLookupInfo> RegisteredCommands
        {
            get
            {
                return _commandHandlers.Select(h => new AggregateLookupInfo(h.Key, h.Value.MachingProperty)).ToArray();
            }
        }

        public Type AggregateType => typeof(TAggregate);
    }
}