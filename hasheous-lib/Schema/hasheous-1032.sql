ALTER TABLE `Signatures_Publishers`
CHANGE `Publisher` `Publisher` varchar(255) DEFAULT NULL;

ALTER TABLE `Signatures_Roms`
CHANGE `Languages` `Languages` LONGTEXT DEFAULT NULL,
CHANGE `Countries` `Countries` LONGTEXT DEFAULT NULL;