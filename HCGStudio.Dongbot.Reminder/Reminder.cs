using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HCGStudio.DongBot.Core.Attributes;
using HCGStudio.DongBot.Core.Messages;
using HCGStudio.DongBot.Core.Service;
using Newtonsoft.Json;

namespace HCGStudio.DongBot.Reminder
{
    [Service("Reminder")]
    public class Reminder : IDisposable
    {
        public IMessageSender MessageSender { get; set; }
        private List<ReminderEvents> Events { get; }
        private Timer Timer { get; }
        public Reminder()
        {
            try
            {
                Events = JsonConvert.DeserializeObject<List<ReminderEvents>>(File.ReadAllText("reminders.json"));
                Timer = new Timer(async state =>
                {
                    if(DateTime.Now.Minute != 0)
                        return;
                    await Remind();
                },null,0,1000);
                
            }
            catch
            {
                Events = null;
            }
        }

        [OnKeyword("查看ddl", "查看提醒", "lsddl", KeywordPolicy = KeywordPolicy.Trim,InvokePolicies = InvokePolicies.Group)]
        public async Task ShowAllReminder(long groupId, long userId)
        {

            var builder = new MessageBuilder();
            builder.Append(new AtMessage(userId));
            try
            {
                if (Events == null)
                {
                    builder.Append((SimpleMessage)"配置文件不存在！");
                    return;
                }
                var thisGroup = (from reminderEvent in Events
                                 where reminderEvent.GroupId == groupId && reminderEvent.DeadLine > DateTime.Now
                                 orderby reminderEvent.DeadLine
                                 select reminderEvent).ToList();
                if (!thisGroup.Any())
                {
                    builder.Append((SimpleMessage)"本群无提醒事项！");
                }
                else
                {
                    builder.Append((SimpleMessage)"本群有以下提醒事项：\n");
                    foreach (var reminderEvent in thisGroup)
                    {
                        builder.Append((SimpleMessage)$"{reminderEvent}\n");
                    }

                }
            }
            finally
            {
                await MessageSender.SendGroupAsync(groupId, builder.ToMessage());
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
                foreach (var reminder in grouping)
                {
                    await MessageSender.SendGroupAsync(reminder.GroupId, (SimpleMessage)reminder.ToString());
                    await Task.Delay(50);
                    await MessageSender.SendGroupAsync(reminder.GroupId, (SimpleMessage) "我也要做加把劲骑士了！");
                    await Task.Delay(100);
                }
            }
        }

        public void Dispose()
        {
            Timer?.Dispose();
        }
    }
}
