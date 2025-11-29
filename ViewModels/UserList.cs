using System.ComponentModel.DataAnnotations;
using Apartment.Enums;

namespace Apartment.ViewModels
{
    public class UserList
    {
        public int Id { get; set; }

        [Display(Name = "Username")]
        public string Username { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        public UserRoles Role { get; set; }

        [Display(Name = "Creation Date")]
        public DateTime CreationDate { get; set; }
    }
}

