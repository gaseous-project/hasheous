CREATE TABLE `Settings` (
  `Setting` varchar(45) NOT NULL,
  `Value` longtext,
  PRIMARY KEY (`Setting`),
  UNIQUE KEY `Setting_UNIQUE` (`Setting`)
);

CREATE TABLE `ServerLogs` (
  `Id` bigint(20) NOT NULL AUTO_INCREMENT,
  `EventTime` datetime NOT NULL,
  `EventType` int(11) NOT NULL,
  `Process` varchar(100) NOT NULL,
  `Message` longtext NOT NULL,
  `Exception` longtext DEFAULT NULL,
  `CorrelationId` varchar(45) DEFAULT NULL,
  `CallingProcess` varchar(255) DEFAULT NULL,
  `CallingUser` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_CorrelationId` (`CorrelationId`),
  KEY `idx_CallingProcess` (`CallingProcess`),
  FULLTEXT KEY `ft_message` (`Message`)
);

CREATE TABLE `Signatures_Games` (
  `Id` BIGINT NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) DEFAULT NULL,
  `Description` varchar(255) DEFAULT NULL,
  `Year` varchar(15) DEFAULT NULL,
  `PublisherId` int DEFAULT NULL,
  `Demo` int DEFAULT NULL,
  `SystemId` int DEFAULT NULL,
  `SystemVariant` varchar(100) DEFAULT NULL,
  `Video` varchar(10) DEFAULT NULL,
  `Country` varchar(5) DEFAULT NULL,
  `Language` varchar(5) DEFAULT NULL,
  `Copyright` varchar(15) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Id_UNIQUE` (`Id`),
  KEY `publisher_Idx` (`PublisherId`),
  KEY `system_Idx` (`SystemId`),
  KEY `ingest_Idx` (`Name`,`Year`,`PublisherId`,`SystemId`) USING BTREE
);

CREATE TABLE `Signatures_Platforms` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Platform` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IdSignatures_Platforms_UNIQUE` (`Id`),
  KEY `Platforms_Idx` (`Platform`,`Id`) USING BTREE
);

CREATE TABLE `Signatures_Publishers` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Publisher` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Id_UNIQUE` (`Id`),
  KEY `publisher_Idx` (`Publisher`,`Id`)
);

CREATE TABLE `Signatures_Roms` (
  `Id` BIGINT NOT NULL AUTO_INCREMENT,
  `GameId` BIGINT DEFAULT NULL,
  `Name` varchar(255) DEFAULT NULL,
  `Size` bigint DEFAULT NULL,
  `CRC` varchar(20) DEFAULT NULL,
  `MD5` varchar(100) DEFAULT NULL,
  `SHA1` varchar(100) DEFAULT NULL,
  `DevelopmentStatus` varchar(100) DEFAULT NULL,
  `Attributes` longtext,
  `RomType` int DEFAULT NULL,
  `RomTypeMedia` varchar(100) DEFAULT NULL,
  `MediaLabel` varchar(100) DEFAULT NULL,
  `MetadataSource` int DEFAULT NULL,
  `IngestorVersion` int DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Id_UNIQUE` (`Id`,`GameId`) USING BTREE,
  KEY `GameId_Idx` (`GameId`),
  KEY `md5_Idx` (`MD5`) USING BTREE,
  KEY `sha1_Idx` (`SHA1`) USING BTREE,
  KEY `name_Idx` (`Name`) USING BTREE
);

CREATE TABLE `Signatures_Sources` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) DEFAULT NULL,
  `Description` varchar(255) DEFAULT NULL,
  `Category` varchar(45) DEFAULT NULL,
  `Version` varchar(45) DEFAULT NULL,
  `Author` longtext,
  `Email` varchar(45) DEFAULT NULL,
  `Homepage` varchar(45) DEFAULT NULL,
  `Url` varchar(45) DEFAULT NULL,
  `SourceType` varchar(45) DEFAULT NULL,
  `SourceMD5` varchar(45) DEFAULT NULL,
  `SourceSHA1` varchar(45) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Id_UNIQUE` (`Id`),
  KEY `sourcemd5_Idx` (`SourceMD5`,`Id`) USING BTREE,
  KEY `sourcesha1_Idx` (`SourceSHA1`,`Id`) USING BTREE
);

CREATE TABLE `Signatures_RomToSource` (
  `SourceId` int NOT NULL,
  `RomId` BIGINT NOT NULL,
  PRIMARY KEY (`SourceId`, `RomId`)
);

CREATE TABLE `Match_SignaturePlatforms` (
  `SignaturePlatformId` int NOT NULL,
  `IGDBPlatformId` bigint NOT NULL,
  `MatchMethod` int DEFAULT NULL,
  `LastSearched` datetime DEFAULT NULL,
  `NextSearch` datetime DEFAULT NULL,
  PRIMARY KEY (`SignaturePlatformId`,`IGDBPlatformId`),
  KEY `idx_SignaturePlatformId` (`SignaturePlatformId`),
  KEY `idx_IGDBPlatformId` (`IGDBPlatformId`)
);

CREATE TABLE `Match_SignatureGames` (
  `SignatureGameId` BIGINT NOT NULL,
  `IGDBGameId` BIGINT NOT NULL,
  `MatchMethod` INT NULL,
  `LastSearched` DATETIME NULL,
  `NextSearch` DATETIME NULL,
  PRIMARY KEY (`SignatureGameId`, `IGDBGameId`),
  KEY `idx_SignatureGameId` (`SignatureGameId`),
  KEY `idx_IGDBGameId` (`IGDBGameId`));

DROP VIEW IF EXISTS `view_Signatures_Games`;
CREATE VIEW `view_Signatures_Games` AS
    SELECT 
        `Signatures_Games`.`Id` AS `Id`,
        `Signatures_Games`.`Name` AS `Name`,
        `Signatures_Games`.`Description` AS `Description`,
        `Signatures_Games`.`Year` AS `Year`,
        `Signatures_Games`.`PublisherId` AS `PublisherId`,
        `Signatures_Publishers`.`Publisher` AS `Publisher`,
        `Signatures_Games`.`Demo` AS `Demo`,
        `Signatures_Games`.`SystemId` AS `PlatformId`,
        `Signatures_Platforms`.`Platform` AS `Platform`,
        `Signatures_Games`.`SystemVariant` AS `SystemVariant`,
        `Signatures_Games`.`VIdeo` AS `Video`,
        `Signatures_Games`.`Country` AS `Country`,
        `Signatures_Games`.`Language` AS `Language`,
        `Signatures_Games`.`Copyright` AS `Copyright`
    FROM
        ((`Signatures_Games`
        JOIN `Signatures_Publishers` ON ((`Signatures_Games`.`PublisherId` = `Signatures_Publishers`.`Id`)))
        JOIN `Signatures_Platforms` ON ((`Signatures_Games`.`SystemId` = `Signatures_Platforms`.`Id`)));