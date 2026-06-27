ALTER TABLE `Signatures_Games`
DROP INDEX `idx_games_name_systemid_publisherid`,
DROP INDEX `ingest_Idx`,
CHANGE `Country` `Country` VARCHAR(100) DEFAULT NULL,
CHANGE `Language` `Language` VARCHAR(100) DEFAULT NULL,
ADD KEY `ingest_Idx` (
    `Name`,
    `Year`,
    `PublisherId`,
    `SystemId`,
    `Country`
) USING BTREE,
ADD KEY `idx_games_name_systemid_publisherid` (
    `Name`,
    `SystemId`,
    `PublisherId`,
    `Country`
) USING BTREE;