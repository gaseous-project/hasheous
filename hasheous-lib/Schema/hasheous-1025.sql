-- Note: unique_visitors should be calculated as COUNT(DISTINCT remote_ip) in aggregation queries to ensure true unique visitor counts per group.
CREATE TABLE `Insights_API_Requests_DailySummary` (
    `summary_date` DATE NOT NULL,
    `client_id` BIGINT UNSIGNED,
    `client_apikey_id` BIGINT UNSIGNED,
    `insightType` INT,
    `country` VARCHAR(4),
    `unique_visitors` INT, -- This should be the count of DISTINCT remote_ip for the group
    `total_requests` INT,
    `average_response_time` FLOAT,
    PRIMARY KEY (
        `summary_date`,
        `client_id`,
        `client_apikey_id`,
        `insightType`,
        `country`
    )
);