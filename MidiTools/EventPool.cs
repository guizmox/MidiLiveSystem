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
    public static class EventPool
    {
        public static int TasksRunning
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

        static EventPool()
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

        public async static Task AddTask(Action value)
        {
            var task = Task.Run(value);
            lock (BackgroundTasks) { BackgroundTasks.Add(task); }
            await task;
        }

    }
}
