using System.ComponentModel.DataAnnotations;
using Apartment.Enums;

namespace Apartment.ViewModels
{
    public class UserList
    {
        public int Id { get; set; }

        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        public UserRoles Role { get; set; }

        [Display(Name = "Creation Date")]
        public DateTime CreationDate { get; set; }

        [Display(Name = "Status")]
        public string? Status { get; set; }

        [Display(Name = "Last Login")]
        public DateTime? LastLogin { get; set; }

        public string? DeactivationReason { get; set; }
    }
}

