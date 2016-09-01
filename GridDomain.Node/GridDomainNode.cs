﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Akka;
using Akka.Actor;
using Akka.DI.Core;
using Akka.DI.Unity;
using Akka.Monitoring;
using Akka.Monitoring.ApplicationInsights;
using Akka.Monitoring.PerformanceCounters;
using GridDomain.Common;
using GridDomain.CQRS;
using GridDomain.CQRS.Messaging;
using GridDomain.CQRS.Messaging.Akka;
using GridDomain.EventSourcing.DomainEventAdapters;
using GridDomain.EventSourcing.VersionedTypeSerialization;
using GridDomain.Logging;
using GridDomain.Node.Actors;
using GridDomain.Node.AkkaMessaging.Routing;
using GridDomain.Node.AkkaMessaging.Waiting;
using GridDomain.Node.Configuration.Composition;
using GridDomain.Node.Configuration.Persistence;
using GridDomain.Scheduling.Akka.Messages;
using GridDomain.Scheduling.Integration;
using GridDomain.Scheduling.Quartz;
using Microsoft.Practices.Unity;
using IUnityContainer = Microsoft.Practices.Unity.IUnityContainer;

namespace GridDomain.Node
{
    public class GridDomainNode : IGridDomainNode
    {
        private static readonly IDictionary<TransportMode, Type> RoutingActorType = new Dictionary
            <TransportMode, Type>
        {
            {TransportMode.Standalone, typeof (LocalSystemRoutingActor)},
            {TransportMode.Cluster, typeof (ClusterSystemRouterActor)}
        };

        private readonly ISoloLogger _log = LogManager.GetLogger();
        private readonly IMessageRouteMap _messageRouting;
        private TransportMode _transportMode;
        public ActorSystem[] Systems;

        private readonly TimeSpan _commandTimeout;
        public static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(30);

        private Quartz.IScheduler _quartzScheduler;
       
        private IActorRef _nodeController;
        private readonly IContainerConfiguration _configuration;
        private readonly IQuartzConfig _quartzConfig;
        private readonly Func<ActorSystem[]> _actorSystemFactory;


        public IActorTransport Transport { get; private set; }

        public ActorSystem System;

        [Obsolete("Use constructor with ActorSystem factory instead")]
        public GridDomainNode(IUnityContainer container,
                              IMessageRouteMap messageRouting,
                              IQuartzConfig quartzConfig = null,
                              params ActorSystem[] actorAllSystems)
            : this(new EmptyContainerConfig(),messageRouting, () => actorAllSystems, quartzConfig)
        {
            Container = container;
        }
   
        [Obsolete("Use constructor with ActorSystem factory instead")]
        public GridDomainNode(IUnityContainer container,
                              IMessageRouteMap messageRouting,
                              TransportMode transportMode,
                              params ActorSystem[] actorAllSystems)
           : this(container, messageRouting, null, actorAllSystems)
        {
        }

        [Obsolete("Use constructor with ActorSystem factory instead")]
        public GridDomainNode(IContainerConfiguration configuration,
                              IMessageRouteMap messageRouting,
                              TransportMode transportMode,
                              params ActorSystem[] actorAllSystems)
            :this(configuration, messageRouting, () => actorAllSystems, null)
        {
        }


        public GridDomainNode(IContainerConfiguration configuration,
                            IMessageRouteMap messageRouting,
                            Func<ActorSystem> actorSystemFactory) : this(configuration, messageRouting, () => new [] { actorSystemFactory()}, null)
        {
        }
        public GridDomainNode(IContainerConfiguration configuration,
                              IMessageRouteMap messageRouting,
                              Func<ActorSystem[]> actorSystemFactory) : this(configuration, messageRouting,actorSystemFactory,null)
        {
        }

        public GridDomainNode(IContainerConfiguration configuration,
                              IMessageRouteMap messageRouting,
                              Func<ActorSystem[]> actorSystemFactory,
                              IQuartzConfig quartzConfig,
                              TimeSpan? commandTimeout = null)
        {
            _actorSystemFactory = actorSystemFactory;
            _quartzConfig = quartzConfig ?? new InMemoryQuartzConfig();
            _configuration = configuration;
            _messageRouting = new CompositeRouteMap(messageRouting, 
                                                    //new SchedulingRouteMap(),
                                                    new TransportMessageDumpMap()
                                                    );
            _commandTimeout = commandTimeout ?? DefaultCommandTimeout;
            Container = new UnityContainer();
        }

