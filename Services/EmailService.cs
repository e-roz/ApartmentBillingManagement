using Apartment.Options;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Apartment.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly IAuditService _auditService;

        public EmailService(IOptions<EmailSettings> emailSettings, IAuditService auditService)
        {
            _emailSettings = emailSettings.Value;
            _auditService = auditService;
        }

        public async Task SendNewAccountEmailAsync(string email, string username, string temporaryPassword)
        {
            using (var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort))
            {
                client.Credentials = new NetworkCredential(_emailSettings.SmtpUser, _emailSettings.SmtpPass);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromAddress, _emailSettings.FromName),
                    Subject = "Your New Account Credentials",
                    Body = $@"
                        <p>Hello {username},</p>
                        <p>An account has been created for you in the Apartment Management System.</p>
                        <p>Your login details are:</p>
                        <ul>
                            <li><strong>Username:</strong> {email}</li>
                            <li><strong>Temporary Password:</strong> {temporaryPassword}</li>
                        </ul>
                        <p>You will be required to change your password upon your first login.</p>
                        <br>
                        <p>Thank you,</p>
                        <p>The Apartment Management Team</p>
                    ",
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
            }

            await _auditService.LogAsync(
                action: Enums.AuditActionType.SystemAction,
                userId: -1, // System action
                details: $"Sent new account email with temporary password to {email}.",
                entityType: "Email"
            );
        }

        public async Task SendPasswordResetEmailAsync(string email, string username, string temporaryPassword)
        {
            using (var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort))
            {
                client.Credentials = new NetworkCredential(_emailSettings.SmtpUser, _emailSettings.SmtpPass);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromAddress, _emailSettings.FromName),
                    Subject = "Password Reset - Your New Temporary Password",
                    Body = $@"
                        <p>Hello {username},</p>
                        <p>Your password has been reset by an administrator. This is your new temporary password as requested:</p>
                        <p style=""font-size: 16px; font-weight: bold; color: #6BB57C; padding: 10px; background-color: #f0f8f2; border-radius: 5px; text-align: center; margin: 20px 0;"">
                            {temporaryPassword}
                        </p>
                        <p><strong>Important:</strong> You will be required to change this password upon your next login for security purposes.</p>
                        <p>If you did not request this password reset, please contact the administrator immediately.</p>
                        <br>
                        <p>Thank you,</p>
                        <p>The Apartment Management Team</p>
                    ",
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
            }

            await _auditService.LogAsync(
                action: Enums.AuditActionType.SystemAction,
                userId: -1, // System action
                details: $"Sent password reset email with temporary password to {email}.",
                entityType: "Email"
            );
        }
    }
}
