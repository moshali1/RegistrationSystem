using System.Net;

namespace RegistrationSystem.Core.Application.Messaging;

public static class PendingStatusEmailBuilder
{
    public static (string Subject, string PlainText, string Html) Build(
        string creatorDisplayName,
        string competitorFullName,
        string cid,
        string divisionName,
        string categoryName,
        string statusComment,
        string registrationEditUrl,
        DateTimeOffset? registrationDeadline)
    {
        var subject = $"Action Required: Registration {cid} Needs Correction";

        var deadlineText = registrationDeadline.HasValue
            ? $"Please make the corrections before {registrationDeadline.Value:MMMM d, yyyy}."
            : "Please make the corrections as soon as possible.";

        var safeComment = WebUtility.HtmlEncode(statusComment);

        var plainText = $"""
            Assalamu Alaikum {creatorDisplayName},

            Your registration for {competitorFullName} (ID: {cid}) in the {divisionName} - {categoryName} category has been reviewed and requires your attention.

            Reason: {statusComment}

            {deadlineText}

            To update the registration, visit:
            {registrationEditUrl}

            If you have questions, please reply to this email.

            JazakAllah Khair,
            Imam Al-Shatibi Quran Competition
            """;

        var html = $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; color: #1e293b;">
                <div style="background-color: #0e7490; padding: 24px; border-radius: 8px 8px 0 0;">
                    <h1 style="color: white; margin: 0; font-size: 20px;">Registration Requires Correction</h1>
                </div>
                <div style="padding: 24px; border: 1px solid #e2e8f0; border-top: none; border-radius: 0 0 8px 8px;">
                    <p>Assalamu Alaikum <strong>{WebUtility.HtmlEncode(creatorDisplayName)}</strong>,</p>
                    <p>Your registration for <strong>{WebUtility.HtmlEncode(competitorFullName)}</strong> (ID: <code>{WebUtility.HtmlEncode(cid)}</code>) in the <strong>{WebUtility.HtmlEncode(divisionName)} &ndash; {WebUtility.HtmlEncode(categoryName)}</strong> category has been reviewed and requires your attention.</p>
                    <div style="background-color: #fef3c7; border: 1px solid #f59e0b; border-radius: 6px; padding: 16px; margin: 16px 0;">
                        <strong style="color: #92400e;">Reason:</strong>
                        <p style="margin: 8px 0 0; color: #78350f;">{safeComment}</p>
                    </div>
                    <p>{deadlineText}</p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{registrationEditUrl}" style="display: inline-block; background-color: #0e7490; color: white; padding: 12px 24px; border-radius: 6px; text-decoration: none; font-weight: 600;">Update Registration</a>
                    </div>
                    <p style="color: #64748b; font-size: 14px;">If you have questions, please reply to this email.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="color: #94a3b8; font-size: 12px; margin: 0;">Imam Al-Shatibi Quran Competition</p>
                </div>
            </div>
            """;

        return (subject, plainText, html);
    }
}
