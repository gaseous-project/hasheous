FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
ARG TARGETARCH
ARG BUILDPLATFORM
WORKDIR /App
EXPOSE 80

RUN echo "Target: $TARGETARCH"
RUN echo "Build: $BUILDPLATFORM"

# Copy everything
COPY . ./

# Build Hasheous Task Runner
# Restore as distinct layers
RUN dotnet restore "hasheous-taskrunner/hasheous-taskrunner.csproj" -a $TARGETARCH
# Build and publish a release
RUN dotnet publish "hasheous-taskrunner/hasheous-taskrunner.csproj" --use-current-runtime --self-contained true -c Release -o out -a $TARGETARCH

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
ENV INDOCKER=1
WORKDIR /App

RUN apt update && apt upgrade -y

COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "hasheous-taskrunner.dll"]
