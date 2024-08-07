using Elsa.Workflows.Contracts;
using Elsa.Workflows.Options;

namespace Elsa.Workflows;

/// <inheritdoc />
public class ActivityInvoker : IActivityInvoker
{
    private readonly IActivityExecutionPipeline _pipeline;

    /// <summary>
    /// Constructor.
    /// </summary>
    public ActivityInvoker(IActivityExecutionPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(WorkflowExecutionContext workflowExecutionContext, IActivity activity, ActivityInvocationOptions? options = default)
    {
        // Setup an activity execution context, potentially reusing an existing one if requested.
        var existingActivityExecutionContext = options?.ExistingActivityExecutionContext;

        // Perform a lookup to make sure the activity execution context is part of the workflow execution context.
        var activityExecutionContext = existingActivityExecutionContext != null
            ? workflowExecutionContext.ActivityExecutionContexts.FirstOrDefault(x => x.Id == existingActivityExecutionContext.Id)
            : default;

        if (activityExecutionContext == null)
        {
            // Create a new activity execution context.
            activityExecutionContext = await workflowExecutionContext.CreateActivityExecutionContextAsync(activity, options);

            // Add the activity context to the workflow context.
            workflowExecutionContext.AddActivityExecutionContext(activityExecutionContext);
        }

        // Execute the activity execution pipeline.
        await InvokeAsync(activityExecutionContext);
    }

    /// <inheritdoc />
    public async Task InvokeAsync(ActivityExecutionContext activityExecutionContext)
    {
        // Execute the activity execution pipeline.
        await _pipeline.ExecuteAsync(activityExecutionContext);
    }
}