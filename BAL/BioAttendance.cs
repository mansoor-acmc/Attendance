using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Text;
using System.Net;
using System.Runtime.InteropServices;
using System.Configuration;
using System.IO;
using System.Diagnostics;

namespace BioStationAPI
{
    public class BioAttendance
    {
        private const int USER_PAGE_SIZE = 1024;
        private string dirImagesFolderPath = @"c:\temp\";
        public static IntPtr sdkContext = IntPtr.Zero;
        //IntPtr versionPtr = IntPtr.Zero;
        IntPtr deviceListObj = IntPtr.Zero;
        List<SFEventMachine> allEventLogs = new List<SFEventMachine>();
        public List<BAL.MachineLastLog> MachineLogs { get; set; }
        EventLog eventLog;

        public BioAttendance(EventLog _eventLog, List<BAL.MachineLastLog> _machineLogs)
        {
            dirImagesFolderPath = ConfigurationManager.AppSettings["BiostarImagesFolder"];
            //versionPtr = API.BS2_Version();
            if (sdkContext == IntPtr.Zero)
                sdkContext = API.BS2_AllocateContext();
            eventLog = _eventLog;
            MachineLogs = _machineLogs;
        }

        //~BioAttendance()
        //{
        //    this.DisAllocate();
        //}

        /// <summary>
        /// If old images exist then Delete them. Otherwise if directory does not exist then create it for images storage
        /// </summary>
        public void CheckOldImageFiles()
        {
            if (Directory.Exists(dirImagesFolderPath))
            {
                DirectoryInfo dir = new DirectoryInfo(dirImagesFolderPath);
                foreach (FileInfo fi in dir.GetFiles())
                {
                    if (fi.CreationTime < DateTime.Now.AddDays(-1))
                    {
                        fi.Delete();
                    }
                }
            }
            else
                Directory.CreateDirectory(dirImagesFolderPath);
        }

