namespace GridDomain.Node
{
    public interface IAppInsightsConfiguration
    {
        string Key { get; }
        bool IsEnabled { get; }
    }
}