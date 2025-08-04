FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
ARG TARGETARCH
ARG BUILDPLATFORM
WORKDIR /App
EXPOSE 80

RUN echo "Target: $TARGETARCH"
RUN echo "Build: $BUILDPLATFORM"

# Copy everything
COPY . ./

# Build Hasheous Service Orchestrator
# Restore as distinct layers
RUN dotnet restore "service-orchestrator/service-orchestrator.csproj" -a $TARGETARCH
# Build and publish a release
RUN dotnet publish "service-orchestrator/service-orchestrator.csproj" --use-current-runtime --self-contained true -c Release -o out -a $TARGETARCH

# Build Hasheous Service Host
# Restore as distinct layers
RUN dotnet restore "service-host/service-host.csproj" -a $TARGETARCH
# Build and publish a release
RUN dotnet publish "service-host/service-host.csproj" --use-current-runtime --self-contained true -c Release -o out -a $TARGETARCH

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
ENV INDOCKER=1
WORKDIR /App

RUN apt update && apt upgrade -y && apt install mariadb-client -y

COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "service-orchestrator.dll"]
