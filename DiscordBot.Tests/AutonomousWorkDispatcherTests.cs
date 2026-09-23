using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DiscordBot.Jobs;
using MandarinBotNet.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Quartz;

namespace DiscordBot.Tests;

[TestFixture]
public sealed class AutonomousWorkDispatcherTests
{
    [Test]
    public void Defaults_AreSafeAndDisabled()
    {
        var options = new AutonomousWorkDispatcherOptions();

        options.Enabled.Should().BeFalse();
        options.Cron.Should().Be("0 0/10 * * * ?");
        options.Owner.Should().Be("AlexBDevCorner");
        options.Repository.Should().Be("AutonomousWork");
        options.Workflow.Should().Be("dispatch.yml");
        options.Ref.Should().Be("master");
        options.Token.Should().BeEmpty();
    }

    [Test]
    public void MandarinBotDefaults_DispatcherIsDisabledWithoutToken()
    {
        var options = new MandarinBotOptions();

        options.AutonomousWorkDispatcher.Enabled.Should().BeFalse();
        options.AutonomousWorkDispatcher.Cron.Should()
            .Be(AutonomousWorkDispatcherOptions.DefaultCron);
        options.AutonomousWorkDispatcher.Owner.Should()
            .Be(AutonomousWorkDispatcherOptions.DefaultOwner);
        options.AutonomousWorkDispatcher.Repository.Should()
            .Be(AutonomousWorkDispatcherOptions.DefaultRepository);
        options.AutonomousWorkDispatcher.Workflow.Should()
            .Be(AutonomousWorkDispatcherOptions.DefaultWorkflow);
        options.AutonomousWorkDispatcher.Ref.Should()
            .Be(AutonomousWorkDispatcherOptions.DefaultRef);
        options.AutonomousWorkDispatcher.Token.Should().BeEmpty();
    }

    [Test]
    public void Validate_DisabledWithoutToken_ReturnsSuccess()
    {
        var options = CreateValidOptions();
        options = new MandarinBotOptions
        {
            Discord = options.Discord,
            FantasyPremierLeague = options.FantasyPremierLeague,
            Schedules = options.Schedules,
            Notifications = options.Notifications,
            WelcomeMessages = options.WelcomeMessages,
            AutonomousWorkDispatcher = new AutonomousWorkDispatcherOptions
            {
                Enabled = false,
                Cron = "0 0/10 * * * ?",
                Owner = string.Empty,
                Repository = string.Empty,
                Workflow = string.Empty,
                Ref = string.Empty,
                Token = string.Empty
            }
        };
        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public void Validate_DisabledInvalidCron_ReturnsFailure()
    {
        var options = CreateValidOptions(
            dispatcher: new AutonomousWorkDispatcherOptions
            {
                Enabled = false,
                Cron = "not-a-cron",
                Token = string.Empty
            });
        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("Bot:AutonomousWorkDispatcher:Cron"));
    }

    [Test]
    public void Validate_EnabledWithAllValues_ReturnsSuccess()
    {
        var options = CreateValidOptions(
            dispatcher: new AutonomousWorkDispatcherOptions
            {
                Enabled = true,
                Cron = "0 0/10 * * * ?",
                Owner = "AlexBDevCorner",
                Repository = "AutonomousWork",
                Workflow = "dispatch.yml",
                Ref = "master",
                Token = "secret-token"
            });
        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public void Validate_EnabledWithoutToken_ReturnsFailure()
    {
        var options = CreateValidOptions(
            dispatcher: new AutonomousWorkDispatcherOptions
            {
                Enabled = true,
                Cron = "0 0/10 * * * ?",
                Owner = "AlexBDevCorner",
                Repository = "AutonomousWork",
                Workflow = "dispatch.yml",
                Ref = "master",
                Token = string.Empty
            });
        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("Bot:AutonomousWorkDispatcher:Token"));
    }

