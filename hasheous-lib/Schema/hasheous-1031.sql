-- Add MergedIntoId column to DataObject table to track merge operations
ALTER TABLE `DataObject`
ADD COLUMN `MergedIntoId` BIGINT NULL,
ADD INDEX `idx_mergedintoid` (`MergedIntoId`);
