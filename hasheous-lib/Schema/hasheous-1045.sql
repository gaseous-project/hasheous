CREATE TABLE `localisation_languages` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `FullCode` VARCHAR(85) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    `EnglishName` VARCHAR(255) NOT NULL,
    `NativeName` VARCHAR(255) NOT NULL,
    `EnglishRegionName` VARCHAR(255) DEFAULT NULL,
    `NativeRegionName` VARCHAR(255) DEFAULT NULL,
    `Direction` INT NOT NULL,
    `Version` INT NOT NULL,
    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `updated_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    KEY `idx_localisation_languages_code` (`FullCode`)
);

CREATE TABLE `localisation_strings` (
    `language_id` INT NOT NULL,
    `key` VARCHAR(255) NOT NULL,
    `value` TEXT NOT NULL,
    `AIGenerated` BOOLEAN NOT NULL,
    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `updated_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`language_id`, `key`)
);