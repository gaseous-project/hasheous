ALTER TABLE `Signatures_Roms` ADD CONSTRAINT `fk_Signatures_Games` FOREIGN KEY (`GameId`) REFERENCES `Signatures_Games` (`Id`) ON DELETE CASCADE;
