FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App
EXPOSE 80

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore "hasheous/hasheous.csproj"
# Build and publish a release
RUN dotnet publish "hasheous/hasheous.csproj" --use-current-runtime --self-contained false -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App
COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "hasheous.dll"]
