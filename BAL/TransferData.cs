using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Data;
using System.Data.OleDb;
using System.Configuration;
using System.Data.Odbc;
using System.IO;
using BAL.AttendanceServices;

namespace BAL
{
    public enum ConStrings
    {
        Biostar,
        Hanvon,
        Biotime
    }
    public class TransferData
    {
        EventLog eventLog;
        Monitoring logMonitor;
        SqlConnection conn = null;
        public List<MachineLastLog> MachineLogs = null;
        public static int PunchNotKnown = 100; //If Punch-in OR Punch-out not known, like in Hanvon machine or text based machines

        public void StartConn()
        {
            string conString = ConfigurationManager.ConnectionStrings["connHanvon"].ConnectionString;
            conn = new SqlConnection(conString);
            conn.Open();
        }
        public void StartConn(ConStrings constrings)
        {
            string conString = ConfigurationManager.ConnectionStrings["connHanvon"].ConnectionString;
            if(constrings.Equals(ConStrings.Biostar))
                conString = ConfigurationManager.ConnectionStrings["connBiostar"].ConnectionString;
            else if (constrings.Equals(ConStrings.Biotime))
                conString = ConfigurationManager.ConnectionStrings["connBiotime"].ConnectionString;

            conn = new SqlConnection(conString);
            conn.Open();
        }

        public void StopConn()
        {
            if (conn.State == ConnectionState.Open)
                conn.Close();
        }

        public TransferData(EventLog _eventLog)
        {
            eventLog = _eventLog;
            logMonitor = new Monitoring();
            MachineLogs = new List<MachineLastLog>();
        }

        private void WriteLog11(string message, EventLogEntryType eventType, Exception exp)
        {
            if (eventLog != null)
                eventLog.WriteEntry(DateTime.Now.ToString("yyyy-MMM-dd HH:mm:ss") + "--- " + message, eventType);

            if (exp != null)
                logMonitor.Expection = exp;
            logMonitor.Description = message;
            logMonitor.WriteInLog();
        }

        public void LeaveManagement()
        {
            string[] company = ConfigurationManager.AppSettings["Company"].Split(',');

            for (int i = 0; i < company.Length; i++)
            {
                CallContext context = new CallContext();
                context.Company = company[i];
                context.MessageId = Guid.NewGuid().ToString();

                EmpTimeCardServiceClient client = new EmpTimeCardServiceClient();
                client.applyLeaveTransaction(context);
            }
        }

        public string ImportTimecardFromService()
        {
            string result = "";
            DateTime dtFromTime = DateTime.Now.AddMinutes(int.Parse(ConfigurationManager.AppSettings["PreviousMinutesToInclude"]));
            DateTime dtEndTime = DateTime.Now;//new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day).AddDays(1);

            //for profile staggering OR for early coming employees, start time before it's default.
            dtFromTime = dtFromTime.AddHours(int.Parse(ConfigurationManager.AppSettings["StartBufferHours"]));
            //for late coming employees, add some hours after it's default.
            dtEndTime = dtEndTime.AddHours(int.Parse(ConfigurationManager.AppSettings["EndBufferHours"]));
            BAL.ErrorObject.WriteLog("Checking Between Timing: " + dtFromTime.ToString("G")+" To "+dtEndTime.ToString("G"), null, EventLogEntryType.Information, null);

            string[] company = ConfigurationManager.AppSettings["Company"].Split(',');

            for (int i = 0; i < company.Length; i++)
            {
                result= ArrangeImportedData(dtFromTime, dtEndTime, string.Empty, false, company[i]);
            }
            return result;
        }

        public string ImportTimecardFromApp(DateTime dtFromTime, DateTime dtEndTime, string employeesNumbers, string company, bool isSaveRaw=false)
        {
            return ArrangeImportedData(dtFromTime, dtEndTime, employeesNumbers, isSaveRaw, company);
        }

        DataTable ImportTimeCardsFromNewSys(DateTime dtFromTime, DateTime dtEndTime, string employeesNumbers, string company)
        {

            string strConn = ConfigurationManager.AppSettings["AttendanceTable"];
            DataTable dt = new DataTable("NewApp");

            SqlConnection conn = new SqlConnection(strConn);
            try
            {
                conn.Open();

                string sqlText = "SELECT * FROM PunchHistory where Company ='" + company + "' and Punch BETWEEN '" + dtFromTime.ToString("yyyy-MM-dd HH:mm:ss.000") + "' AND '" + dtEndTime.ToString("yyyy-MM-dd HH:mm:ss.000") + "' AND EmployeeNum !='' order by EmployeeNum, Punch;";
                if (!string.IsNullOrEmpty(employeesNumbers))
                    sqlText = "SELECT * FROM PunchHistory where Company ='" + company + "' and Punch BETWEEN '" + dtFromTime.ToString("yyyy-MM-dd HH:mm:ss.000") + "' AND '" + dtEndTime.ToString("yyyy-MM-dd HH:mm:ss.000") + "' AND EmployeeNum in (" + employeesNumbers + ") order by EmployeeNum, Punch;";
                SqlDataAdapter da = new SqlDataAdapter(sqlText, conn);
                da.Fill(dt);
            }
            catch (Exception exp)
            {
                BAL.ErrorObject.WriteLog("Error occured while collecting data from New System: " + exp.Message,eventLog, EventLogEntryType.Error, exp);
            }
            finally
            {
                conn.Close();
            }
            return dt;
        }

