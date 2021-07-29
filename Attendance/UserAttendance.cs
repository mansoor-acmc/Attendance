using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using BAL;
using System.Configuration;

namespace Attendance
{
    public partial class UserAttendance : Form
    {
        System.Timers.Timer timer;
        //System.Timers.Timer timerLeaveMgt;
        EventLog eventLog1;
        bool isClosing = false;
        
        public List<BAL.MachineLastLog> MachineLogs { get; set; }

        public UserAttendance()
        {
            InitializeComponent();

            eventLog1 = new EventLog();
            this.eventLog1.Log = "Application";
            this.eventLog1.Source = "Attendance";

            MachineLogs = new List<MachineLastLog>();

            double interval = double.Parse(ConfigurationManager.AppSettings["interval"]) * (60 * 1000);

            timer = new System.Timers.Timer(interval);
            timer.Interval = double.Parse(ConfigurationManager.AppSettings["interval"]) * (60 * 1000);
            timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);

            this.Hide();
            
            //dateEnd.Value = DateTime.Now;
            //dateStart.Value = dateEnd.Value.AddDays(-1);

            //timerLeaveMgt = new System.Timers.Timer(interval);
            //timerLeaveMgt.Elapsed += new System.Timers.ElapsedEventHandler(timerLeaveMgt_Elapsed);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] company = ConfigurationManager.AppSettings["Company"].Split(',');
            cmbCompany.DataSource = company;

            MachineLogs = new TransferData(null).GetMachineLastLogs();

            timer.Start();

            startAttendanceServiceToolStripMenuItem.Enabled = false;
            stopAttendanceServiceToolStripMenuItem.Enabled = true;
        }

        void timerLeaveMgt_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            eventLog1.WriteEntry(DateTime.Now.ToString("yyyy-MMM-dd HH:mm:ss") + "--- Checking new approved leaves...", EventLogEntryType.Information);
            TransferData copyData = new TransferData(eventLog1);
            copyData.LeaveManagement();
        }

        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            TimeSpan ts = DateTime.Now.TimeOfDay;

            timer.Enabled = false;
            //Import previous whole day time cards at night ---- one time.
            //Check if time is Mid-Night between 2:00am and 2:05am

            if (ts.Hours == 2 && ts.Minutes >= 0 && ts.Minutes <= 5)
            {
                TransferData copyData = new TransferData(eventLog1);
                //then run Over night attendance cards import.
                eventLog1.WriteEntry(DateTime.Now.ToString("yyyy-MMM-dd HH:mm:ss") + "--- Now running the Over Night attendance cards for import...", EventLogEntryType.Information);
                int attendanceDownloadDays = int.Parse(ConfigurationManager.AppSettings["AttendanceDownloadDays"]);
                copyData.RunOvernightImport(attendanceDownloadDays);
            }
            else
            {
                //Normal import.
                RunImport();
            }

            timer.Enabled = true;


        }

        private string RunImport()
        {
            string isImported = string.Empty;
            try
            {
                BAL.ErrorObject.WriteLog("Checking AUTO new attendance cards for import... ", eventLog1, EventLogEntryType.Information, null);
                TransferData copyData = new TransferData(eventLog1);
                copyData.MachineLogs = MachineLogs;

                isImported = copyData.ImportTimecardFromService();
                MachineLogs = copyData.MachineLogs;

                if (!string.IsNullOrEmpty(isImported))
                {
                    //eventLog1.WriteEntry(DateTime.Now.ToString("yyyy-MMM-dd HH:mm:ss") + "--- attendance cards have not been imported. Reason: " + isImported, EventLogEntryType.Warning);
                    BAL.ErrorObject.WriteLog("Attendance cards have not been imported. Reason: " + isImported + Environment.NewLine, eventLog1, EventLogEntryType.Information, null);
                }
                BAL.ErrorObject.WriteLog("-------------------------", null, EventLogEntryType.Information, null);
            }
            catch (Exception exp)
            {
                BAL.ErrorObject.WriteLog("Error Occured. Reason: ", eventLog1, EventLogEntryType.Information, exp);
            }
            return isImported;
        }

        private void startAttendanceServiceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            timer.Start();

            startAttendanceServiceToolStripMenuItem.Enabled = false;
            stopAttendanceServiceToolStripMenuItem.Enabled = true; 
        }

        private void stopAttendanceServiceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            timer.Stop();

            startAttendanceServiceToolStripMenuItem.Enabled = true;
            stopAttendanceServiceToolStripMenuItem.Enabled = false;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            isClosing = true;
            this.Close();

        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string isImported = RunImport();
            
            if (string.IsNullOrEmpty(isImported))
                MessageBox.Show("Attendances have been imported successfully.", "Time & Attendance", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void IsEnabledControls(bool isEnabled)
        {
            btnImport.Enabled = isEnabled;
            btnCancel.Enabled = isEnabled;
            dateStart.Enabled = isEnabled;
            dateEnd.Enabled = isEnabled;
            txtEmployeesNumbers.Enabled = isEnabled;
            cmbCompany.Enabled = isEnabled;
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            if (dateStart.Value.Date > dateEnd.Value)
            {
                MessageBox.Show("'Start Date' must not be greater than 'End Date'.", "Time & Attendance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            IsEnabledControls(false);
            DateTime dtStart = dateStart.Value.Date;
            DateTime dtEnd = dateEnd.Value.Date;

            BAL.ErrorObject.WriteLog("Checking MANUAL new attendance cards for import... ", eventLog1, EventLogEntryType.Information, null);
            BAL.ErrorObject.WriteLog("Checking Between Timing: " + dtStart.ToString("G") + " To " + dtEnd.ToString("G"), null, EventLogEntryType.Information, null);
            //eventLog1.WriteEntry(DateTime.Now.ToString("yyyy-MMM-dd HH:mm:ss") + "--- Checking new attendance cards for import...", EventLogEntryType.Information);
            TransferData copyData = new TransferData(eventLog1);
            string isImported = copyData.ImportTimecardFromApp(dtStart, dtEnd.AddDays(1).AddSeconds(-1),txtEmployeesNumbers.Text,cmbCompany.Text,true);
            if (!string.IsNullOrEmpty(isImported))
                BAL.ErrorObject.WriteLog("attendance cards have not been imported. Reason: " + isImported, eventLog1, EventLogEntryType.Information, null);
                
            else
                MessageBox.Show("Attendances have been imported successfully for company "+cmbCompany.Text,"Time & Attendance",MessageBoxButtons.OK,MessageBoxIcon.Information);

            IsEnabledControls(true);

            this.WindowState = FormWindowState.Minimized;
            this.Hide();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.Hide();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }
        }

        
    }
}
