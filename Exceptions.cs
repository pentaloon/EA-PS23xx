using System;

namespace ATS.Instruments.Drivers.EA.PS2000
{
    #region Driver specific exceptions

    [Serializable]
    public class PS2000DriverException : Exception
    {
        public PS2000DriverException() { }
        public PS2000DriverException(string message) : base(message) { }
        public PS2000DriverException(string message, Exception inner) : base(message, inner) { }
        protected PS2000DriverException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
    #endregion
}
