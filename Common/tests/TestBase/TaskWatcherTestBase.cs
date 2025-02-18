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
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Core.Base;
using ArmoniK.Core.Common.Storage;
using ArmoniK.Core.Common.Storage.Events;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using NUnit.Framework;

using TaskStatus = ArmoniK.Api.gRPC.V1.TaskStatus;

namespace ArmoniK.Core.Common.Tests.TestBase;

[TestFixture]
public class TaskWatcherTestBase
{
  [SetUp]
  public async Task SetUp()
  {
    GetInstance();

    if (!RunTests || CheckForSkipSetup())
    {
      return;
    }

    await TaskTable!.Init(CancellationToken.None)
                    .ConfigureAwait(false);

    await TaskTable!.CreateTasks(new[]
                                 {
                                   taskCompletedData_,
                                   taskCreatingData_,
                                   TaskProcessingData,
                                   taskProcessingData2_,
                                   TaskSubmittedData,
                                   taskFailedData_,
                                 })
                    .ConfigureAwait(false);
  }

  [TearDown]
  public virtual void TearDown()
  {
    TaskTable   = null;
    TaskWatcher = null;
    RunTests    = false;
  }

  private static readonly TaskOptions Options = new(new Dictionary<string, string>
                                                    {
                                                      {
                                                        "key1", "val1"
                                                      },
                                                      {
                                                        "key2", "val2"
                                                      },
                                                    },
                                                    TimeSpan.MaxValue,
                                                    5,
                                                    1,
                                                    "part1",
                                                    "applicationName",
                                                    "applicationVersion",
                                                    "applicationNamespace",
                                                    "applicationService",
                                                    "engineType");

  private static readonly TaskData TaskSubmittedData = new("SessionId",
                                                           "TaskSubmittedId",
                                                           "",
                                                           "",
                                                           "PayloadId",
                                                           new[]
                                                           {
                                                             "parent1",
                                                           },
                                                           new[]
                                                           {
                                                             "dependency1",
                                                           },
                                                           new[]
                                                           {
                                                             "output1",
                                                           },
                                                           Array.Empty<string>(),
                                                           TaskStatus.Submitted,
                                                           Options with
                                                           {
                                                             PartitionId = "part2",
                                                           },
                                                           new Output(false,
                                                                      ""));

  private readonly TaskData taskCompletedData_ = new("SessionId",
                                                     "TaskCompletedId",
                                                     "OwnerPodId",
                                                     "OwnerPodName",
                                                     "PayloadId",
                                                     new[]
                                                     {
                                                       "parent1",
                                                     },
                                                     new[]
                                                     {
                                                       "dependency1",
                                                     },
                                                     new[]
                                                     {
                                                       "output1",
                                                       "output2",
                                                     },
                                                     Array.Empty<string>(),
                                                     TaskStatus.Completed,
                                                     Options,
                                                     new Output(true,
                                                                ""));

  private readonly TaskData taskCreatingData_ = new("SessionId",
                                                    "TaskCreatingId",
                                                    "OwnerPodId",
                                                    "OwnerPodName",
                                                    "PayloadId",
                                                    new[]
                                                    {
                                                      "parent1",
                                                    },
                                                    new[]
                                                    {
                                                      "dependency1",
                                                    },
                                                    new[]
                                                    {
                                                      "output1",
                                                    },
                                                    Array.Empty<string>(),
                                                    TaskStatus.Creating,
                                                    Options,
                                                    new Output(false,
                                                               ""));

  private static readonly TaskData TaskProcessingData = new("SessionId",
                                                            "TaskProcessingId",
                                                            "OwnerPodId",
                                                            "OwnerPodName",
                                                            "PayloadId",
                                                            new[]
                                                            {
                                                              "parent1",
                                                            },
                                                            new[]
                                                            {
                                                              "dependency1",
                                                            },
                                                            new[]
                                                            {
                                                              "output1",
                                                            },
                                                            Array.Empty<string>(),
                                                            TaskStatus.Processing,
                                                            Options,
                                                            new Output(false,
                                                                       ""));

  private readonly TaskData taskProcessingData2_ = new("SessionId",
                                                       "TaskAnotherProcessingId",
                                                       "OwnerPodId",
                                                       "OwnerPodName",
                                                       "PayloadId",
                                                       new[]
                                                       {
                                                         "parent1",
                                                       },
                                                       new[]
                                                       {
                                                         "dependency1",
                                                       },
                                                       new[]
                                                       {
                                                         "output1",
                                                       },
                                                       Array.Empty<string>(),
                                                       TaskStatus.Processing,
                                                       Options,
                                                       new Output(false,
                                                                  ""));

  private readonly TaskData taskFailedData_ = new("SessionId",
                                                  "TaskFailedId",
                                                  "OwnerPodId",
                                                  "OwnerPodName",
                                                  "PayloadId",
                                                  new[]
                                                  {
                                                    "parent1",
                                                  },
                                                  new[]
                                                  {
                                                    "dependency1",
                                                  },
                                                  new[]
                                                  {
                                                    "output1",
                                                  },
                                                  Array.Empty<string>(),
                                                  TaskStatus.Error,
                                                  Options,
                                                  new Output(false,
                                                             "sad task"));

  private static bool CheckForSkipSetup()
  {
    var category = TestContext.CurrentContext.Test.Properties.Get("Category") as string;
    return category is "SkipSetUp";
  }


  /* Interface to test */
  protected ITaskTable?   TaskTable;
  protected ITaskWatcher? TaskWatcher;

  /* Boolean to control that tests are executed in
   * an instance of this class */
  protected bool RunTests;

