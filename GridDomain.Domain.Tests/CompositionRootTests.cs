﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Akka.Actor;
using GridDomain.CQRS;
using GridDomain.Node;
using GridDomain.Node.Configuration;
using GridDomain.Node.Configuration.Persistence;
using GridDomain.Scheduling.Integration;
using GridDomain.Tests.Framework.Configuration;
using Microsoft.Practices.Unity;
using Moq;
using NUnit.Framework;

namespace GridDomain.Tests
{
    [TestFixture]
    public abstract class CompositionRootTests
    {
        [TestCase(TransportMode.Cluster)]
        [TestCase(TransportMode.Standalone)]
        public void All_base_registrations_can_be_resolved(TransportMode transportMode)
        {
            var container = CreateContainer(transportMode, new LocalDbConfiguration());
            ResolveAll(container);
        }


        [TestCase(TransportMode.Cluster)]
        [TestCase(TransportMode.Standalone)]
        public void Container_can_be_disposed(TransportMode transportMode)
        {
            var container = CreateContainer(transportMode, new LocalDbConfiguration());
            var registrations = container.Registrations.ToArray();

            foreach (var reg in registrations)
            {
                Console.WriteLine("Registration");
                Console.WriteLine(reg.Name);
                Console.WriteLine(reg.MappedToType);
                Console.WriteLine(reg.RegisteredType);
                Console.WriteLine("end of registration");
                Console.WriteLine();
            }
            
            container.Dispose();
        }


        protected abstract IUnityContainer CreateContainer(TransportMode mode, IDbConfiguration conf);

        private void ResolveAll(IUnityContainer container)
        {
            Console.WriteLine();
            var errors = new Dictionary<ContainerRegistration, Exception>();
            foreach (var reg in container.Registrations
                .Where(r => !r.RegisteredType.Name.Contains("Actor") 
                         && !r.RegisteredType.Name.Contains(nameof(ScheduledQuartzJob))))
            {
                try
                {
                    container.Resolve(reg.RegisteredType, reg.Name);
                    Console.WriteLine($"resolved {reg.RegisteredType} {reg.Name}");
                }
                catch (Exception ex)
                {
                    errors[reg] = ex;
                }
            }

            Console.WriteLine();
            if (!errors.Any()) return;

            var builder = new StringBuilder();
            foreach (var error in errors.Take(5))
            {
                builder.AppendLine($"Exception while resolving {error.Key.RegisteredType} {error.Key.Name}");
            }
            Assert.Fail("Can not resolve registrations: \r\n " + builder);
        }

        protected readonly IDictionary<TransportMode, Func<ActorSystem>> ActorSystemBuilders = new Dictionary
            <TransportMode, Func<ActorSystem>>
        {
            {TransportMode.Standalone, () => ActorSystemFactory.CreateActorSystem(new AutoTestAkkaConfiguration())},
            {
                TransportMode.Cluster,
                () => ActorSystemFactory.CreateCluster(new AutoTestAkkaConfiguration()).RandomNode()
            }
        };
    }
}