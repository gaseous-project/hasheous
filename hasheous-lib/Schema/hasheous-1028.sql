-- Add AllowManualAssignment and RoleDependsOn columns to Roles table
ALTER TABLE `Roles`
ADD COLUMN `AllowManualAssignment` TINYINT(1) NOT NULL DEFAULT 0;

ALTER TABLE `Roles`
ADD COLUMN `RoleDependsOn` CHAR(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';