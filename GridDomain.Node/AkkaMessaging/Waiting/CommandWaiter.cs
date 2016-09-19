using System;
using System.Collections.Generic;
using System.Linq;
using Akka;
using Akka.Actor;
using GridDomain.CQRS;
using GridDomain.Node.Actors;

namespace GridDomain.Node.AkkaMessaging.Waiting
{
    public class CommandWaiter : MessageWaiter<ExpectedMessage>
    {
        private readonly ICommand _command;

        public CommandWaiter(IActorRef notifyActor, ICommand command, params ExpectedMessage[] expectedMessage) : base(notifyActor, expectedMessage)
        {
            _command = command;
        }

        //execution stops on first expected fault
        protected override bool WaitIsOver(object message,ExpectedMessage expect)
        {
            return IsExpectedFault(message, expect)
                 //message faults are not counted while waiting for messages
                 || MessageReceivedCounters.All(c => !(typeof(IFault).IsAssignableFrom(c.Key) && c.Value == 0));
        }

        //message is fault that caller wish to know about
        //if no special processor type of fault is specified, we will stop on any fault
        private bool IsExpectedFault(object message, ExpectedMessage expect)
        {
            var fault = message as IFault;
            return fault != null && (expect.Source == null || expect.Source == fault.Processor);
        }

        protected override object BuildAnswerMessage(object message)
        {
            object answerMessage = null;
            message.Match()
                   .With<IFault>(f => answerMessage = f)
                   .Default(m =>
                   {
                       answerMessage = new CommandExecutionFinished(_command, m);
                   });

            return answerMessage;
        }
    }
}