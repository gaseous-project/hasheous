-- Add IsDeleted column to DataObject table for soft delete functionality
ALTER TABLE `DataObject`
ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
ADD INDEX `idx_isdeleted` (`IsDeleted`);
