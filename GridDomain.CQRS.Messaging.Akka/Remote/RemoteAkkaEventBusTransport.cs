using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;

namespace GridDomain.CQRS.Messaging.Akka.Remote
{
    public class RemoteAkkaEventBusTransport : IActorTransport
    {
        private readonly IActorTransport _local;
        private readonly IActorRef _remoteSubscriber;
        private readonly TimeSpan _timeout;
        public RemoteAkkaEventBusTransport(IActorTransport local,  IActorRef remoteSubscriber, TimeSpan timeout)
        {
            _timeout = timeout;
            _remoteSubscriber = remoteSubscriber;
            _local = local;
        }

        public void Publish(params object[] messages)
        {
            _local.Publish(messages);

            var remotePublish = new PublishMany(messages.Select(m => new Publish(m, m.GetType())).ToArray());

            _remoteSubscriber.Ask<PublishManyAck>(remotePublish, _timeout).Wait();
        }

        public void Subscribe<TMessage>(IActorRef actor)
        {
             Subscribe(typeof(TMessage),actor);
        }

        public void Unsubscribe(IActorRef actor, Type topic)
        {
            _local.Unsubscribe(actor,topic);
            _remoteSubscriber.Ask<UnsubscribeAck>(new Unsubscribe(actor, topic),_timeout).Wait();
        }

        public void Subscribe(Type messageType, IActorRef actor, IActorRef subscribeNotificationWaiter = null)
        {
            _local.Subscribe(messageType, actor, subscribeNotificationWaiter);
            _remoteSubscriber.Ask<SubscribeAck>(new Subscribe(actor, messageType,subscribeNotificationWaiter)).Wait();
        }
    }
}