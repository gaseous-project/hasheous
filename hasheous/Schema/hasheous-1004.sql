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

CREATE TABLE `Company` (
  `Id` bigint(20) NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `UpdatedDate` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `Company_SignatureMap` (
  `CompanyId` bigint(20) NOT NULL,
  `SignatureId` int NOT NULL,
  PRIMARY KEY (`CompanyId`, `SignatureId`),
  KEY `SignatureId` (`SignatureId`), 
  CONSTRAINT `Company_SignatureMap_ibfk_1` FOREIGN KEY (`CompanyId`) REFERENCES `Company` (`Id`) ON DELETE CASCADE, 
  CONSTRAINT `Company_SignatureMap_ibfk_2` FOREIGN KEY (`SignatureId`) REFERENCES `Signatures_Publishers` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `Platform` (
  `Id` bigint(20) NOT NULL AUTO_INCREMENT,
  `Company` bigint(20) NOT NULL DEFAULT 0,
  `Name` varchar(255) DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `UpdatedDate` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
)

CREATE TABLE `Platform_SignatureNames` (
  `PlatformId` bigint(20) NOT NULL,
  `SignaturePlatformId` bigint(20) NOT NULL,
  PRIMARY KEY (`PlatformId`, `SignaturePlatformId`)
)