        DataTable ImportFromTextFile(DateTime dtFromTime, DateTime dtEndTime, string employeesNumbers)
        {
            //To use ODBC text reader, create scheme.ini in the folder of text files with following format
            /*
             * [fl01.txt]
ColNameHeader=False
Format=FixedLength
MaxScanRows=25
CharacterSet=OEM
Col1=MACHINE Char Width 10
Col2=F2 Char Width 2
Col3=ECODE Char Width 4
Col4=PUNCH Char Width 8
Col5=TIME Char Width 6
             * */

            DataTable dt = new DataTable("HistoryFile");
            dt.Columns.Add("Ecode");
            dt.Columns.Add("Punch", typeof(DateTime));
            dt.Columns.Add("Machine");

            string strConn = ConfigurationManager.AppSettings["TextDatasource"];
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            //string filename = "fl03.txt";
            //string folderName = "Y:\\";
            BAL.ErrorObject.WriteLog("Starting Textfiles Attendance...", null, EventLogEntryType.Information, null);
            try
            {
                DirectoryInfo di = new DirectoryInfo(ConfigurationManager.AppSettings["OdbcFilesFolder"]);
                FileInfo[] files = di.GetFiles("*.txt");
                strConn = strConn.Replace("txtFilesFolder", di.FullName);


                using (OdbcConnection con = new OdbcConnection(strConn))
                {
                    foreach (FileInfo fi in files)
                    {
                        string sql = string.Format("SELECT * FROM {0} WHERE Punch>='{1}' AND Punch<='{2}'", fi.Name, dtFromTime.ToString("yyyyMMdd"), dtEndTime.ToString("yyyyMMdd"));
                        if (!string.IsNullOrEmpty(employeesNumbers))
                        {
                            string[] empls = employeesNumbers.Trim().Split(',');
                            string emplsWhere = "";
                            for (int i = 0; i < empls.Count(); i++)
                            {
                                if (emplsWhere.Length > 0) emplsWhere += " OR ";
                                if (empls[i].Trim().Length == 5)
                                    emplsWhere += "ecode='" + empls[i].Trim().Substring(1) + "'";
                                else
                                    emplsWhere += "ecode='" + empls[i].Trim() + "'";
                            }
                            sql = sql + " AND (" + emplsWhere + ")";
                        }
                        sql = sql + " ORDER BY Ecode, Punch";

                        using (OdbcCommand cmd = new OdbcCommand(sql, con))
                        {
                            //OdbcParameter param1 = new OdbcParameter("@1", OdbcType.Date);
                            //OdbcParameter param2 = new OdbcParameter("@2", OdbcType.Date);
                            //param1.Value = dtFromTime.Date;//.ToString("yyyyMMddhhmmss");
                            //param2.Value = dtEndTime.Date;//.ToString("yyyyMMddhhmmss");
                            //cmd.Parameters.Add(param1);
                            //cmd.Parameters.Add(param2);

                            con.Open();
                            try
                            {
                                string format = "yyyyMMddHHmmss";
                                OdbcDataReader drTimes = cmd.ExecuteReader();
                                while (drTimes.Read())
                                {
                                    DataRow drAdd = dt.NewRow();
                                    drAdd[0] = drTimes["Ecode"].ToString().Trim();
                                    string dtTime = drTimes["Punch"].ToString().Trim() + drTimes["Time"].ToString().Trim();
                                    drAdd[1] = DateTime.ParseExact(dtTime, format, System.Globalization.CultureInfo.InvariantCulture);
                                    drAdd[2] = drTimes["Machine"].ToString().Trim();
                                    dt.Rows.Add(drAdd);
                                }
                            }
                            finally
                            {
                                con.Close();
                            }
                        }
                    }
                }

                DataView dv = dt.DefaultView;
                dv.Sort = "Ecode ASC, Punch ASC";
                dt = dv.ToTable("HistoryFile");
            }
            catch (Exception exp)
            {
                BAL.ErrorObject.WriteLog("Error occured while collecting data from Textfiles: " + exp.Message, eventLog, EventLogEntryType.Error, exp);
            }
            BAL.ErrorObject.WriteLog("Textfiles Attendance received: " + dt.Rows.Count.ToString(), null, EventLogEntryType.Information, null);
            return dt;
        }

