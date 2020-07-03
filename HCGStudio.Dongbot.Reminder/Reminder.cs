using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HCGStudio.DongBot.Core.Attributes;
using HCGStudio.DongBot.Core.Messages;
using HCGStudio.DongBot.Core.Service;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HCGStudio.DongBot.Reminder
{
    [Service("Reminder")]
    public class Reminder
    {
        private readonly ILogger<Reminder> _logger;
        private readonly IMessageSender _messageSender;

        public Reminder(ILogger<Reminder> logger, IMessageSender messageSender)
        {
            try
            {
                _messageSender = messageSender;
                _logger = logger;
                var events = JsonConvert.DeserializeObject<List<ReminderEvents>>(File.ReadAllText("reminders.json"));
                Events.Clear();
                Events.AddRange(events);
                Timer ??= new Timer(async state =>
                {
                    var last = state as TimeState;
                    if (last?.LastInvoke == DateTime.Now.Hour)
                        return;
                    if (last != null)
                        last.LastInvoke = DateTime.Now.Hour;
                    if (DateTime.Now.Minute != 0)
                        return;
                    await Remind();
                }, new TimeState(), 0, 1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        private static List<ReminderEvents> Events { get; } = new List<ReminderEvents>();
        private static Timer Timer { get; set; }

        [Information("重载提醒事项", "提醒", "从磁盘中加载提醒事项，需要权限")]
        [OnKeyword("重载提醒事项", InvokePolicies = InvokePolicies.Group | InvokePolicies.GroupAt, RequireSuperUser = true)]
        public async Task ReloadReminders(long groupId, long userId)
        {
            try
            {
                var newEvents =
                    JsonConvert.DeserializeObject<IEnumerable<ReminderEvents>>(
                        await File.ReadAllTextAsync("reminders.json"));
                Events.Clear();
                Events.AddRange(newEvents);
                await _messageSender.SendGroupAsync(groupId, (SimpleMessage) "重新加载成功！");
                await ShowAllReminder(groupId, userId);
            }
            catch
            {
                await _messageSender.SendGroupAsync(groupId, (SimpleMessage) "重新加载失败！");
            }
        }

        [Information("查看提醒", "提醒", "查看本群中所有的提醒")]
        [OnKeyword("查看ddl", "查看提醒", "lsddl", KeywordPolicy = KeywordPolicy.Trim, InvokePolicies = InvokePolicies.Group)]
        public async Task ShowAllReminder(long groupId, long userId)
        {
            var builder = new MessageBuilder();
            builder.Append(new AtMessage(userId));
            try
            {
                if (Events.Count == 0)
                {
                    builder.Append((SimpleMessage) "配置文件不存在！");
                    return;
                }

                var thisGroup = (from reminderEvent in Events
                    where reminderEvent.GroupId == groupId && reminderEvent.DeadLine > DateTime.Now
                    orderby reminderEvent.DeadLine
                    select reminderEvent).ToList();
                if (!thisGroup.Any())
                {
                    builder.Append((SimpleMessage) "本群无提醒事项！");
                }
                else
                {
                    builder.Append((SimpleMessage) "本群有以下提醒事项：\n");
                    foreach (var reminderEvent in thisGroup) builder.Append((SimpleMessage) $"{reminderEvent}\n");
                }
            }
            finally
            {
                await _messageSender.SendGroupAsync(groupId, builder.ToMessage());
            }
        }

        public async Task Remind()
        {
            var currentHour = DateTime.Now.Hour;
            var events = from remindEvent in Events
                where remindEvent.DeadLine > DateTime.Now && remindEvent.RemindHours.Contains(currentHour)
                orderby remindEvent.DeadLine
                group remindEvent by remindEvent.GroupId
                into newGroup
                orderby newGroup.Key
                select newGroup;
            foreach (var grouping in events)
            {
                var builder = new MessageBuilder();
                foreach (var reminder in grouping) builder.Append((SimpleMessage) $"{reminder}\n");

                await _messageSender.SendGroupAsync(grouping.Key, builder.ToMessage());
                await _messageSender.SendGroupAsync(grouping.Key, (SimpleMessage) "我也要做加把劲骑士了！");
            }
        }

        private class TimeState
        {
            public int LastInvoke { get; set; } = -1;
        }
    }
}