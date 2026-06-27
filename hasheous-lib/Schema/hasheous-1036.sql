ALTER TABLE `Signatures_Roms`
ADD INDEX (
    `GameId`,
    `MD5`,
    `SHA1`,
    `SHA256`,
    `CRC`,
    `IngestorVersion`
);

ALTER TABLE `Signatures_Roms`
ADD INDEX (
    `GameId`,
    `MD5`,
    `SHA1`,
    `IngestorVersion`
);