        DataTable ImportTimeCardsFromAIMS(DateTime dtFromTime, DateTime dtEndTime, string employeesNumbers)
        {           

            string strConn = ConfigurationManager.AppSettings["AimsDatasource"];
            DataTable dt = new DataTable("History");
            OleDbConnection conn = new OleDbConnection(strConn);

            try
            {
                conn.Open();

                //string sqlText = "SELECT ecode,WDate,DateIn,DateOut FROM Attdaily where wdate BETWEEN #" + dtFromTime.ToString("yyyy/MM/ddTHH:mm:ss") + "# AND #" + dtEndTime.ToString("yyyy/MM/ddTHH:mm:ss") + "# ORDER BY wdate";
                //OleDbDataAdapter da = new OleDbDataAdapter(sqlText, conn);
                //da.Fill(ds, "Attendance");

                string sqlText = "SELECT * FROM AttHist where punch BETWEEN #" + dtFromTime.ToString("yy/MM/dd HH:mm:ss") + "# AND #" + dtEndTime.ToString("yy/MM/dd HH:mm:ss") + "# order by ecode, punch;";
                if (!string.IsNullOrEmpty(employeesNumbers))
                {
                    string[] empls = employeesNumbers.Trim().Split(',');
                    string emplsWhere = "";
                    for (int i = 0; i < empls.Count(); i++)
                    {
                        if (emplsWhere.Length > 0) emplsWhere += " OR ";
                        if(empls[i].Trim().Length==5)
                            emplsWhere += "ecode='" + empls[i].Trim().Substring(1) + "' ";
                        else
                            emplsWhere += "ecode='" + empls[i].Trim() + "' ";
                    }
                    sqlText = "SELECT * FROM AttHist where (" + emplsWhere + ") AND punch BETWEEN #" + dtFromTime.ToString("yy/MM/dd HH:mm:ss") + "# AND #" + dtEndTime.ToString("yy/MM/dd HH:mm:ss") + "# order by ecode, punch;";
                }
                //string sqlText = "SELECT * FROM AttHist where (ecode='1004' or ecode='1104' or ecode='1197' or "+
                //    "ecode='1057' or ecode='1383' or ecode='0167' or ecode='0221' or ecode='0376' or ecode='0615' or "+
                //    "ecode='0623' or ecode='1449' or ecode='1464' or ecode='1611' or ecode='1748' or ecode='1867' or "+
                //    "ecode='1887' or ecode='2026' or ecode='2048' or ecode='2135') and punch BETWEEN #" + dtFromTime.ToString("yy/MM/dd HH:mm:ss") + "# AND #" + dtEndTime.ToString("yy/MM/dd HH:mm:ss") + "# order by ecode, punch;";
                OleDbDataAdapter da = new OleDbDataAdapter(sqlText, conn);
                da.Fill(dt);

                //sqlText = "SELECT distinct ecode FROM AttHist where punch BETWEEN #" + dtFromTime.ToString("yyyy/MM/ddTHH:mm:ss") + "# (ecode='2048' OR ecode='1449' OR ecode='2026' OR ecode='1611' or ecode='376' OR ecode='1197' OR ecode='1867') AND AND #" + dtEndTime.ToString("yyyy/MM/ddTHH:mm:ss") + "# order by ecode";
                //da = new OleDbDataAdapter(sqlText, conn);
                //da.Fill(ds, "Employees");

            }   
            catch (Exception exp)
            {
                BAL.ErrorObject.WriteLog("Error occured while collecting data from AIMS: "+exp.Message,eventLog, EventLogEntryType.Error, exp);
            }
            finally
            {
                conn.Close();
            }
            return dt;
        }

        

        DataTable ImportTimeCardsFromHanvon(DateTime dtFromTime, DateTime dtEndTime, string employeesNumbers)
        {            
            DataTable dtReturn = new DataTable("Hanvon");

            BAL.ErrorObject.WriteLog("Starting Hanvon Attendance...", null, EventLogEntryType.Information, null);
            try
            {
                StartConn();

                string sqlText = "SELECT emp.RealEmployeeCode ecode,res.CardTime Punch, dev.DevName Machine from KQZ_CARD res INNER JOIN KQZ_Employee emp on res.EMPLOYEEID = emp.EmployeeID " +
                    "INNER JOIN KQZ_DevInfo dev on res.DevID = dev.DevID " +
                    " WHERE res.CardTime BETWEEN '" + dtFromTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' and '" + dtEndTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' order by res.CardTime";
                if (!string.IsNullOrEmpty(employeesNumbers))
                {
                    string[] empls = employeesNumbers.Trim().Split(',');
                    string emplsWhere = "";
                    for (int i = 0; i < empls.Count(); i++)
                    {
                        if (emplsWhere.Length > 0) emplsWhere += " OR ";
                        emplsWhere += "ecode='" + empls[i].Trim() + "' ";
                    }
                    sqlText = "select emp.RealEmployeeCode ecode,res.CardTime Punch, dev.DevName Machine from KQZ_CARD res inner join KQZ_Employee emp on res.EMPLOYEEID = emp.EmployeeID " +
                        "INNER JOIN KQZ_DevInfo dev on res.DevID = dev.DevID " + 
                        " where emp.EmployeeID in (" + employeesNumbers + ") AND res.CardTime between '" + dtFromTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' and '" + dtEndTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' order by res.CardTime";
                }
      
                SqlCommand comm = new SqlCommand(sqlText, conn);
                comm.CommandType = CommandType.Text;
                SqlDataAdapter da = new SqlDataAdapter(comm);
                da.Fill(dtReturn);
            }
            catch (Exception exp)
            {
                BAL.ErrorObject.WriteLog("Error occured while collecting data from Hanvon: " + exp.Message,eventLog, EventLogEntryType.Error, exp);
            }
            finally
            {
                StopConn();
            }
            BAL.ErrorObject.WriteLog("Hanvon Attendance received: " + dtReturn.Rows.Count.ToString(), null, EventLogEntryType.Information, null);
            return dtReturn;
        }

        DataTable ImportTimeCardsFromBiostar(DateTime dtFromTime, DateTime dtEndTime, string employeesNumbers)
        {
            DataTable dtReturn = new DataTable("Biostar");

            BAL.ErrorObject.WriteLog("Starting Biostar Attendance...", null, EventLogEntryType.Information, null);
            try
            {
                StartConn(ConStrings.Biostar);

                string sqlText = "SELECT user_id Ecode, devdt Punch, devnm Machine, tk JobId, '' ImagePath FROM PunchLog " +
                    " WHERE devdt BETWEEN '" + dtFromTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' AND '" + dtEndTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' ORDER BY devdt";
                if (!string.IsNullOrEmpty(employeesNumbers))
                {                    
                    sqlText = "SELECT user_id Ecode, devdt Punch, devnm Machine, tk JobId, '' ImagePath FROM PunchLog " +
                        " WHERE user_id IN (" + employeesNumbers + ") AND devdt BETWEEN '" + dtFromTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' AND '" + dtEndTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' ORDER BY devdt";
                }

                SqlCommand comm = new SqlCommand(sqlText, conn);
                comm.CommandType = CommandType.Text;
                SqlDataAdapter da = new SqlDataAdapter(comm);
                da.Fill(dtReturn);
            }
            catch (Exception exp)
            {
                BAL.ErrorObject.WriteLog("Error occured while collecting data from Biostar: " + exp.Message, eventLog, EventLogEntryType.Error, exp);
            }
            finally
            {
                StopConn();
            }
            return dtReturn;
        }

