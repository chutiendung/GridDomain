using System;
using GridDomain.CQRS;

namespace GridDomain.Tests.SampleDomain.Commands
{
    public class AsyncMethodCommand : Command
    {
        public AsyncMethodCommand(int parameter, Guid aggregateId, Guid sagaId = default(Guid),TimeSpan? sleepTime = null):base(Guid.NewGuid(),sagaId)
        {
            Parameter = parameter;
            AggregateId = aggregateId;
            SleepTime = sleepTime??TimeSpan.FromMilliseconds(50);
        }

        public Guid AggregateId { get; }
        public TimeSpan SleepTime { get; }
        public int Parameter { get; }
    }
}