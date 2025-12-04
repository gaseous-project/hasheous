CREATE TABLE `Insights_API_Requests_DailySummary` (
    `summary_date` DATE NOT NULL,
    `client_id` BIGINT UNSIGNED,
    `insightType` INT,
    `country` VARCHAR(4),
    `unique_visitors` INT,
    `total_requests` INT,
    `average_response_time` FLOAT,
    PRIMARY KEY (
        `summary_date`,
        `client_id`,
        `insightType`,
        `country`
    )
);