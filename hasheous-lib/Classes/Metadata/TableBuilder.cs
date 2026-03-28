using System.Reflection;
using Classes;
using Classes.Metadata;
using hasheous_server.Classes.MetadataLib;
using IGDB.Models;

namespace Classes.Metadata.Utility
{
    public class TableBuilder
    {
        public static void BuildTables()
        {
            // build IGDB tables
            BuildTableFromType(typeof(AgeRating), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(AgeRatingCategory), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(AgeRatingContentDescriptionV2), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(AgeRatingOrganization), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(AlternativeName), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Artwork), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Character), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CharacterGender), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CharacterMugShot), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CharacterSpecies), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Collection), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CollectionMembership), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CollectionMembershipType), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CollectionRelation), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CollectionRelationType), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CollectionType), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Company), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CompanyLogo), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CompanyStatus), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(CompanyWebsite), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Cover), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Event), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(EventLogo), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(EventNetwork), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(ExternalGame), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(ExternalGameSource), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Franchise), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Game), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameEngine), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameEngineLogo), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameLocalization), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameMode), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameReleaseFormat), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameStatus), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameTimeToBeat), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameType), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameVersion), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameVersionFeature), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameVersionFeatureValue), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(GameVideo), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Genre), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(InvolvedCompany), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Keyword), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Language), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(LanguageSupport), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(LanguageSupportType), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(MultiplayerMode), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(NetworkType), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Platform), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(PlatformFamily), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(PlatformLogo), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(PlatformVersion), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(PlatformVersionCompany), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(PlatformVersionReleaseDate), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(PlatformWebsite), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(PlayerPerspective), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(PopularityPrimitive), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(PopularityType), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Region), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(ReleaseDate), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(ReleaseDateRegion), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(ReleaseDateStatus), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Screenshot), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Theme), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(Website), Storage.TablePrefix.IGDB.ToString());
            BuildTableFromType(typeof(WebsiteType), Storage.TablePrefix.IGDB.ToString());
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
        /// <param name="prefix">The prefix to use for the table name.</param>
        public static void BuildTableFromType(Type type, string prefix)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            db.BuildTableFromType("hasheous", prefix, type);
        }
    }
}