using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TimeChangeBulk
{
    public partial class TimeChange : Form
    {
        public TimeChange()
        {
            InitializeComponent();
        }

        private void TimeChange_Load(object sender, EventArgs e)
        {

        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnChange_Click(object sender, EventArgs e)
        {

        }

        DataTable ReadFromTextFile(DateTime dtFromTime, DateTime dtEndTime)
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
            dt.Columns.Add("Punch");
            dt.Columns.Add("Time");
            dt.Columns.Add("Machine");

            string strConn = ConfigurationManager.AppSettings["TextDatasource"];
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            //string filename = "fl03.txt";
            //string folderName = "Y:\\";
            
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
                        //if (!string.IsNullOrEmpty(employeesNumbers))
                        //{
                        //    string[] empls = employeesNumbers.Trim().Split(',');
                        //    string emplsWhere = "";
                        //    for (int i = 0; i < empls.Count(); i++)
                        //    {
                        //        if (emplsWhere.Length > 0) emplsWhere += " OR ";
                        //        if (empls[i].Trim().Length == 5)
                        //            emplsWhere += "ecode='" + empls[i].Trim().Substring(1) + "'";
                        //        else
                        //            emplsWhere += "ecode='" + empls[i].Trim() + "'";
                        //    }
                        //    sql = sql + " AND (" + emplsWhere + ")";
                        //}
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
                                //string format = "yyyyMMddHHmmss";
                                OdbcDataReader drTimes = cmd.ExecuteReader();
                                while (drTimes.Read())
                                {
                                    DataRow drAdd = dt.NewRow();
                                    drAdd[0] = drTimes["Ecode"].ToString().Trim();
                                    string dtTime = drTimes["Punch"].ToString().Trim() + drTimes["Time"].ToString().Trim();
                                    drAdd[1] = drTimes["Punch"].ToString().Trim();
                                    drAdd[2] = drTimes["Time"].ToString().Trim();
                                    drAdd[3] = drTimes["Machine"].ToString().Trim();
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
                MessageBox.Show("Error occured while collecting data from Textfiles: " + exp.Message, "Attendance Time Change",
                    MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
            MessageBox.Show("Textfiles Attendance received: " + dt.Rows.Count.ToString(),"Attendance Time Change",MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return dt;
        }

    }
}
