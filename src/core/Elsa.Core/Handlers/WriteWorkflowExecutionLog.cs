using System.Threading;
using System.Threading.Tasks;
using Elsa.Events;
using Elsa.Services;
using MediatR;
using Newtonsoft.Json.Linq;

namespace Elsa.Handlers
{
    public class WriteWorkflowExecutionLog : INotificationHandler<ActivityExecuting>, INotificationHandler<ActivityExecuted>, INotificationHandler<ActivityFaulted>
    {
        private readonly IWorkflowExecutionLog _workflowExecutionLog;
        public WriteWorkflowExecutionLog(IWorkflowExecutionLog workflowExecutionLog) => _workflowExecutionLog = workflowExecutionLog;
        public async Task Handle(ActivityExecuting notification, CancellationToken cancellationToken) => await WriteEntryAsync(notification.Resuming ? "Resuming" : "Executing", default, notification, null, cancellationToken);
        public async Task Handle(ActivityExecuted notification, CancellationToken cancellationToken) => await WriteEntryAsync(notification.Resuming ? "Resumed" : "Executed", default, notification, null, cancellationToken);

        public async Task Handle(ActivityFaulted notification, CancellationToken cancellationToken)
        {
            var exception = notification.Exception;

            var data = JObject.FromObject(new
            {
                exception.Message,
                exception.StackTrace
            });

            await WriteEntryAsync("Faulted", exception.Message, notification, data, cancellationToken);
        }

        private async Task WriteEntryAsync(string eventName, string? message, ActivityNotification notification, JObject? data, CancellationToken cancellationToken)
        {
            var workflowInstance = notification.WorkflowExecutionContext.WorkflowInstance;
            var activityBlueprint = notification.Activity;
            await _workflowExecutionLog.AddEntryAsync(eventName, workflowInstance, activityBlueprint, message, data, default, cancellationToken);
        }
    }
}