using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace aspnet_biometric.Models
{
    public class Fido2Credential
    {
        [Key]
        public int Id { get; set; }
        
        // Foreign key to User
        [Required]
        public string UserId { get; set; } = null!;
        
        // Navigation property
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
        
        // FIDO2 specific fields
        public byte[]? PublicKey { get; set; }
        public byte[]? UserHandle { get; set; }
        public uint SignatureCounter { get; set; }
        public string? CredType { get; set; }
        public DateTime RegDate { get; set; }
        public Guid AaGuid { get; set; }
    }
}