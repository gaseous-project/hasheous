-- Active: 1715784858477@@mariadb@3306@hasheous
ALTER TABLE `DataObject`
ADD COLUMN `IsBlockedFromMatching` BOOLEAN NOT NULL DEFAULT 0;