ALTER TABLE `Signatures_Sources`
ADD COLUMN `processed_at` DATETIME NULL AFTER `SourceType`;

ALTER TABLE `Signatures_Games`
ADD COLUMN `created_at` DATETIME NULL,
ADD COLUMN `updated_at` DATETIME NULL;

ALTER TABLE `Signatures_Roms`
RENAME COLUMN `DateAdded` TO `created_at`,
RENAME COLUMN `DateUpdated` TO `updated_at`;