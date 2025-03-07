# <img src="./hasheous/wwwroot/images/logo.svg" height="28" style="float: right;" /> Hasheous Server

This project aims to match the content of DAT's like those provided by TOSEC and No-Intro to metadata provided by various vendors (IGDB only at present), to simplify identifying ROMs and other images when adding to ROM managers such as Hasheous's sister project Gaseous (https://github.com/gaseous-project/gaseous-server)

Hasheous is hosted at https://hasheous.org/

## Features
* Supports TOSEC, MAME, MESS, Redump, and No-Intro
* Matches to the following metadata sources:
  * IGDB
  * VIMMS Lair Manual Project (provides scans of game manuals)
* Proxies the following metadata sources:
  * IGDB
* Provides an API for developers to submit fixes to data matching
* Is completely free to use

## Discord Server
Join our Discord server: https://discord.gg/Nhu7wpT3k4

## Requirements
* MariaDB 11.1.2 or greater
  * This is the database version Hasheous has been tested and developed against. Your mileage may vary with earlier versions.
* Internet Game Database API Key. See: https://api-docs.igdb.com/#account-creation

## Third Party Projects
The following projects are used by Hasheous
* https://dotnet.microsoft.com/en-us/apps/aspnet
* https://github.com/JamesNK/Newtonsoft.Json
* https://www.nuget.org/packages/MySql.Data/8.0.32.1
* https://github.com/kamranayub/igdb-dotnet

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
* Redump: http://redump.org
* No-Intro: https://no-intro.org

If there are other DAT's you'd like to see support for, please raise an issue with a link to the DAT's.

## Adding signature DAT files
### TOSEC
1. Download the DAT files from the source website. For example; from https://www.tosecdev.org/downloads/category/56-2023-01-23
2. Extract the archive
3. Copy the DAT files to `~/.hasheous-server/Data/Signatures/TOSEC/`

### MAME Arcade
1. Download the DAT files from the source website. For example; from https://www.progettosnaps.net/dats/MAME
2. Extract the archive
3. Copy the file name `MAME 0.257 (arcade).dat` files to `~/.hasheous-server/Data/Signatures/MAME Arcade/`

### MAME MESS
1. Download the DAT files from the source website. For example; from https://www.progettosnaps.net/dats/MAME
2. Extract the archive
3. Copy the file name `MAME 0.257 (mess).dat` files to `~/.hasheous-server/Data/Signatures/MAME MESS/`

### Redump
1. Download the DAT files from the source website. For example; from http://redump.org/downloads/
2. Copy the DAT files to `~/.hasheous-server/Data/Signatures/Redump/`

### No-Intro
1. Download the DAT files from the source website. For example; from https://datomatic.no-intro.org/index.php?page=download&op=daily&s=64
2. Set `Type` to `Standard DAT` and click Request
3. Extract the archive and copy the `*.dat` files from the `./No-Intro` directory to `~/.hasheous-server/Data/Signatures/NoIntro/DAT`
4. Download the DB files from the source website.
5. Set `Type` to `DB` and click Request
6. Extract the archive and copy the `*.xml` files from the `./No-Intro` directory to `~/.hasheous-server/Data/Signatures/NoIntro/DB`
