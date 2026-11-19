using Microsoft.AspNetCore.Identity;

namespace Haworth.MdPipeline.Data.Identity;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
