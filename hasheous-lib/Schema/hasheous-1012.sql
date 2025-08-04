CREATE TABLE `IGDB_ReleaseDate` (
    `Id` BIGINT NOT NULL,
    `Category` INT(11) NULL DEFAULT NULL,
    `Checksum` VARCHAR(45) NULL DEFAULT NULL,
    `CreatedAt` DATETIME NULL DEFAULT NULL,
    `Date` DATETIME NULL,
    `Game` BIGINT NULL,
    `Human` VARCHAR(100) NULL,
    `m` INT NULL,
    `Platform` BIGINT NULL,
    `Region` INT NULL,
    `Status` BIGINT NULL,
    `UpdatedAt` DATETIME NULL DEFAULT NULL,
    `y` INT NULL,
    `dateAdded` DATETIME NULL DEFAULT NULL,
    `lastUpdated` DATETIME NULL DEFAULT NULL,
    PRIMARY KEY (`Id`)
);