        DataTable ImportTimeCardsFromBiotime(DateTime dtFromTime, DateTime dtEndTime, string employeesNumbers)
        {
            DataTable dtReturn = new DataTable("Biotime");

            BAL.ErrorObject.WriteLog("Starting Biotime Attendance...", null, EventLogEntryType.Information, null);

            try
            {
                StartConn(ConStrings.Biotime);

                string sqlText = "SELECT emp_code Ecode, punch_time Punch, UPPER(trans.terminal_sn) Machine, punch_state JobID, '' ImagePath, ter.Alias FROM iclock_transaction trans " +
                    "INNER JOIN iclock_terminal ter ON trans.terminal_sn=ter.sn " +
                    "WHERE punch_time BETWEEN '" + dtFromTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' AND '" + dtEndTime.ToString("yyyy-MM-ddTHH:mm:ss") + "' ";
                if (!string.IsNullOrEmpty(employeesNumbers))
                {
                    sqlText += "AND emp_code IN (" + employeesNumbers + ") ";
                }
                sqlText += "ORDER BY punch_time";

                SqlCommand comm = new SqlCommand(sqlText, conn);
                comm.CommandType = CommandType.Text;
                SqlDataAdapter da = new SqlDataAdapter(comm);
                da.Fill(dtReturn);
            }
            catch (Exception exp)
            {
                BAL.ErrorObject.WriteLog("Error occured while collecting data from Biotime: " + exp.Message, eventLog, EventLogEntryType.Error, exp);
            }
            finally
            {
                StopConn();
            }

            //if(dtReturn.Rows.Count>0)
            //{
            //    foreach (DataRow dr in dtReturn.Rows)
            //    {
            //        string imgName = string.Empty;
            //        string imgFullPath = string.Empty;
            //        string rootImageFolder = ConfigurationManager.AppSettings["BiotimeSharedFolder"];
            //        DateTime punchTime = (DateTime)dr["Punch"];

            //        if (dr["Machine"] != DBNull.Value && dr["Machine"].ToString().Length > 0)
            //        {
            //            imgName = punchTime.ToString("yyyyMMddHHmmss") + "-" + dr["Ecode"].ToString() + ".jpg";
            //            imgFullPath = rootImageFolder + punchTime.ToString("yyyyMM") + "\\" + dr["Machine"].ToString().ToUpper() + "\\";

            //            dr["ImagePath"] = imgFullPath + imgName;
            //            dr.AcceptChanges();
            //        }
            //    }
            //}
            return dtReturn;
        }
        