        private void OnSystemTermination()
        {
            _log.Debug("grid node Actor system terminated");
        }
        private void OnSystemTermination(Task obj)
        {
            _log.Debug("grid node Actor system terminated");
        }

        public IUnityContainer Container { get; }

        public Guid Id { get; } = Guid.NewGuid();

        public void Start(IDbConfiguration databaseConfiguration)
        {
            Systems = _actorSystemFactory.Invoke();
            _transportMode = Systems.Length > 1 ? TransportMode.Cluster : TransportMode.Standalone;
            System = Systems.First();
            System.WhenTerminated.ContinueWith(OnSystemTermination);
            System.RegisterOnTermination(OnSystemTermination);
            System.AddDependencyResolver(new UnityDependencyResolver(Container, System));

            ConfigureContainer(Container, databaseConfiguration, _quartzConfig, System);

            var appInsightsConfig = Container.Resolve<IAppInsightsConfiguration>();
            var perfCountersConfig = Container.Resolve<IPerformanceCountersConfiguration>();

            if (appInsightsConfig.IsEnabled)
            {
                var monitor = new ActorAppInsightsMonitor(appInsightsConfig.Key);
                ActorMonitoringExtension.RegisterMonitor(System, monitor);
            }
            if (perfCountersConfig.IsEnabled)
            {
                ActorMonitoringExtension.RegisterMonitor(System, new ActorPerformanceCountersMonitor());
            }

            StartController(System);

            

            Transport = Container.Resolve<IActorTransport>();
            _quartzScheduler = Container.Resolve<Quartz.IScheduler>();
        }

        private void ConfigureContainer(IUnityContainer unityContainer,
                                        IDbConfiguration databaseConfiguration, 
                                        IQuartzConfig quartzConfig, 
                                        ActorSystem actorSystem)
        {
            unityContainer.Register(new GridNodeContainerConfiguration(actorSystem,
                                                                       _transportMode,
                                                                       quartzConfig));

            var persistentScheduler = System.ActorOf(System.DI().Props<AdvancedSchedulingActor>(),nameof(AdvancedSchedulingActor));
            unityContainer.RegisterInstance(new TypedMessageActor<ScheduleMessage>(persistentScheduler));
            unityContainer.RegisterInstance(new TypedMessageActor<ScheduleCommand>(persistentScheduler));
            unityContainer.RegisterInstance(new TypedMessageActor<Unschedule>(persistentScheduler));
            unityContainer.RegisterInstance(_messageRouting);

            _configuration.Register(unityContainer);
        }

        bool _stopping = false;
        private CommandExecutor _commandExecutor;

        public EventAdaptersCatalog EventAdaptersCatalog { get; } = AkkaDomainEventsAdapter.UpgradeChain;

        public void Stop()
        {
            if (_stopping) return;
            _stopping = true;

            _quartzScheduler.Shutdown(false);
            System.Terminate();
            System.Dispose();
            _log.Info("GridDomain node {Id} stopped",Id);
        }

        private void StartController(ActorSystem actorSystem)
        {
            _stopping = false;
            _log.Info("Launching GridDomain node {Id}",Id);

            var props = actorSystem.DI().Props<GridNodeController>();
            _nodeController = actorSystem.ActorOf(props,nameof(GridNodeController));

            _nodeController.Ask(new GridNodeController.Start
            {
                RoutingActorType = RoutingActorType[_transportMode]
            })
            .Wait(TimeSpan.FromSeconds(2));

            _log.Info("GridDomain node {Id} started at home {Home}", Id, actorSystem.Settings.Home);

            _commandExecutor = CommandExecutor.New(System,_commandTimeout);
            //TODO: gather all container registrations to one place in gridNode
            Container.RegisterInstance<ICommandExecutor>(_commandExecutor);
        }

        public void Execute(params ICommand[] commands)
        {
            _commandExecutor.Execute(commands);
        }

        public Task<object> Execute(ICommand command, ExpectedMessage[] expectedMessage, TimeSpan? timeout = null)
        {
            return _commandExecutor.Execute(command, expectedMessage, timeout);
        }
    }
}