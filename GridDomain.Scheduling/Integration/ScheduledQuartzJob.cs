using System;
using System.IO;
using Akka.Actor;
using Akka.DI.Core;
using GridDomain.Common;
using GridDomain.CQRS;
using GridDomain.CQRS.Messaging;
using GridDomain.EventSourcing;
using GridDomain.Scheduling.Akka.Messages;
using GridDomain.Scheduling.Quartz.Logging;
using Quartz;
using Wire;

namespace GridDomain.Scheduling.Integration
{
    public class ScheduledQuartzJob : IJob
    {
        private const string CommandKey = nameof(CommandKey);
        private const string EventKey = nameof(EventKey);
        private const string ScheduleKey = nameof(ScheduleKey);
        private const string ExecutionOptionsKey = nameof(ExecutionOptionsKey);

        private readonly IQuartzLogger _quartzLogger;
        private readonly ActorSystem _actorSystem;
        private readonly IPublisher _publisher;
        private readonly ICommandExecutor _executor;


        public ScheduledQuartzJob(IQuartzLogger quartzLogger,
                                  ICommandExecutor executor,
                                  IPublisher publisher)
        {
            Condition.NotNull(() => quartzLogger);
            Condition.NotNull(() => executor);
            Condition.NotNull(() => publisher);

            _publisher = publisher;
            _executor = executor;
            _quartzLogger = quartzLogger;
        }

        public void Execute(IJobExecutionContext context)
        {
            bool isFirstTimeFiring = true;
            try
            {
                isFirstTimeFiring = context.RefireCount == 0;
                var jobDataMap = context.JobDetail.JobDataMap;
                if (jobDataMap.ContainsKey(CommandKey))
                {
                    var command = GetCommand(jobDataMap);
                    var options = GetExecutionOptions(jobDataMap);
                    var key = GetScheduleKey(jobDataMap);

                    var expect = ExpectedMessage.Once(options.SuccessEventType,options.MessageIdFieldName,options.SuccessMessageId);
                    var plan = new CommandPlan(command, options.Timeout, expect);
                    var result = _executor.Execute<object>(plan);
                    _publisher.Publish(new JobCompleted(key.Name,key.Group,result));
                }
                else
                {
                    var messageToFire = GetEvent(jobDataMap);
                    _publisher.Publish(messageToFire);
                }
            }
            catch (Exception e)
            {
                _quartzLogger.LogFailure(context.JobDetail.Key.Name, e);
                var jobExecutionException = new JobExecutionException(e) { RefireImmediately = isFirstTimeFiring };
                throw jobExecutionException;
            }
        }

        public static IJobDetail Create(ScheduleKey key, Command command, ExecutionOptions executionOptions)
        {
            var serializedCommand = Serialize(command);
            var serializedKey = Serialize(key);
            var serializedOptions = Serialize(executionOptions);

            var jobDataMap = new JobDataMap
            {
                { CommandKey, serializedCommand },
                { ScheduleKey, serializedKey },
                { ExecutionOptionsKey, serializedOptions }
            };
            return CreateJob(key, jobDataMap);
        }

        public static IJobDetail Create(ScheduleKey key, DomainEvent eventToSchedule)
        {
            var serializedEvent = Serialize(eventToSchedule);
            var serializedKey = Serialize(key); var jobDataMap = new JobDataMap
            {
                { EventKey, serializedEvent },
                { ScheduleKey, serializedKey }
            };
            return CreateJob(key, jobDataMap);
        }

        private static byte[] Serialize(object source)
        {
            using (var stream = new MemoryStream())
            {
                new Serializer().Serialize(source, stream);
                return stream.ToArray();
            }
        }

        private static T Deserialize<T>(byte[] source)
        {
            using (var stream = new MemoryStream(source))
            {
                return new Serializer().Deserialize<T>(stream);
            }
        }

        private static DomainEvent GetEvent(JobDataMap jobDataMap)
        {
            var bytes = jobDataMap[EventKey] as byte[];
            return Deserialize<DomainEvent>(bytes);
        }

        private static Command GetCommand(JobDataMap jobDatMap)
        {
            var bytes = jobDatMap[CommandKey] as byte[];
            return Deserialize<Command>(bytes);
        }

        private static ScheduleKey GetScheduleKey(JobDataMap jobDatMap)
        {
            var bytes = jobDatMap[ScheduleKey] as byte[];
            return Deserialize<ScheduleKey>(bytes);
        }

        private static ExtendedExecutionOptions GetExecutionOptions(JobDataMap jobDatMap)
        {
            var bytes = jobDatMap[ExecutionOptionsKey] as byte[];
            return Deserialize<ExtendedExecutionOptions>(bytes);
        }

        private static IJobDetail CreateJob(ScheduleKey key, JobDataMap jobDataMap)
        {
            var jobKey = new JobKey(key.Name, key.Group);
            return JobBuilder
                .Create<ScheduledQuartzJob>()
                .WithIdentity(jobKey)
                .WithDescription(key.Description)
                .UsingJobData(jobDataMap)
                .RequestRecovery(true)
                .Build();
        }
    }
}