        string ArrangeImportedData(DateTime dtFromTime, DateTime dtEndTime, string employeesNumbers, bool isSaveRaw=false, string company="ACMC")
        {
            CallContext context = new CallContext();
            context.Company = company;
            context.MessageId = Guid.NewGuid().ToString();

            DataSet ds = new DataSet();
            AxdEmpTimeCard empTimeCard = new AxdEmpTimeCard();
            List<TimeCardSmallContract> timeCards = new List<TimeCardSmallContract>();

            string[] whichDevices = ConfigurationManager.AppSettings["WhichDevice"].Split(',');

            foreach (string whichDevice in whichDevices)
            {
                if (whichDevice.Equals("Biotime"))
                {
                    ds.Tables.Add(ImportTimeCardsFromBiotime(dtFromTime, dtEndTime, employeesNumbers));
                }
                else if (whichDevice.Equals("Biostar"))
                {
                    BAL.ErrorObject.WriteLog("Starting Biostar Attendance...", null, EventLogEntryType.Information, null);
                    //Import Attendances from BIO STATION Machines
                    BioStationAPI.BioAttendance biostarAtt = new BioStationAPI.BioAttendance(eventLog, this.MachineLogs);
                    
                    biostarAtt.CheckOldImageFiles();
                    if (biostarAtt.GetAttendances(dtFromTime, dtEndTime) > 0)
                        ds.Tables.Add(biostarAtt.ConvertLogInTable());
                    //biostarAtt.DisAllocate();
                    this.MachineLogs = biostarAtt.MachineLogs;

                    //ds.Tables.Add(ImportTimeCardsFromBiostar(dtFromTime, dtEndTime, employeesNumbers));
                }
                //Import Attendances from OTHER Machines
                //ds.Tables.Add(ImportTimeCardsFromAIMS(dtFromTime, dtEndTime, employeesNumbers));
                else if (whichDevice.Equals("Hanvon"))
                    ds.Tables.Add(ImportTimeCardsFromHanvon(dtFromTime, dtEndTime, employeesNumbers));
                else if (whichDevice.Equals("NewSys"))
                    ds.Tables.Add(ImportTimeCardsFromNewSys(dtFromTime, dtEndTime, employeesNumbers, company));
                else if (whichDevice.Equals("Textfile"))
                {
                    if (company == "ACMC")
                        ds.Tables.Add(ImportFromTextFile(dtFromTime, dtEndTime, employeesNumbers));
                }
            }
            
            try
            {
                #region History                
                if (ds.Tables["History"] != null && ds.Tables["History"].Rows.Count > 0)
                {
                    BAL.ErrorObject.WriteLog("Arranging History Database...", null, EventLogEntryType.Information, null);
                    foreach (DataRow dr in ds.Tables["History"].Rows)
                    {
                        //now create time card to import in Axapta
                        DateTime shiftDate = DateTime.Parse(dr["punch"].ToString()).Date;

                        AxdType_DateTime beginCardTime = new AxdType_DateTime()
                        {
                            localDateTime = DateTime.Parse(dr["punch"].ToString()).ToUniversalTime(),
                            timezone = AxdEnum_Timezone.GMTPLUS0300KUWAIT_RIYADH,
                        };
                        beginCardTime.Value = beginCardTime.localDateTime;
                        
                        if (beginCardTime.localDateTime.Year == 1900)
                            continue;

                        TimeCardSmallContract timeCard = new TimeCardSmallContract()
                        {                            
                            EmployeeId = int.Parse(dr["ecode"].ToString()),
                            EmployeeIdSpecified = true                            
                        };
                        //timeCard.ShiftDate = shiftDate;
                        //timeCard.ShiftDateSpecified = true;
                        timeCard.CardTime = beginCardTime.localDateTime;
                        timeCard.CardTimeSpecified = true;                        
                        //timeCard.EndCardTimeSpecified = false;

                        timeCards.Add(timeCard);
                    }
                }
                #endregion

                #region Text Files               
                if (ds.Tables["HistoryFile"] != null && ds.Tables["HistoryFile"].Rows.Count > 0)
                {
                    BAL.ErrorObject.WriteLog("TextFile cards received: " + ds.Tables["HistoryFile"].Rows.Count.ToString(), null, EventLogEntryType.Information, null);
                    foreach (DataRow dr in ds.Tables["HistoryFile"].Rows)
                    {
                        //now create time card to import in Axapta
                        //DateTime shiftDate = DateTime.Parse(dr["punch"].ToString()).Date;

                        DateTime shiftDate = (DateTime)dr["Punch"];//DateTime.ParseExact(dr["punch"].ToString(), "yyyyMMddTHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                        AxdType_DateTime beginCardTime = new AxdType_DateTime()
                        {
                            localDateTime = shiftDate.ToUniversalTime(),
                            timezone = AxdEnum_Timezone.GMTPLUS0300KUWAIT_RIYADH,
                        };
                        beginCardTime.Value = beginCardTime.localDateTime;

                        if (beginCardTime.localDateTime.Year == 1900)
                            continue;

                        TimeCardSmallContract timeCard = new TimeCardSmallContract()
                        {
                            EmployeeId = int.Parse(dr["Ecode"].ToString()),
                            EmployeeIdSpecified = true,
                            JobClockInOut = PunchNotKnown,
                            JobClockInOutSpecified = true
                        };

                        timeCard.CardTime = beginCardTime.localDateTime;
                        timeCard.CardTimeSpecified = true;
                        timeCard.Machine = dr["Machine"].ToString();

                        timeCards.Add(timeCard);
                    }

                }
                #endregion

                #region NewApp                
                if (ds.Tables["NewApp"] != null && ds.Tables["NewApp"].Rows.Count > 0)
                {
                    BAL.ErrorObject.WriteLog("NewApp cards Received: " + ds.Tables["NewApp"].Rows.Count.ToString(), null, EventLogEntryType.Information, null);
                    foreach (DataRow dr in ds.Tables["NewApp"].Rows)
                    {
                        //now create time card to import in Axapta
                        DateTime shiftDate = DateTime.Parse(dr["Punch"].ToString()).Date;

                        AxdType_DateTime beginCardTime = new AxdType_DateTime()
                        {
                            localDateTime = DateTime.Parse(dr["Punch"].ToString()).ToUniversalTime(),
                            timezone = AxdEnum_Timezone.GMTPLUS0300KUWAIT_RIYADH,
                        };
                        beginCardTime.Value = beginCardTime.localDateTime;

                        if (beginCardTime.localDateTime.Year == 1900)
                            continue;

                        TimeCardSmallContract timeCard = new TimeCardSmallContract()
                        {
                            EmployeeId = int.Parse(dr["EmployeeNum"].ToString()),
                            EmployeeIdSpecified = true
                        };
                       
                        timeCard.CardTime = beginCardTime.localDateTime;
                        timeCard.CardTimeSpecified = true;                      

                        timeCards.Add(timeCard);
                    }

                }
                #endregion

                #region Hanvon                
                if (ds.Tables["Hanvon"] != null && ds.Tables["Hanvon"].Rows.Count > 0)
                {
                    BAL.ErrorObject.WriteLog("Hanvon cards received: " + ds.Tables["Hanvon"].Rows.Count.ToString(), null, EventLogEntryType.Information, null);
                    foreach (DataRow dr in ds.Tables["Hanvon"].Rows)
                    {
                        //now create time card to import in Axapta
                        DateTime shiftDate = DateTime.Parse(dr["Punch"].ToString()).Date;

                        AxdType_DateTime beginCardTime = new AxdType_DateTime()
                        {
                            localDateTime = DateTime.Parse(dr["punch"].ToString()).ToUniversalTime(),
                            timezone = AxdEnum_Timezone.GMTPLUS0300KUWAIT_RIYADH,
                        };
                        beginCardTime.Value = beginCardTime.localDateTime;

                        if (beginCardTime.localDateTime.Year == 1900)
                            continue;

                        TimeCardSmallContract timeCard = new TimeCardSmallContract()
                        {
                            EmployeeId = int.Parse(dr["ecode"].ToString()),
                            EmployeeIdSpecified = true,
                            Machine = dr["Machine"].ToString(),
                            JobClockInOut = PunchNotKnown,
                            JobClockInOutSpecified = true
                        };
                        //timeCard.ShiftDate = shiftDate;
                        //timeCard.ShiftDateSpecified = true;
                        timeCard.CardTime = beginCardTime.localDateTime;
                        timeCard.CardTimeSpecified = true;
                        //timeCard.EndCardTimeSpecified = false;

                        timeCards.Add(timeCard);
                    }
                }
                #endregion

                #region BioStar                
                if (ds.Tables["Biostar"] != null && ds.Tables["Biostar"].Rows.Count > 0)
                {
                    BAL.ErrorObject.WriteLog("Biostar cards received: " + ds.Tables["Biostar"].Rows.Count.ToString(), null, EventLogEntryType.Information, null);
                    foreach (DataRow dr in ds.Tables["Biostar"].Rows)
                    {
                        if (string.IsNullOrEmpty(dr["Ecode"].ToString()))
                            continue;

                        //now create time card to import in Axapta
                        DateTime shiftDate = DateTime.Parse(dr["Punch"].ToString()).Date;
                        if (shiftDate.Year == 1900)
                            continue;

                        AxdType_DateTime beginCardTime = new AxdType_DateTime()
                        {
                            localDateTime = DateTime.Parse(dr["Punch"].ToString()),
                            timezone = AxdEnum_Timezone.GMTPLUS0300KUWAIT_RIYADH,
                            timezoneSpecified = true
                        };
                        beginCardTime.Value = beginCardTime.localDateTime;
                        TimeZoneInfo tzi = TimeZoneInfo.Local;
                        shiftDate=TimeZoneInfo.ConvertTime(beginCardTime.localDateTime, TimeZoneInfo.Utc, tzi);

                       
                        TimeCardSmallContract timeCard = new TimeCardSmallContract()
                        {
                            EmployeeId = int.Parse(dr["Ecode"].ToString()),
                            EmployeeIdSpecified = true
                        };
                        
                        timeCard.CardTime = shiftDate;
                        timeCard.CardTimeSpecified = true;                        
                        timeCard.Machine = dr["Machine"].ToString();
                        timeCard.JobClockInOut = int.Parse(dr["JobId"].ToString())-1;
                        timeCard.JobClockInOutSpecified = true;
                        if (dr["ImagePath"] != DBNull.Value)
                            timeCard.FaceImage = dr["ImagePath"].ToString();
                        timeCard.LogCardId = uint.Parse( dr["LogId"].ToString());
                        timeCard.LogCardIdSpecified = true;

                        timeCards.Add(timeCard);
                    }
                }
                #endregion


                #region Biotime                
                if (ds.Tables["Biotime"] != null && ds.Tables["Biotime"].Rows.Count > 0)
                {
                    BAL.ErrorObject.WriteLog("Biotime cards received: " + ds.Tables["Biotime"].Rows.Count.ToString(), null, EventLogEntryType.Information, null);
                    foreach (DataRow dr in ds.Tables["Biotime"].Rows)
                    {
                        if (string.IsNullOrEmpty(dr["Ecode"].ToString()))
                            continue;

                        //now create time card to import in Axapta
                        DateTime shiftDate = DateTime.Parse(dr["Punch"].ToString()).Date;
                        if (shiftDate.Year == 1900)
                            continue;

                        //AxdType_DateTime beginCardTime = new AxdType_DateTime()
                        //{
                        //    localDateTime = DateTime.Parse(dr["Punch"].ToString()),
                        //    timezone = AxdEnum_Timezone.GMTPLUS0300KUWAIT_RIYADH,
                        //    timezoneSpecified = false
                        //};
                        //beginCardTime.Value = beginCardTime.localDateTime;
                        //TimeZoneInfo tzi = TimeZoneInfo.Local;
                        //shiftDate = TimeZoneInfo.ConvertTime(beginCardTime.localDateTime, TimeZoneInfo.Utc, tzi);
                        DateTime punchTime = (DateTime)dr["Punch"];

                        TimeCardSmallContract timeCard = new TimeCardSmallContract()
                        {
                            EmployeeId = int.Parse(dr["Ecode"].ToString()),
                            EmployeeIdSpecified = true
                        };

                        timeCard.CardTime = punchTime;
                        timeCard.CardTimeSpecified = true;
                        timeCard.Machine = dr["Machine"].ToString().ToUpper()+" "+ dr["Alias"].ToString().ToUpper();
                        timeCard.JobClockInOut = int.Parse(dr["JobId"].ToString()) - 1;
                        timeCard.JobClockInOutSpecified = true;                        
                        timeCard.LogCardId = 0;
                        timeCard.LogCardIdSpecified = false;

                        string imgName = string.Empty;
                        string imgFullPath = string.Empty;
                        string rootImageFolder = ConfigurationManager.AppSettings["BiotimeSharedFolder"];
                        
                        if (dr["Machine"] != DBNull.Value && dr["Machine"].ToString().Length > 0)
                        {
                            imgName = punchTime.ToString("yyyyMMddHHmmss") + "-" + dr["Ecode"].ToString() + ".jpg";
                            imgFullPath = rootImageFolder + punchTime.ToString("yyyyMM") + "\\" + dr["Machine"].ToString().ToUpper() + "\\";
                            if (!string.IsNullOrEmpty(imgName))
                                timeCard.FaceImage = imgFullPath + imgName;
                        }

                        timeCards.Add(timeCard);
                    }
                }
                #endregion

                if (timeCards.Count > 0)
                {
                    BAL.ErrorObject.WriteLog("Attendance total cards to upload: " + timeCards.Count.ToString(), null, EventLogEntryType.Information, null);

                    timeCards = timeCards.OrderBy(t => t.EmployeeId).ThenBy(t => t.CardTime).ToList();
                    EmpTimeCardServiceClient client = new EmpTimeCardServiceClient();

                    //Now send the cards in chunks.
                    int pos=0, size=0;
                    int chunkSize = int.Parse(ConfigurationManager.AppSettings["CheckSizeUpload"]);
                    do
                    {
                        if (timeCards.Count() > pos + chunkSize)
                            size = chunkSize;
                        else
                            size = timeCards.Count() - pos;


                        TimeCardSmallContract[] chunk = new TimeCardSmallContract[size];
                        timeCards.CopyTo(pos, chunk, 0, size);
                        var empUploaded = string.Join(",", chunk.ToList().Select(t => t.EmployeeId.ToString()));

                        if (isSaveRaw)
                        {                            
                            BAL.ErrorObject.WriteLog("Attendances Uploading RAW..." + empUploaded, null, EventLogEntryType.Information, null);
                            client.SaveRawAttendance(context, chunk);
                            //BAL.ErrorObject.WriteLog("Attendances Uploaded RAW: " , null, EventLogEntryType.Information, null);
                        }
                        else
                        {
                            try
                            {                                
                                BAL.ErrorObject.WriteLog("Attendances Uploading Auto... " + empUploaded, null, EventLogEntryType.Information, null);
                                string result = client.WorkerInOutRegistration(context, chunk);
                                BAL.ErrorObject.WriteLog("Attendances Uploaded Auto: " + result, null, EventLogEntryType.Information, null);
                            }
                            catch (Exception exp)
                            {
                                string result = BAL.ErrorObject.ErrorMessages(exp);
                            }
                        }

                        pos = pos + size;
                    }
                    while (pos < timeCards.Count());
                    //string result = client.WorkerInOutRegistration(context, timeCards.ToArray());
                }
            }
            catch (Exception exp)
            {
                return BAL.ErrorObject.ErrorMessages(exp);
            }

            BAL.ErrorObject.WriteLog(timeCards.Count().ToString() + " time cards have been imported successfully for company "+company,eventLog, EventLogEntryType.Information, null);

            return string.Empty;
        }

