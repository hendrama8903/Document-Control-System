using DMS.Common.Models;
using DMS.Hubs;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using MDC.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace DMS.Services
{
    public class DocumentReviewReminderService
    {
        private const int DaysAhead = 30;
        private const int ReminderThrottleDays = 7;

        private readonly DBContext db;
        private readonly EmailService emailService;
        private readonly IHubContext<NotificationsHub> hubContext;
        private readonly string baseUrl;

        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;
        private UserRepo userRepo = UserRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;
        private NotificationRepo notificationRepo = NotificationRepo.Instance;

        public DocumentReviewReminderService(DBContext db, IOptions<EmailConfiguration> emailOptions, IWebHostEnvironment environment,
            IHubContext<NotificationsHub> hubContext, IConfiguration configuration)
        {
            this.db = db;
            this.emailService = new EmailService(emailOptions, environment);
            this.hubContext = hubContext;
            this.baseUrl = configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
        }

        public async Task SendRemindersAsync()
        {
            IList<DocumentMaintenance> dueDocuments = documentMaintenanceRepo.GetDueForReview(DaysAhead, ReminderThrottleDays, db);

            if (dueDocuments.Count == 0)
            {
                return;
            }

            IList<User> allUsers = userRepo.Search(new User(), db, null, null);
            List<User> documentControlUsers = allUsers.Where(x => x.DOCUMENT_CONTROL_ACCESS == "1").ToList();

            IList<MSystem> emailTemplate = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "DOCUMENT_REVIEW_REMINDER_EMAIL_TEMPLATE" }, db, null, null);
            IList<MSystem> notificationTemplate = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "DOCUMENT_REVIEW_REMINDER_NOTIFICATION" }, db, null, null);

            CultureInfo cultureInfo = new CultureInfo("id-ID");

            foreach (DocumentMaintenance document in dueDocuments)
            {
                string reviewDateText = document.NEXT_REVIEW_DATE.HasValue
                    ? document.NEXT_REVIEW_DATE.Value.ToString("dddd, d MMMM yyyy", cultureInfo)
                    : "-";

                List<User> recipients = new List<User>();

                User creator = allUsers.FirstOrDefault(x => x.USERNAME == document.DOCUMENT_CREATOR);
                if (creator != null)
                {
                    recipients.Add(creator);
                }

                foreach (User docControlUser in documentControlUsers)
                {
                    if (!recipients.Any(x => x.USERNAME == docControlUser.USERNAME))
                    {
                        recipients.Add(docControlUser);
                    }
                }

                if (recipients.Count == 0)
                {
                    continue;
                }

                string subject = emailTemplate.Where(x => x.SYSTEM_CODE == "SUBJECT").First().SYSTEM_VALUE
                    .Replace("{DOCUMENT_CODE}", document.DOCUMENT_CODE);
                string title = emailTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE;
                string buttonLink = baseUrl + emailTemplate.Where(x => x.SYSTEM_CODE == "BUTTON_LINK").First().SYSTEM_VALUE
                    .Replace("{DOCUMENT_CODE}", document.DOCUMENT_CODE);

                foreach (User recipient in recipients)
                {
                    if (!string.IsNullOrEmpty(recipient.EMAIL))
                    {
                        string body = emailTemplate.Where(x => x.SYSTEM_CODE == "BODY").First().SYSTEM_VALUE
                            .Replace("{FULL_NAME}", recipient.FULL_NAME)
                            .Replace("{DOCUMENT_CODE}", document.DOCUMENT_CODE)
                            .Replace("{DOCUMENT_NAME}", document.DOCUMENT_TRANSACTION_NAME)
                            .Replace("{NEXT_REVIEW_DATE}", reviewDateText);

                        await emailService.SendEmailAsync(new List<string> { recipient.EMAIL }, subject, title, body, buttonLink);
                    }

                    Notification notification = new Notification
                    {
                        NOTIFICATION_TITLE = notificationTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", document.DOCUMENT_CODE),
                        NOTIFICATION_TEXT = notificationTemplate.Where(x => x.SYSTEM_CODE == "TEXT").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", document.DOCUMENT_CODE)
                            .Replace("{DOCUMENT_NAME}", document.DOCUMENT_TRANSACTION_NAME)
                            .Replace("{NEXT_REVIEW_DATE}", reviewDateText),
                        NOTIFICATION_URL = notificationTemplate.Where(x => x.SYSTEM_CODE == "URL").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", document.DOCUMENT_CODE),
                        USERNAME = recipient.USERNAME,
                    };

                    DBResult result = notificationRepo.Insert(notification, "SYSTEM", db);
                    if (result.status)
                    {
                        await hubContext.Clients.Group(recipient.USERNAME).SendAsync("ReceiveMessage", "Document review reminder, check notification!");
                    }
                }

                documentMaintenanceRepo.MarkReviewReminded((int)document.DOCUMENT_TRANSACTION_ID, db);
            }
        }
    }
}
