using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BAL;

namespace TestProject1
{
    [TestClass]
    public class UnitTest1
    {
        //[TestMethod]
        //public void TestAttendanceImport()
        //{
        //    TransferData copyData = new TransferData(null);
        //    string isImported = copyData.ImportTimecardFromApp(new DateTime(2015, 9, 20), new DateTime(2015, 10, 19, 23, 59, 59), string.Empty);
        //    //string isImported = copyData.ImportTimecardFromService();
        //    Assert.AreEqual<string>(string.Empty, isImported, isImported);
        //}
        [TestMethod]
        public void TestAttendanceImportService()
        {
            TransferData copyData = new TransferData(null);
            string isImported = copyData.ImportTimecardFromService();
            //string isImported = copyData.ImportTimecardFromService();
            Assert.AreEqual<string>(string.Empty, isImported, isImported);
        }

        //[TestMethod]
        //public void TestTime()
        //{
        //    DateTime dt = DateTime.Now;//new DateTime(2016, 9, 14, 0, 2, 0);
        //    long ticks = dt.Ticks;
        //    TimeSpan ts = dt.TimeOfDay;
        //    if (ts.Minutes >= 0 && ts.Minutes <= 7)
        //    {
        //        Assert.AreEqual<string>("Good", "Good");
        //    }

        //}

        //[TestMethod]
        //public void CheckDateDiff()
        //{
        //    DateTime startDate, endDate;
        //    int diff;
        //    startDate = new DateTime(2015, 8, 20);
        //    endDate = new DateTime(2015, 9, 19);
        //    diff =(endDate.AddDays(1) -startDate).Days;            
            
        //        Assert.AreEqual(30, diff);
            
        //}
    }
}
