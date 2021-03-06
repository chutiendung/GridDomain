using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Akka;
using Akka.Actor;
using GridDomain.Common;
using GridDomain.CQRS;
using GridDomain.CQRS.Messaging.Akka;

namespace GridDomain.Node.AkkaMessaging.Waiting
{
    public abstract class LocalMessagesWaiter<T> : IMessageWaiter<T>
    {
        private readonly IActorSubscriber _subscriber;
        private readonly ConcurrentBag<object> _ignoredMessages = new ConcurrentBag<object>();
        private readonly ConcurrentBag<object> _allExpectedMessages = new ConcurrentBag<object>();

        private readonly List<Func<object,bool>> _filters = new List<Func<object, bool>>();
        private Func<IEnumerable<object>,bool> _stopCondition;

        private readonly TimeSpan _defaultTimeout;
        public abstract ExpectBuilder<T> ExpectBuilder { get; }
        private readonly List<Type> _messageTypesToSubscribe = new List<Type>();
        private readonly ActorSystem _system;

        public LocalMessagesWaiter(ActorSystem system, IActorSubscriber subscriber, TimeSpan defaultTimeout)
        {
            _system = system;
            _defaultTimeout = defaultTimeout;
            _subscriber = subscriber;
        }

        internal void Subscribe(Type type, 
                                Func<object,bool> filter,
                                Func<IEnumerable<object>, bool> stopCondition)
        {
            _filters.Add(filter);
            _stopCondition = stopCondition;
            _messageTypesToSubscribe.Add(type);
        }

        public IExpectBuilder<T> Expect<TMsg>(Predicate<TMsg> filter = null)
        {
            return ExpectBuilder.And(filter);
        }
        public IExpectBuilder<T> Expect(Type type, Func<object,bool> filter = null)
        {
            return ExpectBuilder.And(type,filter ?? (o => true));
        }

        public async Task<IWaitResults> Start(TimeSpan? timeout = null)
        {
            using (var inbox = Inbox.Create(_system))
            {
                foreach(var type in _messageTypesToSubscribe)
                _subscriber.Subscribe(type, inbox.Receiver);

                var finalTimeout = timeout ?? _defaultTimeout;

                await WaitForMessages(inbox, finalTimeout)
                               .TimeoutAfter(finalTimeout);

                foreach (var type in _messageTypesToSubscribe)
                    _subscriber.Unsubscribe(inbox.Receiver,type);

                return new WaitResults(_allExpectedMessages);
            }
        }

        private async Task WaitForMessages(Inbox inbox, TimeSpan timeoutPerMessage)
        {
            while (!IsAllExpectedMessagedReceived())
            {
                var message = await inbox.ReceiveAsync(timeoutPerMessage).ConfigureAwait(false);
                CheckExecutionError(message);

                if (IsExpected(message)) _allExpectedMessages.Add(message);
                else _ignoredMessages.Add(message);
            }
        }

        private bool IsAllExpectedMessagedReceived()
        {
            return _stopCondition(_allExpectedMessages);
        }

        private bool IsExpected(object message)
        {
            return _filters.Any(f => f(message));
        }

        private static void CheckExecutionError(object t)
        {
            t.Match()
             .With<Status.Failure>(r => ExceptionDispatchInfo.Capture(r.Cause).Throw())
             .With<Failure>(r => ExceptionDispatchInfo.Capture(r.Exception).Throw());
        }
    }
}