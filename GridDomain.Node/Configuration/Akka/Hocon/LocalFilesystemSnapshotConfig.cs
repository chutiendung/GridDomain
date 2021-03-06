namespace GridDomain.Node.Configuration.Akka.Hocon
{
    internal class LocalFilesystemSnapshotConfig : IAkkaConfig
    {
        public string Build()
        {
            return @" 
             snapshot-store {
                            plugin = ""akka.persistence.snapshot-store.local""
                            local {
                                    class = ""Akka.Persistence.Snapshot.LocalSnapshotStore, Akka.Persistence""
                                    plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                                    stream-dispatcher = ""akka.persistence.dispatchers.default-stream-dispatcher""
                                    dir = LocalSnapshots
                            }
                }";
        }
    }
}