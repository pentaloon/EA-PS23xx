using System;
using NationalInstruments.Visa;
using System.Linq;
using System.Text;
using static PS2000.DevConfig;

namespace PS2000
{
    public class PS23xx : IDisposable
    {
        // must use the serial specific implementation instead of the more generic message based session
        // in order to allow termination chars to be handled correctly
        readonly SerialSession serialSession = null;
        private readonly double m_NominalVoltage;
        private readonly double m_NominalCurrent;

        private PS23xx(string _resourceName)
        {
            try
            {
                using (var rm = new ResourceManager())
                {
                    serialSession = (SerialSession)rm.Open(_resourceName.ToUpper());
                    serialSession.SendEndEnabled = false;
                    // Workaround to fix readouts. 
                    // TerminationCharacterEnabled flag is ignored, unless SerialTerminationMethod is set to None ... thanks NI
                    serialSession.ReadTermination = Ivi.Visa.SerialTerminationMethod.None;
                    serialSession.TerminationCharacterEnabled = false;
                }
                m_NominalVoltage = GetNominalVoltage();
                m_NominalCurrent = GetNominalCurrent();
            }
            catch (Exception innerEx)
            {
                throw new PS2000DriverException("Failed to open Session", innerEx);
            }
        }

        public static PS23xx Instance(string ResourceName) => new PS23xx(ResourceName);

        public string GetDeviceType() => Encoding.UTF8.GetString(Query(DeviceObject.DeviceType)).Trim();

        public string GetDeviceSerialNumber() => Encoding.UTF8.GetString(Query(DeviceObject.DeviceSerialNumber)).Trim();

        public string GetDeviceArticleNo() => Encoding.UTF8.GetString(Query(DeviceObject.DeviceArticleNo)).Trim();

        public string GetDeviceManufacturer() => Encoding.UTF8.GetString(Query(DeviceObject.DeviceManufacturer)).Trim();

        public string GetDeviceSoftwareVersion() => Encoding.UTF8.GetString(Query(DeviceObject.DeviceSoftwareVersion)).Trim();

        public float GetNominalVoltage() => BitConverter.ToSingle(Query(DeviceObject.NominalVoltage).Reverse().ToArray(), 0);

        public float GetNominalCurrent() => BitConverter.ToSingle(Query(DeviceObject.NominalCurrent).Reverse().ToArray(), 0);

        public float GetNominalPower() => BitConverter.ToSingle(Query(DeviceObject.NominalPower).Reverse().ToArray(), 0);

        public void SetRemoteControl(DeviceNode outputChannel, bool enable) => Query(DeviceObject.PowerSupplyControl, outputChannel, enable ? PSUCtrl_RemoteCtrlOn : PSUCtrl_RemoteCtrlOff);

        public void SetOutput(DeviceNode outputChannel, bool enable) => Query(DeviceObject.PowerSupplyControl, outputChannel, enable ? PSUCtrl_PowerOn : PSUCtrl_PowerOff);

        public void SetTracking(DeviceNode outputChannel, bool enable) => Query(DeviceObject.PowerSupplyControl, outputChannel, enable ? PSUCtrl_TrackingOn : PSUCtrl_TrackingOff);

        public void AcknowledgeAlarms(DeviceNode outputChannel) => Query(DeviceObject.PowerSupplyControl, outputChannel, PSUCtrl_AcknowledgeAlarms);

        /// <summary>
        /// Set Voltage on the selected channel.
        /// NOTE: Voltage level will be automatically decreased by the device to fit into 160W power limit
        /// </summary>
        /// <param name="devNode"></param>
        /// <param name="voltage"></param>
        public void SetVoltage(DeviceNode devNode, double voltage)
        {
            // Set value of voltage (% of Unom * 256)
            // Set values have to be translated into 16 bit per cent values before transmission. 
            // Percent set value = (25600 * real set value) / nominal value of the device
            // Example: 25600 * 25.5V / 42V = 15543 = 0x3CB7

            short percent = (short)Math.Ceiling(25600 * voltage / m_NominalVoltage);
            byte[] setVal = BitConverter.GetBytes(percent).Reverse().ToArray(); // convert to big-endian (network order)
            Query(DeviceObject.SetValueVoltage, devNode, setVal);
        }

