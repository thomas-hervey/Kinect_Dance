using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FTD2XX_NET;
using System.Runtime.InteropServices;
using System.Threading;

namespace DmxComm
{
    class DmxDriver
    {
        const int DMX_PACKET_SIZE = 513;
        private bool connected;
        private FTDI device;
        private int startAddr;
        Thread txThread;
        static volatile bool running = true;

        private byte[] packet;
        public DmxDriver(int baseDmxAddr)
        {
            startAddr = baseDmxAddr;
            device = new FTDI();
            FTDI.FT_STATUS result = device.OpenByIndex(0);
            if (result == FTDI.FT_STATUS.FT_OK)
            {
                connected = true;
                Console.WriteLine("DMX connected");
            }
            else
            {
                connected = false;
                Console.WriteLine("DMX CONNECTION FAILED");
            }

            packet = new byte[DMX_PACKET_SIZE];
            for (int i = 0; i < DMX_PACKET_SIZE; i++)
            {
                packet[i] = 0;
            }

            txThread = new Thread(() => sendData(packet, device));
            txThread.Start();

            //turn off bit bang mode
            //device.SetBitMode(0x00, 0);
            //device.ResetDevice();
            // device.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);
            //device.SetBaudRate(250000);
            //device.SetTimeouts(1000, 1000);
            //device.SetDataCharacteristics(FTDI.FT_DATA_BITS.FT_BITS_8, FTDI.FT_STOP_BITS.FT_STOP_BITS_2, FTDI.FT_PARITY.FT_PARITY_NONE);
            //device.SetFlowControl(FTDI.FT_FLOW_CONTROL.FT_FLOW_NONE, 0, 0);
            //device.SetLatency(2);

        }

        ~DmxDriver()
        {
            //return to a 0'ed mode on exit
            for (int i = 0; i < DMX_PACKET_SIZE; i++)
            {
                packet[i] = 0;
            }
            Thread.Sleep(50);
            this.stop();
            device.Close();
        }

        public bool deviceConnected()
        {
            return connected;
        }

        public void stop()
        {
            running = false;
            //give the send data cycle a chance to die
            Thread.Sleep(50);
            device.Close();
        }

        public static void sendData(byte[] packet, FTDI device)
        {
            //device.SetBreak(false);
            //System.Threading.Thread.Sleep(40);
            //device.SetBreak(true);
            while (running)
            {
                uint written = 0;
                FTDI.FT_STATUS result;

                byte[] header = new byte[4];
                header[0] = 0x7E; //start code
                header[1] = 6; //DMX TX
                header[2] = 16 & 0xFF; //pack length logical and with max packet size
                header[3] = (16 >> 8) & 0xFF; //packet length shifted by byte length? DMX standard idk


                result = device.Write(header, 4, ref written);//send dmx header

                //Console.WriteLine(result);
                //Console.WriteLine(written);

                packet[0] = 0;
                result = device.Write(packet, 16, ref written);//send data array
                //Console.WriteLine(result);
                //Console.WriteLine(written);


                byte[] endcode = new byte[1];
                endcode[0] = 0xE7;
                device.Write(endcode, 1, ref written);//send dmx end code
                Thread.Sleep(25); //sleep for 1/40 second
            }
        }




        public void shutterStrobe()
        {
            int DMX_STROBE_CHANNEL = 1;
            packet[startAddr + DMX_STROBE_CHANNEL - 1] = 72;

        }





        public enum color_t { WHITE = 0, CTC = 12, YELLOW = 24, BLUE_104 = 36, PINK = 48, GREEN_206 = 60, BLUE_108 = 72, RED = 84, MAGENTA = 96, BLUE_101 = 108, GREEN_202 = 132, PURPLE = 144 };


        public void setColorContinuous(color_t c)
        {
            int COLOR_CHANNEL = 3;


            switch (c)
            {
                case color_t.WHITE:

                case color_t.CTC:
                case color_t.YELLOW:
                case color_t.BLUE_104:
                case color_t.PINK:
                case color_t.GREEN_206:
                case color_t.BLUE_108:
                case color_t.RED:
                case color_t.MAGENTA:
                case color_t.BLUE_101:
                case color_t.GREEN_202:
                case color_t.PURPLE:
                    packet[startAddr + COLOR_CHANNEL - 1] = (byte)c;
                    break;
            }
        }

        public enum speed_t { FAST = 246, MEDIUM = 251, SLOW = 255 };

