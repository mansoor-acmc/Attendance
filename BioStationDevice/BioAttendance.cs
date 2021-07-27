using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Text;
using System.Net;
using System.Runtime.InteropServices;


namespace BioStationAPI
{
    public class BioAttendance
    {
        private const int USER_PAGE_SIZE = 1024;
        IntPtr sdkContext = IntPtr.Zero;
        IntPtr versionPtr = IntPtr.Zero;
        IntPtr deviceListObj = IntPtr.Zero;
        List<BS2Event> allEventLogs = new List<BS2Event>();

        public BioAttendance()
        {            
            versionPtr = API.BS2_Version();
            sdkContext = API.BS2_AllocateContext();
        }

        public void DisAllocate()
        {
            if (sdkContext != null)
            {
                API.BS2_ReleaseContext(sdkContext);
            }

            //API.BS2_ReleaseObject(versionPtr);
            API.BS2_ReleaseObject(deviceListObj);
        }
        public int GetAttendances()
        {            
            UInt32 numDevice = 0;
            numDevice = ConnectDevices(out deviceListObj);
            if (numDevice > 0)
            {
                for (UInt32 idx = 0; idx < numDevice; ++idx)
                {
                    UInt32 deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)idx * sizeof(UInt32)));

                    allEventLogs.AddRange(GetAttendanceLogs(deviceID));
                }
            }

            return allEventLogs.Count;
        }

        public DataTable ConvertLogInTable()
        {
            DataTable dtBiostar = new DataTable("Biostar");
            dtBiostar.Columns.Add("Ecode");
            dtBiostar.Columns.Add("Punch", typeof(DateTime));
            dtBiostar.Columns.Add("Machine");

            if (allEventLogs.Count > 0)
            {
                foreach (BS2Event oneEvent in allEventLogs)
                {
                    DataRow dr = dtBiostar.NewRow();

                    
                    DateTime eventTime = Util.ConvertFromUnixTimestamp(oneEvent.dateTime);
                    string userID = System.Text.Encoding.ASCII.GetString(oneEvent.userID).TrimEnd('\0');                       
                    
                    dr[0] = userID;
                    dr[1] = eventTime;
                    dr[2] = oneEvent.deviceID;

                    dtBiostar.Rows.Add(dr);
                }
            }

            return dtBiostar;
        }

        private UInt32 ConnectDevices(out IntPtr deviceListObj)
        {
            deviceListObj = IntPtr.Zero;
            string hostIpAdd = System.Configuration.ConfigurationManager.AppSettings["hostIpAddr"];
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_SearchDevicesEx(sdkContext, hostIpAdd);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                return 0;

            
            UInt32 numDevice = 0;
            const UInt32 LONG_TIME_STANDBY_7S = 7;
            result = (BS2ErrorCode)API.BS2_SetDeviceSearchingTimeout(sdkContext, LONG_TIME_STANDBY_7S);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                return 0;

            result = (BS2ErrorCode)API.BS2_GetDevices(sdkContext, out deviceListObj, out numDevice);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                return 0;

            return numDevice;
/*
            UInt32 deviceID;

            if (numDevice > 0)
            {
                for (UInt32 idx = 0; idx < numDevice; ++idx)
                {
                    deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)idx * sizeof(UInt32)));

                    BS2SimpleDeviceInfo deviceInfo;
                    result = (BS2ErrorCode)API.BS2_GetDeviceInfo(sdkContext, deviceID, out deviceInfo);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                        return;

                    string deviceAddress = new IPAddress(BitConverter.GetBytes(deviceInfo.ipv4Address)).ToString();
                    ushort devicePort = deviceInfo.port;
                    
                    Console.WriteLine("[{0, 3:##0}] ==> ID[{1, 10}] Type[{2, 16}] Connection mode[{3}] Ip[{4, 16}] port[{5, 5}]",
                            idx,
                            deviceID,
                            API.productNameDictionary[(BS2DeviceTypeEnum)deviceInfo.type],
                            (BS2ConnectionModeEnum)deviceInfo.connectionMode,
                            deviceAddress,
                            devicePort);
                    

                }
            }*/
        }

        private List<BS2Event> GetAttendanceLogs(UInt32 deviceID)
        {
            List<BS2Event> eventLogs = new List<BS2Event>();

            UInt32 lastEventId = 0;
            UInt32 amount = 0;
            bool getAllLog = false;
            UInt32 outNumEventLogs = 0;
            IntPtr outEventLogObjs = IntPtr.Zero;

            if (amount == 0)
            {
                getAllLog = true;
                amount = USER_PAGE_SIZE;
            }

            BS2ErrorCode resultLog = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceID);
            if (resultLog != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Got error({0}).", resultLog);
                return eventLogs;
            }

            Type structureType = typeof(BS2Event);
            int structSize = Marshal.SizeOf(structureType);
            Console.WriteLine("");
            Console.WriteLine("Showing Users Attendance Logs");
            Console.WriteLine("-----------------------------");
            do
            {
                resultLog = (BS2ErrorCode)API.BS2_GetLog(sdkContext, deviceID, lastEventId, amount, out outEventLogObjs, out outNumEventLogs);
                if (resultLog != BS2ErrorCode.BS_SDK_SUCCESS)
                {
                    Console.WriteLine("Got error({0}).", resultLog);
                    break;
                }

                if (outNumEventLogs > 0)
                {
                    IntPtr curEventLogObjs = outEventLogObjs;
                    for (UInt32 idx = 0; idx < outNumEventLogs; idx++)
                    {
                        BS2Event eventLog = (BS2Event)Marshal.PtrToStructure(curEventLogObjs, structureType);
                        switch (((BS2EventCodeEnum)eventLog.code & BS2EventCodeEnum.MASK))
                        {
                            case BS2EventCodeEnum.VERIFY_SUCCESS:
                            case BS2EventCodeEnum.VERIFY_DURESS:
                            case BS2EventCodeEnum.IDENTIFY_SUCCESS:
                            case BS2EventCodeEnum.IDENTIFY_DURESS:
                                //Console.WriteLine(Util.GetLogMsg(eventLog));
                                eventLogs.Add(eventLog);
                                break;
                        }
                        curEventLogObjs += structSize;
                        lastEventId = eventLog.id;
                    }

                    API.BS2_ReleaseObject(outEventLogObjs);
                }

                if (outNumEventLogs < USER_PAGE_SIZE)
                {
                    break;
                }
            }
            while (getAllLog);

            return eventLogs;
        }

    }
}