        public List<UInt32> Init_Get_Devices()
        {
            List<UInt32> devices = new List<uint>();
            UInt32 numDevice = 0;

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_Initialize(sdkContext);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                return devices;

            bool autosearchBiostarDevices = bool.Parse(ConfigurationManager.AppSettings["BiostarAutoDevice"]);
            if (!autosearchBiostarDevices)
            {
                string ipAddrs = ConfigurationManager.AppSettings["ipAddresses"];
                string portBiostar = ConfigurationManager.AppSettings["port"];
                string[] aipAddrs = ipAddrs.Split(',');
                

                for (int i = 0; i < aipAddrs.Length; i++)
                {
                    UInt32 deviceID = ConnectDevice(aipAddrs[i], ushort.Parse(portBiostar));
                    if (deviceID > 0)
                        devices.Add(deviceID);
                }
            }
            else
            {
                numDevice = ConnectDevices(out deviceListObj);
                if (numDevice > 0)
                {
                    for (UInt32 idx = 0; idx < numDevice; ++idx)
                    {
                        UInt32 deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)idx * sizeof(UInt32)));
                        if (deviceID > 0)
                            devices.Add(deviceID);
                    }
                }
            }

            return devices;
        }

        //public List<BS2User> GetUsersInDevices(List<UInt32> devices)
        //{
        //    IntPtr outUidObjs = IntPtr.Zero;
        //    UInt32 numUserIds = 0;
        //    List<BS2User> users = new List<BS2User>();

        //    foreach (UInt32 deviceID in devices)
        //    {
        //        BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetUserList(sdkContext, deviceID, out outUidObjs, out numUserIds, null);
        //        if (result == BS2ErrorCode.BS_SDK_SUCCESS)
        //        {
        //            if (numUserIds > 0)
        //            {
        //                IntPtr curUidObjs = outUidObjs;
        //                BS2UserBlob[] userBlobs = new BS2UserBlob[USER_PAGE_SIZE];

        //                for (UInt32 idx = 0; idx < numUserIds; )
        //                {
        //                    UInt32 available = numUserIds - idx;
        //                    if (available > USER_PAGE_SIZE)
        //                    {
        //                        available = USER_PAGE_SIZE;
        //                    }

        //                    result = (BS2ErrorCode)API.BS2_GetUserDatas(sdkContext, deviceID, curUidObjs, available, userBlobs, (UInt32)BS2UserMaskEnum.ALL);
        //                    if (result == BS2ErrorCode.BS_SDK_SUCCESS)
        //                    {
        //                        for (UInt32 loop = 0; loop < available; ++loop)
        //                        {
        //                            users.Add(userBlobs[loop].user);
        //                            print(sdkContext, userBlobs[loop].user);
        //                            // don't need to release cardObj, FingerObj, FaceObj because we get only BS2User
        //                            if (userBlobs[loop].cardObjs != IntPtr.Zero)
        //                                API.BS2_ReleaseObject(userBlobs[loop].cardObjs);
        //                            if (userBlobs[loop].fingerObjs != IntPtr.Zero)
        //                                API.BS2_ReleaseObject(userBlobs[loop].fingerObjs);
        //                            if (userBlobs[loop].faceObjs != IntPtr.Zero)
        //                                API.BS2_ReleaseObject(userBlobs[loop].faceObjs);
        //                        }

        //                        idx += available;
        //                        curUidObjs += (int)available * BS2Envirionment.BS2_USER_ID_SIZE;
        //                    }
        //                    else
        //                    {
        //                        Console.WriteLine("Got error({0}).", result);
        //                        break;
        //                    }
        //                }

        //                API.BS2_ReleaseObject(outUidObjs);
        //            }
        //        }
        //    }

        //    return users;
        //}

        //void print(IntPtr sdkContext, BS2User user)
        //{
        //    Console.WriteLine(">>>> User id[{0}] numCards[{1}] numFingers[{2}] numFaces[{3}]",
        //                        Encoding.UTF8.GetString(user.userID).TrimEnd('\0'),
        //                        user.numCards,
        //                        user.numFingers,
        //                        user.numFaces);
        //}

        public int GetAttendances(DateTime dtFromTime, DateTime dtToTime)
        {            
            UInt32 numDevice = 0;            

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_Initialize(sdkContext);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                return 0;

            bool autosearchBiostarDevices = bool.Parse(ConfigurationManager.AppSettings["BiostarAutoDevice"]);
            if (!autosearchBiostarDevices)
            {
                string ipAddrs = ConfigurationManager.AppSettings["ipAddresses"];
                string portBiostar = ConfigurationManager.AppSettings["port"];
                string[] aipAddrs = ipAddrs.Split(',');
               

                for (int i = 0; i < aipAddrs.Length; i++)
                {
                    UInt32 deviceID = ConnectDevice(aipAddrs[i], ushort.Parse(portBiostar));
                    if (deviceID > 0)
                    {
                        BAL.ErrorObject.WriteLog("Searching Biostar Device: " + deviceID.ToString(), null, EventLogEntryType.Information, null);
                        uint lastLogId = 0;
                        BAL.MachineLastLog oneLog = new BAL.MachineLastLog();
                        if (MachineLogs.Exists(t=>t.MachineId == deviceID))
                            oneLog = MachineLogs.First(t => t.MachineId == deviceID);

                        if (oneLog.MachineId > 0)
                            lastLogId = oneLog.LogId;
                        else
                        {
                            string nameDevice = DeviceName(deviceID);

                            oneLog = new BAL.MachineLastLog()
                            {
                                MachineId = deviceID,
                                MachineName = nameDevice,
                                LogId = 0
                            };
                            lastLogId = oneLog.LogId;
                            MachineLogs.Add(oneLog);
                        }

                        allEventLogs.AddRange(GetAttendanceLogsByDate(deviceID, dtFromTime, dtToTime));
                        if (allEventLogs.Count > 0)
                        {
                            if (allEventLogs.Where(t => t.machine == (oneLog.MachineId + oneLog.MachineName)).Count() > 0)
                            {
                                var item = allEventLogs.Where(t => t.machine == (oneLog.MachineId + oneLog.MachineName)).OrderByDescending(t => t.id).First();
                                if (item.id > oneLog.LogId)
                                {
                                    BAL.MachineLastLog newLogId = new BAL.MachineLastLog()
                                    {
                                        LogId = item.id,
                                        MachineId = oneLog.MachineId,
                                        MachineName = oneLog.MachineName
                                    };
                                    MachineLogs.Remove(oneLog);
                                    MachineLogs.Add(newLogId);
                                    BAL.ErrorObject.WriteLog("Device: " + deviceID.ToString() + ", Last log: " + item.id.ToString(), null, EventLogEntryType.Information, null);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                numDevice = ConnectDevices(out deviceListObj);
                if (numDevice > 0)
                {
                    for (UInt32 idx = 0; idx < numDevice; ++idx)
                    {
                        UInt32 deviceID = Convert.ToUInt32(Marshal.ReadInt32(deviceListObj, (int)idx * sizeof(UInt32)));

                        uint lastLogId = 0;
                        BAL.MachineLastLog oneLog = new BAL.MachineLastLog();
                        if (MachineLogs.Exists(t => t.MachineId == deviceID))
                            oneLog = MachineLogs.First(t => t.MachineId == deviceID);

                        if (oneLog.MachineId > 0)
                            lastLogId = oneLog.LogId;
                        else
                        {
                            string nameDevice = DeviceName(deviceID);

                            oneLog = new BAL.MachineLastLog()
                            {
                                MachineId = deviceID,
                                MachineName = nameDevice,
                                LogId = 0
                            };
                            lastLogId = oneLog.LogId;
                            MachineLogs.Add(oneLog);
                        }

                        allEventLogs.AddRange(GetAttendanceLogsByDate(deviceID, dtFromTime, dtToTime));
                        if (allEventLogs.Count > 0)
                        {
                            if (allEventLogs.Where(t => t.machine == oneLog.MachineName).Count() > 0)
                            {
                                var item = allEventLogs.Where(t => t.machine == oneLog.MachineName).OrderByDescending(t => t.id).First();
                                if (item.id > oneLog.LogId)
                                {
                                    BAL.MachineLastLog newLogId = new BAL.MachineLastLog()
                                    {
                                        LogId = item.id,
                                        MachineId = oneLog.MachineId,
                                        MachineName = oneLog.MachineName
                                    };
                                    MachineLogs.Remove(oneLog);
                                    MachineLogs.Add(newLogId);
                                    BAL.ErrorObject.WriteLog("Device: " + deviceID.ToString() + ", Last log: " + item.id.ToString(), null, EventLogEntryType.Information, null);
                                }
                            }
                        }
                    }
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
            dtBiostar.Columns.Add("ImagePath");
            dtBiostar.Columns.Add("JobId", typeof(uint));
            dtBiostar.Columns.Add("LogId", typeof(uint));

            if (allEventLogs.Count > 0)
            {
                foreach (SFEventMachine oneEvent in allEventLogs)
                {
                    DataRow dr = dtBiostar.NewRow();

                    dr["Ecode"] = oneEvent.userID;
                    dr["Punch"] = oneEvent.eventDateTime;
                    dr["Machine"] = oneEvent.machine;
                    dr["ImagePath"] = oneEvent.imageFile;
                    dr["JobId"] = oneEvent.tnaKey;
                    dr["LogId"] = oneEvent.id;

                    dtBiostar.Rows.Add(dr);
                }
            }

            return dtBiostar;
        }

        private UInt32 ConnectDevice(string ip, ushort port)
        {
            uint deviceId = 0;
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_SearchDevicesEx(sdkContext, ip);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                return 0;

            //IntPtr deviceListObj = IntPtr.Zero;            
            const UInt32 LONG_TIME_STANDBY_40S = 40;
            result = (BS2ErrorCode)API.BS2_SetDeviceSearchingTimeout(sdkContext, LONG_TIME_STANDBY_40S);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                return 0;

            result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, ip, port, out deviceId);
            if (result == BS2ErrorCode.BS_SDK_SUCCESS)
                return deviceId;
            string msg = "BS2_ConnectDeviceViaIP not successfull.";
            if (deviceId.ToString().Length > 0)
                msg += " DeviceId:" + deviceId.ToString();
            msg += " Error Code=" + result.ToString();
            BAL.ErrorObject.WriteLog(msg, eventLog, EventLogEntryType.Warning, null);

            //deviceId = 542343899;
            //result = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceId);
            //if (result == BS2ErrorCode.BS_SDK_SUCCESS)
            //    return deviceId;

            return 0;
        }

        private UInt32 ConnectDevices(out IntPtr deviceListObj)
        {
            deviceListObj = IntPtr.Zero;
            string hostIpAdd = System.Configuration.ConfigurationManager.AppSettings["hostIpAddr"];
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_SearchDevicesEx(sdkContext, hostIpAdd);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                return 0;

            
            UInt32 numDevice = 0;
            const UInt32 LONG_TIME_STANDBY_7S = 20;
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

        private string DeviceName(uint deviceID)
        {
            BS2SimpleDeviceInfo deviceInfo;
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetDeviceInfo(sdkContext, deviceID, out deviceInfo);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                return string.Empty;

            return " "+API.productNameDictionary[(BS2DeviceTypeEnum)deviceInfo.type];
        }

        private List<SFEventMachine> GetAttendanceLogs(UInt32 deviceID, UInt32 lastEventId )
        {
            List<SFEventMachine> machineEvents = new List<SFEventMachine>();
            string strDeviceName = deviceID + DeviceName(deviceID);

            if (lastEventId == 0)
                lastEventId = 2899;

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
                return machineEvents;
            }

            Type structureType = typeof(BS2EventBlob);
            int structSize = Marshal.SizeOf(structureType);
            Console.WriteLine("");
            Console.WriteLine("Showing Users Attendance Logs");
            Console.WriteLine("-----------------------------");
            do
            {
                resultLog = (BS2ErrorCode)API.BS2_GetLogBlob(sdkContext, deviceID, (ushort)BS2EventMaskEnum.USER_ID, lastEventId, amount, out outEventLogObjs, out outNumEventLogs);
                //resultLog = (BS2ErrorCode)API.BS2_GetLog(sdkContext, deviceID, lastEventId, amount, out outEventLogObjs, out outNumEventLogs);
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
                        BS2Event eventLog1 = (BS2Event)Marshal.PtrToStructure(curEventLogObjs, typeof(BS2Event));
                        BS2EventBlob eventLog = (BS2EventBlob)Marshal.PtrToStructure(curEventLogObjs, structureType);

                        if (eventLog.tnaKey == 1 || eventLog.tnaKey == 2) //Only Punch-in and Punch-out
                        {
                            DateTime eventTime = Util.ConvertFromUnixTimestamp(eventLog.info.dateTime);
                            //var localEventTIme = TimeZoneInfo.ConvertTimeFromUtc(eventTime, TimeZoneInfo.Local);
                            UInt16 imageFileSize = 0;
                            string imgFileNameFull = string.Empty;

                            byte[] userID = new byte[BS2Envirionment.BS2_USER_ID_SIZE];
                            Array.Clear(userID, 0, BS2Envirionment.BS2_USER_ID_SIZE);
                            Array.Copy(eventLog.objectID, userID, userID.Length);

                            if (eventLog.image != null && eventLog.imageSize > 0)
                            {
                                imgFileNameFull = String.Format(dirImagesFolderPath + "{0}.jpg", deviceID.ToString() + "_" + eventLog.id);
                                imageFileSize = eventLog.imageSize;
                                FileStream file = new FileStream(imgFileNameFull, FileMode.Create, FileAccess.Write);

                                file.Write(eventLog.image, 0, imageFileSize);
                                file.Close();
                            }

                            SFEventMachine mschineEvent = new SFEventMachine
                            {
                                id = eventLog.id,
                                machine = strDeviceName,
                                userID = System.Text.Encoding.ASCII.GetString(userID).TrimEnd('\0'),
                                eventDateTime = eventTime,
                                imageSize = imageFileSize,
                                imageFile = imageFileSize > 0 ? imgFileNameFull : string.Empty,
                                jobCode = eventLog.jobCode,
                                tnaKey = eventLog.tnaKey
                            };

                            machineEvents.Add(mschineEvent);
                        }
                        curEventLogObjs += structSize;
                        lastEventId = eventLog.id;
                    }
                    API.BS2_ReleaseObject(outEventLogObjs);
                }
                else
                    break;
                

                if (outNumEventLogs >= USER_PAGE_SIZE)
                {
                    break;
                }
            }
            while (getAllLog);

            return machineEvents;
        }

        private List<SFEventMachine> GetAttendanceLogsByDate(UInt32 deviceID, DateTime startDate, DateTime endDate)
        {
            List<SFEventMachine> machineEvents = new List<SFEventMachine>();
            string strDeviceName = deviceID + DeviceName(deviceID);
            ushort eventCode = (ushort)BS2EventCodeEnum.ALL;//VERIFY_SUCCESS_CARD_FACE;
            byte tnaKey = Convert.ToByte(0);
            UInt32 outNumEventLogs = 0;
            IntPtr outEventLogObjs = IntPtr.Zero;

            BS2ErrorCode resultLog = (BS2ErrorCode)API.BS2_ConnectDevice(sdkContext, deviceID);
            if (resultLog != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Got error({0}).", resultLog);
                return machineEvents;
            }

            Type structureType = typeof(BS2Event);
            int structSize = Marshal.SizeOf(structureType);

            uint eventStart = (uint)Util.ConvertToUnixTimestamp(startDate);
            uint eventEnd = (uint)Util.ConvertToUnixTimestamp(endDate);

            //DateTime eventTimeStart = Util.ConvertFromUnixTimestamp(eventStart);
            //DateTime eventTimeEnd = Util.ConvertFromUnixTimestamp(eventEnd);


            resultLog = (BS2ErrorCode)API.BS2_GetFilteredLog(sdkContext, deviceID, IntPtr.Zero, eventCode, eventStart, eventEnd, tnaKey, out outEventLogObjs, out outNumEventLogs);
            
            if (resultLog != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Got error({0}).", resultLog);
                return machineEvents;
            }
            try
            {
                if (outNumEventLogs > 0)
                {
                    IntPtr curEventLogObjs = outEventLogObjs;

                    for (UInt32 idx = 0; idx < outNumEventLogs; idx++)
                    {
                        BS2Event eventLog = (BS2Event)Marshal.PtrToStructure(curEventLogObjs, typeof(BS2Event));
                      
                        BS2EventCodeEnum code = (BS2EventCodeEnum)eventLog.code;
                        string codeStr = code.ToString();
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_ID_PIN:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_ID_FINGER:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_ID_FINGER_PIN:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_ID_FACE:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_ID_FACE_PIN:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_CARD:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_CARD_PIN:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_CARD_FINGER:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_CARD_FINGER_PIN:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_CARD_FACE:
                        //    case BS2EventCodeEnum.VERIFY_SUCCESS_CARD_FACE_PIN:
                        //if (eventLog.param == 1 || eventLog.param == 2) //Only Punch-in and Punch-out
                        {
                            if (code == BS2EventCodeEnum.VERIFY_SUCCESS_CARD_FACE || 
                                code == BS2EventCodeEnum.VERIFY_SUCCESS_CARD_FINGER ||
                                code == BS2EventCodeEnum.VERIFY_SUCCESS_ID_FACE ||
                                code == BS2EventCodeEnum.VERIFY_SUCCESS_CARD_FACE ||
                                code == BS2EventCodeEnum.IDENTIFY_SUCCESS_FACE ||
                                code == BS2EventCodeEnum.VERIFY_SUCCESS_CARD)
                            {
                                DateTime eventTime = Util.ConvertFromUnixTimestamp(eventLog.dateTime);
                                //var localEventTIme = TimeZoneInfo.ConvertTimeFromUtc(eventTime, TimeZoneInfo.Local);
                                UInt16 imageFileSize = 0;

                                byte[] userID = new byte[BS2Envirionment.BS2_USER_ID_SIZE];
                                Array.Clear(userID, 0, BS2Envirionment.BS2_USER_ID_SIZE);
                                Array.Copy(eventLog.userID, userID, userID.Length);

                                //Now check Image exist
                                string imgFileNameFull = string.Empty;
                                bool hasImage = Convert.ToBoolean(eventLog.image & (byte)BS2EventImageBitPos.BS2_IMAGEFIELD_POS_IMAGE);
                                if (hasImage)
                                {
                                    IntPtr outImageLogObjs = IntPtr.Zero;
                                    uint outImageSize = 0;
                                    resultLog = (BS2ErrorCode)API.BS2_GetImageLog(sdkContext, deviceID, eventLog.id, out outImageLogObjs, out outImageSize);
                                    if (resultLog == BS2ErrorCode.BS_SDK_SUCCESS && outImageSize > 0)
                                    {
                                        int written = 0;
                                        imageFileSize = (ushort)outImageSize;

                                        //imageFileSize = eventLog.imageSize;
                                        imgFileNameFull = String.Format(dirImagesFolderPath + "{0}.jpg", eventLog.deviceID.ToString() + "_" + eventLog.id);
                                        if (!File.Exists(imgFileNameFull))
                                        {
                                            FileStream file = new FileStream(imgFileNameFull, FileMode.Create, FileAccess.Write);
                                            WriteFile(file.Handle, outImageLogObjs, (int)outImageSize, out written, IntPtr.Zero);
                                            //file.Write(eventLog.image, 0, imageFileSize);
                                            file.Close();
                                        }
                                    }
                                }
                                SFEventMachine mschineEvent = new SFEventMachine
                                {
                                    id = eventLog.id,
                                    machine = strDeviceName,
                                    userID = System.Text.Encoding.ASCII.GetString(userID).TrimEnd('\0'),
                                    eventDateTime = eventTime,
                                    imageSize = imageFileSize,
                                    imageFile = imageFileSize > 0 ? imgFileNameFull : string.Empty,
                                    jobCode = eventLog.code,
                                    eventCode = (BS2EventCodeEnum)eventLog.code,
                                    tnaKey = eventLog.param
                                };
                                //BS2TNAKeyEnum tnaKeyEnum = (BS2TNAKeyEnum)eventLog.param;


                                machineEvents.Add(mschineEvent);
                            }

                            curEventLogObjs += structSize;
                        }
                       
                    }

                }
            }
            finally
            {
                API.BS2_ReleaseObject(outEventLogObjs);
                
                
            }
            BAL.ErrorObject.WriteLog("Biostar Device: " + deviceID.ToString() + ", Cards Received: " + machineEvents.Count.ToString(), null, EventLogEntryType.Information, null);
            return machineEvents;
        }


        public void DisAllocate()
        {
            try
            {
                //API.BS2_ReleaseObject(deviceListObj);                
                if (sdkContext != IntPtr.Zero)
                {
                    API.BS2_ReleaseContext(sdkContext);
                    sdkContext = IntPtr.Zero;
                }
              
                
            }
            catch (Exception exp)
            {
                BAL.ErrorObject.WriteLog("Error: ", eventLog, EventLogEntryType.Error, exp);
            }
        }

        [DllImport("kernel32.dll")]
        private static extern bool WriteFile(IntPtr hFile, IntPtr lpBuffer, int NumberOfBytesToWrite, out int lpNumberOfBytesWritten, IntPtr lpOverlapped);
    }
}
