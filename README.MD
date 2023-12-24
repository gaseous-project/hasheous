# Hasheous Server

This project aims to match the content of DAT's like those provided by TOSEC to metadata provided by various vendors (IGDB only at present), to simplify identifying ROMs and other images when adding to ROM managers such as Hasheous's sister project Gaseous (https://github.com/gaseous-project/gaseous-server)

**Note:** this project is very much in the early stages of development and should not be used for production purposes.

**Extra Note:** Do not expose this server to the internet without taking proper precautions.

## Requirements
* MariaDB 11.1.2 or MySQL Server 8+
  * These are the database versions Hasheous has been tested and developed against. Your mileage may vary with earlier versions.
  * Currently MariaDB is the preferred database server.
* Internet Game Database API Key. See: https://api-docs.igdb.com/#account-creation

## Third Party Projects
The following projects are used by Hasheous
* https://dotnet.microsoft.com/en-us/apps/aspnet
* https://github.com/JamesNK/Newtonsoft.Json
* https://www.nuget.org/packages/MySql.Data/8.0.32.1
* https://github.com/kamranayub/igdb-dotnet

## Discord Server
[![Join our Discord server!](https://invite.casperiv.dev/?inviteCode=Nhu7wpT3k4&format=svg)](https://discord.gg/Nhu7wpT3k4)

## Configuration File
When Hasheous-Server is started for the first time, it creates a configuration file at ~/.hasheous-server/config.json if it doesn't exist. Some values can be filled in using environment variables (such as in the case of using docker).

### DatabaseConfiguration
| Attribute | Environment Variable |
| --------- | -------------------- |
| HostName  | dbhost               |
| UserName  | dbuser               |
| Password  | dbpass               |

### IGDBConfiguration
| Attribute | Environment Variable |
| --------- | -------------------- |
| ClientId  | igdbclientid         |
| Secret.   | igdbclientsecret     |

### config.json
```json
{
  "DatabaseConfiguration": {
    "HostName": "localhost",
    "UserName": "hasheous",
    "Password": "hasheous",
    "DatabaseName": "hasheous",
    "Port": 3306
  },
  "IGDBConfiguration": {
    "ClientId": "<clientid>",
    "Secret": "<secret>"
  },
  "LoggingConfiguration": {
    "DebugLogging": false,
    "LogRetention": 7
  }
}
```

## Docker
### Deploy with the prebuilt Docker image
Dockerfile and docker-compose.yml files have been provided to make deployment of the server as easy as possible.
1. Download the docker-compose.yml file.
2. Open the docker-compose.yml file and edit the igdbclientid and igdbclientsecret to the values retrieved from your IGDB account
3. Run the command ```docker-compose up -d```
4. Connect to the host on port 7157

### Build and deploy a Docker image from source
Dockerfile and docker-compose-build.yml files have been provided to make deployment of the server as easy as possible.
1. Clone the repo with ```git clone https://github.com/gaseous-project/hasheous.git```
2. Change into the hasheous directory
3. Open the docker-compose--build.yml file and edit the igdbclientid and igdbclientsecret to the values retrieved from your IGDB account
4. Run the command ```docker-compose --file docker-compose-build.yml up -d```
5. Connect to the host on port 7157

# Adding Content
Currently supported DAT's:
* TOSEC: https://www.tosecdev.org/downloads/category/56-2023-01-23
* MAME Arcade and MAME Mess: https://www.progettosnaps.net/dats/MAME

If there are other DAT's you'd like to see support for, please raise an issue with a link to the DAT's.

## Adding signature DAT files
### TOSEC
1. Download the DAT files from the source website. For example; from https://www.tosecdev.org/downloads/category/56-2023-01-23
2. Extract the archive
3. Copy the DAT files to ~/.hasheous-server/Data/Signatures/TOSEC/

### MAME Arcade
1. Download the DAT files from the source website. For example; from https://www.progettosnaps.net/dats/MAME
2. Extract the archive
3. Copy the file name "MAME 0.257 (arcade).dat" files to ~/.hasheous-server/Data/Signatures/MAME Arcade/

### MAME MESS
1. Download the DAT files from the source website. For example; from https://www.progettosnaps.net/dats/MAME
2. Extract the archive
3. Copy the file name "MAME 0.257 (mess).dat" files to ~/.hasheous-server/Data/Signatures/MAME MESS/

