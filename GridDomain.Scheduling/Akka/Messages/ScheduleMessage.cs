using System;
using GridDomain.EventSourcing;

namespace GridDomain.Scheduling.Akka.Messages
{
    public class ScheduleMessage
    {
        public DomainEvent Event { get; }
        public ScheduleKey Key { get; }
        public DateTime RunAt { get; }

        public ScheduleMessage(DomainEvent eventToSchedule, ScheduleKey key, DateTime runAt)
        {
            Event = eventToSchedule;
            Key = key;
            RunAt = runAt;
        }
    }
}