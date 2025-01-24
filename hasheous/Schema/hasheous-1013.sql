ALTER TABLE `Signatures_Roms`
ADD COLUMN `Countries` varchar(255) NOT NULL DEFAULT '{}',
ADD COLUMN `Languages` varchar(255) NOT NULL DEFAULT '{}'