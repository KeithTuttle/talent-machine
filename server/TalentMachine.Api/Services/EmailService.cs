using System.Net;
using System.Net.Mail;

namespace TalentMachine.Api.Services;

/// <summary>
/// Sends email via Gmail SMTP (an App Password, not the account password — see
/// appsettings.json's Email block for setup). Uses the built-in SmtpClient rather
/// than a mail library; fine at this volume (Gmail caps free accounts ~500/day).
/// Nothing here sends automatically without an explicit caller action.
/// </summary>
public class EmailService
{
    private readonly string? _user;
    private readonly string? _appPassword;
    private readonly string _appUrl;
    private readonly ILogger<EmailService> _logger;

    private const string FromName = "The Talent Machine Company";

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _user = config["Email:GmailUser"];
        _appPassword = config["Email:GmailAppPassword"];
        _appUrl = (config["Email:AppUrl"] ?? "http://localhost:5201").TrimEnd('/');
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_user) && !string.IsNullOrWhiteSpace(_appPassword);
    public string AppUrl => _appUrl;

    /// <summary>
    /// Sends a plain-text email to one or more recipients (as BCC so families
    /// don't see each other's addresses), optionally with a PDF attachment.
    /// Returns false (never throws) on any failure so a mail outage never blocks
    /// the caller. <paramref name="recipients"/> are placed on BCC; the sending
    /// account is the visible To.
    /// </summary>
    public async Task<bool> SendAsync(
        IEnumerable<string> recipients, string subject, string body,
        byte[]? pdf = null, string? pdfName = null, CancellationToken ct = default)
    {
        if (!IsConfigured) return false;
        var to = recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList();
        if (to.Count == 0) return false;

        using var message = new MailMessage
        {
            From = new MailAddress(_user!, FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        // Send to self (visible), everyone else BCC'd for privacy.
        message.To.Add(_user!);
        foreach (var r in to) message.Bcc.Add(r);

        MemoryStream? stream = null;
        if (pdf is not null)
        {
            stream = new MemoryStream(pdf);
            message.Attachments.Add(new Attachment(stream, pdfName ?? "attachment.pdf", "application/pdf"));
        }

        using var client = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_user, _appPassword),
        };

        try
        {
            await client.SendMailAsync(message, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email to {Count} recipient(s)", to.Count);
            return false;
        }
        finally
        {
            stream?.Dispose();
        }
    }

    /// <summary>Sends an invite email with the join code. Never throws.</summary>
    public async Task<bool> SendInviteEmailAsync(
        string toEmail, string inviteeName, string tenantName, string code, CancellationToken ct = default)
    {
        if (!IsConfigured) return false;

        var subject = $"You're invited to join {tenantName} on The Talent Machine";
        var body = $"""
            Hi {inviteeName},

            You've been invited to join "{tenantName}" on The Talent Machine Company's
            production planner.

            To join:
              1. Go to {_appUrl} and sign in (or create an account with this email).
              2. Open the Team page.
              3. Under "Join a team", enter this invite code:

                     {code}

            That's it — you'll see the shows you've been given access to.

            — The Talent Machine Company
            """;

        using var message = new MailMessage
        {
            From = new MailAddress(_user!, FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_user, _appPassword),
        };

        try
        {
            await client.SendMailAsync(message, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send invite email to {Email}", toEmail);
            return false;
        }
    }
}
