using System;
using System.Runtime.InteropServices;

namespace BioStationAPI
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SFEventMachine
    {
        public UInt32 id;
        public string machine;
        public string userID;
        public DateTime eventDateTime;
        public byte tnaKey;
        public UInt32 jobCode;
        public BS2EventCodeEnum eventCode;
        public UInt16 imageSize;
        public string imageFile;
    }
}