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

using ArmoniK.Api.gRPC.V1;
using ArmoniK.Api.gRPC.V1.Worker;
using ArmoniK.Core.Base;
using ArmoniK.Core.Common.Exceptions;
using ArmoniK.Core.Common.Storage;

using Google.Protobuf;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ArmoniK.Core.Common.Pollster;

/// <summary>
///   Prefetch data needed to execute a task
/// </summary>
public class DataPrefetcher : IInitializable
{
  private readonly ActivitySource?         activitySource_;
  private readonly ILogger<DataPrefetcher> logger_;
  private readonly IObjectStorage          objectStorage_;

  private bool isInitialized_;

  /// <summary>
  ///   Create data prefetcher for tasks
  /// </summary>
  /// <param name="objectStorage">Interface to manage data</param>
  /// <param name="activitySource">Activity source for tracing</param>
  /// <param name="logger">Logger used to print logs</param>
  public DataPrefetcher(IObjectStorage          objectStorage,
                        ActivitySource?         activitySource,
                        ILogger<DataPrefetcher> logger)
  {
    objectStorage_  = objectStorage;
    logger_         = logger;
    activitySource_ = activitySource;
  }

  /// <inheritdoc />
  public Task<HealthCheckResult> Check(HealthCheckTag tag)
    => Task.FromResult(isInitialized_
                         ? HealthCheckResult.Healthy()
                         : HealthCheckResult.Unhealthy());

  /// <inheritdoc />
  public async Task Init(CancellationToken cancellationToken)
  {
    if (!isInitialized_)
    {
      await objectStorage_.Init(cancellationToken)
                          .ConfigureAwait(false);
      isInitialized_ = true;
    }
  }

  /// <summary>
  ///   Method used to prefetch data before executing a task
  /// </summary>
  /// <param name="taskData">Task metadata</param>
  /// <param name="cancellationToken">Token used to cancel the execution of the method</param>
  /// <returns>
  ///   Queue containing the request containing the data for the task which can be sent to the worker
  /// </returns>
  /// <exception cref="ObjectDataNotFoundException">input data are not found</exception>
  /// <exception cref="InvalidOperationException">invalid transition between states</exception>
  public async Task<Queue<ProcessRequest.Types.ComputeRequest>> PrefetchDataAsync(TaskData          taskData,
                                                                                  CancellationToken cancellationToken)
  {
    using var activity = activitySource_?.StartActivity();

    activity?.AddEvent(new ActivityEvent("Load payload"));

    var payloadChunks = await objectStorage_.GetValuesAsync(taskData.PayloadId,
                                                            cancellationToken)
                                            .Select(bytes => UnsafeByteOperations.UnsafeWrap(bytes))
                                            .ToListAsync(cancellationToken)
                                            .ConfigureAwait(false);

    var computeRequests = new ComputeRequestQueue(logger_);
    computeRequests.Init(PayloadConfiguration.MaxChunkSize,
                         taskData.SessionId,
                         taskData.TaskId,
                         taskData.Options.ToGrpcTaskOptions(),
                         payloadChunks.FirstOrDefault(),
                         taskData.ExpectedOutputIds);

    for (var i = 1; i < payloadChunks.Count; i++)
    {
      computeRequests.AddPayloadChunk(payloadChunks[i]);
    }

    computeRequests.CompletePayload();

    foreach (var dataDependency in taskData.DataDependencies)
    {
      var dependencyChunks = await objectStorage_.GetValuesAsync(dataDependency,
                                                                 cancellationToken)
                                                 .Select(bytes => UnsafeByteOperations.UnsafeWrap(bytes))
                                                 .ToListAsync(cancellationToken)
                                                 .ConfigureAwait(false);

      computeRequests.InitDataDependency(dataDependency);
      foreach (var chunk in dependencyChunks)
      {
        computeRequests.AddDataDependencyChunk(chunk);
      }

      computeRequests.CompleteDataDependency();
    }

    return computeRequests.GetQueue();
  }
}
