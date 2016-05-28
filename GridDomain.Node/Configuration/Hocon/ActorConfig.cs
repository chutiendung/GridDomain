using System;
using System.Linq;
using Akka.Actor;
using GridDomain.Node.Configuration;


internal class ActorConfig: IAkkaConfig
{
    private readonly string[] _seedNodes;
    private readonly int _port;
    private readonly string _name;

    private ActorConfig(int port, string name, params string[] seedNodes)
    {
        _name = name;
        _port = port;
        _seedNodes = seedNodes;
    }

    public static ActorConfig ClusterSeedNode(IAkkaNetworkAddress address, params IAkkaNetworkAddress[] otherSeeds)
    {
        var allSeeds = otherSeeds.Union(new []{ address});
        var seedNodes = allSeeds.Select(GetSeedNetworkAddress).ToArray();
        return new ActorConfig(address.PortNumber,address.Name, seedNodes);
    }

    public static ActorConfig Standalone(IAkkaNetworkAddress address)
    {
            return ClusterSeedNode(address);
    }

    public static ActorConfig ClusterNonSeedNode(string name, IAkkaNetworkAddress[] seedNodesAddresses)
    {
        var seedNodes = seedNodesAddresses.Select(GetSeedNetworkAddress).ToArray();
        return new ActorConfig(0, name, seedNodes);
    }

    public string Build()
    {
        string actorConfig = @"   
       actor {
             serializers {
                         wire = ""Akka.Serialization.WireSerializer, Akka.Serialization.Wire""
             }

             serialization-bindings {
                                    ""System.Object"" = wire
             }
             
             loggers = [""Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog""]
             debug {
                   receive = on
                   autoreceive = on
                   lifecycle = on
                   event-stream = on
                   unhandled = on
             }

       }";

        var deploy = BuildClusterProvider(_seedNodes) + BuildTransport(_name, _port);

        return actorConfig + Environment.NewLine + deploy;
    }

    private string BuildTransport(string name, int port)
    {
        string transportString = 
           @"remote {
                    helios.tcp {
                               transport -class = ""Akka.Remote.Transport.Helios.HeliosTcpTransport, Akka.Remote""
                               transport-protocol = tcp
                               port = " + port + @"
                    }
                    hostname = " + name + @"
            }";
        return transportString;
    }

    private string BuildClusterProvider(params string[] seedNodes)
    {
        string seeds = string.Join(Environment.NewLine, seedNodes.Select(n => @"""" + n + @""""));

        string clusterConfigString =
            @"
            actor.provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
            cluster {
                            seed-nodes = [" + seeds + @"]
            }";
     

        return clusterConfigString;
    }

    private static string GetSeedNetworkAddress(IAkkaNetworkAddress conf)
    {
        string networkAddress = $"akka.tcp://{conf.Name}@{conf.Host}:{conf.PortNumber}";
        return networkAddress;
    }
}