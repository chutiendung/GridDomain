using System;

namespace GridDomain.CQRS.Messaging.MessageRouting
{
    public class ExecutionPolicy
    {
        public ExecutionPolicy(bool synchronious = false, bool breakExecutionChain = false, TimeSpan? timeout = null)
        {
            this.Synchronous = synchronious;
            BreakExecutionChain = breakExecutionChain;
            Timeout = timeout ?? TimeSpan.FromSeconds(1);
        }

        public bool Synchronous { get; }
        
        public TimeSpan Timeout { get; }
        
        public bool BreakExecutionChain { get; }   

        public static ExecutionPolicy Default => new ExecutionPolicy();
        public static ExecutionPolicy Sync => new ExecutionPolicy(true,true);
    }
}