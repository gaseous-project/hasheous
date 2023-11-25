CREATE TABLE `IGDB_AgeRating` (
  `Id` bigint NOT NULL,
  `Category` int DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `ContentDescriptions` longtext DEFAULT NULL,
  `Rating` int DEFAULT NULL,
  `RatingCoverUrl` varchar(255) DEFAULT NULL,
  `Synopsis` longtext,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_AgeRatingContentDescription` (
  `Id` bigint NOT NULL,
  `Category` int DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Description` varchar(255) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_AlternativeName` (
  `Id` bigint NOT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Comment` longtext,
  `Game` bigint DEFAULT NULL,
  `Name` varchar(255) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Artwork` (
  `Id` bigint NOT NULL,
  `AlphaChannel` tinyint(1) DEFAULT NULL,
  `Animated` tinyint(1) DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Game` bigint DEFAULT NULL,
  `Height` int DEFAULT NULL,
  `ImageId` varchar(45) DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `Width` int DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Collection` (
  `Id` bigint NOT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Games` longtext DEFAULT NULL,
  `Name` varchar(255) DEFAULT NULL,
  `Slug` varchar(100) DEFAULT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Company` (
  `Id` bigint NOT NULL,
  `ChangeDate` datetime DEFAULT NULL,
  `ChangeDateCategory` int DEFAULT NULL,
  `ChangedCompanyId` bigint DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Country` int DEFAULT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `Description` longtext,
  `Developed` longtext DEFAULT NULL,
  `Logo` bigint DEFAULT NULL,
  `Name` varchar(255) DEFAULT NULL,
  `Parent` bigint DEFAULT NULL,
  `Published` longtext DEFAULT NULL,
  `Slug` varchar(100) DEFAULT NULL,
  `StartDate` datetime DEFAULT NULL,
  `StartDateCategory` int DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `Websites` longtext DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_CompanyLogo` (
  `Id` bigint NOT NULL,
  `AlphaChannel` tinyint(1) DEFAULT NULL,
  `Animated` tinyint(1) DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Height` int DEFAULT NULL,
  `ImageId` varchar(45) DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `Width` int DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Cover` (
  `Id` bigint NOT NULL,
  `AlphaChannel` tinyint(1) DEFAULT NULL,
  `Animated` tinyint(1) DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Game` bigint DEFAULT NULL,
  `Height` int DEFAULT NULL,
  `ImageId` varchar(45) DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `Width` int DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_ExternalGame` (
  `Id` bigint NOT NULL,
  `Category` int DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `Countries` longtext DEFAULT NULL,
  `Game` bigint DEFAULT NULL,
  `Media` int DEFAULT NULL,
  `Name` varchar(255) DEFAULT NULL,
  `Platform` bigint DEFAULT NULL,
  `Uid` varchar(255) DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `Year` int DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Franchise` (
  `Id` bigint NOT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Games` longtext DEFAULT NULL,
  `Name` varchar(255) DEFAULT NULL,
  `Slug` varchar(255) DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Game` (
  `Id` bigint NOT NULL,
  `AgeRatings` longtext DEFAULT NULL,
  `AggregatedRating` double DEFAULT NULL,
  `AggregatedRatingCount` int DEFAULT NULL,
  `AlternativeNames` longtext DEFAULT NULL,
  `Artworks` longtext DEFAULT NULL,
  `Bundles` longtext DEFAULT NULL,
  `Category` int DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Collection` bigint DEFAULT NULL,
  `Cover` bigint DEFAULT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `Dlcs` longtext DEFAULT NULL,
  `Expansions` longtext DEFAULT NULL,
  `ExternalGames` longtext DEFAULT NULL,
  `FirstReleaseDate` datetime DEFAULT NULL,
  `Follows` int DEFAULT NULL,
  `Franchise` bigint DEFAULT NULL,
  `Franchises` longtext DEFAULT NULL,
  `GameEngines` longtext DEFAULT NULL,
  `GameModes` longtext DEFAULT NULL,
  `Genres` longtext DEFAULT NULL,
  `Hypes` int DEFAULT NULL,
  `InvolvedCompanies` longtext DEFAULT NULL,
  `Keywords` longtext DEFAULT NULL,
  `MultiplayerModes` longtext DEFAULT NULL,
  `Name` varchar(255) DEFAULT NULL,
  `ParentGame` bigint DEFAULT NULL,
  `Platforms` longtext DEFAULT NULL,
  `PlayerPerspectives` longtext DEFAULT NULL,
  `Rating` double DEFAULT NULL,
  `RatingCount` int DEFAULT NULL,
  `ReleaseDates` longtext DEFAULT NULL,
  `Screenshots` longtext DEFAULT NULL,
  `SimilarGames` longtext DEFAULT NULL,
  `Slug` varchar(100) DEFAULT NULL,
  `StandaloneExpansions` longtext DEFAULT NULL,
  `Status` int DEFAULT NULL,
  `StoryLine` longtext,
  `Summary` longtext,
  `Tags` longtext DEFAULT NULL,
  `Themes` longtext DEFAULT NULL,
  `TotalRating` double DEFAULT NULL,
  `TotalRatingCount` int DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `VersionParent` bigint DEFAULT NULL,
  `VersionTitle` varchar(100) DEFAULT NULL,
  `Videos` longtext DEFAULT NULL,
  `Websites` longtext DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Id_UNIQUE` (`Id`)
);

CREATE TABLE `IGDB_GameMode` (
  `Id` bigint NOT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Name` varchar(100) DEFAULT NULL,
  `Slug` varchar(100) DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_GameVideo` (
  `Id` bigint NOT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Game` bigint DEFAULT NULL,
  `Name` varchar(100) DEFAULT NULL,
  `VideoId` varchar(45) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Genre` (
  `Id` bigint NOT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Name` varchar(255) DEFAULT NULL,
  `Slug` varchar(100) DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_InvolvedCompany` (
  `Id` bigint NOT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Company` bigint DEFAULT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `Developer` tinyint(1) DEFAULT NULL,
  `Game` bigint DEFAULT NULL,
  `Porting` tinyint(1) DEFAULT NULL,
  `Publisher` tinyint(1) DEFAULT NULL,
  `Supporting` tinyint(1) DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_MultiplayerMode` (
  `Id` bigint NOT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `CampaignCoop` tinyint(1) DEFAULT NULL,
  `DropIn` tinyint(1) DEFAULT NULL,
  `Game` bigint DEFAULT NULL,
  `LanCoop` tinyint(1) DEFAULT NULL,
  `OfflineCoop` tinyint(1) DEFAULT NULL,
  `OfflineCoopMax` int DEFAULT NULL,
  `OfflineMax` int DEFAULT NULL,
  `OnlineCoop` tinyint(1) DEFAULT NULL,
  `OnlineCoopMax` int DEFAULT NULL,
  `OnlineMax` int DEFAULT NULL,
  `Platform` bigint DEFAULT NULL,
  `SplitScreen` tinyint(1) DEFAULT NULL,
  `SplitScreenOnline` tinyint(1) DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Platform` (
  `Id` bigint NOT NULL,
  `Abbreviation` varchar(45) DEFAULT NULL,
  `AlternativeName` varchar(255) DEFAULT NULL,
  `Category` int DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `Generation` int DEFAULT NULL,
  `Name` varchar(45) DEFAULT NULL,
  `PlatformFamily` bigint DEFAULT NULL,
  `PlatformLogo` bigint DEFAULT NULL,
  `Slug` varchar(45) DEFAULT NULL,
  `Summary` longtext,
  `UpdatedAt` datetime DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `Versions` longtext DEFAULT NULL,
  `Websites` longtext DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Id_UNIQUE` (`Id`)
);

CREATE TABLE `IGDB_PlatformLogo` (
  `Id` bigint NOT NULL,
  `AlphaChannel` tinyint(1) DEFAULT NULL,
  `Animated` tinyint(1) DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Height` int DEFAULT NULL,
  `ImageId` varchar(45) DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `Width` int DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_PlatformVersion` (
  `Id` bigint NOT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Companies` longtext DEFAULT NULL,
  `Connectivity` longtext,
  `CPU` longtext,
  `Graphics` longtext,
  `MainManufacturer` bigint DEFAULT NULL,
  `Media` longtext,
  `Memory` longtext,
  `Name` longtext,
  `OS` longtext,
  `Output` longtext,
  `PlatformLogo` bigint DEFAULT NULL,
  `PlatformVersionReleaseDates` longtext DEFAULT NULL,
  `Resolutions` longtext,
  `Slug` longtext,
  `Sound` longtext,
  `Storage` longtext,
  `Summary` longtext,
  `Url` varchar(255) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_PlayerPerspective` (
  `Id` bigint NOT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Name` varchar(100) DEFAULT NULL,
  `Slug` varchar(45) DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Screenshot` (
  `Id` bigint NOT NULL,
  `AlphaChannel` tinyint(1) DEFAULT NULL,
  `Animated` tinyint(1) DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Game` bigint DEFAULT NULL,
  `Height` int DEFAULT NULL,
  `ImageId` varchar(45) DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `Width` int DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);

CREATE TABLE `IGDB_Theme` (
  `Id` bigint NOT NULL,
  `CreatedAt` datetime DEFAULT NULL,
  `Checksum` varchar(45) DEFAULT NULL,
  `Name` varchar(100) DEFAULT NULL,
  `Slug` varchar(45) DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Url` varchar(255) DEFAULT NULL,
  `dateAdded` datetime DEFAULT NULL,
  `lastUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
);
