CREATE TABLE `Task_Clients` (
    `id` BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    `public_id` VARCHAR(64) NOT NULL UNIQUE,
    `client_name` VARCHAR(255) NOT NULL,
    `owner_id` VARCHAR(128) NOT NULL,
    `api_key` VARCHAR(255) NOT NULL UNIQUE,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `last_heartbeat` DATETIME,
    `version` VARCHAR(64),
    `capabilities` LONGTEXT,
    INDEX `idx_public_id` (`public_id`),
    INDEX `idx_client_name` (`client_name`),
    INDEX `idx_owner_id` (`owner_id`),
    INDEX `idx_last_heartbeat` (`last_heartbeat`)
);

CREATE TABLE `Task_Queue` (
    `id` BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    `create_time` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `dataobjectid` BIGINT NOT NULL,
    `task_name` INT NOT NULL,
    `status` INT NOT NULL DEFAULT 0,
    `client_id` BIGINT,
    `required_capabilities` LONGTEXT,
    `parameters` LONGTEXT,
    `result` LONGTEXT,
    `error_message` LONGTEXT,
    `start_time` DATETIME NULL,
    `completion_time` DATETIME NULL,
    UNIQUE INDEX `idx_dojbtaskname` (`dataobjectid`, `task_name`),
    INDEX `idx_dataobjectid` (`dataobjectid`),
    INDEX `idx_status` (`status`),
    INDEX `idx_client_id` (`client_id`),
    CONSTRAINT `fk_task_queue_client_id` FOREIGN KEY (`client_id`) REFERENCES `Task_Clients` (`id`) ON DELETE SET NULL,
    CONSTRAINT `fk_task_queue_dataobjectid` FOREIGN KEY (`dataobjectid`) REFERENCES `DataObject` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `Task_Queue_Capabilities` (
    `task_queue_id` BIGINT NOT NULL,
    `capability_id` INT NOT NULL,
    PRIMARY KEY (
        `task_queue_id`,
        `capability_id`
    ),
    CONSTRAINT `fk_tqc_task_queue_id` FOREIGN KEY (`task_queue_id`) REFERENCES `Task_Queue` (`id`) ON DELETE CASCADE
);

ALTER TABLE `hasheous`.`DataObject_Tags`
DROP PRIMARY KEY,
ADD PRIMARY KEY (
    `DataObjectId`,
    `TagId`,
    `AIAssigned`
);