using System;
using System.Collections.Generic;
using System.Data;
using Classes;
using Microsoft.AspNetCore.Identity;

namespace Authentication
{
    /// <summary>
    /// Class that represents the Role table in the MySQL Database
    /// </summary>
    public class RoleTable
    {
        private Database _database;

        /// <summary>
        /// Constructor that takes a MySQLDatabase instance 
        /// </summary>
        /// <param name="database"></param>
        public RoleTable(Database database)
        {
            _database = database;
        }

        /// <summary>
        /// Deltes a role from the Roles table
        /// </summary>
        /// <param name="roleId">The role Id</param>
        /// <returns></returns>
        public int Delete(string roleId)
        {
            string commandText = "Delete from Roles where Id = @id";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("@id", roleId);

            return (int)_database.ExecuteNonQuery(commandText, parameters);
        }

        /// <summary>
        /// Inserts a new Role in the Roles table
        /// </summary>
        /// <param name="roleName">The role's name</param>
        /// <returns></returns>
        public int Insert(ApplicationRole role)
        {
            string commandText = "Insert into Roles (Id, Name, AllowManualAssignment, RoleDependsOn) values (@id, @name, @allowManualAssignment, @roleDependsOn)";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("@name", role.Name);
            parameters.Add("@id", role.Id);
            parameters.Add("@allowManualAssignment", role.AllowManualAssignment);
            parameters.Add("@roleDependsOn", role.RoleDependsOn.ToString());

            return (int)_database.ExecuteNonQuery(commandText, parameters);
        }

        /// <summary>
        /// Returns a role name given the roleId
        /// </summary>
        /// <param name="roleId">The role Id</param>
        /// <returns>Role name</returns>
        public string? GetRoleName(string roleId)
        {
            string commandText = "Select Name from Roles where Id = @id";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("@id", roleId);

            DataTable table = _database.ExecuteCMD(commandText, parameters);

            if (table.Rows.Count == 0)
            {
                return null;
            }
            else
            {
                return (string)table.Rows[0][0];
            }
        }

        /// <summary>
        /// Returns the role Id given a role name
        /// </summary>
        /// <param name="roleName">Role's name</param>
        /// <returns>Role's Id</returns>
        public string? GetRoleId(string roleName)
        {
            string? roleId = null;
            string commandText = "Select Id from Roles where Name = @name";
            Dictionary<string, object> parameters = new Dictionary<string, object>() { { "@name", roleName } };

            DataTable result = _database.ExecuteCMD(commandText, parameters);
            if (result.Rows.Count > 0)
            {
                return Convert.ToString(result.Rows[0][0]);
            }

            return roleId;
        }

        /// <summary>
        /// Gets the ApplicationRole given the role Id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public ApplicationRole? GetRoleById(string roleId)
        {
            string commandText = "Select Id, Name, AllowManualAssignment, RoleDependsOn from Roles where Id = @id";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("@id", roleId);

            var rows = _database.ExecuteCMDDict(commandText, parameters);
            ApplicationRole? role = null;

            if (rows.Count > 0)
            {
                var row = rows[0];
                role = new ApplicationRole();
                role.Id = (string)row["Id"];
                role.Name = (string)row["Name"];
                role.NormalizedName = ((string)row["Name"]).ToUpper();
                role.AllowManualAssignment = Convert.ToBoolean(row["AllowManualAssignment"]);
                role.RoleDependsOn = Guid.Parse((string)row["RoleDependsOn"]);
            }

            return role;
        }

        /// <summary>
        /// Gets the ApplicationRole given the role name
        /// </summary>
        /// <param name="roleName"></param>
        /// <returns></returns>
        public ApplicationRole? GetRoleByName(string roleName)
        {
            string commandText = "Select Id, Name, AllowManualAssignment, RoleDependsOn from Roles where Name = @name";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("@name", roleName);

            var rows = _database.ExecuteCMDDict(commandText, parameters);
            ApplicationRole? role = null;

            if (rows.Count > 0)
            {
                var row = rows[0];
                role = new ApplicationRole();
                role.Id = (string)row["Id"];
                role.Name = (string)row["Name"];
                role.NormalizedName = ((string)row["Name"]).ToUpper();
                role.AllowManualAssignment = Convert.ToBoolean(row["AllowManualAssignment"]);
                role.RoleDependsOn = Guid.Parse((string)row["RoleDependsOn"]);
            }

            return role;
        }

        public int Update(ApplicationRole role)
        {
            string commandText = "Update Roles set Name = @name, AllowManualAssignment = @allowManualAssignment, RoleDependsOn = @roleDependsOn where Id = @id";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("@id", role.Id);
            parameters.Add("@name", role.Name);
            parameters.Add("@allowManualAssignment", role.AllowManualAssignment);
            parameters.Add("@roleDependsOn", role.RoleDependsOn.ToString());

            return (int)_database.ExecuteNonQuery(commandText, parameters);
        }

        public List<ApplicationRole> GetRoles()
        {
            List<ApplicationRole> roles = new List<ApplicationRole>();

            string commandText = "Select Id, Name, AllowManualAssignment, RoleDependsOn from Roles";

            var rows = _database.ExecuteCMDDict(commandText);
            foreach (Dictionary<string, object> row in rows)
            {
                ApplicationRole role = (ApplicationRole)Activator.CreateInstance(typeof(ApplicationRole));
                role.Id = (string)row["Id"];
                role.Name = (string)row["Name"];
                role.NormalizedName = ((string)row["Name"]).ToUpper();
                role.AllowManualAssignment = Convert.ToBoolean(row["AllowManualAssignment"]);
                role.RoleDependsOn = Guid.Parse((string)row["RoleDependsOn"]);
                roles.Add(role);
            }

            return roles;
        }
    }
}
