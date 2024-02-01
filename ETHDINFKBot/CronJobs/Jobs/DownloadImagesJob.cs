﻿using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ETHDINFKBot.CronJobs.Jobs
{
    public class DownloadImagesJob : CronJobService
    {
        private readonly ILogger<DownloadImagesJob> _logger;
        private readonly string Name = "DownloadImagesJob";

        public DownloadImagesJob(IScheduleConfig<DownloadImagesJob> config, ILogger<DownloadImagesJob> logger)
            : base(config.CronExpression, config.TimeZoneInfo)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"{Name} starts.");
            return base.StartAsync(cancellationToken);
        }
        public void BackupDB(string sourceConnectionString, string targetConnectionString)
        {
            try
            {
                // TODO job for maria db
                /*
                using (var location = new SqliteConnection(sourceConnectionString))
                using (var destination = new SqliteConnection(targetConnectionString))
                {
                    location.Open();
                    destination.Open();
                    location.BackupDatabase(destination);
                }*/
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed Backup");
                //textChannel.SendMessageAsync("Failed DB Backup: " + ex.Message);
            }
        }
        public override Task DoWork(CancellationToken cancellationToken)
        {

            ProcessChannels();


            return Task.CompletedTask;
        }

        private async void ProcessChannels()
        {



            // todo config
            ulong guildId = 747752542741725244;
            //ulong spamChannel = 768600365602963496;
            //var guild = Program.Client.GetGuild(guildId);
            //var textChannel = guild.GetTextChannel(spamChannel);

            var keyValueDBManager = DatabaseManager.KeyValueManager;

            var imageScrapeChannelIdsString = keyValueDBManager.Get<string>("ImageScrapeChannelIds");

            int scrapePerRun = keyValueDBManager.Get<int>("MessageScrapePerRun");

            if (scrapePerRun == 0)
            {
                scrapePerRun = 10_000;
            }

            if (imageScrapeChannelIdsString == null)
            {
                _logger.LogError("ImageScrapeChannelIds not set");
                return;
            }

            var imageScrapeChannelIds = imageScrapeChannelIdsString.Split(',').Select(x => ulong.Parse(x)).ToList();

            var guild = Program.Client.GetGuild(guildId);

            var channels = guild.Channels.Where(x => imageScrapeChannelIds.Contains(x.Id)).ToList();

            foreach (var channel in channels)
            {
                var textChannel = channel as SocketTextChannel;
                ulong lastMessageForChannel = keyValueDBManager.Get<ulong>($"LastScapedMessageForChannel_{channel.Id}");

                var messages = textChannel.GetMessagesAsync(lastMessageForChannel, Direction.Before, scrapePerRun).FlattenAsync().Result.ToList();

                if (messages.Count == 0)
                {
                    _logger.LogInformation($"No new messages for channel {channel.Name}");
                }

                _logger.LogInformation($"Found {messages.Count} new messages for channel {channel.Name}");

                int botCount = 0;
                int noUrlCount = 0;

                foreach (var message in messages)
                {
                    if(message.Author.IsBot)
                    {
                        botCount++;
                        continue;
                    }

                    if (message.Attachments.Count == 0 && message.Embeds.Count == 0)
                    {
                        noUrlCount++;
                        continue;
                    }

                    
                }
            }
        }

        private async void DownloadFile(HttpClient client, SocketMessage message, ulong messageId, string url, int index, string basePath, string downloadFileName)
        {
            // dont download webp images if possible
            url = url.Replace("&format=webp", "");

            // remove width and height query params
            url = Regex.Replace(url, @"&width=\d+", "");
            url = Regex.Replace(url, @"&height=\d+", "");

            // if the parameter is at the start with ? then remove it
            url = Regex.Replace(url, @"\?width=\d+", "?");
            url = Regex.Replace(url, @"\?height=\d+", "?");

            // if url ends with ? then remove it
            url = Regex.Replace(url, @"\?$", "");

            try
            {
                string fileName = downloadFileName;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = url.Split('/').Last();
                    fileName = fileName.Split('?').First();
                }

                fileName = fileName.ToLower(); // so no png and PNG


                if (!fileName.Contains("."))
                {
                    _logger.LogInformation($"Filename '{fileName}' is invalid from content: ```{message.Content}```", false);
                    throw new Exception("Invalid filename");
                }


                // remove any . except the last one
                string fileExtension = fileName.Split('.').Last();
                string name = fileName.Substring(0, fileName.Length - fileExtension.Length - 1);

                // limit filename to 150 chars max
                if (name.Length > 100)
                    name = name.Substring(0, 100);


                name = name.Replace(".", "");

                // remove any non alphanumeric chars from name
                name = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");

                fileName = $"{message.Id}_{index}_{name}.{fileExtension}";

                var emojiDate = SnowflakeUtils.FromSnowflake(messageId);
                string additionalFolder = $"{emojiDate.Year}-{emojiDate.Month:00}";

                // put image into folder Python/memes
                string filePath = Path.Combine(basePath, additionalFolder, fileName);

                if(Directory.Exists(Path.GetDirectoryName(filePath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                // check if folder exists
                if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                {
                    _logger.LogInformation($"Folder {Path.GetDirectoryName(filePath)} does not exist", false);
                    _logger.LogInformation($"Content: ```{message.Content}```");

                }

                // check if file exists
                if (File.Exists(filePath))
                {
                    //await Context.Channel.SendMessageAsync($"File {filePath} already exists", false);
                    return;
                }


                byte[] bytes = client.GetByteArrayAsync(url).Result;
                File.WriteAllBytes(filePath, bytes);
            }
            catch (HttpException ex)
            {
                // if status code 404 then skip
                if (ex.HttpCode == HttpStatusCode.NotFound) return;

                _logger.LogInformation($"Download error in attachment url <{url}>: " + ex.Message.ToString(), false);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"{Name} is stopping.");
            return base.StopAsync(cancellationToken);
        }
    }
}
