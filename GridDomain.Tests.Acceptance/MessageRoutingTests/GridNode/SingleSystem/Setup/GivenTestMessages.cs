using System;

namespace GridDomain.Tests.Acceptance.MessageRoutingTests
{
    class GivenTestMessages : IGivenCommands<TestMessage>
    {
        public TestMessage[] GetCommands()
        {
            var guid = Guid.NewGuid();
            int count = 0;

            var commands = new[]
            {
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count},
                new TestMessage() {CorrelationId = guid, ExecuteOrder = ++count}
            };
            return commands;
        }
    }
}