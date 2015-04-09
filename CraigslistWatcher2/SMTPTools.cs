using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mime;

namespace CraigslistWatcher2
{
    internal static class SMTPTools
    {
        internal static void SendMail(
            string emailTo,
            string subject,
            string body,
            bool isBodyHTML = false,
            string[] attachements = null)
        {
            bool enableSSL = true;

            //if (attachements != null && attachements.Length > 0
            //    && emailTo.Contains("@mailasail.com"))
            //    emailTo = emailTo.Replace("@mailasail.com", "+attach@mailasail.com");

            using (MailMessage mail = new MailMessage())
            {
                mail.From = new MailAddress(CraigslistWatcher2.Settings.Default.smtpuser);
                mail.To.Add(emailTo);
                mail.Subject = subject;
                mail.Body = body;
                mail.BodyEncoding = Encoding.UTF8;
                mail.IsBodyHtml = isBodyHTML;

                if (attachements != null)
                {
                    foreach (string file in attachements)
                    {
                        Attachment attachment = new Attachment(file, MediaTypeNames.Application.Octet);
                        ContentDisposition disposition = attachment.ContentDisposition;
                        disposition.CreationDate = File.GetCreationTime(file);
                        disposition.ModificationDate = File.GetLastWriteTime(file);
                        disposition.ReadDate = File.GetLastAccessTime(file);
                        disposition.FileName = Path.GetFileName(file);
                        disposition.Size = new FileInfo(file).Length;
                        disposition.DispositionType = DispositionTypeNames.Attachment;
                        mail.Attachments.Add(attachment);
                    }
                }

                using (SmtpClient smtp = new SmtpClient(CraigslistWatcher2.Settings.Default.smtpserver, CraigslistWatcher2.Settings.Default.smtpport))
                {
                    smtp.Credentials = new NetworkCredential(CraigslistWatcher2.Settings.Default.smtpuser, SecurityTools.ToInsecureString(SecurityTools.DecryptString(CraigslistWatcher2.Settings.Default.smtppassword)));
                    smtp.EnableSsl = enableSSL;
                    smtp.Send(mail);
                }
            }
        }

        internal static void TrySMTP()
        {
            bool haveInfo = false;
            while (!haveInfo)
            {
                if (!String.IsNullOrEmpty(CraigslistWatcher2.Settings.Default.smtpserver)
                       && !String.IsNullOrEmpty(CraigslistWatcher2.Settings.Default.smtpport.ToString())
                       && !String.IsNullOrEmpty(CraigslistWatcher2.Settings.Default.smtpuser)
                       && !String.IsNullOrEmpty(CraigslistWatcher2.Settings.Default.smtppassword))
                {
                    haveInfo = true;
                }
                else
                {
                    Console.WriteLine("Invalid SMTP configuration, please re-enter information");
                    Console.WriteLine("Please enter SMTP server address (example: smtp.gmail.com):");
                    CraigslistWatcher2.Settings.Default.smtpserver = Console.ReadLine();
                    Console.WriteLine("Please enter SMTP server port (example: 587):");
                    CraigslistWatcher2.Settings.Default.smtpport = Convert.ToInt32(Console.ReadLine());
                    Console.WriteLine("Please enter user name (example: name@gmail.com):");
                    CraigslistWatcher2.Settings.Default.smtpuser = Console.ReadLine();
                    Console.WriteLine("Please enter password:");
                    ConsoleKeyInfo key; String pass = "";
                    do
                    {//https://stackoverflow.com/questions/3404421/password-masking-console-application
                        key = Console.ReadKey(true);

                        // Backspace Should Not Work
                        if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                        {
                            pass += key.KeyChar;
                            Console.Write("*");
                        }
                        else
                        {
                            if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                            {
                                pass = pass.Substring(0, (pass.Length - 1));
                                Console.Write("\b \b");
                            }
                        }
                    }
                    // Stops Receving Keys Once Enter is Pressed
                    while (key.Key != ConsoleKey.Enter);
                    CraigslistWatcher2.Settings.Default.smtppassword = SecurityTools.EncryptString(SecurityTools.ToSecureString(pass));
                    CraigslistWatcher2.Settings.Default.Save();
                }
            }
        }
    }
}
