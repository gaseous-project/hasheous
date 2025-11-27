CREATE TABLE `Task_Clients` (
    `id` BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    `public_id` VARCHAR(64) NOT NULL UNIQUE,
    `client_name` VARCHAR(255) NOT NULL,
    `owner_id` VARCHAR(128) NOT NULL,
    `api_key` VARCHAR(255) NOT NULL UNIQUE,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `last_heartbeat` DATETIME,
    `version` VARCHAR(64),
    `capabilities` JSON,
    INDEX `idx_public_id` (`public_id`),
    INDEX `idx_client_name` (`client_name`),
    INDEX `idx_owner_id` (`owner_id`),
    INDEX `idx_last_heartbeat` (`last_heartbeat`)
);

CREATE TABLE `Task_Queue` (
    `id` BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    `create_time` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `task_name` INT NOT NULL,
    `status` INT NOT NULL DEFAULT 0,
    `client_id` BIGINT,
    `parameters` JSON,
    `result` JSON,
    `error_message` TEXT,
    `start_time` DATETIME NULL,
    `completion_time` DATETIME NULL,
    FOREIGN KEY (`client_id`) REFERENCES `Task_Clients` (`id`) ON DELETE
    SET
        INDEX `idx_status` (`status`)
);