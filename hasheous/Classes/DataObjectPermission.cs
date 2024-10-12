using System.Data;
using Authentication;
using Classes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace hasheous_server.Classes
{
    public class DataObjectPermission
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public DataObjectPermission(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public enum PermissionType
        {
            Create,
            Read,
            Update,
            Delete,
            NotApplicable
        }

        /// <summary>
        /// Check the permission for the object
        /// </summary>
        /// <param name="user">
        /// The user to check the permission for
        /// </param>
        /// <param name="ObjectType">
        /// The type of object to check the permission for
        /// </param>
        /// <param name="RequestedPermission">
        /// The permission to check for
        /// </param>
        /// <param name="ObjectId">
        /// The ID of the object to check the permission for
        /// </param>
        /// <returns>
        /// A boolean indicating if the user has the permission for the object
        /// </returns>
        public async Task<bool> CheckAsync(Authentication.ApplicationUser user, Classes.DataObjects.DataObjectType ObjectType, PermissionType RequestedPermission, long? ObjectId = null)
        {
            var roles = await _userManager.GetRolesAsync(user);

            // if the user is in the Admin role, allow them to create the object.
            // if the user is in the moderator role, allow them to create any object except for the app object.
            switch (ObjectType)
            {
                case Classes.DataObjects.DataObjectType.App:
                    // admins are always allowed to create and modify apps
                    if (roles.Contains("Admin"))
                    {
                        return true;
                    }

                    // if an ObjectId is provided, get the users permission to modify the object
                    if (ObjectId == null)
                    {
                        return false;
                    }

                    Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                    string sql;
                    switch (RequestedPermission)
                    {
                        case PermissionType.Read:
                            sql = "SELECT * FROM DataObject_ACL WHERE `DataObject_ID` = @DataObject_ID AND `UserId` = @UserId AND `Read` = 1;";
                            break;

                        case PermissionType.Update:
                            sql = "SELECT * FROM DataObject_ACL WHERE `DataObject_ID` = @DataObject_ID AND `UserId` = @UserId AND `Write` = 1;";
                            break;

                        case PermissionType.Delete:
                            sql = "SELECT * FROM DataObject_ACL WHERE `DataObject_ID` = @DataObject_ID AND `UserId` = @UserId AND `Delete` = 1;";
                            break;

                        default:
                            return false;
                    }
                    Dictionary<string, object> parameters = new Dictionary<string, object>
                    {
                        { "@DataObject_ID", ObjectId },
                        { "@UserId", user.Id }
                    };
                    DataTable dt = db.ExecuteCMD(sql, parameters);

                    if (dt.Rows.Count > 0)
                    {
                        // if the user has permission to the object, allow them to create and modify it
                        return true;
                    }

                    break;

                default:
                    // admins and moderators are always allowed to create and modify objects
                    if (roles.Contains("Admin") || roles.Contains("Moderator"))
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }

        /// <summary>
        /// Get the permission for the object
        /// </summary>
        /// <param name="user">
        /// The user to check the permission for
        /// </param>
        /// <param name="ObjectType">
        /// The type of object to check the permission for
        /// </param>
        /// <param name="ObjectId">
        /// The ID of the object to check the permission for
        /// </param>
        /// <returns>
        /// The permission for the object
        /// </returns>
        /// <remarks>
        /// This method is used to get the permission for the object. It is only relevent for the app object type.
        /// </remarks>
        public List<PermissionType> GetObjectPermission(Authentication.ApplicationUser user, Classes.DataObjects.DataObjectType ObjectType, long ObjectId)
        {
            if (user == null)
            {
                return new List<PermissionType> {
                    PermissionType.NotApplicable
                    };
            }

            if (ObjectType != Classes.DataObjects.DataObjectType.App)
            {
                return new List<PermissionType> {
                    PermissionType.NotApplicable
                    };
            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM DataObject_ACL WHERE DataObject_ID = @DataObject_ID AND UserId = @UserId;";
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@DataObject_ID", ObjectId },
                { "@UserId", user.Id }
            };
            DataTable dt = db.ExecuteCMD(sql, parameters);

            List<PermissionType> permissions = new List<PermissionType>();

            if (dt.Rows.Count > 0)
            {
                if ((bool)dt.Rows[0]["Read"] == true)
                {
                    permissions.Add(PermissionType.Read);
                }
                if ((bool)dt.Rows[0]["Write"] == true)
                {
                    permissions.Add(PermissionType.Update);
                }
                if ((bool)dt.Rows[0]["Delete"] == true)
                {
                    permissions.Add(PermissionType.Delete);
                }
            }

            return permissions;
        }

        /// <summary>
        /// Get object permission list
        /// </summary>
        /// <param name="ObjectId">
        /// The ID of the object to check the permission for
        /// </param>
        /// <returns>
        /// A Dictionary of the permission for the object
        /// </returns>
        /// <remarks>
        /// This method is used to get the permission for the object. It is only relevent for the app object type.
        /// </remarks>
        public Dictionary<string, List<PermissionType>> GetObjectPermissionList(long ObjectId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT DataObject_ACL.*, Users.Email FROM DataObject_ACL JOIN Users ON DataObject_ACL.UserId = Users.Id WHERE DataObject_ID = @DataObject_ID;";
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@DataObject_ID", ObjectId }
            };
            DataTable dt = db.ExecuteCMD(sql, parameters);

            Dictionary<string, List<PermissionType>> permissionsList = new Dictionary<string, List<PermissionType>>();
            foreach (DataRow row in dt.Rows)
            {
                string email = row["Email"].ToString();

                List<PermissionType> permissions = new List<PermissionType>();
                if ((bool)dt.Rows[0]["Read"] == true)
                {
                    permissions.Add(PermissionType.Read);
                }
                if ((bool)dt.Rows[0]["Write"] == true)
                {
                    permissions.Add(PermissionType.Update);
                }
                if ((bool)dt.Rows[0]["Delete"] == true)
                {
                    permissions.Add(PermissionType.Delete);
                }

                permissionsList.Add(email, permissions);
            }

            return permissionsList;
        }
    }
}