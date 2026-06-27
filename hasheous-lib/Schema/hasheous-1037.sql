CREATE TABLE `UserAppKeys` (
    `Id` BIGINT(20) NOT NULL AUTO_INCREMENT,
    `UserId` VARCHAR(128) NOT NULL,
    `DataObjectId` BIGINT(20) NOT NULL,
    `APIKey` VARCHAR(128) NOT NULL,
    `Created` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `LastUsed` DATETIME NULL,
    `Revoked` BOOLEAN NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `APIKey` (`APIKey`),
    UNIQUE KEY `UserApp` (`UserId`, `DataObjectId`),
    INDEX `UserId` (`UserId`),
    INDEX `DataObjectId` (`DataObjectId`),
    CONSTRAINT `UserAppKeys_ibfk_1` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE ON UPDATE NO ACTION,
    CONSTRAINT `UserAppKeys_ibfk_2` FOREIGN KEY (`DataObjectId`) REFERENCES `DataObject` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
);