        public string RunOvernightImport(int attendanceDownloadDays)
        {
            string results = string.Empty;
            string[] company = ConfigurationManager.AppSettings["Company"].Split(',');

            for (int i = 0; i < company.Length; i++)
            {
                CallContext context = new CallContext();
                context.Company = company[i];
                context.MessageId = Guid.NewGuid().ToString();
                try
                {

                    DateTime startDate = DateTime.Now.AddDays(-attendanceDownloadDays);
                    DateTime endDate = DateTime.Now;

                    EmpTimeCardServiceClient client = new EmpTimeCardServiceClient();
                    results = client.WorkerInOutRegistrationForDay(context, startDate, endDate);
                }
                catch (Exception exp)
                {
                    return BAL.ErrorObject.ErrorMessages(exp);
                }
            }
            return results;
        }

        public List<MachineLastLog> GetMachineLastLogs()
        {
            List<MachineLastLog> result = new List<MachineLastLog>();
            CallContext context = new CallContext();
            context.Company = "ACMC";
            context.MessageId = Guid.NewGuid().ToString();

            EmpTimeCardServiceClient client = new EmpTimeCardServiceClient();
            var logs = client.GetLastMachineLogIds(context);
            foreach (TimeCardSmallContract one in logs)
            {
                MachineLastLog oneRow = new MachineLastLog()
                {
                    LogId = UInt32.Parse(one.LogCardId.ToString()),
                    MachineName = one.Machine
                };

                if (oneRow.MachineName.Length > 0)
                {
                    string[] machineNameId = oneRow.MachineName.Split(' ');
                    if (machineNameId.Length > 0)                  
                        UInt32.TryParse(machineNameId[0], out oneRow.MachineId);                    
                }

                result.Add(oneRow);
            }
            client.Close();

            return result;
        }

