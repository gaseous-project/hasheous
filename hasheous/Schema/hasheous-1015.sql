CREATE TABLE `IGDB_GameLocalization` (
    `Checksum` longtext DEFAULT NULL,
    `Cover` BIGINT DEFAULT NULL,
    `CreatedAt` BIGINT DEFAULT NULL,
    `Game` BIGINT DEFAULT NULL,
    `Id` BIGINT NOT NULL,
    `Name` longtext DEFAULT NULL,
    `Region` BIGINT DEFAULT NULL,
    `UpdatedAt` BIGINT DEFAULT NULL,
    `dateAdded` datetime DEFAULT NULL,
    `lastUpdated` datetime DEFAULT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Region` (
    `Category` longtext DEFAULT NULL,
    `Checksum` longtext DEFAULT NULL,
    `CreatedAt` DATETIME DEFAULT NULL,
    `Id` BIGINT NOT NULL,
    `Identifier` longtext DEFAULT NULL,
    `Name` longtext DEFAULT NULL,
    `UpdatedAt` DATETIME DEFAULT NULL,
    `dateAdded` datetime DEFAULT NULL,
    `lastUpdated` datetime DEFAULT NULL,
    PRIMARY KEY (`Id`)
);