CREATE TABLE `PlatformMap` (
  `Id` BIGINT NOT NULL,
  `RetroPieDirectoryName` VARCHAR(45) NULL,
  `WebEmulator_Type` VARCHAR(45) NULL,
  `WebEmulator_Core` VARCHAR(45) NULL,
  `AvailableWebEmulators` LONGTEXT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE INDEX `Id_UNIQUE` (`Id` ASC) VISIBLE);

CREATE TABLE `PlatformMap_AlternateNames` (
  `Id` BIGINT NOT NULL,
  `Name` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`Id`, `Name`));

CREATE TABLE `PlatformMap_Extensions` (
  `Id` BIGINT NOT NULL,
  `Extension` VARCHAR(45) NOT NULL,
  PRIMARY KEY (`Id`, `Extension`));

CREATE TABLE `PlatformMap_UniqueExtensions` (
  `Id` BIGINT NOT NULL,
  `Extension` VARCHAR(45) NOT NULL,
  PRIMARY KEY (`Id`, `Extension`));

CREATE TABLE `PlatformMap_Bios` (
  `Id` BIGINT NOT NULL,
  `Filename` VARCHAR(45) NOT NULL,
  `Description` LONGTEXT NOT NULL,
  `Hash` VARCHAR(45) NOT NULL,
  PRIMARY KEY (`Id`, `Filename`, `Hash`));