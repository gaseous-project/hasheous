CREATE TABLE `Screenscraper_HashToGameMap` (
    `Hash` varchar(255) NOT NULL,
    `HashType` varchar(50) NOT NULL,
    `GameId` BIGINT NOT NULL,
    PRIMARY KEY (`Hash`, `HashType`, `GameId`),
    INDEX `IX_Screenscraper_HashToGameMap_Hash` (`Hash`, `HashType`),
    INDEX `IX_Screenscraper_HashToGameMap_GameId` (`GameId`)
);

CREATE TABLE `Screenscraper_FailedHashLookups` (
    `Hash` varchar(255) NOT NULL,
    `HashType` varchar(50) NOT NULL,
    `LookupDate` datetime NOT NULL,
    PRIMARY KEY (`Hash`, `HashType`),
    INDEX `IX_Screenscraper_FailedHashLookups_Hash` (`Hash`, `HashType`),
    INDEX `IX_Screenscraper_FailedHashLookups_LookupDate` (`LookupDate`)
)