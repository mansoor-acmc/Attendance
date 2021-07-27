using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BAL
{
    public class Monitoring
    {
        public Monitoring()
        {

        }

        private int mintUserID = 0;
        private string mstrActionType = string.Empty;
        private int mintTabID = 0;
        private Exception mobjExpection = null;				
        private string mstrParameter = string.Empty;
        private int mintPortalID = 0;        
        private string localMainFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles)+"\\";
        private string mstrUserName = string.Empty;

        

        public int UserID
        {
            set { mintUserID = value; }
            get { return mintUserID; }
        }

        public Exception Expection
        {
            set { mobjExpection = value; }
            get { return mobjExpection; }
        }

        public string ActionType
        {
            set { mstrActionType = value; }
            get { return mstrActionType; }
        }

        public int TabID
        {
            set { mintTabID = value; }
            get { return mintTabID; }
        }

        public int PortalID
        {
            set { mintPortalID = value; }
            get { return mintPortalID; }
        }

        public string Description
        {
            set { mstrParameter = value; }
            get { return mstrParameter; }
        }

        public string UserName
        {
            set { mstrUserName = value; }
            get { return mstrUserName; }
        }


        public void WriteInLog()
        {
            string strFileName = System.DateTime.Now.ToString("yyyyMMdd");
            string strFiletext = LogFileText(this);
            if (CheckFileExistance(strFileName))
            {
                lock (this)
                {
                    StreamWriter swLogFile = new StreamWriter(localMainFolderPath + "CustomLog" + "\\" + strFileName + "_Log" + ".log", true);
                    swLogFile.WriteLine(strFiletext);
                    swLogFile.Close();
                }
            }
            else
            {
                lock (this)
                {
                    StreamWriter swLogFile1 = new StreamWriter(localMainFolderPath + "CustomLog" + "\\" + strFileName + "_Log" + ".log", false);
                    swLogFile1.WriteLine(strFiletext);
                    swLogFile1.Close();
                }
            }


        }
        public void WriteInLog(string strUserName)
        {
            string strFileName = strUserName; //System.DateTime.Now.ToString("yyyyMMdd");
            string strFiletext = LogFileText(this);
            if (CheckFileExistance(strFileName))
            {

                StreamWriter swLogFile = new StreamWriter(localMainFolderPath + "CustomLog" + "\\" + strFileName + "_Log" + ".log", true);
                swLogFile.WriteLine(strFiletext);
                swLogFile.Close();
            }
            else
            {
                StreamWriter swLogFile1 = new StreamWriter(localMainFolderPath + "CustomLog" + "\\" + strFileName + "_Log" + ".log", false);
                swLogFile1.WriteLine(strFiletext);
                swLogFile1.Close();
            }



        }

        private bool CheckFileExistance(string fileName)
        {

            if (!Directory.Exists(Path.Combine(localMainFolderPath, "CustomLog")))
                Directory.CreateDirectory(Path.Combine(localMainFolderPath, "CustomLog"));
            
            if (File.Exists(Path.Combine(localMainFolderPath, "CustomLog" + "\\" + fileName + "_Log" + ".log")))
                return true;
            else
                return false;


        }


        private string LogFileText(Monitoring objMonitroing)
        {
            string strMessage = "";
            
            /*strMessage = "---------------" + DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss tt") + "---------------";
            strMessage += "\r\n Operating System :" + Environment.OSVersion.Platform.ToString() + " " + Environment.OSVersion.Version.ToString();
            strMessage += "\r\n Host Name :" + Environment.MachineName;
            strMessage += "\r\n Logged-in Username: " + Environment.UserName+"@"+Environment.UserDomainName;           
            strMessage += "\r\n Action Type: " + objMonitroing.ActionType;
            strMessage += "\r\n Description: " + objMonitroing.Description ;
            if (objMonitroing.Expection != null)
            {
                strMessage += "\r\n Exception Information: " + objMonitroing.Expection;
                strMessage += "\r\n " + objMonitroing.Expection.Message;

                if (objMonitroing.Expection.InnerException != null)
                {
                    strMessage += "\r\n Inner Exception:" + objMonitroing.Expection.InnerException;
                }
            }
            strMessage += "\r\n----------------------------------------------------------\r\n\r\n";
            */
            strMessage = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + ": " + objMonitroing.ActionType + objMonitroing.Description;
            return strMessage;
        }
    }
}