namespace VisaStatusApi
{
    public class Settings
    {
        public bool isuat { get; set; }
        public string dbproduction { get; set; } = null!;
        public string dbuat { get; set; } = null!;
        public int modifieduserid { get; set; } = 0;
        public LogSetting logsetting { get; set; } = null!;
        public int defaultstageid { get; set; }

        public MailSetting mailsetting { get; set; } = null!;

        public string token { get; set; } = null!;


    }

    public class LogSetting
    {
        public string filename { get; set; } = null!;
    }

    public class MailSetting
    {
        public string host { get; set; } = null!;
        public string username { get; set; } = null!;
        public string password { get; set; } = null!;
        public string fromemail { get; set; } = null!;
        public string name { get; set; } = null!;
        public int port { get; set; }
        public bool enablessl { get; set; }
        public string cc { get; set; } = null!;
        public string bcc { get; set; } = null!;
        public string to { get; set; } = null!;

    }

}
