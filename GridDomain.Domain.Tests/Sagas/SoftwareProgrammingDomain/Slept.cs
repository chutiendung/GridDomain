using System;
using GridDomain.EventSourcing;

namespace GridDomain.Tests.Sagas.SoftwareProgrammingDomain
{
    public class Slept : DomainEvent
    {
        public Guid SofaId { get; }

        public Slept(Guid sofaId):base(sofaId)
        {
            SofaId = sofaId;
        }
    }
}