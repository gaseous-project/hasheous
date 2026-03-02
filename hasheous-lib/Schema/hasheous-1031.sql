ALTER TABLE `DataObject_SignatureMap` CHANGE `SignatureId` `SignatureId` BIGINT NOT NULL;

ALTER TABLE `DataObject_SignatureMap` ADD CONSTRAINT `fk_DO_Signatures_Games` FOREIGN KEY (`SignatureId`) REFERENCES `Signatures_Games` (`Id`) ON DELETE CASCADE;
