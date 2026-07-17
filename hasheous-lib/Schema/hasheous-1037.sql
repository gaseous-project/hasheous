CREATE FULLTEXT INDEX idx_publisher_ft ON Signatures_Publishers (`Publisher`);

CREATE FULLTEXT INDEX idx_platform_ft ON Signatures_Platforms (`Platform`);

CREATE FULLTEXT INDEX idx_rom_ft ON Signatures_Roms (`Name`);

CREATE FULLTEXT INDEX idx_name_ft ON DataObject (`Name`);