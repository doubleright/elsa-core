using System.Runtime.CompilerServices;
using Elsa.Common.Models;
using Elsa.Extensions;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Management;
using Elsa.Workflows.Models;
using Elsa.Workflows.Options;
using Elsa.Workflows.Runtime.Stimuli;
using Elsa.Workflows.UIHints;
using JetBrains.Annotations;

namespace Elsa.Workflows.Runtime.Activities;

/// <summary>
/// Creates a new workflow instance of the specified workflow and dispatches it for execution.
/// </summary>
[Activity("Elsa", "Composition", "Create a new workflow instance of the specified workflow and execute it.", Kind = ActivityKind.Task)]
[UsedImplicitly]
public class ExecuteWorkflow : Activity<ExecuteWorkflowResult>
{
    /// <inheritdoc />
    public ExecuteWorkflow([CallerFilePath] string? source = default, [CallerLineNumber] int? line = default) : base(source, line)
    {
    }

    /// <summary>
    /// The definition ID of the workflow to execute. 
    /// </summary>
    [Input(
        DisplayName = "Workflow Definition",
        Description = "The definition ID of the workflow to execute.",
        UIHint = InputUIHints.WorkflowDefinitionPicker
    )]
    public Input<string> WorkflowDefinitionId { get; set; } = default!;

    /// <summary>
    /// The correlation ID to associate the workflow with. 
    /// </summary>
    [Input(
        DisplayName = "Correlation ID",
        Description = "The correlation ID to associate the workflow with."
    )]
    public Input<string?> CorrelationId { get; set; } = default!;

    /// <summary>
    /// The input to send to the workflow.
    /// </summary>
    [Input(Description = "The input to send to the workflow.")]
    public Input<IDictionary<string, object>?> Input { get; set; } = default!;
    
    /// <summary>
    /// True to wait for the child workflow to complete before completing this activity. If not set, the child workflow will be executed until it either completes or goes idle before this activity completes.
    /// </summary>
    [Input(Description = "Wait for the child workflow to complete before completing this activity.")]
    public Input<bool> WaitForCompletion { get; set; } = default!;

    /// <inheritdoc />
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var result = await ExecuteWorkflowAsync(context);
        var waitForCompletion = WaitForCompletion.Get(context);
        
        if(!waitForCompletion || result.Status == WorkflowStatus.Finished)
        {
            context.SetResult(result);
            await context.CompleteActivityAsync();
            return;
        }
        
        // Since the child workflow is still running, we need to wait for it to complete using a bookmark.
        var bookmarkOptions = new CreateBookmarkArgs
        {
            Callback = OnChildWorkflowCompletedAsync,
            Stimulus = new ExecuteWorkflowStimulus(result.WorkflowInstanceId),
            IncludeActivityInstanceId = false
        };
        context.CreateBookmark(bookmarkOptions);
    }

    private async ValueTask<ExecuteWorkflowResult> ExecuteWorkflowAsync(ActivityExecutionContext context)
    {
        var workflowDefinitionId = WorkflowDefinitionId.Get(context);
        var input = Input.GetOrDefault(context) ?? new Dictionary<string, object>();
        var correlationId = CorrelationId.GetOrDefault(context);
        var workflowInvoker = context.GetRequiredService<IWorkflowInvoker>();
        var identityGenerator = context.GetRequiredService<IIdentityGenerator>();
        var workflowDefinitionService = context.GetRequiredService<IWorkflowDefinitionService>();
        var workflowGraph = await workflowDefinitionService.FindWorkflowGraphAsync(workflowDefinitionId, VersionOptions.Published, context.CancellationToken);

        if (workflowGraph == null)
            throw new Exception($"No published version of workflow definition with ID {workflowDefinitionId} found.");

        var options = new RunWorkflowOptions
        {
            ParentWorkflowInstanceId = context.WorkflowExecutionContext.Id,
            Input = input,
            CorrelationId = correlationId,
            WorkflowInstanceId = identityGenerator.GenerateId()
        };

        var workflowResult = await workflowInvoker.InvokeAsync(workflowGraph, options, context.CancellationToken);
        var info = new ExecuteWorkflowResult
        {
            WorkflowInstanceId = options.WorkflowInstanceId,
            Status = workflowResult.WorkflowState.Status,
            SubStatus = workflowResult.WorkflowState.SubStatus,
            Output = workflowResult.WorkflowState.Output
        };

        return info;
    }
    
    private async ValueTask OnChildWorkflowCompletedAsync(ActivityExecutionContext context)
    {
        var input = context.WorkflowInput;
        context.Set(Result, input);
        await context.CompleteActivityAsync();
    }
}