CREATE TABLE `Tags` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `type` INT NOT NULL DEFAULT 0,
    `name` VARCHAR(255) NOT NULL,
    PRIMARY KEY (`id`),
    UNIQUE INDEX `idx_name` (`type`, `name` ASC)
);

CREATE TABLE `DataObject_Tags` (
    `DataObjectId` BIGINT NOT NULL,
    `TagId` BIGINT NOT NULL,
    `AIAssigned` BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (`DataObjectId`, `TagId`),
    INDEX `idx_TagId` (`TagId` ASC),
    CONSTRAINT `fk_DataObject_Tag` FOREIGN KEY (`DataObjectId`) REFERENCES `DataObject` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `fk_Tag` FOREIGN KEY (`TagId`) REFERENCES `Tags` (`id`) ON DELETE CASCADE
);