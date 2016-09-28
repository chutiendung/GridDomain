using System.Threading;
using GridDomain.CQRS.Messaging.Akka;
using GridDomain.Tests.CommandsExecution;
using NUnit.Framework;

namespace GridDomain.Tests
{
    [TestFixture]
    public class Given_Node_When_Start_TrasportRegistrations_Test : InMemorySampleDomainTests
    {
        [Then]
        public void Transport_contains_all_registrations()
        {
            var transport = (AkkaEventBusTransport) GridNode.Transport;
            Thread.Sleep(200);
            CollectionAssert.IsNotEmpty(transport.Subscribers);
        }
    }
}