        public void randomStrobe(speed_t speed)
        {
            int STROBE_CHANNEL = 1;
            byte temp = 0;
            switch (speed)
            {
                case speed_t.FAST:
                    temp = 128;
                    break;
                case speed_t.MEDIUM:
                    temp = 148;
                    break;
                case speed_t.SLOW:
                    temp = 168;
                    break;
            }

            packet[startAddr + STROBE_CHANNEL - 1] = temp;
        }

        public void setRandomColors(speed_t speed)
        {
            int COLOR_CHANNEL = 3;
            switch (speed)
            {
                case speed_t.FAST:
                case speed_t.MEDIUM:
                case speed_t.SLOW:
                    packet[COLOR_CHANNEL + startAddr - 1] = (byte)speed;
                    break;
            }
        }

        public void setLampOn()
        {
            int DMX_LAMP_ON = 237;
            int LAMP_ON_CHANNEL = 1;

            packet[startAddr + LAMP_ON_CHANNEL - 1] = (byte)DMX_LAMP_ON;
        }

        public void setLampOff()
        {
            int DMX_LAMP_OFF = 254;
            int DMX_LAMP_OFF_CHANNEL = 1;

            packet[startAddr + DMX_LAMP_OFF_CHANNEL - 1] = (byte)DMX_LAMP_OFF;
        }

        public void setShutterOpen()
        {
            int DMX_SHUTTER_OPEN = 49;
            int DMX_SHUTTER_CHANNEL = 1;

            packet[startAddr + DMX_SHUTTER_CHANNEL - 1] = (byte)DMX_SHUTTER_OPEN;
        }

        public void setShutterClose()
        {
            int DMX_SHUTTER_CLOSE = 2;
            int DMX_SHUTTER_CHANNEL = 1;

            packet[startAddr + DMX_SHUTTER_CHANNEL - 1] = (byte)DMX_SHUTTER_CLOSE;
        }

        public void setDimmerLevel(int level)
        {
            int DMX_DIMMER_CHANNEL = 2;

            if (level >= 0 && level <= 255)
            {
                packet[startAddr + DMX_DIMMER_CHANNEL - 1] = (byte)level;
            }
        }

        //only using standard gobos for now
        const int GOBO_COUNT = 8;
        const int GOBO_CHANNEL = 4;
        public void setGoboStandard(int goboVal)
        {
            byte[] dmxGoboVals = new byte[] { 19, 29, 39, 49, 59, 69, 79, 85 };

            if (goboVal <= GOBO_COUNT && goboVal > 0)
            {
                packet[startAddr + GOBO_CHANNEL - 1] = dmxGoboVals[goboVal - 1];
            }
        }

        //valid for standard and indexed mode
        public void clearGobo()
        {
            packet[startAddr + GOBO_CHANNEL - 1] = 0;
        }
        const int MAX_FOCUS = 255;
        const int MIN_FOCUS = 0;
        const int FOCUS_CHANNEL = 6;

        //focus 0 = infinity, 255 = 2 meters
        public void setFocus(int focus)
        {
            if (focus >= MIN_FOCUS && focus <= MAX_FOCUS)
            {
                packet[startAddr + FOCUS_CHANNEL - 1] = (byte)focus;
            }
        }

        const int PRISM_CHANNEL = 7;
        public void setPrismOff()
        {
            packet[startAddr + PRISM_CHANNEL - 1] = 0;
        }

        //intensity range 0-59
        public enum rotation_direction_t { CW, CCW };

        public void setPrismRotate(rotation_direction_t direction, int intensity)
        {
            int temp;
            if (direction == rotation_direction_t.CCW)
            {
                packet[PRISM_CHANNEL + startAddr - 1] = (byte)(intensity + 20);
            }
            else if (direction == rotation_direction_t.CW)
            {
                packet[PRISM_CHANNEL + startAddr - 1] = (byte)(intensity + 90);
            }
            //else not a valid direction
        }

        //no prism/gobo macros for now

        const int NEUTRAL = 128;
        const int MIN_PAN = -128;
        const int MAX_PAN = 127;
        const int PAN_CHANNEL = 8;

        //negative values go left, positive right, 0 is neutral
        public void setPan(int panVal)
        {
            if (panVal >= MIN_PAN && panVal <= MAX_PAN)
            {
                packet[PAN_CHANNEL + startAddr - 1] = (byte)(panVal + 128);
            }
        }

        const int MIN_TILT = -128;
        const int MAX_TILT = 127;
        const int TILT_CHANNEL = 10;

        public void setTilt(int tiltVal)
        {
            if (tiltVal >= MIN_TILT && tiltVal <= MAX_TILT)
            {
                packet[TILT_CHANNEL + startAddr - 1] = (byte)(tiltVal + 128);
            }
        }
    }
}
