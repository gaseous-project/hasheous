CREATE TABLE `DataObjectHistory` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `ObjectId` BIGINT NOT NULL,
    `ObjectType` INT NOT NULL,
    `UserId` CHAR(36) NULL,
    `ChangeTimestamp` DATETIME NOT NULL,
    `PreEditJson` LONGBLOB NOT NULL,
    `DiffJson` LONGBLOB NOT NULL,
    INDEX `idx_objectid` (`ObjectId`),
    INDEX `idx_objecttype` (`ObjectType`),
    INDEX `idx_userid` (`UserId`),
    INDEX `idx_timestamp` (`ChangeTimestamp`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

ALTER TABLE `DataObject`
ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
ADD COLUMN `MergedIntoId` BIGINT NULL,
ADD INDEX `idx_isdeleted` (`IsDeleted`),
ADD INDEX `idx_mergedintoid` (`MergedIntoId`);