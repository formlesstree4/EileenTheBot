using Bot.Models.ChannelCommunication;
using Discord;
using Discord.WebSocket;
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Services
{

    public sealed class ChannelCommunicationService : IEileenService
    {
        private readonly ILogger<ChannelCommunicationService> logger;
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly DiscordSocketClient client;

        public ChannelCommunicationService(
            ILogger<ChannelCommunicationService> logger,
            ServerConfigurationService serverConfigurationService,
            DiscordSocketClient client)
        {
            this.logger = logger;
            this.serverConfigurationService = serverConfigurationService;
            this.client = client;

            this.client.JoinedGuild += OnGuildJoined;
            this.client.LeftGuild += OnGuildLeft;
            this.client.Ready += OnClientReady;
        }


        public async Task ScheduleNewTask(IGuild guild, ChannelCommuncationJobEntry jobEntry)
        {
            await ScheduleNewTask(guild.Id, jobEntry);
        }

        public async Task ScheduleNewTask(ulong guildId, ChannelCommuncationJobEntry jobEntry)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(guildId);
            var jobs = configuration.GetOrAddTagData("commJobs", () => new List<ChannelCommuncationJobEntry>());
            jobs.Add(jobEntry);
            HandleJobScheduling(jobEntry);
        }

        public async Task<List<ChannelCommuncationJobEntry>> GetServerJobs(IGuild guild)
        {
            return await GetServerJobs(guild.Id);
        }

        public async Task<List<ChannelCommuncationJobEntry>> GetServerJobs(ulong guildId)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(guildId);
            return configuration.GetOrAddTagData("commJobs", () => new List<ChannelCommuncationJobEntry>());
        }

        public async Task RemoveJob(IGuild guild, string jobName)
        {
            await RemoveJob(guild.Id, jobName);
        }

        public async Task RemoveJob(ulong guildId, string jobName)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(guildId);
            var jobs = configuration.GetOrAddTagData("commJobs", () => new List<ChannelCommuncationJobEntry>());
            var job = jobs.Find(entry => entry.JobName.Equals(jobName, StringComparison.OrdinalIgnoreCase));
            if (job is not null)
            {
                job.HasRun = true;
            }
            RecurringJob.RemoveIfExists(jobName);
        }



        private void HandleJobScheduling(ChannelCommuncationJobEntry jobEntry)
        {
            if (jobEntry.Repeats)
            {
                RecurringJob.AddOrUpdate(jobEntry.JobName, () => RunJobTask(jobEntry), jobEntry.WhenToRun, TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));
            }
            else
            {
                // see if this is in the past
                var timeToRun = jobEntry.Created.AddMinutes(double.Parse(jobEntry.WhenToRun));
                if (timeToRun > DateTime.Now)
                {
                    BackgroundJob.Schedule(() => RunJobTask(jobEntry), timeToRun);
                }
            }
        }

        private async Task RunJobTask(ChannelCommuncationJobEntry job)
        {
            if (job.HasRun) return;
            var channel = await client.GetChannelAsync(job.ChannelId) as ITextChannel;
            await channel.SendMessageAsync(job.Message);
            if (!job.Repeats)
            {
                job.HasRun = true;
            }
        }


        private async Task OnClientReady()
        {
            foreach (var guild in client.Guilds)
            {
                var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(guild.Id);
                var jobs = configuration.GetOrAddTagData("commJobs", () => new List<ChannelCommuncationJobEntry>());
                foreach (var job in jobs.Where(j => !j.HasRun)) HandleJobScheduling(job);
            }
        }

        private async Task OnGuildLeft(SocketGuild arg)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(arg.Id);
            var jobs = configuration.GetOrAddTagData("commJobs", () => new List<ChannelCommuncationJobEntry>());
            foreach (var job in jobs.Where(j => !j.HasRun && j.Repeats))
            {
                RecurringJob.RemoveIfExists(job.JobName);
            }
        }

        private async Task OnGuildJoined(SocketGuild arg)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(arg.Id);
            var jobs = configuration.GetOrAddTagData("commJobs", () => new List<ChannelCommuncationJobEntry>());
            foreach (var job in jobs.Where(j => !j.HasRun && j.Repeats))
            {
                HandleJobScheduling(job);
            }
        }

    }

}
