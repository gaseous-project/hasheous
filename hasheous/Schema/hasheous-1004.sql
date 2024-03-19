CREATE TABLE `Country` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Code` VARCHAR(20) NULL,
  `Value` VARCHAR(255) NULL,
  PRIMARY KEY (`Id`),
  INDEX `id_Code` (`Code` ASC) VISIBLE,
  INDEX `id_Value` (`Value` ASC) VISIBLE);

CREATE TABLE `Language` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Code` VARCHAR(20) NULL,
  `Value` VARCHAR(255) NULL,
  PRIMARY KEY (`Id`),
  INDEX `id_Code` (`Code` ASC) VISIBLE,
  INDEX `id_Value` (`Value` ASC) VISIBLE);

CREATE TABLE `DataObject` (
  `Id` bigint(20) NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) DEFAULT NULL,
  `ObjectType` int NOT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `UpdatedDate` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`, `ObjectType`)
);

CREATE TABLE `DataObject_SignatureMap` (
  `DataObjectId` bigint(20) NOT NULL,
  `SignatureId` int NOT NULL,
  PRIMARY KEY (`DataObjectId`, `SignatureId`),
  KEY `SignatureId` (`SignatureId`), 
  CONSTRAINT `DataObject_SignatureMap_ibfk_1` FOREIGN KEY (`DataObjectId`) REFERENCES `DataObject` (`Id`) ON DELETE CASCADE, 
  CONSTRAINT `DataObject_SignatureMap_ibfk_2` FOREIGN KEY (`SignatureId`) REFERENCES `Signatures_Publishers` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `DataObject_MetadataMap` (
  `DataObjectId` bigint(20) NOT NULL,
  `MetadataId` varchar(50) NOT NULL,
  `SourceId` int(11) NOT NULL,
  `MatchMethod` int(11) NOT NULL,
  `LastSearched` datetime NOT NULL,
  `NextSearch` datetime NOT NULL,
  PRIMARY KEY (`DataObjectId`,`SourceId`),
  KEY `DataObjectId` (`DataObjectId`),
  CONSTRAINT `DataObject_MetadataMap_ibfk_1` FOREIGN KEY (`DataObjectId`) REFERENCES `DataObject` (`Id`) ON DELETE CASCADE
);