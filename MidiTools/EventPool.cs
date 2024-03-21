using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MidiTools
{
    public class EventPool
    {
        internal Guid TaskerGuid;
        internal int LastMinuteProcessing = 0;
        internal string From = "";
        internal int TasksRunning
        {
            get
            {
                lock (BackgroundTasks)
                {
                    return BackgroundTasks.Count(t => !t.IsCompleted);
                }
            }
        }

        private List<Task> BackgroundTasks = new List<Task>();
        private Timer timer;

        public EventPool(string from)
        {
            timer = new Timer(CleanupCompletedTasks, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            From = from;
        }

        private void CleanupCompletedTasks(object state)
        {
            lock (BackgroundTasks)
            {
                LastMinuteProcessing = BackgroundTasks.Count();
                BackgroundTasks.RemoveAll(t => t.IsCompleted);
            }
        }

        public async Task AddTask(Action value)
        {
            var task = Task.Run(value);
            lock (BackgroundTasks) { BackgroundTasks.Add(task); }
            await task;
        }

    }

    public static class UIEventPool
    {
        internal static int TasksRunning
        {
            get
            {
                lock (BackgroundTasks)
                {
                    return BackgroundTasks.Count(t => !t.IsCompleted);
                }
            }
        }

        private static List<Task> BackgroundTasks = new List<Task>();
        private static Timer timer;

        static UIEventPool()
        {
            timer = new Timer(CleanupCompletedTasks, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        private static void CleanupCompletedTasks(object state)
        {
            lock (BackgroundTasks)
            {
                BackgroundTasks.RemoveAll(t => t.IsCompleted);
            }
        }

        public static async Task AddTask(Action value)
        {
            var task = Task.Run(value);
            lock (BackgroundTasks) { BackgroundTasks.Add(task); }
            await task;
        }

    }
}
