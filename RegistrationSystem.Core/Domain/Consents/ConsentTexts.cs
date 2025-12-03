namespace RegistrationSystem.Core.Domain.Consents;

/// <summary>
/// Provides consent text templates for different user roles.
/// Update version numbers and text when consent language changes.
/// </summary>
public static class ConsentTexts
{
    // Version constants - update these when consent text changes
    private const string ParentVersion = "parent-v1";
    private const string TeacherVersion = "teacher-v1";
    private const string AdultVersion = "adult-v1";

    /// <summary>
    /// Gets the consent text version identifier for the given role and year.
    /// </summary>
    public static string GetVersion(ConsentRole role, int year) => role switch
    {
        ConsentRole.ParentGuardian => $"{ParentVersion}-{year}",
        ConsentRole.Teacher => $"{TeacherVersion}-{year}",
        ConsentRole.AdultStudent => $"{AdultVersion}-{year}",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    /// <summary>
    /// Gets the full consent text for the given role, competition name, and year.
    /// </summary>
    public static string GetConsentText(ConsentRole role, string competitionName, int year) => role switch
    {
        ConsentRole.ParentGuardian => GetParentGuardianText(competitionName, year),
        ConsentRole.Teacher => GetTeacherText(competitionName, year),
        ConsentRole.AdultStudent => GetAdultStudentText(competitionName, year),
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    /// <summary>
    /// Gets the consent title for the given role.
    /// </summary>
    public static string GetTitle(ConsentRole role, int year) => role switch
    {
        ConsentRole.ParentGuardian => $"Parent / Guardian Consent – {year}",
        ConsentRole.Teacher => $"Teacher / Institution Representative Consent – {year}",
        ConsentRole.AdultStudent => $"Adult Participant Consent – {year}",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static string GetParentGuardianText(string competitionName, int year) => $"""
        I am the parent or legal guardian of any child I register using this account for the {year} {competitionName} ("Competition").

        By continuing, I confirm that:

        1. I am authorized to provide consent on behalf of each child I register.

        2. I consent to the collection and use of my child's personal information (including name, date of birth, contact details, ID document, and photo) for the purposes of registration, eligibility verification, scheduling, judging, and administration of the Competition, as described in the Privacy Policy.

        3. I understand that the Competition may be photographed, recorded, and livestreamed. I consent to my child's name, image, voice, recitation, city, and competition results being displayed during livestreams, broadcasts, and official Competition materials, including on imamshatibi.org, register.imamshatibi.org, and official social media and promotional channels.

        4. If my child is selected as a winner eligible for an Umrah prize, I consent to the collection and use of passport and travel details for the sole purpose of arranging group travel, as described in the Privacy Policy and Terms of Service.

        5. I have read and agree to the Terms of Service, and I will ensure that each child I register abides by the official Rules and Regulations of the Competition.

        I understand that by using this account to register any participant under 18 for the {year} Competition, this consent will apply to all such registrations.
        """;

    private static string GetTeacherText(string competitionName, int year) => $"""
        I am a teacher or authorized representative of a Qur'an institution, school, or program. I am using this account to register students for the {year} {competitionName} ("Competition").

        By continuing, I confirm that:

        1. I have obtained written permission or equivalent authorization from the parent or legal guardian of each minor I register, allowing the child to participate in the Competition.

        2. I consent, on behalf of my institution, to the collection and use of the students' personal information (including name, date of birth, contact details, ID document, and photo) for the purposes of registration, eligibility verification, scheduling, judging, and administration of the Competition, as described in the Privacy Policy.

        3. I understand that the Competition may be photographed, recorded, and livestreamed. I confirm that parents or guardians have been informed that their child's name, image, voice, recitation, city, and competition results may appear during livestreams, broadcasts, and official Competition materials, including on imamshatibi.org, register.imamshatibi.org, and official social media and promotional channels.

        4. If any student I register is selected as a winner eligible for an Umrah prize, I understand that their parent or legal guardian will be required to provide passport and travel details for the sole purpose of arranging group travel, as described in the Privacy Policy and Terms of Service.

        5. I have read and agree to the Terms of Service, and I will ensure that the students I register are aware of and comply with the official Rules and Regulations of the Competition.

        I understand that by using this account to register minors for the {year} Competition, I am representing that appropriate parental/guardian consent has already been obtained for each student.
        """;

    private static string GetAdultStudentText(string competitionName, int year) => $"""
        I am at least 18 years old and I am registering myself for the {year} {competitionName} ("Competition").

        By continuing, I confirm that:

        1. I consent to the collection and use of my personal information (including name, date of birth, contact details, ID document, and photo) for the purposes of registration, eligibility verification, scheduling, judging, and administration of the Competition, as described in the Privacy Policy.

        2. I understand that the Competition may be photographed, recorded, and livestreamed. I consent to my name, image, voice, recitation, city, and competition results being displayed during livestreams, broadcasts, and official Competition materials, including on imamshatibi.org, register.imamshatibi.org, and official social media and promotional channels.

        3. If I am selected as a winner eligible for an Umrah prize, I consent to the collection and use of my passport and travel details for the sole purpose of arranging group travel, as described in the Privacy Policy and Terms of Service.

        4. I have read and agree to the Terms of Service, and I agree to follow all official Rules and Regulations of the Competition.

        I understand that this consent applies to my participation in the {year} Competition.
        """;
}