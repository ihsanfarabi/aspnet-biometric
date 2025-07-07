using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using aspnet_biometric.Models;
using aspnet_biometric.Data;
using Microsoft.EntityFrameworkCore;

namespace aspnet_biometric.Pages
{
    [Authorize]
    public class HomeModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public HomeModel(UserManager<User> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public string UserDisplayName { get; set; } = string.Empty;
        public int PasskeyCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            UserDisplayName = user.DisplayName ?? user.Email ?? "User";
            
            // Count the number of passkeys/credentials for this user
            PasskeyCount = await _context.Fido2Credentials
                .Where(c => c.UserId == user.Id)
                .CountAsync();

            return Page();
        }
    }
} 