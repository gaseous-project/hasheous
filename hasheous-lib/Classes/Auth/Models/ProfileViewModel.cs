namespace Authentication
{
    public class ProfileBasicViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string EmailAddress { get; set; }
        public bool EmailConfirmed { get; set; }
        public List<String> Roles { get; set; }
        public SecurityProfileViewModel SecurityProfile { get; set; }
        public string HighestRole
        {
            get
            {
                string _highestRole = "";
                foreach (string role in Roles)
                {
                    switch (role)
                    {
                        case "Admin":
                            // there is no higher
                            _highestRole = role;
                            break;
                        case "Moderator":
                            // only one high is Admin, so check for that
                            if (_highestRole != "Admin")
                            {
                                _highestRole = role;
                            }
                            break;
                        case "Member":
                            // make sure _highestRole isn't already set to Moderator or Admin
                            if (_highestRole != "Admin" && _highestRole != "Moderator")
                            {
                                _highestRole = role;
                            }
                            break;
                        case "Verified Email":
                            // Verified Email is a status role, not hierarchical - skip it
                            break;
                        default:
                            if (_highestRole == "")
                            {
                                _highestRole = "Member";
                            }
                            break;
                    }
                }

                if (_highestRole == "")
                {
                    _highestRole = "Member";
                }

                return _highestRole;
            }
        }
    }
}