        #region Old Method
        //public string ImportTimeCardsOldVersion()
        //{
        //    CallContext context = new CallContext();
        //    context.Company = "ACMC";
        //    context.MessageId = Guid.NewGuid().ToString();

        //    EmpTimeCardServiceClient client = new EmpTimeCardServiceClient();
        //    //long lastSwipeId;

        //    try
        //    {
        //        //lastSwipeId = client.getLastSwipeId(context);

        //        StartConn();
        //        DateTime dtFromTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day).AddDays(int.Parse(ConfigurationManager.AppSettings["PreviousMinutesToInclude"]));
        //        string sqlText = "SELECT res.*,emp.RealEmployeeCode " +
        //            "FROM KQZ_DZ_VIEW_RESULT_PERIOD res inner join KQZ_Employee emp " +
        //            "on res.EMPLOYEEID = emp.EmployeeID " +
        //            "where SHIFTDATE >= '" + dtFromTime.ToString("yyyy-MM-dd") + "' and SHIFTDATE <= '" + DateTime.Now.ToString("yyyy-MM-dd") + "' ";
        //        DataTable dtReturn = new DataTable("TimeCard");
        //        SqlCommand comm = new SqlCommand(sqlText, conn);
        //        comm.CommandType = CommandType.Text;
        //        SqlDataAdapter da = new SqlDataAdapter(comm);
        //        da.Fill(dtReturn);

