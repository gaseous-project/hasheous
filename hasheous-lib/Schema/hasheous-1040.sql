ALTER TABLE Insights_API_Requests_Hourly
ADD COLUMN endpoint_address VARCHAR(255) NULL AFTER client_apikey_id,
ADD COLUMN `method` VARCHAR(10) NULL AFTER endpoint_address;

ALTER TABLE Insights_API_Requests_Daily
ADD COLUMN endpoint_address VARCHAR(255) NULL AFTER client_apikey_id,
ADD COLUMN `method` VARCHAR(10) NULL AFTER endpoint_address;

ALTER TABLE Insights_API_Requests_Monthly
ADD COLUMN endpoint_address VARCHAR(255) NULL AFTER client_apikey_id,
ADD COLUMN `method` VARCHAR(10) NULL AFTER endpoint_address;

CREATE INDEX idx_hourly_endpoint_address ON Insights_API_Requests_Hourly (endpoint_address, `method`);

CREATE INDEX idx_daily_endpoint_address ON Insights_API_Requests_Daily (endpoint_address, `method`);

CREATE INDEX idx_monthly_endpoint_address ON Insights_API_Requests_Monthly (endpoint_address, `method`);