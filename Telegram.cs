using System;
using System.Linq;
using static PS2000.DevConfig;

namespace PS2000
{
    internal class Telegram
    {
        /*start delimiter*/
        const byte HeaderLength = 3;
        const byte ChecksumLength = 2;
        readonly byte[] m_DataBytes;
        readonly byte m_Size; //of bytes sent or allocated for reply bytes
        readonly DeviceObject m_DeviceObject;
        readonly DeviceNode m_DeviceNode;
        readonly bool m_Writing;
        public int ReplyLength => TelegramLength();

        public static Telegram CreateNew(DeviceObject DevObj, DeviceNode DeviceNode, byte[] SetValues) => new Telegram(DevObj, DeviceNode, SetValues);

        private Telegram(DeviceObject devObj, DeviceNode deviceNode, byte[] setValues)
        {
            m_DeviceNode = deviceNode;
            m_DeviceObject = devObj;
            m_DataBytes = setValues ?? (new byte[0]);
            m_Size = AllocatedLength();
            m_Writing = IsWriteEligible(devObj) && m_DataBytes.Length > 0;
            if (m_Writing && m_DataBytes.Length != m_Size) throw new PS2000DriverException("unexpected input data length");
        }

        public byte[] Unwrap(byte[] buffer) => buffer.Skip(HeaderLength).Take(buffer.Length - (HeaderLength + ChecksumLength)).ToArray();

        public byte[] Wrap()
        {
            byte[] buffer = new byte[HeaderLength + m_DataBytes.Length + ChecksumLength];
            buffer[0] = (m_Writing) ? CreateStartDelimiter(MessageType.Send, CastType.Broadcast, Direction.Out)
                /*else*/ : CreateStartDelimiter(MessageType.Query, CastType.Broadcast, Direction.Out);
            buffer[1] = (byte)m_DeviceNode;
            buffer[2] = (byte)m_DeviceObject;
            if (m_DataBytes.Length > 0) Array.Copy(m_DataBytes, 0, buffer, 3, m_DataBytes.Length);
            Array.Copy(Checksum(buffer), 0, buffer, HeaderLength + m_DataBytes.Length, ChecksumLength);
            return buffer;
        }

        public override string ToString()
        {
            return string.Join(" ", Wrap().Select(x => x.ToString("X2")).ToArray());
        }

        private byte AllocatedLength()
        {
            switch (m_DeviceObject)
            {
                case DeviceObject.DeviceType:
                case DeviceObject.DeviceSerialNumber:
                case DeviceObject.DeviceArticleNo:
                case DeviceObject.DeviceManufacturer:
                case DeviceObject.DeviceSoftwareVersion:
                    return 16;
                case DeviceObject.NominalVoltage:
                case DeviceObject.NominalCurrent:
                case DeviceObject.NominalPower:
                    return 4;
                case DeviceObject.DeviceClass:
                case DeviceObject.OVPThreshold:
                case DeviceObject.OCPThreshold:
                case DeviceObject.SetValueVoltage:
                case DeviceObject.SetValueCurrent:
                case DeviceObject.PowerSupplyControl:
                    return 2;
                case DeviceObject.StatusAndActualValues:
                case DeviceObject.StatusAndSetValues:
                    return 11;
                default:
                    throw new PS2000DriverException("Unsupported device object type!");
            }
        }

        private byte TelegramLength()
        {
            switch (m_DeviceObject)
            {
                case DeviceObject.DeviceType:
                    return 17;
                case DeviceObject.DeviceSerialNumber:
                    return 16;
                case DeviceObject.DeviceSoftwareVersion:
                    return 21;
                case DeviceObject.DeviceArticleNo:
                    return 14;
                case DeviceObject.DeviceManufacturer:
                    return 8;
                case DeviceObject.NominalVoltage:
                case DeviceObject.NominalCurrent:
                case DeviceObject.NominalPower:
                    return 9;
                case DeviceObject.DeviceClass:
                case DeviceObject.OVPThreshold:
                case DeviceObject.OCPThreshold:
                case DeviceObject.SetValueVoltage:
                case DeviceObject.SetValueCurrent:
                case DeviceObject.PowerSupplyControl:
                    return 2;
                case DeviceObject.StatusAndActualValues:
                case DeviceObject.StatusAndSetValues:
                    return 11;
                default:
                    throw new PS2000DriverException("Unsupported device object type!");
            }
        }

        private bool IsWriteEligible(DeviceObject devObj)
        {
            switch (devObj)
            {
                case DeviceObject.DeviceType:
                case DeviceObject.DeviceSerialNumber:
                case DeviceObject.DeviceArticleNo:
                case DeviceObject.DeviceManufacturer:
                case DeviceObject.DeviceSoftwareVersion:
                case DeviceObject.NominalVoltage:
                case DeviceObject.NominalCurrent:
                case DeviceObject.NominalPower:
                case DeviceObject.DeviceClass:
                case DeviceObject.StatusAndActualValues:
                case DeviceObject.StatusAndSetValues:
                    return false;
                case DeviceObject.OVPThreshold:
                case DeviceObject.OCPThreshold:
                case DeviceObject.SetValueVoltage:
                case DeviceObject.SetValueCurrent:
                case DeviceObject.PowerSupplyControl:
                    return true;
                default:
                    throw new PS2000DriverException("Unsupported device object type!");
            }
        }

        // Start delimiter (1 byte)
        // 0 1 2 3 4 5 6 7
        //             ^-^---- Bit 6-7: transmission type, 00: reserved 01: query data 10: answer to a query 11: send data 
        //           ^-------- Bit 5: Cast type, 1: sending/querying from device 0: answer from control unit
        //         ^---------- Bit 4: Direction, 0: from device to control unit (to PSU) 1: from control unit to device (to PC) 
        // ^-^-^-------------- Bit 0-3: data length - 1 of the data in data field (telegram bytes 3-18)
        //                              at a query, data length of the expected data
        private byte CreateStartDelimiter(MessageType _messageType, CastType _castType, Direction _direction)
            => (byte)((byte)_messageType + (byte)_castType + (byte)_direction + (m_Size - 1));

        private byte[] Checksum(byte[] buffer)
        {
            ushort checksum = 0;
            for (int i = 0; i < (HeaderLength + m_DataBytes.Length); i++)
                checksum += buffer[i];
            return BitConverter.GetBytes(checksum).Reverse().ToArray();
        }
    }
}
