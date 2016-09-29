using System;
using System.Threading;
using Akka.Actor;
using CommonDomain.Core;
using GridDomain.Common;
using GridDomain.CQRS.Messaging;
using GridDomain.EventSourcing;
using GridDomain.Node;
using GridDomain.Node.AkkaMessaging;
using GridDomain.Node.Configuration.Akka;
using GridDomain.Node.Configuration.Composition;
using GridDomain.Node.Configuration.Persistence;
using GridDomain.Tests.Framework.Configuration;
using GridDomain.Tools;
using GridDomain.Tools.Repositories;

namespace GridDomain.Tests.Framework
{
    public abstract class ExtendedNodeCommandTest : NodeCommandsTest
    {
        protected readonly bool InMemory;
        private static readonly AutoTestAkkaConfiguration AkkaCfg = new AutoTestAkkaConfiguration();
        protected abstract IContainerConfiguration CreateConfiguration();
        protected abstract IMessageRouteMap CreateMap();

        protected ExtendedNodeCommandTest(bool inMemory) : 
            base( inMemory ? AkkaCfg.ToStandAloneInMemorySystemConfig() : AkkaCfg.ToStandAloneSystemConfig()
                , AkkaCfg.Network.SystemName
                , !inMemory)
        {
            InMemory = inMemory;
        }

        protected override GridDomainNode CreateGridDomainNode(AkkaConfiguration akkaConf, IDbConfiguration dbConfig)
        {
            Func<ActorSystem[]> actorSystem = () => new [] { InMemory ? Sys : akkaConf.CreateSystem()};

            return new GridDomainNode(CreateConfiguration(),CreateMap(), actorSystem);
        }

        protected virtual void SaveInJournal<TAggregate>(Guid id, params DomainEvent[] messages) where TAggregate : AggregateBase
        {
            string persistId = AggregateActorName.New<TAggregate>(id).ToString();
            var persistActor = GridNode.System.ActorOf(
                Props.Create(() => new EventsRepositoryActor(persistId)), Guid.NewGuid().ToString());

            foreach (var o in messages)
                persistActor.Ask<EventsRepositoryActor.Persisted>(new EventsRepositoryActor.Persist(o)).Wait();

            //Thread.Sleep(500);
        }
    }
}