using Markdig;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp;
using System.Security.Authentication;


namespace VisaStatusApi
{
    public static class MarkdownParser
    {
        public static string Parse(string markdown)
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
            return Markdown.ToHtml(markdown, pipeline);
        }
    }

    public class MailTaglist
    {
        public string companyname { get; set; } = null!;
        public string buname { get; set; } = null!;
        public string reqtitle { get; set; } = null!;
        public string jobcode { get; set; } = null!;
        public string candidatename { get; set; } = null!;

        public string candidateno { get; set; } = null!;

        public string stage { get; set; } = null!;

        public string status { get; set; } = null!;

        public string statusdate { get; set; } = null!;
        public string ccemail { get; set; } = null!;
        public string bccemail { get; set; } = null!;
        public string error { get; set; } = null!;


    }

    public class Mailer<T>
    {
        public static bool EmailWithParser(IOptions<Settings> _appsetting,
           Microsoft.Extensions.Logging.ILogger _logger,
            string MailToName, string MailToAddress, string subject, string mailtemplate, MailTaglist mailTaglist, string MailCCAddress, string MailBCCAddress)
        {
            bool _result;
            try
            {


                string sEmailTemplatePath = String.Format("{0}\\{1}.md",
                    AppDomain.CurrentDomain.BaseDirectory, mailtemplate);

                string sBody = "";
                sBody = System.IO.File.ReadAllText(sEmailTemplatePath);

                if (!string.IsNullOrEmpty(sBody))
                {

                    sBody = sBody.Replace("{{companyname}}", Convert.ToString(mailTaglist.companyname));
                    sBody = sBody.Replace("{{buname}}", Convert.ToString(mailTaglist.buname));
                    sBody = sBody.Replace("{{reqtitle}}", Convert.ToString(mailTaglist.reqtitle));
                    sBody = sBody.Replace("{{jobcode}}", Convert.ToString(mailTaglist.jobcode));
                    sBody = sBody.Replace("{{candidatename}}", Convert.ToString(mailTaglist.candidatename));
                    sBody = sBody.Replace("{{candidateno}}", Convert.ToString(mailTaglist.candidateno));
                    sBody = sBody.Replace("{{stage}}", Convert.ToString(mailTaglist.stage));
                    sBody = sBody.Replace("{{status}}", Convert.ToString(mailTaglist.status));
                    //sBody = sBody.Replace("{{statusdate}}", Convert.ToString(mailTaglist.statusdate));
                    sBody = sBody.Replace("{{statusdate}}", DateTime.UtcNow.ToString("dd-MMM-yyyy"));
                    sBody = sBody.Replace("{{error}}", Convert.ToString(mailTaglist.error));


                }


                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_appsetting.Value.mailsetting.name, _appsetting.Value.mailsetting.fromemail));
                message.To.Add(new MailboxAddress(MailToName, MailToAddress));



                if (!string.IsNullOrEmpty(MailCCAddress))
                {
                    foreach (var item in MailCCAddress.Split(";"))
                    {
                        if (!string.IsNullOrEmpty(item))
                            message.Cc.Add(new MailboxAddress(item, item));
                    }
                }

                if (!string.IsNullOrEmpty(MailBCCAddress))
                {
                    foreach (var item in MailBCCAddress.Split(";"))
                    {
                        if (!string.IsNullOrEmpty(item))
                            message.Bcc.Add(new MailboxAddress(item, item));
                    }
                }


                message.Subject = subject;
                message.Body = new TextPart(MimeKit.Text.TextFormat.Html)
                {
                    Text = MarkdownParser.Parse(sBody)
                };
                using (var client = new SmtpClient.SmtpClient())
                {
                    client.CheckCertificateRevocation = false;
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    client.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 |
                        SslProtocols.Tls12 | SslProtocols.Tls13;

                    client.Connect(_appsetting.Value.mailsetting.host, _appsetting.Value.mailsetting.port, SecureSocketOptions.Auto);
                    client.Authenticate(_appsetting.Value.mailsetting.username, _appsetting.Value.mailsetting.password);
                    client.Send(message);
                    client.Disconnect(true);
                }
                _result = true;

            }
            catch (Exception ex)
            {
                _result = false;
                _logger.LogError(string.Format("Error : {0}", ex.Message));
            }

            return _result;
        }

    }
}
