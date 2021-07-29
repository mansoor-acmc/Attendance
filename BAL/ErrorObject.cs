using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SoapUtility.AttendanceServices;

namespace BAL
{
    public static class ErrorObject
    {
        public static string ErrorMessages(Exception exp)
        {
            string errors = "";
            Monitoring logMonitor = new Monitoring();

            errors = exp.Message;

            //if (exp is System.ServiceModel.FaultException<AifFault>)
            //{
            //    var extendedExp = (System.ServiceModel.FaultException<AifFault>)exp;
            //    if (extendedExp != null)
            //    {
            //        if (extendedExp.Detail != null)
            //        {
            //            if (extendedExp.Detail.InfologMessageList != null && extendedExp.Detail.InfologMessageList.Length > 0)
            //            {
            //                var infoLogs = extendedExp.Detail.InfologMessageList;
            //                errors = "";
            //                foreach (var infoLog in infoLogs)
            //                {
            //                    errors += infoLog.Message.Replace('\t', ' ') + Environment.NewLine;
            //                }
            //            }
            //        }
            //    }
            //    exp = extendedExp;
            //}


            logMonitor.Expection = exp;
            logMonitor.Description = errors;
            logMonitor.WriteInLog();

            return errors;
        }

        public static void WriteLog(string message, EventLog eventLog, EventLogEntryType eventType, Exception exp)
        {
            if (eventLog != null)
                eventLog.WriteEntry(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + "--- " + message, eventType);

            Monitoring logMonitor = new Monitoring();
            if (exp != null)
                logMonitor.Expection = exp;
            logMonitor.Description = message;
            logMonitor.WriteInLog();
        }
    }
}
