using System;
using System.Collections.Generic;
using System.Data;
using hasheous_server.Classes;
using Microsoft.AspNetCore.Identity;

namespace Authentication
{
    /// <summary>
    /// Class that implements the ASP.NET Identity
    /// IRole interface 
    /// </summary>
    public class ApplicationRole : IdentityRole
    {
        /// <summary>
        /// Flag indicating whether this role can be manually assigned by administrators
        /// </summary>
        public bool AllowManualAssignment { get; set; } = false;

        /// <summary>
        /// If set, indicates this role depends on another role (referenced by GUID).
        /// Establishes a hierarchy of roles (e.g., Admin depends on Moderator depends on Member)
        /// </summary>
        public Guid RoleDependsOn { get; set; } = Guid.Empty;
    }
}
