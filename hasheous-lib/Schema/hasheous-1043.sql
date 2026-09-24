-- Speeds up ClientGetTask's task-selection filter (status/client_id lookup ordered by create_time)
CREATE INDEX `idx_task_queue_status_client_createtime` ON `Task_Queue` (
    `status`,
    `client_id`,
    `create_time`
);