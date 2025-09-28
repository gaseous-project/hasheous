FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0-noble

# update apt-get
RUN apt-get update && curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
RUN apt update && apt upgrade -y && apt install default-jdk nodejs wget mariadb-client git -y && apt-get clean