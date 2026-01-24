-- Create DataObjectHistory table for tracking changes to DataObjects
CREATE TABLE IF NOT EXISTS `DataObjectHistory` (
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
