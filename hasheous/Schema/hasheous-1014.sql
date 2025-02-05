ALTER TABLE `Signatures_Games` ADD COLUMN `Category` VARCHAR(255);

DROP VIEW view_Signatures_Games;
CREATE VIEW `view_Signatures_Games` AS
select
    `Signatures_Games`.`Id` AS `Id`,
    `Signatures_Games`.`Name` AS `Name`,
    `Signatures_Games`.`Description` AS `Description`,
    `Signatures_Games`.`Year` AS `Year`,
    `Signatures_Games`.`PublisherId` AS `PublisherId`,
    `Signatures_Publishers`.`Publisher` AS `Publisher`,
    `Signatures_Games`.`Demo` AS `Demo`,
    `Signatures_Games`.`SystemId` AS `PlatformId`,
    `Signatures_Platforms`.`Platform` AS `Platform`,
    `Signatures_Games`.`SystemVariant` AS `SystemVariant`,
    `Signatures_Games`.`Video` AS `Video`,
    `Signatures_Games`.`Country` AS `Country`,
    `Signatures_Games`.`Language` AS `Language`,
    `Signatures_Games`.`Copyright` AS `Copyright`,
    `Signatures_Games`.`Category` AS `Category`
from (
        (
            `Signatures_Games`
            left join `Signatures_Publishers` on (
                `Signatures_Games`.`PublisherId` = `Signatures_Publishers`.`Id`
            )
        )
        join `Signatures_Platforms` on (
            `Signatures_Games`.`SystemId` = `Signatures_Platforms`.`Id`
        )
    )