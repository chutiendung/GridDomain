using System;
using System.Threading.Tasks;

namespace GridDomain.Node.AkkaMessaging.Waiting
{
    public class ExpectBuilder
    {
        private readonly AkkaMessageLocalWaiter _waiter;

        public ExpectBuilder(AkkaMessageLocalWaiter waiter)
        {
            _waiter = waiter;
        }

        public Task<IWaitResults> Within(TimeSpan timeout)
        {
            return _waiter.Start(timeout);
        }

 
        public ExpectBuilder And<TMsg>(Predicate<TMsg> filter = null)
        {
            filter = filter ?? (t => true);
            _waiter.Subscribe(oldPredicate => (c => oldPredicate(c) && _waiter.WasReceived(filter)), filter);
            return this;
        }
        public ExpectBuilder Or<TMsg>(Predicate<TMsg> filter = null)
        {
            filter = filter ?? (t => true);
            _waiter.Subscribe(oldPredicate => (c => oldPredicate(c) || _waiter.WasReceived(filter)), filter);
            return this;
        }

   
    }
}