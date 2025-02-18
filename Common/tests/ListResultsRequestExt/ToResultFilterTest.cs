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

using ArmoniK.Api.gRPC.V1;
using ArmoniK.Api.gRPC.V1.Results;

using Armonik.Api.Grpc.V1.SortDirection;

using ArmoniK.Core.Common.gRPC;
using ArmoniK.Core.Common.Storage;

using NUnit.Framework;

using static Google.Protobuf.WellKnownTypes.Timestamp;

namespace ArmoniK.Core.Common.Tests.ListResultsRequestExt;

[TestFixture(TestOf = typeof(ToResultFilterTest))]
public class ToResultFilterTest
{
  private readonly Result result_ = new("SessionId",
                                        "ResultId",
                                        "Name",
                                        "OwnerTaskId",
                                        ResultStatus.Created,
                                        new List<string>(),
                                        DateTime.UtcNow,
                                        Array.Empty<byte>());

  private static readonly ListResultsRequest.Types.Sort Sort = new()
                                                               {
                                                                 Field = new ResultField
                                                                         {
                                                                           ResultRawField = ResultRawField.CreatedAt,
                                                                         },
                                                                 Direction = SortDirection.Asc,
                                                               };

  [Test]
  public void FilterStatusShouldSucceed()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            Status = ResultStatus.Created,
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsTrue(func.Invoke(result_));
  }

  [Test]
  public void FilterStatusShouldFail()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            Status = ResultStatus.Aborted,
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsFalse(func.Invoke(result_));
  }

  [Test]
  public void FilterSessionIdShouldSucceed()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            SessionId = "SessionId",
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsTrue(func.Invoke(result_));
  }

  [Test]
  public void FilterSessionIdShouldFail()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            SessionId = "BadSessionId",
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsFalse(func.Invoke(result_));
  }

  [Test]
  public void FilterNameShouldSucceed()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            Name = "Name",
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsTrue(func.Invoke(result_));
  }

  [Test]
  public void FilterNameShouldFail()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            Name = "BadName",
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsFalse(func.Invoke(result_));
  }

  [Test]
  public void FilterOwnerTaskIdShouldSucceed()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            OwnerTaskId = "OwnerTaskId",
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsTrue(func.Invoke(result_));
  }

  [Test]
  public void FilterOwnerTaskIdShouldFail()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            OwnerTaskId = "BadOwnerTaskId",
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsFalse(func.Invoke(result_));
  }

  [Test]
  public void FilterCreatedBeforeShouldSucceed()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            CreatedBefore = FromDateTime(DateTime.UtcNow),
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsTrue(func.Invoke(result_));
  }

  [Test]
  public void FilterCreatedBeforeShouldFail()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            CreatedBefore = FromDateTime(DateTime.UtcNow),
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsFalse(func.Invoke(result_ with
                               {
                                 CreationDate = DateTime.UtcNow + TimeSpan.FromHours(3),
                               }));
  }

  [Test]
  public void FilterCreatedAfterShouldSucceed()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            CreatedAfter = FromDateTime(DateTime.UtcNow),
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsTrue(func.Invoke(result_ with
                              {
                                CreationDate = DateTime.UtcNow + TimeSpan.FromHours(3),
                              }));
  }

  [Test]
  public void FilterCreatedAfterShouldFail()
  {
    var func = new ListResultsRequest
               {
                 Filter = new ListResultsRequest.Types.Filter
                          {
                            CreatedAfter = FromDateTime(DateTime.UtcNow),
                          },
                 Sort = Sort,
               }.Filter.ToResultFilter()
                .Compile();

    Assert.IsFalse(func.Invoke(result_));
  }
}