    [TestCase("", "AutonomousWork", "dispatch.yml", "master", "Bot:AutonomousWorkDispatcher:Owner")]
    [TestCase("AlexBDevCorner", "", "dispatch.yml", "master", "Bot:AutonomousWorkDispatcher:Repository")]
    [TestCase("AlexBDevCorner", "AutonomousWork", "", "master", "Bot:AutonomousWorkDispatcher:Workflow")]
    [TestCase("AlexBDevCorner", "AutonomousWork", "dispatch.yml", "", "Bot:AutonomousWorkDispatcher:Ref")]
    public void Validate_EnabledMissingField_ReturnsFailure(
        string owner,
        string repository,
        string workflow,
        string gitRef,
        string expectedPath)
    {
        var options = CreateValidOptions(
            dispatcher: new AutonomousWorkDispatcherOptions
            {
                Enabled = true,
                Cron = "0 0/10 * * * ?",
                Owner = owner,
                Repository = repository,
                Workflow = workflow,
                Ref = gitRef,
                Token = "secret-token"
            });
        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(expectedPath));
    }

    [Test]
    public void Validate_EnabledInvalidCron_ReturnsFailure()
    {
        var options = CreateValidOptions(
            dispatcher: new AutonomousWorkDispatcherOptions
            {
                Enabled = true,
                Cron = "bad",
                Owner = "AlexBDevCorner",
                Repository = "AutonomousWork",
                Workflow = "dispatch.yml",
                Ref = "master",
                Token = "secret-token"
            });
        var validator = new MandarinBotOptionsValidator(requireOperationalConfiguration: false);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("Bot:AutonomousWorkDispatcher:Cron"));
    }

    [Test]
    public void Bind_EnvironmentStyleKeys_PreservesDispatcherSettingsIncludingToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Bot:AutonomousWorkDispatcher:Enabled"] = "true",
                ["Bot:AutonomousWorkDispatcher:Cron"] = "0 0/10 * * * ?",
                ["Bot:AutonomousWorkDispatcher:Owner"] = "AlexBDevCorner",
                ["Bot:AutonomousWorkDispatcher:Repository"] = "AutonomousWork",
                ["Bot:AutonomousWorkDispatcher:Workflow"] = "dispatch.yml",
                ["Bot:AutonomousWorkDispatcher:Ref"] = "master",
                ["Bot:AutonomousWorkDispatcher:Token"] = "runtime-secret"
            })
            .Build();

        var options = configuration
            .GetSection(MandarinBotOptions.SectionName)
            .Get<MandarinBotOptions>();

        options.Should().NotBeNull();
        options!.AutonomousWorkDispatcher.Enabled.Should().BeTrue();
        options.AutonomousWorkDispatcher.Cron.Should().Be("0 0/10 * * * ?");
        options.AutonomousWorkDispatcher.Token.Should().Be("runtime-secret");
    }

    [Test]
    public void CreateTrigger_DefaultCron_RunsEveryTenMinutesInConfiguredTimeZone()
    {
        var dispatcher = new AutonomousWorkDispatcherOptions
        {
            Enabled = true,
            Cron = "0 0/10 * * * ?"
        };

        var trigger = JobSchedules.CreateAutonomousWorkDispatcherTrigger(dispatcher, "Europe/Riga");

        var cronTrigger = trigger.Should().BeAssignableTo<ICronTrigger>().Subject;
        cronTrigger.CronExpressionString.Should().Be("0 0/10 * * * ?");
        cronTrigger.TimeZone.Should().Be(JobSchedules.GetTimeZone(new JobSchedulesOptions { TimeZoneId = "Europe/Riga" }));
        trigger.MisfireInstruction.Should().Be(MisfireInstruction.CronTrigger.DoNothing);
        trigger.JobKey.Should().Be(JobSchedules.AutonomousWorkDispatcherJobKey);
        trigger.Key.Name.Should().Be(JobSchedules.AutonomousWorkDispatcherTriggerName);
    }

    [Test]
    public void CreateTrigger_EveryTenMinutes_FiresSixTimesPerHour()
    {
        var dispatcher = new AutonomousWorkDispatcherOptions
        {
            Enabled = true,
            Cron = "0 0/10 * * * ?"
        };
        var trigger = JobSchedules.CreateAutonomousWorkDispatcherTrigger(dispatcher, "Europe/Riga");
        var reference = new DateTimeOffset(2027, 1, 15, 10, 0, 0, TimeSpan.Zero);

        var fireTimes = new List<DateTimeOffset>();
        var next = trigger.GetFireTimeAfter(reference);
        while (next is not null && fireTimes.Count < 6)
        {
            fireTimes.Add(next.Value);
            next = trigger.GetFireTimeAfter(next.Value);
        }

        fireTimes.Should().HaveCount(6);
        var minutes = fireTimes
            .Select(t => TimeZoneInfo.ConvertTime(t, JobSchedules.GetTimeZone(new JobSchedulesOptions { TimeZoneId = "Europe/Riga" })).Minute)
            .ToArray();
        minutes.Should().BeEquivalentTo([10, 20, 30, 40, 50, 0], o => o.WithStrictOrdering());
    }

    [Test]
    public void JobType_DispatcherJob_DisallowsConcurrentExecution()
    {
        var attribute = Attribute.GetCustomAttribute(
            typeof(AutonomousWorkDispatcherJob),
            typeof(DisallowConcurrentExecutionAttribute));

        attribute.Should().NotBeNull();
    }

    [Test]
    public void JobType_DispatcherJob_DoesNotDependOnDiscordConnectivity()
    {
        var constructors = typeof(AutonomousWorkDispatcherJob).GetConstructors();

        constructors.Should().ContainSingle();
        var parameterTypes = constructors[0].GetParameters().Select(p => p.ParameterType).ToArray();
        parameterTypes.Should().Contain(t => t == typeof(IAutonomousWorkDispatcherClient));
        parameterTypes.Should().NotContain(t => t.FullName != null && t.FullName.Contains("DiscordConnection", StringComparison.Ordinal));
        parameterTypes.Should().NotContain(t => t.FullName != null && t.FullName.Contains("Notification", StringComparison.Ordinal));
        parameterTypes.Should().NotContain(t => t == typeof(DiscordOptions));
        parameterTypes.Should().NotContain(t => t == typeof(FantasyPremierLeagueOptions));
    }

    [Test]
    public async Task TriggerAsync_Success_SendsExpectedEndpointPayloadAndHeaders()
    {
        const string token = "dispatcher-secret-token";
        var options = new AutonomousWorkDispatcherOptions
        {
            Enabled = true,
            Cron = "0 0/10 * * * ?",
            Owner = "AlexBDevCorner",
            Repository = "AutonomousWork",
            Workflow = "dispatch.yml",
            Ref = "master",
            Token = token
        };
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new CapturingHandler((request, ct) =>
        {
            captured = request;
            return request.Content!.ReadAsStringAsync(ct).ContinueWith(t =>
            {
                body = t.Result;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }, ct);
        });
        var client = CreateClient(handler, options);

        await client.TriggerDispatcherAsync(CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.PathAndQuery.Should().Be("/repos/AlexBDevCorner/AutonomousWork/actions/workflows/dispatch.yml/dispatches");
        var payload = JsonDocument.Parse(body!);
        payload.RootElement.GetProperty("ref").GetString().Should().Be("master");

        captured.Headers.Authorization.Should().NotBeNull();
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be(token);
        captured.Headers.Accept.Should().ContainSingle(h => h.MediaType == "application/vnd.github+json");
        captured.Headers.Should().Contain(h => h.Key == "X-GitHub-Api-Version");
        captured.Headers.GetValues("X-GitHub-Api-Version").Should().ContainSingle().Which.Should().Be("2022-11-28");
        captured.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [TestCase(HttpStatusCode.OK)]
    [TestCase(HttpStatusCode.Created)]
    [TestCase(HttpStatusCode.Accepted)]
    [TestCase(HttpStatusCode.NoContent)]
    public async Task TriggerAsync_AnySuccessStatus_TreatsAsSuccess(HttpStatusCode status)
    {
        var options = EnabledOptions();
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        var client = CreateClient(handler, options);

        Func<Task> act = () => client.TriggerDispatcherAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task TriggerAsync_NonSuccess_ThrowsWithoutTokenInMessage()
    {
        const string token = "super-secret-dispatcher-token-123";
        var options = EnabledOptions(token);
        var handler = new CapturingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("""{"message":"validation failed"}""", Encoding.UTF8, "application/json")
            }));
        var client = CreateClient(handler, options, new RecordingLogger<AutonomousWorkDispatcherClient>());

        Func<Task> act = () => client.TriggerDispatcherAsync(CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AutonomousWorkDispatcherException>();
        exception.Which.Message.Should().NotContain(token);
        exception.Which.Message.Should().Contain("AlexBDevCorner");
        exception.Which.Message.Should().Contain("422");
        exception.Which.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task TriggerAsync_TransportFailure_ThrowsWithoutTokenInMessage()
    {
        const string token = "another-secret-token-456";
        var options = EnabledOptions(token);
        var handler = new CapturingHandler((_, _) => throw new HttpRequestException("boom"));
        var client = CreateClient(handler, options);

        Func<Task> act = () => client.TriggerDispatcherAsync(CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AutonomousWorkDispatcherException>();
        exception.Which.Message.Should().NotContain(token);
    }

    [Test]
    public async Task TriggerAsync_NonSuccess_DoesNotLogToken()
    {
        const string token = "log-redaction-check-token";
        var options = EnabledOptions(token);
        var logger = new RecordingLogger<AutonomousWorkDispatcherClient>();
        var handler = new CapturingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("forbidden", Encoding.UTF8, "text/plain")
            }));
        var client = CreateClient(handler, options, logger);

        Func<Task> act = () => client.TriggerDispatcherAsync(CancellationToken.None);
        await act.Should().ThrowAsync<AutonomousWorkDispatcherException>();

        logger.Entries.Should().NotBeEmpty();
        foreach (var entry in logger.Entries)
        {
            entry.Message.Should().NotContain(token);
            if (entry.Exception is not null)
            {
                entry.Exception.ToString().Should().NotContain(token);
            }

            foreach (var value in entry.Properties.Values)
            {
                value?.ToString().Should().NotContain(token);
            }
        }
    }

    [Test]
    public async Task JobExecute_Success_CallsClientOnce()
    {
        var dispatcher = new TestDispatcherClient();
        var job = new AutonomousWorkDispatcherJob(
            dispatcher,
            TimeProvider.System,
            new RecordingLogger<AutonomousWorkDispatcherJob>());
        var context = CreateJobContext<AutonomousWorkDispatcherJob>("dispatcher-success");

        await job.Execute(context);

        dispatcher.CallCount.Should().Be(1);
    }

    [Test]
    public async Task JobExecute_ClientFailure_PropagatesToFailExecution()
    {
        var dispatcher = new TestDispatcherClient
        {
            Throw = new AutonomousWorkDispatcherException("GitHub returned 500.")
        };
        var job = new AutonomousWorkDispatcherJob(
            dispatcher,
            TimeProvider.System,
            new RecordingLogger<AutonomousWorkDispatcherJob>());
        var context = CreateJobContext<AutonomousWorkDispatcherJob>("dispatcher-failure");

        Func<Task> act = () => job.Execute(context);

        await act.Should().ThrowAsync<AutonomousWorkDispatcherException>();
        dispatcher.CallCount.Should().Be(1);
    }

    [Test]
    public void AddDispatcherClient_RegistersTypedHttpClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(EnabledOptions());

        services.AddAutonomousWorkDispatcherClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetService<IAutonomousWorkDispatcherClient>();
        client.Should().NotBeNull();
    }

    [Test]
    public void Scheduling_Enabled_RegistersDispatcherJobWithTenMinuteCron()
    {
        var botOptions = DisabledSchedulesOptions(EnabledOptions());
        using var provider = CreateSchedulingProvider(botOptions);
        var quartzOptions = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        quartzOptions.JobDetails.Should().ContainSingle(j => j.Key.Equals(JobSchedules.AutonomousWorkDispatcherJobKey));
        var trigger = quartzOptions.Triggers.Should().ContainSingle(t => t.JobKey.Equals(JobSchedules.AutonomousWorkDispatcherJobKey)).Subject;
        var cronTrigger = trigger.Should().BeAssignableTo<ICronTrigger>().Subject;
        cronTrigger.CronExpressionString.Should().Be("0 0/10 * * * ?");
    }

    [Test]
    public void Scheduling_Disabled_DoesNotRegisterDispatcherJob()
    {
        var botOptions = DisabledSchedulesOptions(new AutonomousWorkDispatcherOptions { Enabled = false });
        using var provider = CreateSchedulingProvider(botOptions);
        var quartzOptions = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        quartzOptions.JobDetails.Should().NotContain(j => j.Key.Equals(JobSchedules.AutonomousWorkDispatcherJobKey));
        quartzOptions.Triggers.Should().NotContain(t => t.JobKey.Equals(JobSchedules.AutonomousWorkDispatcherJobKey));
    }

    [Test]
    public void Scheduling_ExistingJobsAndDispatcher_BothRegisteredWithoutOverlap()
    {
        var botOptions = DisabledSchedulesOptions(EnabledOptions());
        botOptions = new MandarinBotOptions
        {
            Discord = botOptions.Discord,
            FantasyPremierLeague = botOptions.FantasyPremierLeague,
            Schedules = new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = new ScheduledJobOptions { Enabled = true, Cron = "0 0/15 * * * ?" },
                UclFantasyNotifications = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" },
                ClassicStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                HeadToHeadStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                BenchWarmingLeague = new ScheduledJobOptions { Enabled = false, Cron = "0 0 18 * * ?" },
                FplStatisticsCollection = new ScheduledJobOptions { Enabled = false, Cron = "0 0 19 * * ?" },
                FplGameweekRecap = new ScheduledJobOptions { Enabled = false, Cron = "0 0 20 * * ?" },
                FplLiveInsights = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" },
                FplPriceChanges = new ScheduledJobOptions { Enabled = false, Cron = "0 0 12-22 * * ?" },
                FplChipWatch = new ScheduledJobOptions { Enabled = false, Cron = "0 0 6,12,18 * * ?" }
            },
            Notifications = botOptions.Notifications,
            WelcomeMessages = botOptions.WelcomeMessages,
            AutonomousWorkDispatcher = botOptions.AutonomousWorkDispatcher
        };
        using var provider = CreateSchedulingProvider(botOptions);
        var quartzOptions = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        quartzOptions.JobDetails.Should().Contain(j => j.Key.Equals(JobSchedules.PremierLeagueNotificationJobKey));
        quartzOptions.JobDetails.Should().Contain(j => j.Key.Equals(JobSchedules.AutonomousWorkDispatcherJobKey));
        quartzOptions.JobDetails.Should().HaveCount(2);
    }

    private static MandarinBotOptions CreateValidOptions(
        AutonomousWorkDispatcherOptions? dispatcher = null)
    {
        return new MandarinBotOptions
        {
            Discord = new DiscordOptions
            {
                Token = "test-token",
                ReadinessTimeout = TimeSpan.FromSeconds(30)
            },
            FantasyPremierLeague = new FantasyPremierLeagueOptions
            {
                ClassicLeagueId = 123,
                HeadToHeadLeagueId = 456
            },
            Schedules = new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = new ScheduledJobOptions { Enabled = true, Cron = "0 0 * * * ?" },
                UclFantasyNotifications = new ScheduledJobOptions { Enabled = false, Cron = "0 0 * * * ?" },
                ClassicStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                HeadToHeadStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                BenchWarmingLeague = new ScheduledJobOptions { Enabled = false, Cron = "0 0 18 * * ?" },
                FplStatisticsCollection = new ScheduledJobOptions { Enabled = false, Cron = "0 0 19 * * ?" },
                FplGameweekRecap = new ScheduledJobOptions { Enabled = false, Cron = "0 0 20 * * ?" },
                FplLiveInsights = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" }
            },
            Notifications = new NotificationOptions
            {
                Targets =
                [
                    new NotificationTargetOptions { GuildId = 100, ChannelId = 101 }
                ]
            },
            AutonomousWorkDispatcher = dispatcher ?? new AutonomousWorkDispatcherOptions()
        };
    }

    private static MandarinBotOptions DisabledSchedulesOptions(
        AutonomousWorkDispatcherOptions dispatcher)
    {
        return new MandarinBotOptions
        {
            Discord = new DiscordOptions
            {
                Token = "test-token",
                ReadinessTimeout = TimeSpan.FromSeconds(30)
            },
            FantasyPremierLeague = new FantasyPremierLeagueOptions
            {
                ClassicLeagueId = 123,
                HeadToHeadLeagueId = 456
            },
            Schedules = new JobSchedulesOptions
            {
                TimeZoneId = "Europe/Riga",
                PremierLeagueNotifications = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" },
                UclFantasyNotifications = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" },
                ClassicStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                HeadToHeadStandings = new ScheduledJobOptions { Enabled = false, Cron = "0 0 17 * * ?" },
                BenchWarmingLeague = new ScheduledJobOptions { Enabled = false, Cron = "0 0 18 * * ?" },
                FplStatisticsCollection = new ScheduledJobOptions { Enabled = false, Cron = "0 0 19 * * ?" },
                FplGameweekRecap = new ScheduledJobOptions { Enabled = false, Cron = "0 0 20 * * ?" },
                FplLiveInsights = new ScheduledJobOptions { Enabled = false, Cron = "0 0/15 * * * ?" },
                FplPriceChanges = new ScheduledJobOptions { Enabled = false, Cron = "0 0 12-22 * * ?" },
                FplChipWatch = new ScheduledJobOptions { Enabled = false, Cron = "0 0 6,12,18 * * ?" }
            },
            Notifications = new NotificationOptions
            {
                Targets =
                [
                    new NotificationTargetOptions { GuildId = 100, ChannelId = 101 }
                ]
            },
            AutonomousWorkDispatcher = dispatcher
        };
    }

    private static ServiceProvider CreateSchedulingProvider(MandarinBotOptions botOptions)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(botOptions));
        services.AddSingleton(botOptions.Schedules);
        services.AddSingleton(botOptions.AutonomousWorkDispatcher);
        services.AddSingleton(TimeProvider.System);
        services.AddMandarinBotScheduling();
        return services.BuildServiceProvider();
    }

    private static AutonomousWorkDispatcherOptions EnabledOptions(string token = "test-token")
    {
        return new AutonomousWorkDispatcherOptions
        {
            Enabled = true,
            Cron = AutonomousWorkDispatcherOptions.DefaultCron,
            Owner = AutonomousWorkDispatcherOptions.DefaultOwner,
            Repository = AutonomousWorkDispatcherOptions.DefaultRepository,
            Workflow = AutonomousWorkDispatcherOptions.DefaultWorkflow,
            Ref = AutonomousWorkDispatcherOptions.DefaultRef,
            Token = token
        };
    }

    private static AutonomousWorkDispatcherClient CreateClient(
        HttpMessageHandler handler,
        AutonomousWorkDispatcherOptions options,
        ILogger<AutonomousWorkDispatcherClient>? logger = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        return new AutonomousWorkDispatcherClient(
            httpClient,
            options,
            logger ?? new RecordingLogger<AutonomousWorkDispatcherClient>());
    }

    private static IJobExecutionContext CreateJobContext<TJob>(string fireId)
        where TJob : IJob
    {
        var context = DispatchProxy.Create<IJobExecutionContext, JobContextProxy>();
        var proxy = (JobContextProxy)(object)context;
        proxy.JobDetail = JobBuilder.Create<TJob>().WithIdentity("test-job").Build();
        proxy.FireInstanceId = fireId;
        return context;
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return sendAsync(request, cancellationToken);
        }
    }

    private sealed class TestDispatcherClient : IAutonomousWorkDispatcherClient
    {
        public int CallCount { get; private set; }

        public Exception? Throw { get; set; }

        public Task TriggerDispatcherAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            if (Throw is not null)
            {
                throw Throw;
            }

            return Task.CompletedTask;
        }
    }

    public class JobContextProxy : DispatchProxy
    {
        public required IJobDetail JobDetail { get; set; }

        public required string FireInstanceId { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_JobDetail" => JobDetail,
                "get_FireInstanceId" => FireInstanceId,
                "get_RefireCount" => 0,
                "get_CancellationToken" => CancellationToken.None,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }
}
