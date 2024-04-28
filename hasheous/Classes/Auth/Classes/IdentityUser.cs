using hasheous_server.Classes;
using Microsoft.AspNetCore.Identity;
using System;
using System.Security.Claims;

namespace Authentication
{
    /// <summary>
    /// Class that implements the ASP.NET Identity
    /// IUser interface 
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        public SecurityProfileViewModel SecurityProfile { get; set; }

        public static implicit operator ClaimsPrincipal?(ApplicationUser? v)
        {
            throw new NotImplementedException();
        }
    }
}
