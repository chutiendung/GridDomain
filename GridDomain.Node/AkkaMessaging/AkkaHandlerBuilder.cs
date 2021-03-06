using GridDomain.CQRS;
using GridDomain.CQRS.Messaging.MessageRouting;

namespace GridDomain.Node.AkkaMessaging
{
    public class AkkaHandlerBuilder<TMessage, THandler> :
        IHandlerBuilder<TMessage, THandler> where THandler : IHandler<TMessage>
    {
        private readonly IHandler<CreateHandlerRouteMessage> _routingRegistrator;
        public string CorrelationPropertyName;

        public AkkaHandlerBuilder(IHandler<CreateHandlerRouteMessage> routingRegistrator)
        {
            _routingRegistrator = routingRegistrator;
        }

        public void Register()
        {
            _routingRegistrator.Handle(CreateHandlerRouteMessage.New<TMessage, THandler>(CorrelationPropertyName));
        }

        public IHandlerBuilder<TMessage, THandler> WithCorrelation(string name)
        {
            CorrelationPropertyName = name;
            return this;
        }
    }
}