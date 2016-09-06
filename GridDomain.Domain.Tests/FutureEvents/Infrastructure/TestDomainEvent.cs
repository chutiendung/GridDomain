using GridDomain.EventSourcing;
using System;

namespace GridDomain.Tests.FutureEvents.Infrastructure
{
    public class TestDomainEvent : DomainEvent
    {
        public Guid Id { get; }
        public string Value;
        public DateTime HandledOn;
        public TestDomainEvent(string value, Guid sourceId, Guid id = default(Guid), DateTime? createdTime = default(DateTime?), Guid sagaId = default(Guid)) : base(sourceId, createdTime, sagaId)
        {
            Value = value;
            Id = id;
        }
    }
}