        // Set current limit on the selected channel.
        // NOTE: Voltage level will be automatically decreased by the device to fit into 160W power limit
        public void SetCurrent(DeviceNode devNode, double current)
        {
            // Set value of current(% of Inom * 256)
            short percent = (short)Math.Ceiling(25600 * current / m_NominalCurrent);
            byte[] setVal = BitConverter.GetBytes(percent).Reverse().ToArray(); // convert to big-endian (network order)
            Query(DeviceObject.SetValueCurrent, devNode, setVal);
        }

        /// <summary>
        /// Retrieve actual values from the selected channel (+ status)
        /// </summary>
        /// <param name="outputChannel"></param>
        /// <param name="voltage"></param>
        /// <param name="current"></param>
        /// <param name="remote"></param>
        /// <param name="statusFlags"></param>
        public void RetrieveChannelStatus(DeviceNode outputChannel, out double voltage, out double current, out bool remote, out byte statusFlags)
        {
            var reply = Query(DeviceObject.StatusAndActualValues, outputChannel);
            voltage = m_NominalVoltage * ToPercent(reply, 2, 2);
            current = m_NominalCurrent * ToPercent(reply, 4, 2);
            remote = reply[0] != 0;
            statusFlags = reply[1];
            // bit 0: Output on
            // bit 1-2: Controller state 00 = CV; 01 = CC
            // bit 3: Tracking active
            // bit 4: OVP active
            // bit 5: OCP active
            // bit 6: OPP active
            // bit 7: OTP active
        }

        /// <summary>
        /// Retrieve set values from the selected channel (+ status)
        /// </summary>
        /// <param name="outputChannel"></param>
        /// <param name="voltage"></param>
        /// <param name="current"></param>
        /// <param name="remote"></param>
        /// <param name="statusFlags"></param>
        public void RetrieveChannelSettings(DeviceNode outputChannel, out double voltage, out double current, out bool remote, out byte statusFlags)
        {
            var reply = Query(DeviceObject.StatusAndSetValues, outputChannel);
            voltage = m_NominalVoltage * ToPercent(reply, 2, 2);
            current = m_NominalCurrent * ToPercent(reply, 4, 2);
            remote = reply[0] != 0;
            statusFlags = reply[1];
        }

        private double ToPercent(byte[] data, int index, int length)
        {
            byte[] i16b = new byte[2];
            Array.Copy(data, index, i16b, 0, length);
            return BitConverter.ToInt16(i16b.Reverse().ToArray(), 0) / 25600.0;
        }

        private byte[] Query(DeviceObject devObj, DeviceNode devNode = DeviceNode.Output1, byte[] setValues = null)
        {
            #region Debug
            // Console.WriteLine("*** {0} ***", devObj.ToString());
            #endregion
            var tgm = Telegram.CreateNew(devObj, devNode, setValues);
            var reqBytes = tgm.Wrap();
            byte[] result;
            try
            {
                #region Debug
                //Console.WriteLine("sending  {0}, expecting {1} byte(s) in response", BitConverter.ToString(reqBytes).Replace("-", " "), tg.ReplyLength);
                #endregion
                serialSession.RawIO.Write(reqBytes);
                var reply = serialSession.RawIO.Read(tgm.ReplyLength);
                #region Debug
                //Console.WriteLine("received {0}, {1} byte(s)", BitConverter.ToString(rep).Replace("-", " "), rep.Length);
                #endregion
                result = tgm.Unwrap(reply);
                #region Debug
                //if (serialSession.BytesAvailable > 0)
                //{
                //    Console.WriteLine("received extra bytes: {0}", BitConverter.ToString(serialSession.RawIO.Read(serialSession.BytesAvailable))
                //        .Replace("-", " "));
                //}
                #endregion
                // set * commands return a subsequent "FF 00 01 7F" message (undocumented feature, courtesy of EA)
                // it appears to be safe to just discard it, so let's clear the buffer before moving further 
                // (otherwise subsequent queries may return garbage values)
                serialSession.Flush(Ivi.Visa.IOBuffers.Read, true);
            }
            catch (Exception innerEx)
            {
                throw new PS2000DriverException("Communication error", innerEx);
            }
            return result;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && serialSession != null)
                {
                    serialSession.Clear();
                    serialSession.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
