using System.Net;

namespace RegistrationSystem.Core.Application.Messaging;

public static class PendingStatusEmailBuilder
{
    public static (string Subject, string PlainText, string Html) Build(
        string parentName,
        string competitorFullName,
        string cid,
        string divisionName,
        string categoryName,
        string statusComment,
        string registrationEditUrl,
        DateTimeOffset? registrationDeadline,
        string siteUrl)
    {
        var subject = $"Action Required: Registration Form {cid} \u2013 Status Pending";

        var deadlineText = registrationDeadline.HasValue
            ? registrationDeadline.Value.ToString("MMMM d, yyyy")
            : "as soon as possible";

        var logoUrl = $"{siteUrl.TrimEnd('/')}/images/bannerlogo.png";

        var plainText = $"""
            Imam Al-Shatibi Quran Competition

            Assalamu Alaikum {parentName},

            We are reaching out regarding the registration form submitted for {competitorFullName} (CID: {cid}) in the {divisionName} - {categoryName} category.

            After reviewing the submitted information, we found the following issue(s) that need to be addressed:

            Reason: {statusComment}

            Please update the registration form by {deadlineText}.

            To update the registration, visit:
            {registrationEditUrl}

            If you have any questions or need assistance, please don't hesitate to contact us at contact@imamshatibi.org.

            JazakAllahu khayran,
            Imam Al-Shatibi Quran Competition Team
            """;

        var safeParentName = WebUtility.HtmlEncode(parentName);
        var safeCompetitorName = WebUtility.HtmlEncode(competitorFullName);
        var safeCid = WebUtility.HtmlEncode(cid);
        var safeDivision = WebUtility.HtmlEncode(divisionName);
        var safeCategory = WebUtility.HtmlEncode(categoryName);
        var safeComment = WebUtility.HtmlEncode(statusComment);

        var html = $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; color: #1e293b;">
                <!-- Banner Logo -->
                <div style="text-align: center; padding: 24px 24px 16px; background-color: #f8fafc; border-radius: 8px 8px 0 0;">
                    <img src="{logoUrl}" alt="Imam Al-Shatibi Quran Competition" style="max-width: 280px; height: auto;" />
                </div>

                <!-- Header -->
                <div style="background-color: #0e7490; padding: 20px 24px;">
                    <h1 style="color: white; margin: 0; font-size: 18px; font-weight: 600;">Action Required: Registration Status Pending</h1>
                </div>

                <!-- Body -->
                <div style="padding: 24px; border: 1px solid #e2e8f0; border-top: none; border-radius: 0 0 8px 8px;">
                    <p style="margin: 0 0 16px;">Assalāmu ʿalaykum <strong>{safeParentName}</strong>,</p>

                    <p style="margin: 0 0 16px;">We are reaching out regarding the registration form submitted for <strong>{safeCompetitorName}</strong> (CID: <code style="background-color: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 13px;">{safeCid}</code>) in the <strong>{safeDivision} &ndash; {safeCategory}</strong> category.</p>

                    <p style="margin: 0 0 12px;">After reviewing the submitted information, we found the following issue(s) that need to be addressed:</p>

                    <!-- Reason Box -->
                    <div style="background-color: #fef3c7; border-left: 4px solid #f59e0b; border-radius: 4px; padding: 16px; margin: 0 0 16px;">
                        <strong style="color: #92400e; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px;">Reason</strong>
                        <p style="margin: 8px 0 0; color: #78350f;">{safeComment}</p>
                    </div>

                    <p style="margin: 0 0 20px;">Please update the registration form by <strong>{deadlineText}</strong>.</p>

                    <!-- CTA Button -->
                    <div style="text-align: center; margin: 0 0 24px;">
                        <a href="{registrationEditUrl}" style="display: inline-block; background-color: #0e7490; color: white; padding: 14px 32px; border-radius: 6px; text-decoration: none; font-weight: 600; font-size: 15px;">Update Registration</a>
                    </div>

                    <p style="margin: 0 0 16px; color: #64748b; font-size: 14px;">If you have any questions or need assistance, please don&rsquo;t hesitate to contact us at <a href="mailto:contact@imamshatibi.org" style="color: #0e7490; text-decoration: none;">contact@imamshatibi.org</a>.</p>

                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;" />

                    <p style="margin: 0; color: #64748b; font-size: 13px;">Jazāk Allāhu khayran,</p>
                    <p style="margin: 4px 0 0; color: #475569; font-size: 13px; font-weight: 600;">Imam Al-Shatibi Quran Competition Team</p>
                </div>
            </div>
            """;

        return (subject, plainText, html);
    }
}
