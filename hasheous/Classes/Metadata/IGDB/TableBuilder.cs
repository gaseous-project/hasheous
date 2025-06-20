using System.Reflection;
using Classes;
using Classes.Metadata;
using IGDB.Models;

namespace Classes.Metadata.Utility
{
    public class TableBuilder
    {
        public static void BuildTables()
        {
            BuildTableFromType(typeof(AgeRating));
            BuildTableFromType(typeof(AgeRatingCategory));
            BuildTableFromType(typeof(AgeRatingContentDescriptionV2));
            BuildTableFromType(typeof(AgeRatingOrganization));
            BuildTableFromType(typeof(AlternativeName));
            BuildTableFromType(typeof(Artwork));
            BuildTableFromType(typeof(Character));
            BuildTableFromType(typeof(CharacterGender));
            BuildTableFromType(typeof(CharacterMugShot));
            BuildTableFromType(typeof(CharacterSpecies));
            BuildTableFromType(typeof(Collection));
            BuildTableFromType(typeof(CollectionMembership));
            BuildTableFromType(typeof(CollectionMembershipType));
            BuildTableFromType(typeof(CollectionRelation));
            BuildTableFromType(typeof(CollectionRelationType));
            BuildTableFromType(typeof(CollectionType));
            BuildTableFromType(typeof(Company));
            BuildTableFromType(typeof(CompanyLogo));
            BuildTableFromType(typeof(CompanyStatus));
            BuildTableFromType(typeof(CompanyWebsite));
            BuildTableFromType(typeof(Cover));
            BuildTableFromType(typeof(Event));
            BuildTableFromType(typeof(EventLogo));
            BuildTableFromType(typeof(EventNetwork));
            BuildTableFromType(typeof(ExternalGame));
            BuildTableFromType(typeof(ExternalGameSource));
            BuildTableFromType(typeof(Franchise));
            BuildTableFromType(typeof(Game));
            BuildTableFromType(typeof(GameEngine));
            BuildTableFromType(typeof(GameEngineLogo));
            BuildTableFromType(typeof(GameLocalization));
            BuildTableFromType(typeof(GameMode));
            BuildTableFromType(typeof(GameReleaseFormat));
            BuildTableFromType(typeof(GameStatus));
            BuildTableFromType(typeof(GameTimeToBeat));
            BuildTableFromType(typeof(GameType));
            BuildTableFromType(typeof(GameVersion));
            BuildTableFromType(typeof(GameVersionFeature));
            BuildTableFromType(typeof(GameVersionFeatureValue));
            BuildTableFromType(typeof(GameVideo));
            BuildTableFromType(typeof(Genre));
            BuildTableFromType(typeof(InvolvedCompany));
            BuildTableFromType(typeof(Keyword));
            BuildTableFromType(typeof(Language));
            BuildTableFromType(typeof(LanguageSupport));
            BuildTableFromType(typeof(LanguageSupportType));
            BuildTableFromType(typeof(MultiplayerMode));
            BuildTableFromType(typeof(NetworkType));
            BuildTableFromType(typeof(Platform));
            BuildTableFromType(typeof(PlatformFamily));
            BuildTableFromType(typeof(PlatformLogo));
            BuildTableFromType(typeof(PlatformVersion));
            BuildTableFromType(typeof(PlatformVersionCompany));
            BuildTableFromType(typeof(PlatformVersionReleaseDate));
            BuildTableFromType(typeof(PlatformWebsite));
            BuildTableFromType(typeof(PlayerPerspective));
            BuildTableFromType(typeof(PopularityPrimitive));
            BuildTableFromType(typeof(PopularityType));
            BuildTableFromType(typeof(Region));
            BuildTableFromType(typeof(ReleaseDate));
            BuildTableFromType(typeof(ReleaseDateRegion));
            BuildTableFromType(typeof(ReleaseDateStatus));
            BuildTableFromType(typeof(Screenshot));
            BuildTableFromType(typeof(Theme));
            BuildTableFromType(typeof(Website));
            BuildTableFromType(typeof(WebsiteType));
        }

        /// <summary>
        /// Builds a table from a type definition, or modifies an existing table.
        /// This is used to create or update tables in the database based on the properties of a class.
        /// Updates are limited to adding new columns, as the table structure should not change once created.
        /// If the table already exists, it will only add new columns that are not already present.
        /// This is useful for maintaining a consistent schema across different versions of the application.
        /// The method is generic and can be used with any type that has properties that can be mapped to database columns.
        /// The method does not return any value, but it will throw an exception if there is an error during the table creation or modification process.
        /// </summary>
        /// <param name="type">The type definition of the class for which the table should be built.</param>
        public static void BuildTableFromType(Type type)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            db.BuildTableFromType("hasheous", Storage.TablePrefix.IGDB.ToString(), type);
        }
    }
}