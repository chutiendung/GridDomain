using System;
using Akka.IO;
using GridDomain.EventSourcing;

namespace GridDomain.Tests.Sagas.SoftwareProgrammingDomain.Events
{
    public class SleptWellEvent: DomainEvent
    {
        public Guid SofaId { get; }
        public Guid PersonId => SourceId;

        public SleptWellEvent(Guid sourceId, Guid sofaId, Guid sagaId = new Guid(), DateTime? createdTime = null) : base(sourceId, createdTime, sagaId)
        {
            SofaId = sofaId;
        }
    }
}