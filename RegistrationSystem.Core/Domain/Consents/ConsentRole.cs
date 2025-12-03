namespace RegistrationSystem.Core.Domain.Consents;

/// <summary>
/// How the person is acting when giving consent.
/// </summary>
public enum ConsentRole
{
    /// <summary>
    /// Parent or legal guardian consenting for minors they register.
    /// </summary>
    ParentGuardian,

    /// <summary>
    /// Teacher or institution representative consenting for students they register.
    /// </summary>
    Teacher,

    /// <summary>
    /// Adult participant (18+) consenting for themselves.
    /// </summary>
    AdultStudent
}