        //        if (dtReturn.Rows.Count > 0)
        //        {
        //            //int i = 0;
        //            AxdEmpTimeCard empTimeCard = new AxdEmpTimeCard();
        //            List<TimeCardContract> timeCards = new List<TimeCardContract>();
        //            foreach (DataRow dr in dtReturn.Rows)
        //            {
        //                if (dr["IsShouldAtt"].ToString() == "0")//This is Weekend holiday.
        //                    continue;

        //                DateTime shiftDate = dr["ShiftDate"] == DBNull.Value ? new DateTime(1900, 1, 1) : DateTime.Parse(dr["ShiftDate"].ToString());

        //                AxdType_DateTime beginCardTime = new AxdType_DateTime()
        //                {
        //                    localDateTime = dr["BeginCardTime"] == DBNull.Value ? new DateTime(1900, 1, 1, 3, 0, 0).ToUniversalTime() : DateTime.Parse(dr["BeginCardTime"].ToString()).ToUniversalTime(),
        //                    timezone = AxdEnum_Timezone.GMTPLUS0300KUWAIT_RIYADH,
        //                };
        //                beginCardTime.Value = beginCardTime.localDateTime;
        //                AxdType_DateTime endCardTime = new AxdType_DateTime()
        //                {
        //                    localDateTime = dr["EndCardTime"] == DBNull.Value ? new DateTime(1900, 1, 1, 3, 0, 0).ToUniversalTime() : DateTime.Parse(dr["EndCardTime"].ToString()).ToUniversalTime(),
        //                    timezone = AxdEnum_Timezone.GMTPLUS0300KUWAIT_RIYADH,
        //                };
        //                endCardTime.Value = endCardTime.localDateTime;

        //                TimeSpan tsBegin = beginCardTime.localDateTime.TimeOfDay;
        //                if (beginCardTime.localDateTime.Year == 1900)
        //                    tsBegin = DateTime.Parse(dr["NewBeginTime"].ToString()).TimeOfDay;
        //                TimeSpan tsEnd = endCardTime.localDateTime.TimeOfDay;
        //                if (endCardTime.localDateTime.Year == 1900)
        //                    tsEnd = DateTime.Parse(dr["NewEndTime"].ToString()).TimeOfDay;

        //                AttendanceStatus status = AttendanceStatus.Normal;
        //                if (tsBegin.Minutes > 15)
        //                    status = AttendanceStatus.LateComing;
        //                if (tsEnd.Hours < DateTime.Parse(dr["NewEndTime"].ToString()).ToUniversalTime().TimeOfDay.Hours)
        //                {
        //                    if (tsEnd.Minutes < 45)
        //                        status = AttendanceStatus.EarlyGoing;
        //                }
        //                if (tsBegin.Minutes > 15 && tsEnd.Hours < DateTime.Parse(dr["NewEndTime"].ToString()).ToUniversalTime().TimeOfDay.Hours)
        //                {
        //                    if (tsEnd.Minutes < 45)
        //                        status = AttendanceStatus.LateAndEarly;
        //                }

        //                int totalWorkingMin = int.Parse(Math.Floor((tsEnd - tsBegin).TotalMinutes).ToString());
        //                if (beginCardTime.localDateTime.Year == 1900 && endCardTime.localDateTime.Year == 1900)
        //                {
        //                    totalWorkingMin = 0;
        //                    status = AttendanceStatus.Absent;
        //                }

        //                int totalLateComing = int.Parse(Math.Floor((DateTime.Parse(dr["NewEndTime"].ToString()).ToUniversalTime().TimeOfDay - DateTime.Parse(dr["NewBeginTime"].ToString()).ToUniversalTime().TimeOfDay).TotalMinutes).ToString()) - totalWorkingMin;

        //                if (beginCardTime.localDateTime.Year == 1900 && endCardTime.localDateTime.Year == 1900)
        //                    continue;

        //                TimeCardContract timeCard = new TimeCardContract()
        //                {
        //                    CardId = -1,
        //                    ShiftId = int.Parse(dr["ShiftID"].ToString()),
        //                    ShiftIdSpecified = true,
        //                    EmployeeId = int.Parse(dr["RealEmployeeCode"].ToString()),
        //                    EmployeeIdSpecified = true,
        //                    TotalWorkingMin = totalWorkingMin,
        //                    TotalLateWorkingMin = totalLateComing,
        //                    Status = status,
        //                    TotalLateWorkingMinSpecified = true,
        //                    TotalWorkingMinSpecified = true,
        //                    StatusSpecified = true
        //                };
        //                timeCard.ShiftDate = shiftDate;
        //                timeCard.ShiftDateSpecified = true;
        //                timeCard.BeginCardTime = beginCardTime.localDateTime;
        //                timeCard.BeginCardTimeSpecified = true;
        //                timeCard.EndCardTime = endCardTime.localDateTime;
        //                timeCard.EndCardTimeSpecified = true;

        //                timeCards.Add(timeCard);
        //            }

        //            //client.CreateAttendance(context, timeCards.ToArray());
        //            string result = client.WorkerInOutRegistration(context, timeCards.ToArray());

        //            //if (keys.Count() > 0)
        //            WriteLog(timeCards.Count().ToString() + " time cards have been imported successfully.", EventLogEntryType.Information);
        //        }
        //    }
        //    catch (Exception exp)
        //    {
        //        return BAL.ErrorObject.ErrorMessages(exp);
        //    }
        //    finally
        //    {
        //        StopConn();
        //    }

        //    return string.Empty;
        //}
        #endregion
    }
}
