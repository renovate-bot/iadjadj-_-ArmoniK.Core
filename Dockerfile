FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG VERSION=1.0.0.0

WORKDIR /src
# git ls-tree -r HEAD --name-only --full-tree | grep "csproj$" | xargs -I % sh -c "export D=\$(dirname %) ; echo COPY [\\\"%\\\", \\\"\$D/\\\"]"
COPY ["Adaptors/Amqp/src/ArmoniK.Core.Adapters.Amqp.csproj", "Adaptors/Amqp/src/"]
COPY ["Adaptors/LocalStorage/src/ArmoniK.Core.Adapters.LocalStorage.csproj", "Adaptors/LocalStorage/src/"]
COPY ["Adaptors/Memory/src/ArmoniK.Core.Adapters.Memory.csproj", "Adaptors/Memory/src/"]
COPY ["Adaptors/MongoDB/src/ArmoniK.Core.Adapters.MongoDB.csproj", "Adaptors/MongoDB/src/"]
COPY ["Adaptors/QueueCommon/src/ArmoniK.Core.Adapters.QueueCommon.csproj", "Adaptors/QueueCommon/src/"]
COPY ["Adaptors/RabbitMQ/src/ArmoniK.Core.Adapters.RabbitMQ.csproj", "Adaptors/RabbitMQ/src/"]
COPY ["Adaptors/Redis/src/ArmoniK.Core.Adapters.Redis.csproj", "Adaptors/Redis/src/"]
COPY ["Adaptors/S3/src/ArmoniK.Core.Adapters.S3.csproj", "Adaptors/S3/src/"]
COPY ["Base/src/ArmoniK.Core.Base.csproj", "Base/src/"]
COPY ["Common/src/ArmoniK.Core.Common.csproj", "Common/src/"]
COPY ["Compute/PollingAgent/src/ArmoniK.Core.Compute.PollingAgent.csproj", "Compute/PollingAgent/src/"]
COPY ["Control/Metrics/src/ArmoniK.Core.Control.Metrics.csproj", "Control/Metrics/src/"]
COPY ["Control/PartitionMetrics/src/ArmoniK.Core.Control.PartitionMetrics.csproj", "Control/PartitionMetrics/src/"]
COPY ["Control/Submitter/src/ArmoniK.Core.Control.Submitter.csproj", "Control/Submitter/src/"]
COPY ["Utils/src/ArmoniK.Core.Utils.csproj", "Utils/src/"]

RUN dotnet restore "Compute/PollingAgent/src/ArmoniK.Core.Compute.PollingAgent.csproj"
RUN dotnet restore "Control/Metrics/src/ArmoniK.Core.Control.Metrics.csproj"
RUN dotnet restore "Control/PartitionMetrics/src/ArmoniK.Core.Control.PartitionMetrics.csproj"
RUN dotnet restore "Control/Submitter/src/ArmoniK.Core.Control.Submitter.csproj"

# git ls-tree -r HEAD --name-only --full-tree | grep "csproj$" | xargs -I % sh -c "export D=\$(dirname %) ; echo COPY [\\\"\$D\\\", \\\"\$D\\\"]"
COPY ["Adaptors/Amqp/src", "Adaptors/Amqp/src"]
COPY ["Adaptors/LocalStorage/src", "Adaptors/LocalStorage/src"]
COPY ["Adaptors/Memory/src", "Adaptors/Memory/src"]
COPY ["Adaptors/MongoDB/src", "Adaptors/MongoDB/src"]
COPY ["Adaptors/QueueCommon/src", "Adaptors/QueueCommon/src"]
COPY ["Adaptors/RabbitMQ/src", "Adaptors/RabbitMQ/src"]
COPY ["Adaptors/Redis/src", "Adaptors/Redis/src"]
COPY ["Adaptors/S3/src", "Adaptors/S3/src"]
COPY ["Base/src", "Base/src"]
COPY ["Common/src", "Common/src"]
COPY ["Compute/PollingAgent/src", "Compute/PollingAgent/src"]
COPY ["Control/Metrics/src", "Control/Metrics/src"]
COPY ["Control/PartitionMetrics/src", "Control/PartitionMetrics/src"]
COPY ["Control/Submitter/src", "Control/Submitter/src"]
COPY ["Utils/src", "Utils/src"]

WORKDIR /src/Adaptors/Amqp/src
RUN dotnet build -c Release --no-restore "ArmoniK.Core.Adapters.Amqp.csproj"
RUN dotnet publish "ArmoniK.Core.Adapters.Amqp.csproj" -c Release -o /app/publish/amqp /p:UseAppHost=false -p:PackageVersion=$VERSION -p:Version=$VERSION

WORKDIR /src/Adaptors/RabbitMQ/src
RUN dotnet build -c Release --no-restore "ArmoniK.Core.Adapters.RabbitMQ.csproj"
RUN dotnet publish "ArmoniK.Core.Adapters.RabbitMQ.csproj" -c Release -o /app/publish/rabbit /p:UseAppHost=false -p:PackageVersion=$VERSION -p:Version=$VERSION

WORKDIR /src/Compute/PollingAgent/src
RUN dotnet build -c Release --no-restore "ArmoniK.Core.Compute.PollingAgent.csproj"
RUN dotnet publish "ArmoniK.Core.Compute.PollingAgent.csproj" -c Release -o /app/publish/polling_agent /p:UseAppHost=false -p:PackageVersion=$VERSION -p:Version=$VERSION

WORKDIR /src/Control/Metrics/src
RUN dotnet build -c Release --no-restore "ArmoniK.Core.Control.Metrics.csproj"
RUN dotnet publish "ArmoniK.Core.Control.Metrics.csproj" -c Release -o /app/publish/metrics /p:UseAppHost=false -p:PackageVersion=$VERSION -p:Version=$VERSION

WORKDIR /src/Control/PartitionMetrics/src
RUN dotnet build -c Release --no-restore "ArmoniK.Core.Control.PartitionMetrics.csproj"
RUN dotnet publish "ArmoniK.Core.Control.PartitionMetrics.csproj" -c Release -o /app/publish/partition_metrics /p:UseAppHost=false -p:PackageVersion=$VERSION -p:Version=$VERSION

WORKDIR /src/Control/Submitter/src
RUN dotnet build -c Release --no-restore "ArmoniK.Core.Control.Submitter.csproj"
RUN dotnet publish "ArmoniK.Core.Control.Submitter.csproj" -c Release -o /app/publish/submitter /p:UseAppHost=false -p:PackageVersion=$VERSION -p:Version=$VERSION
