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
        private class RoutingActions
        {
            private class RoutingAction
            {
                internal Guid RoutingGuid;
                internal List<Task> Tasks = new List<Task>();

                internal RoutingAction(Guid routingGuid, Task tRouting)
                {
                    RoutingGuid = routingGuid;
                    lock (Tasks)
                    {
                        Tasks.Add(tRouting);
                    }
                }

                internal void AddTask(Task tRouting)
                {
                    lock (Tasks)
                    {
                        Tasks.Add(tRouting);
                    }
                }

                internal void Clean()
                {
                    lock (Tasks)
                    {
                        Tasks.RemoveAll(t => t.IsCompleted);
                    }
                }
            }

            List<RoutingAction> Actions = new List<RoutingAction>();

            internal void AddTask(Guid routingGuid, Task tRouting)
            {
                lock (Actions)
                {
                    var action = Actions.FirstOrDefault(a => a.RoutingGuid == routingGuid);
                    if (action == null) { Actions.Add(new RoutingAction(routingGuid, tRouting)); }
                    else { action.AddTask(tRouting); }
                }
            }

            internal int GetRoutingActions(Guid routingGuid)
            {
                lock (Actions)
                {
                    var action = Actions.FirstOrDefault(a => a.RoutingGuid == routingGuid);
                    if (action != null)
                    {
                        return action.Tasks.Count(t => !t.IsCompleted);
                    }
                    else { return 0; }
                }
            }

            internal void Clean()
            {
                lock (Actions)
                {
                    foreach (var act in Actions)
                    {
                        act.Clean();
                    }                    
                }
            }
        }

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
        private static RoutingActions BackgroundRoutingTasks = new RoutingActions();
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
            lock (BackgroundRoutingTasks)
            {
                BackgroundRoutingTasks.Clean();
            }
        }

        public async static Task AddTask(Action value)
        {
            var task = Task.Run(value);
            lock (BackgroundTasks) { BackgroundTasks.Add(task); }
            await task;
        }

        public async static Task AddTaskRouting(Guid routingGuid, Action value)
        {
            var task = Task.Run(value);
            lock (BackgroundTasks) { BackgroundTasks.Add(task); }
            BackgroundRoutingTasks.AddTask(routingGuid, task);
            await task;
        }

        internal static int RoutingTasksRunning(Guid routingGuid)
        {
            return BackgroundRoutingTasks.GetRoutingActions(routingGuid);
        }

    }
}
