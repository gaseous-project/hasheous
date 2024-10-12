CREATE TABLE `DataObject_ACL` (
    `DataObject_ID` BIGINT(20) NOT NULL,
    `Read` BOOLEAN NOT NULL DEFAULT 0,
    `Write` BOOLEAN NOT NULL DEFAULT 0,
    `Delete` BOOLEAN NOT NULL DEFAULT 0,
    `UserId` VARCHAR(128) NOT NULL,
    INDEX `DataObject_ID` (`DataObject_ID`),
    INDEX `UserId` (`UserId`),
    CONSTRAINT `DataObject_ACL_ibfk_1` FOREIGN KEY (`DataObject_ID`) REFERENCES `DataObject` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `DataObject_ACL_ibfk_2` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE `ClientAPIKeys` (
    `ClientIdIndex` BIGINT(20) NOT NULL AUTO_INCREMENT,
    `DataObjectId` BIGINT(20) NOT NULL,
    `Name` VARCHAR(255) NOT NULL,
    `APIKey` VARCHAR(255) NOT NULL,
    `Created` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `Expires` DATETIME NULL,
    `Revoked` BOOLEAN NOT NULL DEFAULT 0,
    PRIMARY KEY (`ClientIdIndex`),
    INDEX `DataObjectId` (`DataObjectId`),
    CONSTRAINT `ClientAPIKeys_ibfk_1` FOREIGN KEY (`DataObjectId`) REFERENCES `DataObject` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
);