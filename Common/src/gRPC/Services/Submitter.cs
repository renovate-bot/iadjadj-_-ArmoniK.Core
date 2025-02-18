// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2023. All rights reserved.
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Api.Common.Utils;
using ArmoniK.Api.gRPC.V1;
using ArmoniK.Api.gRPC.V1.Submitter;
using ArmoniK.Core.Base;
using ArmoniK.Core.Common.Exceptions;
using ArmoniK.Core.Common.Storage;
using ArmoniK.Utils;

using Google.Protobuf;

using Grpc.Core;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;

using Output = ArmoniK.Api.gRPC.V1.Output;
using TaskOptions = ArmoniK.Core.Common.Storage.TaskOptions;
using TaskStatus = ArmoniK.Api.gRPC.V1.TaskStatus;

namespace ArmoniK.Core.Common.gRPC.Services;

public class Submitter : ISubmitter
{
  private readonly ActivitySource              activitySource_;
  private readonly ILogger<Submitter>          logger_;
  private readonly IObjectStorage              objectStorage_;
  private readonly IPartitionTable             partitionTable_;
  private readonly IPushQueueStorage           pushQueueStorage_;
  private readonly IResultTable                resultTable_;
  private readonly ISessionTable               sessionTable_;
  private readonly Injection.Options.Submitter submitterOptions_;
  private readonly ITaskTable                  taskTable_;

  [UsedImplicitly]
  public Submitter(IPushQueueStorage           pushQueueStorage,
                   IObjectStorage              objectStorage,
                   ILogger<Submitter>          logger,
                   ISessionTable               sessionTable,
                   ITaskTable                  taskTable,
                   IResultTable                resultTable,
                   IPartitionTable             partitionTable,
                   Injection.Options.Submitter submitterOptions,
                   ActivitySource              activitySource)
  {
    objectStorage_    = objectStorage;
    logger_           = logger;
    sessionTable_     = sessionTable;
    taskTable_        = taskTable;
    resultTable_      = resultTable;
    partitionTable_   = partitionTable;
    submitterOptions_ = submitterOptions;
    activitySource_   = activitySource;
    pushQueueStorage_ = pushQueueStorage;
  }

  /// <inheritdoc />
  public Task<Configuration> GetServiceConfiguration(Empty             request,
                                                     CancellationToken cancellationToken)
    => Task.FromResult(new Configuration
                       {
                         DataChunkMaxSize = PayloadConfiguration.MaxChunkSize,
                       });

