namespace PS2000
{
    #region Device specific data

    public static class DevConfig
    {
        public enum DeviceObject : byte
        {
            DeviceType = 0,
            DeviceSerialNumber = 1,
            NominalVoltage = 2,
            NominalCurrent = 3,
            NominalPower = 4,
            DeviceArticleNo = 6,
            DeviceManufacturer = 8,
            DeviceSoftwareVersion = 9,
            DeviceClass = 19,
            OVPThreshold = 38,
            OCPThreshold = 39,
            SetValueVoltage = 50,
            SetValueCurrent = 51,
            PowerSupplyControl = 54,
            StatusAndActualValues = 71,
            StatusAndSetValues = 72
        }

        public enum DeviceNode : byte
        {
            Output1 = 0,
            Output2 = 1
        }

        public enum MessageType : byte
        {
            Send = 0xC0,
            Query = 0x40,
            Answer = 0x80
        }

        public enum CastType : byte
        {
            Answer = 0x0,
            Broadcast = 0x20
        }

        public enum Direction : byte
        {
            Out = 0x10,
            In = 0x0
        }

        public static byte[] PSUCtrl_RemoteCtrlOn => new byte[] { 0x10, 0x10 };
        public static byte[] PSUCtrl_RemoteCtrlOff => new byte[] { 0x10, 0x00 };
        public static byte[] PSUCtrl_PowerOn => new byte[] { 0x01, 0x01 };
        public static byte[] PSUCtrl_PowerOff => new byte[] { 0x01, 0x00 };
        public static byte[] PSUCtrl_AcknowledgeAlarms => new byte[] { 0x0A, 0x0A };
        public static byte[] PSUCtrl_TrackingOn => new byte[] { 0xF0, 0xF0 };
        public static byte[] PSUCtrl_TrackingOff => new byte[] { 0xF0, 0xE0 };
    }
    #endregion
}
