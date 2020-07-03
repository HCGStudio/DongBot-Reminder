using System;
using System.Collections.Generic;

namespace HCGStudio.DongBot.Reminder
{
    public class ReminderEvents
    {
        public long GroupId { get; set; }
        public DateTime DeadLine { get; set; }
        public SortedSet<int> RemindHours { get; set; } = new SortedSet<int>();
        public string RemindContent { get; set; } = string.Empty;

        public override string ToString()
        {
            var remind = DeadLine - DateTime.Now;
            return $"{RemindContent}, DDL:{DeadLine}, 还剩{(remind.Days < 1 ? "不足一天" : $"{remind.Days}天")}！";
        }
    }
}