  /// <inheritdoc />
  public async Task CancelSession(string            sessionId,
                                  CancellationToken cancellationToken)
  {
    using var _        = logger_.LogFunction();
    using var activity = activitySource_.StartActivity($"{nameof(CancelSession)}");

    if (logger_.IsEnabled(LogLevel.Trace))
    {
      cancellationToken.Register(() => logger_.LogTrace("CancellationToken from ServerCallContext has been triggered"));
    }

    var sessionCancelTask = sessionTable_.CancelSessionAsync(sessionId,
                                                             cancellationToken);

    await taskTable_.CancelSessionAsync(sessionId,
                                        cancellationToken)
                    .ConfigureAwait(false);

    await sessionCancelTask.ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task FinalizeTaskCreation(IEnumerable<TaskCreationRequest> requests,
                                         string                           sessionId,
                                         string                           parentTaskId,
                                         CancellationToken                cancellationToken)
    => await TaskLifeCycleHelper.FinalizeTaskCreation(taskTable_,
                                                      resultTable_,
                                                      pushQueueStorage_,
                                                      requests.ToList(),
                                                      sessionId,
                                                      parentTaskId,
                                                      logger_,
                                                      cancellationToken)
                                .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<CreateSessionReply> CreateSession(IList<string>     partitionIds,
                                                      TaskOptions       defaultTaskOptions,
                                                      CancellationToken cancellationToken)
  {
    using var activity = activitySource_.StartActivity($"{nameof(CreateSession)}");
    if (!partitionIds.Any())
    {
      partitionIds.Add(submitterOptions_.DefaultPartition);
    }

    if (partitionIds.Count == 1 && string.IsNullOrEmpty(partitionIds.Single()))
    {
      partitionIds.Clear();
      partitionIds.Add(submitterOptions_.DefaultPartition);
    }

    if (!await partitionTable_.ArePartitionsExistingAsync(partitionIds,
                                                          cancellationToken)
                              .ConfigureAwait(false))
    {
      throw new PartitionNotFoundException("One of the partitions does not exist");
    }

    if (string.IsNullOrEmpty(defaultTaskOptions.PartitionId))
    {
      defaultTaskOptions = defaultTaskOptions with
                           {
                             PartitionId = submitterOptions_.DefaultPartition,
                           };
    }

    if (!await partitionTable_.ArePartitionsExistingAsync(new[]
                                                          {
                                                            defaultTaskOptions.PartitionId,
                                                          },
                                                          cancellationToken)
                              .ConfigureAwait(false))
    {
      throw new PartitionNotFoundException("The partition in the task options does not exist");
    }

    var sessionId = await sessionTable_.SetSessionDataAsync(partitionIds,
                                                            defaultTaskOptions,
                                                            cancellationToken)
                                       .ConfigureAwait(false);
    return new CreateSessionReply
           {
             SessionId = sessionId,
           };
  }

  /// <inheritdoc />
  public async Task TryGetResult(ResultRequest                    request,
                                 IServerStreamWriter<ResultReply> responseStream,
                                 CancellationToken                cancellationToken)
  {
    using var activity = activitySource_.StartActivity($"{nameof(TryGetResult)}");

    var result = await resultTable_.GetResult(request.Session,
                                              request.ResultId,
                                              cancellationToken)
                                   .ConfigureAwait(false);

    if (result.Status != ResultStatus.Completed)
    {
      var taskData = await taskTable_.ReadTaskAsync(result.OwnerTaskId,
                                                    cancellationToken)
                                     .ConfigureAwait(false);

      switch (taskData.Status)
      {
        case TaskStatus.Processed:
        case TaskStatus.Completed:
          break;
        case TaskStatus.Error:
        case TaskStatus.Timeout:
        case TaskStatus.Cancelled:
        case TaskStatus.Cancelling:
          await responseStream.WriteAsync(new ResultReply
                                          {
                                            Error = new TaskError
                                                    {
                                                      TaskId = taskData.TaskId,
                                                      Errors =
                                                      {
                                                        new Error
                                                        {
                                                          Detail     = taskData.Output.Error,
                                                          TaskStatus = taskData.Status,
                                                        },
                                                      },
                                                    },
                                          },
                                          CancellationToken.None)
                              .ConfigureAwait(false);
          return;
        case TaskStatus.Creating:
        case TaskStatus.Submitted:
        case TaskStatus.Dispatched:
        case TaskStatus.Processing:
          await responseStream.WriteAsync(new ResultReply
                                          {
                                            NotCompletedTask = taskData.TaskId,
                                          },
                                          CancellationToken.None)
                              .ConfigureAwait(false);
          return;

        case TaskStatus.Unspecified:
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    await foreach (var chunk in objectStorage_.GetValuesAsync(request.ResultId,
                                                              cancellationToken)
                                              .ConfigureAwait(false))
    {
      await responseStream.WriteAsync(new ResultReply
                                      {
                                        Result = new DataChunk
                                                 {
                                                   Data = UnsafeByteOperations.UnsafeWrap(new ReadOnlyMemory<byte>(chunk)),
                                                 },
                                      },
                                      CancellationToken.None)
                          .ConfigureAwait(false);
    }

    await responseStream.WriteAsync(new ResultReply
                                    {
                                      Result = new DataChunk
                                               {
                                                 DataComplete = true,
                                               },
                                    },
                                    CancellationToken.None)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<Count> WaitForCompletion(WaitRequest       request,
                                             CancellationToken cancellationToken)
  {
    using var activity = activitySource_.StartActivity($"{nameof(WaitForCompletion)}");

    if (logger_.IsEnabled(LogLevel.Trace))
    {
      cancellationToken.Register(() => logger_.LogTrace("CancellationToken from ServerCallContext has been triggered"));
    }


    Task<IEnumerable<TaskStatusCount>> CountUpdateFunc()
      => taskTable_.CountTasksAsync(request.Filter,
                                    cancellationToken);

    var output              = new Count();
    var countUpdateFunc     = CountUpdateFunc;
    var currentPollingDelay = taskTable_.PollingDelayMin;
    while (true)
    {
      var counts = await countUpdateFunc()
                     .ConfigureAwait(false);
      var notCompleted = 0;
      var error        = false;
      var cancelled    = false;

      // ReSharper disable once PossibleMultipleEnumeration
      foreach (var (status, count) in counts)
      {
        switch (status)
        {
          case TaskStatus.Creating:
            notCompleted += count;
            break;
          case TaskStatus.Submitted:
            notCompleted += count;
            break;
          case TaskStatus.Dispatched:
            notCompleted += count;
            break;
          case TaskStatus.Completed:
            break;
          case TaskStatus.Timeout:
            notCompleted += count;
            break;
          case TaskStatus.Cancelling:
            notCompleted += count;
            cancelled    =  true;
            break;
          case TaskStatus.Cancelled:
            notCompleted += count;
            cancelled    =  true;
            break;
          case TaskStatus.Processing:
            notCompleted += count;
            break;
          case TaskStatus.Error:
            notCompleted += count;
            error        =  true;
            break;
          case TaskStatus.Unspecified:
            notCompleted += count;
            break;
          case TaskStatus.Processed:
            notCompleted += count;
            break;
          default:
            throw new ArmoniKException($"Unknown TaskStatus {status}");
        }
      }

      if (notCompleted == 0 || (request.StopOnFirstTaskError && error) || (request.StopOnFirstTaskCancellation && cancelled))
      {
        // ReSharper disable once PossibleMultipleEnumeration
        output.Values.AddRange(counts.Select(tuple => new StatusCount
                                                      {
                                                        Count  = tuple.Count,
                                                        Status = tuple.Status,
                                                      }));
        logger_.LogDebug("All sub tasks have completed. Returning count={count}",
                         output);
        break;
      }


      await Task.Delay(currentPollingDelay,
                       cancellationToken)
                .ConfigureAwait(false);
      if (2 * currentPollingDelay < taskTable_.PollingDelayMax)
      {
        currentPollingDelay = 2 * currentPollingDelay;
      }
    }

    return output;
  }

  /// <inheritdoc />
  public async Task CompleteTaskAsync(TaskData          taskData,
                                      bool              resubmit,
                                      Output            output,
                                      CancellationToken cancellationToken = default)
  {
    using var activity = activitySource_.StartActivity($"{nameof(CompleteTaskAsync)}");

    Storage.Output cOutput = output;
    var taskDataEnd = taskData with
                      {
                        EndDate = DateTime.UtcNow,
                        CreationToEndDuration = DateTime.UtcNow   - taskData.CreationDate,
                        ProcessingToEndDuration = DateTime.UtcNow - taskData.StartDate,
                      };

    if (cOutput.Success)
    {
      await taskTable_.SetTaskSuccessAsync(taskDataEnd,
                                           cancellationToken)
                      .ConfigureAwait(false);

      logger_.LogInformation("Remove input payload of {task}",
                             taskData.TaskId);

      //Discard value is used to remove warnings CS4014 !!
      _ = Task.Factory.StartNew(async () => await objectStorage_.TryDeleteAsync(taskData.TaskId,
                                                                                CancellationToken.None)
                                                                .ConfigureAwait(false),
                                cancellationToken);
    }
    else
    {
      // TODO FIXME: nothing will resubmit the task if there is a crash there
      if (resubmit && taskData.RetryOfIds.Count < taskData.Options.MaxRetries)
      {
        // not done means that another pod put this task in retry so we do not need to do it a second time
        // so nothing to do
        if (!await taskTable_.SetTaskRetryAsync(taskDataEnd,
                                                cOutput.Error,
                                                cancellationToken)
                             .ConfigureAwait(false))
        {
          return;
        }

        logger_.LogWarning("Resubmit {task}",
                           taskData.TaskId);

        var newTaskId = await taskTable_.RetryTask(taskData,
                                                   cancellationToken)
                                        .ConfigureAwait(false);

        await FinalizeTaskCreation(new List<TaskCreationRequest>
                                   {
                                     new(newTaskId,
                                         taskData.PayloadId,
                                         taskData.Options,
                                         taskData.ExpectedOutputIds,
                                         taskData.DataDependencies),
                                   },
                                   taskData.SessionId,
                                   taskData.TaskId,
                                   cancellationToken)
          .ConfigureAwait(false);
      }
      else
      {
        // not done means that another pod put this task in error so we do not need to do it a second time
        // so nothing to do
        if (!await taskTable_.SetTaskErrorAsync(taskDataEnd,
                                                cOutput.Error,
                                                cancellationToken)
                             .ConfigureAwait(false))
        {
          return;
        }

        await ResultLifeCycleHelper.AbortTaskAndResults(taskTable_,
                                                        resultTable_,
                                                        taskData.TaskId,
                                                        CancellationToken.None)
                                   .ConfigureAwait(false);
      }
    }
  }

  /// <inheritdoc />
  public async Task<AvailabilityReply> WaitForAvailabilityAsync(ResultRequest     request,
                                                                CancellationToken contextCancellationToken)
  {
    using var activity = activitySource_.StartActivity($"{nameof(WaitForAvailabilityAsync)}");

    if (logger_.IsEnabled(LogLevel.Trace))
    {
      contextCancellationToken.Register(() => logger_.LogTrace("CancellationToken from ServerCallContext has been triggered"));
    }

    var currentPollingDelay = taskTable_.PollingDelayMin;
    while (true)
    {
      var result = await resultTable_.GetResult(request.Session,
                                                request.ResultId,
                                                contextCancellationToken)
                                     .ConfigureAwait(false);

      switch (result.Status)
      {
        case ResultStatus.Completed:
          return new AvailabilityReply
                 {
                   Ok = new Empty(),
                 };
        case ResultStatus.Created:
          break;
        case ResultStatus.Aborted:
          var taskData = await taskTable_.ReadTaskAsync(result.OwnerTaskId,
                                                        contextCancellationToken)
                                         .ConfigureAwait(false);

          return new AvailabilityReply
                 {
                   Error = new TaskError
                           {
                             TaskId = taskData.TaskId,
                             Errors =
                             {
                               new Error
                               {
                                 Detail     = taskData.Output.Error,
                                 TaskStatus = taskData.Status,
                               },
                             },
                           },
                 };
        case ResultStatus.Unspecified:
        default:
          throw new ArgumentOutOfRangeException();
      }

      await Task.Delay(currentPollingDelay,
                       contextCancellationToken)
                .ConfigureAwait(false);
      if (2 * currentPollingDelay < taskTable_.PollingDelayMax)
      {
        currentPollingDelay = 2 * currentPollingDelay;
      }
    }
  }

  /// <inheritdoc />
  public async Task SetResult(string                                 sessionId,
                              string                                 ownerTaskId,
                              string                                 key,
                              IAsyncEnumerable<ReadOnlyMemory<byte>> chunks,
                              CancellationToken                      cancellationToken)
  {
    using var activity = activitySource_.StartActivity($"{nameof(SetResult)}");

    await objectStorage_.AddOrUpdateAsync(key,
                                          chunks,
                                          cancellationToken)
                        .ConfigureAwait(false);

    await resultTable_.SetResult(sessionId,
                                 ownerTaskId,
                                 key,
                                 cancellationToken)
                      .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<ICollection<TaskCreationRequest>> CreateTasks(string                        sessionId,
                                                                  string                        parentTaskId,
                                                                  TaskOptions?                  options,
                                                                  IAsyncEnumerable<TaskRequest> taskRequests,
                                                                  CancellationToken             cancellationToken)
  {
    var sessionData = await sessionTable_.GetSessionAsync(sessionId,
                                                          cancellationToken)
                                         .ConfigureAwait(false);

    options = TaskLifeCycleHelper.ValidateSession(sessionData,
                                                  options,
                                                  sessionId,
                                                  pushQueueStorage_.MaxPriority,
                                                  logger_,
                                                  cancellationToken);

    using var logFunction = logger_.LogFunction(parentTaskId);
    using var activity    = activitySource_.StartActivity($"{nameof(CreateTasks)}");
    using var sessionScope = logger_.BeginPropertyScope(("Session", sessionData.SessionId),
                                                        ("TaskId", parentTaskId),
                                                        ("PartitionId", options.PartitionId));

    var requests           = new List<TaskCreationRequest>();
    var payloadUploadTasks = new List<Task>();

    await foreach (var taskRequest in taskRequests.WithCancellation(cancellationToken)
                                                  .ConfigureAwait(false))
    {
      var taskId = Guid.NewGuid()
                       .ToString();

      requests.Add(new TaskCreationRequest(taskId,
                                           taskId,
                                           options,
                                           taskRequest.ExpectedOutputKeys.ToList(),
                                           taskRequest.DataDependencies.ToList()));
      payloadUploadTasks.Add(objectStorage_.AddOrUpdateAsync(taskId,
                                                             taskRequest.PayloadChunks,
                                                             cancellationToken));
    }

    await payloadUploadTasks.WhenAll()
                            .ConfigureAwait(false);

    await TaskLifeCycleHelper.CreateTasks(taskTable_,
                                          resultTable_,
                                          sessionId,
                                          parentTaskId,
                                          requests,
                                          logger_,
                                          cancellationToken)
                             .ConfigureAwait(false);

    return requests;
  }
}
