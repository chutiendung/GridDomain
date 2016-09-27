using System;
using Akka.Actor;
using Akka.TestKit.NUnit3;
using GridDomain.Node;
using GridDomain.Node.Configuration;
using GridDomain.Node.Configuration.Akka;
using GridDomain.Tests.Framework.Configuration;
using NUnit.Framework;

namespace GridDomain.Tests.Acceptance.Persistence
{
    [TestFixture]
    public class Sql_journal_availability_by_persistent_actor// : TestKit
    {
        private readonly AutoTestAkkaConfiguration _conf =
            new AutoTestAkkaConfiguration(AkkaConfiguration.LogVerbosity.Warning);

        private void CheckPersist(IActorRef actor)
        {
            var sqlJournalPing = new SqlJournalPing {Payload = "testPayload"};
            var ack = actor.Ask<Persisted>(sqlJournalPing, TimeSpan.FromSeconds(50)).Result;
            Assert.AreEqual(sqlJournalPing.Payload, ack.Payload);
        }


        [Test]
        public void Sql_journal_is_available_for_akka_cluster_config()
        {
            var actorSystem = ActorSystem.Create(_conf.Network.SystemName, _conf.ToClusterSeedNodeSystemConfig());
            var actor = actorSystem.ActorOf(Props.Create(() => new SqlJournalPingActor("testA")));
            CheckPersist(actor);
        }

        [Test]
        public void Sql_journal_is_available_for_akka_standalone_config()
        {
            var actorSystem = ActorSystem.Create(_conf.Network.SystemName, _conf.ToStandAloneSystemConfig());
            var actor = actorSystem.ActorOf(Props.Create(() => new SqlJournalPingActor("testB")));
            CheckPersist(actor);
        }


        [Test]
        public void Sql_journal_is_available_for_factored_akka_cluster()
        {
            var actorSystem = ActorSystemFactory.CreateCluster(_conf, 2, 2).RandomNode();
            var actor = actorSystem.ActorOf(Props.Create(() => new SqlJournalPingActor("testC")));
            CheckPersist(actor);
        }

        [Test]
        public void Sql_journal_is_available_for_factored_standalone_akka_system()
        {
            var actorSystem = _conf.CreateSystem();
            var actor = actorSystem.ActorOf(Props.Create(() => new SqlJournalPingActor("testD")));
            CheckPersist(actor);
        }
    }
}