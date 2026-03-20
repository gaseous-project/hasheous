ALTER TABLE `Task_Queue`
ADD COLUMN `priority` INT NOT NULL DEFAULT 0 AFTER `task_name`,
ADD COLUMN `identifier` VARCHAR(255) NOT NULL DEFAULT '' AFTER `priority`,
CHANGE `dataobjectid` `dataobjectid` bigint(20) DEFAULT NULL,
ADD INDEX `idx_priority` (`priority`),
ADD INDEX `idx_identifier` (`task_name`, `identifier`);