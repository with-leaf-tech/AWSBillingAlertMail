using Amazon;
using Amazon.CostExplorer;
using Amazon.CostExplorer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AWSBillingAlertMail {
    class Program {
        static Thread thread = null;

        /// <summary>
        /// コンソールエントリ
        /// </summary>
        static void Main(string[] args) {
            thread = new Thread(Main_Loop);
            thread.Start();
        }

        /// <summary>
        /// メインループ 無限ループだが気にしない
        /// </summary>
        private static void Main_Loop() {
            string AWSAcessKey = System.Configuration.ConfigurationManager.AppSettings["AWSAcessKey"]; // AWSアクセスキー
            string AWSSecretKey = System.Configuration.ConfigurationManager.AppSettings["AWSSecretKey"]; // AWSシークレットキー

            string smtpServer = System.Configuration.ConfigurationManager.AppSettings["smtpServer"]; // SMTPサーバ
            string fromMailAddress = System.Configuration.ConfigurationManager.AppSettings["fromMailAddress"]; // 送信元メールアドレス
            string fromAccount = System.Configuration.ConfigurationManager.AppSettings["fromAccount"]; // 送信元メールアカウント
            string fromPassword = System.Configuration.ConfigurationManager.AppSettings["fromPassword"]; // 送信元メールアカウントパスワード
            int fromPort = int.Parse(System.Configuration.ConfigurationManager.AppSettings["fromPort"]); // SMTPポート
            string sendMailAddress = System.Configuration.ConfigurationManager.AppSettings["sendMailAddress"]; // 送信先メールアドレス
            string checkTime = System.Configuration.ConfigurationManager.AppSettings["checkTime"]; // 料金チェックする時間(HH:mm)
            decimal limit = decimal.Parse(System.Configuration.ConfigurationManager.AppSettings["limit"]); // 超過とみなすAmountの閾値

            while (true) {
                DateTime nowTime = DateTime.Now;
                if(nowTime.ToString("yyyy/MM/dd HH:mm") == nowTime.ToString("yyyy/MM/dd ") + checkTime) {
                    string ret = getLimitoverCost(AWSAcessKey, AWSSecretKey, limit);
                    if(ret.Length > 0) {
                        // 超過した場合メール送信
                        sendMail(smtpServer, fromAccount, fromPassword, fromMailAddress, fromPort, sendMailAddress, "AWSコスト超過", ret);
                        Console.WriteLine("AWSコスト確認 " + nowTime.ToString("yyyy/MM/dd ") + checkTime + " コスト超過");
                    }
                    else {
                        Console.WriteLine("AWSコスト確認 " + nowTime.ToString("yyyy/MM/dd ") + checkTime + " 異常なし");
                    }
                    Thread.Sleep(60000);
                }
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// コスト超過しているか確認する
        /// </summary>
        /// <param name="awsAccessKey">AWSアクセスキー</param>
        /// <param name="awsSecretKey">AWSシークレットキー</param>
        /// <param name="limit">超過とみなす閾値</param>
        private static string getLimitoverCost(string awsAccessKey, string awsSecretKey, decimal limit) {
            AmazonCostExplorerClient client = new AmazonCostExplorerClient(awsAccessKey, awsSecretKey, RegionEndpoint.USEast1);
            GetCostAndUsageRequest req = new GetCostAndUsageRequest();
            req.TimePeriod = new DateInterval();
            req.TimePeriod.Start = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd"); // 3日前～本日までチェック
            req.TimePeriod.End = DateTime.Now.ToString("yyyy-MM-dd");
            req.Granularity = Granularity.DAILY;
            req.Metrics = new List<string>() { "AMORTIZED_COST" };

            StringBuilder sb = new StringBuilder(); // 超過情報
            GetCostAndUsageResponse cost = client.GetCostAndUsage(req);
            for (int i = 0; i < cost.ResultsByTime.Count; i++) {
                string start = cost.ResultsByTime[i].TimePeriod.Start;
                string end = cost.ResultsByTime[i].TimePeriod.End;
                foreach (string key in cost.ResultsByTime[i].Total.Keys) {
                    decimal amount = decimal.Parse(cost.ResultsByTime[i].Total[key].Amount);
                    string unit = cost.ResultsByTime[i].Total[key].Unit;
                    if(amount > limit) { // 閾値を超えた
                        sb.Append(start + "～" + end + " Amount=" + amount + " Unit=" + unit + Environment.NewLine);
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// メール送信する
        /// </summary>
        /// <param name="smtp">SMTPサーバ</param>
        /// <param name="fromAccount">送信元アカウント</param>
        /// <param name="fromPassword">送信元パスワード</param>
        /// <param name="fromMailAddress">送信元メールアドレス</param>
        /// <param name="fromPort">SMTPポート</param>
        /// <param name="sendAddress">送信先メールアドレス</param>
        /// <param name="subject">件名</param>
        /// <param name="message">メール本文</param>
        private static void sendMail(string smtp, string fromAccount, string fromPassword, string fromMailAddress, int fromPort, string sendAddress, string subject, string message) {
            MailMessage msg = new MailMessage(fromMailAddress, sendAddress);

            msg.Subject = subject;
            msg.Body = message;

            SmtpClient sc = new SmtpClient();
            sc.Host = smtp;
            sc.Credentials = new NetworkCredential(fromAccount, fromPassword);
            sc.EnableSsl = false;
            sc.Port = fromPort;
            sc.Timeout = 3000;
            sc.Send(msg);
            msg.Dispose();

        }

    }
}
