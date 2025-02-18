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

using Google.Protobuf.WellKnownTypes;

namespace ArmoniK.Core.Common.Storage;

public record TaskOptions(IDictionary<string, string> Options,
                          TimeSpan                    MaxDuration,
                          int                         MaxRetries,
                          int                         Priority,
                          string                      PartitionId,
                          string                      ApplicationName,
                          string                      ApplicationVersion,
                          string                      ApplicationNamespace,
                          string                      ApplicationService,
                          string                      EngineType)
{
  public static TaskOptions Merge(TaskOptions? taskOption,
                                  TaskOptions  defaultOption)
  {
    if (taskOption is null)
    {
      return defaultOption;
    }

    var options = new Dictionary<string, string>(defaultOption.Options);
    foreach (var option in taskOption.Options)
    {
      options[option.Key] = option.Value;
    }

    return new TaskOptions(options,
                           taskOption.MaxDuration == TimeSpan.Zero
                             ? taskOption.MaxDuration
                             : defaultOption.MaxDuration,
                           taskOption.MaxRetries == 0
                             ? taskOption.MaxRetries
                             : defaultOption.MaxRetries,
                           taskOption.Priority,
                           taskOption.PartitionId != string.Empty
                             ? taskOption.PartitionId
                             : defaultOption.PartitionId,
                           taskOption.ApplicationName != string.Empty
                             ? taskOption.ApplicationName
                             : defaultOption.ApplicationName,
                           taskOption.ApplicationVersion != string.Empty
                             ? taskOption.ApplicationVersion
                             : defaultOption.ApplicationVersion,
                           taskOption.ApplicationNamespace != string.Empty
                             ? taskOption.ApplicationNamespace
                             : defaultOption.ApplicationNamespace,
                           taskOption.ApplicationService != string.Empty
                             ? taskOption.ApplicationService
                             : defaultOption.ApplicationService,
                           taskOption.EngineType != string.Empty
                             ? taskOption.EngineType
                             : defaultOption.EngineType);
  }
}

public static class GrpcTaskOptionsExt
{
  public static TaskOptions ToTaskOptions(this Api.gRPC.V1.TaskOptions taskOption)
    => new(taskOption.Options,
           taskOption.MaxDuration.ToTimeSpan(),
           taskOption.MaxRetries,
           taskOption.Priority,
           taskOption.PartitionId,
           taskOption.ApplicationName,
           taskOption.ApplicationVersion,
           taskOption.ApplicationNamespace,
           taskOption.ApplicationService,
           taskOption.EngineType);

  public static TaskOptions? ToNullableTaskOptions(this Api.gRPC.V1.TaskOptions? taskOption)
    => taskOption?.ToTaskOptions();
}

public static class TaskOptionsExt
{
  public static Api.gRPC.V1.TaskOptions ToGrpcTaskOptions(this TaskOptions taskOption)
    => new()
       {
         MaxDuration          = Duration.FromTimeSpan(taskOption.MaxDuration),
         ApplicationName      = taskOption.ApplicationName,
         ApplicationVersion   = taskOption.ApplicationVersion,
         ApplicationNamespace = taskOption.ApplicationNamespace,
         ApplicationService   = taskOption.ApplicationService,
         EngineType           = taskOption.EngineType,
         MaxRetries           = taskOption.MaxRetries,
         Options =
         {
           taskOption.Options,
         },
         Priority    = taskOption.Priority,
         PartitionId = taskOption.PartitionId,
       };
}
