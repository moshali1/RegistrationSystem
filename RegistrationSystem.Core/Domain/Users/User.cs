namespace RegistrationSystem.Core.Domain.Users;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    // From Azure Claims (synced on login)
    public string ObjectIdentifier { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty; // Teacher, Parent, Student
    public string Role { get; set; } = string.Empty; // Admin, or empty

    // Profile info (synced to Azure via Graph API)
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    // One-time profile verification - once true, user cannot edit profile
    public bool IsProfileVerified { get; set; }
}