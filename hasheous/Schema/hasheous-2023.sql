CREATE TABLE `UserArchiveObservations` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UserId` VARCHAR(128) NOT NULL,
    `ArchiveType` VARCHAR(5) NOT NULL,
    `ArchiveMD5` VARCHAR(32) NOT NULL,
    `ArchiveSHA1` VARCHAR(40) NOT NULL,
    `ArchiveSHA256` VARCHAR(64) NOT NULL,
    `ArchiveCRC32` VARCHAR(8) NOT NULL,
    `ArchiveSize` BIGINT NOT NULL,
    `ContentMD5` VARCHAR(32) NOT NULL,
    `ContentSHA1` VARCHAR(40) NOT NULL,
    `ContentSHA256` VARCHAR(64) NOT NULL,
    `ContentCRC32` VARCHAR(8) NOT NULL,
    INDEX `idx_UserId` (`UserId`),
    INDEX `idx_ArchiveType` (`ArchiveType`),
    INDEX `idx_ArchiveMD5` (`ArchiveMD5`),
    INDEX `idx_ArchiveSHA1` (`ArchiveSHA1`),
    INDEX `idx_ArchiveSHA256` (`ArchiveSHA256`),
    INDEX `idx_ArchiveCRC32` (`ArchiveCRC32`),
    INDEX `idx_UserId_ArchiveMD5_ArchiveSHA1_ArchiveSHA256` (
        `UserId`,
        `ArchiveMD5`,
        `ArchiveSHA1`,
        `ArchiveSHA256`
    )
);