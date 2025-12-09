DROP TABLE `Insights_API_Requests_DailySummary`;

CREATE TABLE Insights_API_Requests_Hourly (
    event_datetime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    insightType INT DEFAULT 0,
    remote_ip VARCHAR(45) NOT NULL,
    user_id VARCHAR(255),
    user_agent VARCHAR(255),
    country VARCHAR(4),
    client_id BIGINT UNSIGNED,
    client_apikey_id BIGINT UNSIGNED,
    total_requests INT NOT NULL DEFAULT 1,
    average_execution_time_ms FLOAT NOT NULL DEFAULT 0,
    INDEX idx_event_datetime (event_datetime),
    INDEX idx_insight_type (insightType),
    INDEX idx_endpoint_address (endpoint_address),
    INDEX idx_remote_ip (remote_ip),
    INDEX idx_user_id (user_id),
    INDEX idx_country (country),
    INDEX idx_client_id (client_id),
    INDEX idx_client_apikey_id (client_apikey_id)
);

CREATE TABLE Insights_API_Requests_Daily (
    event_datetime DATE NOT NULL,
    insightType INT DEFAULT 0,
    remote_ip VARCHAR(45) NOT NULL,
    user_id VARCHAR(255),
    user_agent VARCHAR(255),
    country VARCHAR(4),
    client_id BIGINT UNSIGNED,
    client_apikey_id BIGINT UNSIGNED,
    total_requests INT NOT NULL DEFAULT 1,
    average_execution_time_ms FLOAT NOT NULL DEFAULT 0,
    INDEX idx_event_datetime (event_datetime),
    INDEX idx_insight_type (insightType),
    INDEX idx_endpoint_address (endpoint_address),
    INDEX idx_remote_ip (remote_ip),
    INDEX idx_user_id (user_id),
    INDEX idx_country (country),
    INDEX idx_client_id (client_id),
    INDEX idx_client_apikey_id (client_apikey_id)
);

CREATE TABLE Insights_API_Requests_Monthly (
    event_datetime DATE NOT NULL,
    insightType INT DEFAULT 0,
    remote_ip VARCHAR(45) NOT NULL,
    user_id VARCHAR(255),
    user_agent VARCHAR(255),
    country VARCHAR(4),
    client_id BIGINT UNSIGNED,
    client_apikey_id BIGINT UNSIGNED,
    total_requests INT NOT NULL DEFAULT 1,
    average_execution_time_ms FLOAT NOT NULL DEFAULT 0,
    INDEX idx_event_datetime (event_datetime),
    INDEX idx_insight_type (insightType),
    INDEX idx_endpoint_address (endpoint_address),
    INDEX idx_remote_ip (remote_ip),
    INDEX idx_user_id (user_id),
    INDEX idx_country (country),
    INDEX idx_client_id (client_id),
    INDEX idx_client_apikey_id (client_apikey_id)
);