  /* Function be override so it returns the suitable instance
   * of ResultTable and ResultWatcher to the corresponding interface implementation */
  public virtual void GetInstance()
  {
  }

  [Test]
  [Category("SkipSetUp")]
  public async Task InitShouldSucceed()
  {
    if (RunTests)
    {
      Assert.AreNotEqual(HealthStatus.Healthy,
                         (await TaskWatcher!.Check(HealthCheckTag.Liveness)
                                            .ConfigureAwait(false)).Status);
      Assert.AreNotEqual(HealthStatus.Healthy,
                         (await TaskWatcher.Check(HealthCheckTag.Readiness)
                                           .ConfigureAwait(false)).Status);
      Assert.AreNotEqual(HealthStatus.Healthy,
                         (await TaskWatcher.Check(HealthCheckTag.Startup)
                                           .ConfigureAwait(false)).Status);

      await TaskWatcher.Init(CancellationToken.None)
                       .ConfigureAwait(false);

      Assert.AreEqual(HealthStatus.Healthy,
                      (await TaskWatcher.Check(HealthCheckTag.Liveness)
                                        .ConfigureAwait(false)).Status);
      Assert.AreEqual(HealthStatus.Healthy,
                      (await TaskWatcher.Check(HealthCheckTag.Readiness)
                                        .ConfigureAwait(false)).Status);
      Assert.AreEqual(HealthStatus.Healthy,
                      (await TaskWatcher.Check(HealthCheckTag.Startup)
                                        .ConfigureAwait(false)).Status);
    }
  }

  /// <summary>
  ///   This method produces the events (new task and status update)
  ///   that will be used to test the ITaskWatcher interface.
  /// </summary>
  /// <param name="taskTable">Task table interface</param>
  /// <param name="cancellationToken">Token used to cancel the execution of the method</param>
  /// <returns>
  ///   Task representing the asynchronous execution of the method
  /// </returns>
  private static async Task ProduceEvents(ITaskTable        taskTable,
                                          CancellationToken cancellationToken)
  {
    await taskTable.CreateTasks(new[]
                                {
                                  new TaskData("SessionId",
                                               "TaskEventCreating1",
                                               "OwnerPodId",
                                               "OwnerPodName",
                                               "PayloadId",
                                               new[]
                                               {
                                                 "parent1",
                                               },
                                               new[]
                                               {
                                                 "dependency1",
                                               },
                                               new[]
                                               {
                                                 "output1",
                                                 "output2",
                                               },
                                               Array.Empty<string>(),
                                               TaskStatus.Creating,
                                               Options,
                                               new Output(true,
                                                          "")),
                                  new TaskData("SessionId",
                                               "TaskEventCreating2",
                                               "OwnerPodId",
                                               "OwnerPodName",
                                               "PayloadId",
                                               new[]
                                               {
                                                 "parent1",
                                               },
                                               new[]
                                               {
                                                 "dependency1",
                                               },
                                               new[]
                                               {
                                                 "output1",
                                                 "output2",
                                               },
                                               Array.Empty<string>(),
                                               TaskStatus.Creating,
                                               Options,
                                               new Output(true,
                                                          "")),
                                },
                                cancellationToken)
                   .ConfigureAwait(false);

    await taskTable.SetTaskErrorAsync(TaskProcessingData,
                                      "Testing SetTaskError",
                                      CancellationToken.None)
                   .ConfigureAwait(false);

    await taskTable.StartTask(TaskSubmittedData,
                              CancellationToken.None)
                   .ConfigureAwait(false);

    await taskTable.CancelTaskAsync(new List<string>
                                    {
                                      "TaskSubmittedId",
                                    },
                                    CancellationToken.None)
                   .ConfigureAwait(false);
  }

  [Test]
  public async Task WatchNewResultShouldSucceed()
  {
    if (RunTests)
    {
      var cts = new CancellationTokenSource();

      var watchEnumerator = await TaskWatcher!.GetNewTasks("SessionId",
                                                           cts.Token)
                                              .ConfigureAwait(false);

      await ProduceEvents(TaskTable!,
                          cts.Token)
        .ConfigureAwait(false);

      cts.CancelAfter(TimeSpan.FromSeconds(1));

      var newResults = new List<NewTask>();
      Assert.ThrowsAsync<OperationCanceledException>(async () =>
                                                     {
                                                       await foreach (var cur in watchEnumerator.WithCancellation(cts.Token)
                                                                                                .ConfigureAwait(false))
                                                       {
                                                         Console.WriteLine(cur);
                                                         newResults.Add(cur);
                                                       }
                                                     });

      Assert.AreEqual(2,
                      newResults.Count);
    }
  }

  [Test]
  public async Task WatchResultStatusUpdateShouldSucceed()
  {
    if (RunTests)
    {
      var cts = new CancellationTokenSource();

      var watchEnumerator = await TaskWatcher!.GetTaskStatusUpdates("SessionId",
                                                                    cts.Token)
                                              .ConfigureAwait(false);

      await ProduceEvents(TaskTable!,
                          cts.Token)
        .ConfigureAwait(false);

      cts.CancelAfter(TimeSpan.FromSeconds(2));

      var newResults = new List<TaskStatusUpdate>();
      Assert.ThrowsAsync<OperationCanceledException>(async () =>
                                                     {
                                                       await foreach (var cur in watchEnumerator.WithCancellation(cts.Token)
                                                                                                .ConfigureAwait(false))
                                                       {
                                                         Console.WriteLine(cur);
                                                         newResults.Add(cur);
                                                       }
                                                     });

      Assert.AreEqual(3,
                      newResults.Count);
    }
  }
}
