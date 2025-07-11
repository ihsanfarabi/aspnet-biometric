using Microsoft.AspNetCore.Identity;

namespace aspnet_biometric.Models
{
    public class User : IdentityUser
    {
        // Keep the original byte[] Id for FIDO2 compatibility
        public byte[]? Fido2Id { get; set; }
        public string? DisplayName { get; set; }
        
        // Flag to enable/disable biometric authentication without deleting credentials
        public bool IsBiometricEnabled { get; set; } = false;

        // Navigation property for FIDO2 credentials
        public virtual ICollection<Fido2Credential> Fido2Credentials { get; set; } = new List<Fido2Credential>();
    }
}