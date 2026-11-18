ALTER TABLE `Task_Queue`
ADD COLUMN `parent_task_id` BIGINT NULL,
ADD CONSTRAINT `fk_task_queue_parent_task` FOREIGN KEY (`parent_task_id`) REFERENCES `Task_Queue` (`id`) ON DELETE CASCADE;