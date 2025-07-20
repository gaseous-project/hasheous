ALTER TABLE `MatchUserVotes`
CHANGE `MetadataGameId` `MetadataGameId` varchar(128) NOT NULL,
DROP COLUMN `MetadataPlatformId`;

CREATE INDEX idx_event_client_ip ON Insights_API_Requests (
    client_id,
    event_datetime,
    remote_ip
);

CREATE INDEX idx_event_time_client ON Insights_API_Requests (
    event_datetime,
    client_id,
    remote_ip
);

CREATE INDEX idx_client_event_country_ip ON Insights_API_Requests (
    client_id,
    event_datetime,
    country,
    remote_ip
);

CREATE INDEX idx_country_code ON Country (Code);

CREATE INDEX idx_client_event ON Insights_API_Requests (client_id, event_datetime);

CREATE INDEX idx_client_event_exec ON Insights_API_Requests (
    client_id,
    event_datetime,
    execution_time_ms
);

CREATE INDEX idx_client_event_apikey_ip ON Insights_API_Requests (
    client_id,
    event_datetime,
    client_apikey_id,
    remote_ip
);

CREATE INDEX idx_clientapikeys_id_dataobj ON ClientAPIKeys (ClientIdIndex, DataObjectId);

CREATE INDEX idx_clientapikeys_name ON ClientAPIKeys (Name);