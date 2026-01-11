FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
ARG TARGETARCH
ARG BUILDPLATFORM
WORKDIR /App

RUN echo "Target: $TARGETARCH"
RUN echo "Build: $BUILDPLATFORM"

# Copy everything
COPY . ./

# Build Hasheous Task Runner
# Restore as distinct layers
RUN dotnet restore "hasheous-taskrunner/hasheous-taskrunner.csproj" -a $TARGETARCH
# Build and publish a release (self-contained with single-file)
RUN dotnet publish "hasheous-taskrunner/hasheous-taskrunner.csproj" --self-contained true -c Release -o out -a $TARGETARCH

# Build runtime image - use minimal base since we have a self-contained executable
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0
ENV INDOCKER=1
WORKDIR /App

RUN apt update && apt upgrade -y

COPY --from=build-env /App/out .
ENTRYPOINT ["./hasheous-taskrunner"]
