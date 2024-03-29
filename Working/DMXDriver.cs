﻿using System;
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

        static volatile bool dataThreadRunning = true;

        private byte[] packet;
        /// <summary>
        /// DmxDriver object, only one instance of this object should ever exist
        /// </summary>
        /// <param name="baseDmxAddr">The address the DMX light is set to</param>
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
            packet[12 - 1 + startAddr] = 251;
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
            device.Close();
        }

        /// <summary>
        /// Returns the connection status
        /// </summary>
        /// <returns></returns>
        public bool deviceConnected()
        {
            return connected;
        }



        /// <summary>
        /// Stops the device
        /// </summary>
        public void stop()
        {
            //return to a 0'ed mode on exit
            for (int i = 0; i < DMX_PACKET_SIZE; i++)
            {
                packet[i] = 0;
            }
            //give the send data cycle a chance to die
            Thread.Sleep(50);
            dataThreadRunning = false;
        }

        /// <summary>
        /// Send data thread for the device
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="device"></param>
        private static void sendData(byte[] packet, FTDI device)
        {
            //device.SetBreak(false);
            //System.Threading.Thread.Sleep(40);
            //device.SetBreak(true);
            while (dataThreadRunning)
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
                lock (packet)
                {
                    packet[0] = 0;
                    result = device.Write(packet, 16, ref written);//send data array
                    //Console.WriteLine(result);
                    //Console.WriteLine(written);
                }


                byte[] endcode = new byte[1];
                endcode[0] = 0xE7;
                device.Write(endcode, 1, ref written);//send dmx end code
                Thread.Sleep(25); //sleep for 1/40 second
            }
        }



        /// <summary>
        /// Sets the DMX light to strobe mode
        /// </summary>
        public void shutterStrobe()
        {
            int DMX_STROBE_CHANNEL = 1;

            lock (packet)
            {
                packet[startAddr + DMX_STROBE_CHANNEL - 1] = 72;
            }

        }





        public enum color_t { WHITE = 0, CTC = 12, YELLOW = 24, BLUE_104 = 36, PINK = 48, GREEN_206 = 60, BLUE_108 = 72, RED = 84, MAGENTA = 96, BLUE_101 = 108, GREEN_202 = 132, PURPLE = 144 };

        const int COLOR_CHANNEL = 3;
        /// <summary>
        /// Function sets a color on the DMX light
        /// </summary>
        /// <param name="c">Enumerated type color_t</param>
        public void setColorContinuous(color_t c)
        {

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
                    lock (packet)
                    {
                        packet[startAddr + COLOR_CHANNEL - 1] = (byte)c;
                    }
                    break;
            }
        }


        /// <summary>
        /// Convienence function that allows one to programmatically iterate over the colors
        /// </summary>
        public void setNextColor()
        {
            var colors = (color_t[])Enum.GetValues(typeof(color_t));
            for (int i = 0; i < colors.Length; i++)
            {
                if ((byte)colors[i] == packet[startAddr + COLOR_CHANNEL - 1])
                {

                    if (i + 1 < colors.Length)
                    {
                        lock (packet)
                        {
                            packet[startAddr + COLOR_CHANNEL - 1] = (byte)colors[i + 1];
                        }
                        return;
                    }
                    else
                    {
                        lock (packet)
                        {
                            packet[startAddr + COLOR_CHANNEL - 1] = (byte)colors[0];
                        }
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Convienence function that allows one to programmatically iterate over the colors
        /// </summary>
        public void setPrevColor()
        {
            var colors = (color_t[])Enum.GetValues(typeof(color_t));
            for (int i = 0; i < colors.Length; i++)
            {
                if ((byte)colors[i] == packet[startAddr + COLOR_CHANNEL - 1])
                {
                    if (i == 0)
                    {
                        lock (packet)
                        {
                            packet[startAddr + COLOR_CHANNEL - 1] = (byte)colors[colors.Length - 1];
                        }

                        return;
                    }
                    else
                    {
                        lock (packet)
                        {
                            packet[startAddr + COLOR_CHANNEL - 1] = (byte)colors[i - 1];
                        }
                        return;
                    }
                }
            }
        }

        public enum speed_t { FAST = 246, MEDIUM = 251, SLOW = 255 };

        /// <summary>
        /// Sets a random strobe effect on the DMX light(random strobe pattern), can be set to FAST MEDIUM SLOW
        /// </summary>
        /// <param name="speed">speed_t enum type</param>
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
            lock (packet)
            {
                packet[startAddr + STROBE_CHANNEL - 1] = temp;
            }
        }

        /// <summary>
        /// Sets the light to randomly change colors, speeds FAST MEDIUM or SLOW
        /// </summary>
        /// <param name="speed">enum speed_t</param>
        public void setRandomColors(speed_t speed)
        {
            int COLOR_CHANNEL = 3;
            switch (speed)
            {
                case speed_t.FAST:
                case speed_t.MEDIUM:
                case speed_t.SLOW:
                    lock (packet)
                    {
                        packet[COLOR_CHANNEL + startAddr - 1] = (byte)speed;
                    }
                    break;
            }
        }

        /// <summary>
        /// Turns device light on, note: may also need to open shutter
        /// </summary>
        public void setLampOn()
        {
            int DMX_LAMP_ON = 237;
            int LAMP_ON_CHANNEL = 1;
            lock (packet)
            {
                packet[startAddr + LAMP_ON_CHANNEL - 1] = (byte)DMX_LAMP_ON;
            }

        }

        /// <summary>
        /// Turns device light off
        /// </summary>
        public void setLampOff()
        {
            int DMX_LAMP_OFF = 254;
            int DMX_LAMP_OFF_CHANNEL = 1;
            lock (packet)
            {
                packet[startAddr + DMX_LAMP_OFF_CHANNEL - 1] = (byte)DMX_LAMP_OFF;
            }

        }

        /// <summary>
        /// Opens device shutter
        /// </summary>
        public void setShutterOpen()
        {
            int DMX_SHUTTER_OPEN = 49;
            int DMX_SHUTTER_CHANNEL = 1;
            lock (packet)
            {
                packet[startAddr + DMX_SHUTTER_CHANNEL - 1] = (byte)DMX_SHUTTER_OPEN;
            }

        }
        /// <summary>
        /// Closes device shutter
        /// </summary>
        public void setShutterClose()
        {
            int DMX_SHUTTER_CLOSE = 2;
            int DMX_SHUTTER_CHANNEL = 1;
            lock (packet)
            {
                packet[startAddr + DMX_SHUTTER_CHANNEL - 1] = (byte)DMX_SHUTTER_CLOSE;
            }

        }

        /// <summary>
        /// Sets the dimmer level for the device
        /// </summary>
        /// <param name="level">int 0-255, 255 being max brightness</param>
        public void setDimmerLevel(int level)
        {
            int DMX_DIMMER_CHANNEL = 2;

            if (level >= 0 && level <= 255)
            {
                lock (packet)
                {
                    packet[startAddr + DMX_DIMMER_CHANNEL - 1] = (byte)level;
                }

            }
        }

        /// <summary>
        /// Sets the gobo speed
        /// </summary>
        public void setGoboSpin(rotation_direction_t direction, int intensity)
        {
            if (direction == rotation_direction_t.CCW)
            {
                packet[GOBO_CHANNEL + startAddr - 1] = (byte)(intensity + 210);
            }
            else if (direction == rotation_direction_t.CW)
            {
                packet[GOBO_CHANNEL + startAddr - 1] = (byte)(255 - intensity);
            }
            //else not a valid direction
        }

        //only using standard gobos for now
        const int GOBO_COUNT = 8;
        const int GOBO_CHANNEL = 4;

        /// <summary>
        /// Set gobo, undocumented which values are what, but takes an int from 0-8
        /// </summary>
        /// <param name="goboVal">int 0-8</param>
        public void setGoboStandard(int goboVal)
        {
            byte[] dmxGoboVals = new byte[] { 19, 29, 39, 49, 59, 69, 79, 85 };

            if (goboVal <= GOBO_COUNT && goboVal > 0)
            {
                lock (packet)
                {
                    packet[startAddr + GOBO_CHANNEL - 1] = dmxGoboVals[goboVal - 1];
                }

            }
        }

        /// <summary>
        /// Clears the gobo
        /// </summary>
        public void clearGobo()
        {
            lock (packet)
            {
                packet[startAddr + GOBO_CHANNEL - 1] = 0;
            }

        }
        const int MAX_FOCUS = 255;
        const int MIN_FOCUS = 0;
        const int FOCUS_CHANNEL = 6;

        //focus 0 = infinity, 255 = 2 meters

        /// <summary>
        /// Sets the light focus
        /// </summary>
        /// <param name="focus">value from 0 to 255, 0 = focus infinity, 255 = focus to 2 meters</param>
        public void setFocus(int focus)
        {
            if (focus >= MIN_FOCUS && focus <= MAX_FOCUS)
            {
                lock (packet)
                {
                    packet[startAddr + FOCUS_CHANNEL - 1] = (byte)focus;
                }

            }
        }

        const int PRISM_CHANNEL = 7;
        /// <summary>
        /// Turns the prism rotation off
        /// </summary>
        public void setPrismOff()
        {
            lock (packet)
            {
                packet[startAddr + PRISM_CHANNEL - 1] = 0;
            }

        }

        //intensity range 0-59
        public enum rotation_direction_t { CW, CCW };

        /// <summary>
        /// Function is used to rotate a gobo
        /// </summary>
        /// <param name="direction">enum for clockwise and counterclockwise</param>
        /// <param name="intensity">int from 0-59, 59 being max rotate speed</param>
        public void setPrismRotate(rotation_direction_t direction, int intensity)
        {
            int temp;
            if (direction == rotation_direction_t.CCW)
            {
                lock (packet)
                {
                    packet[PRISM_CHANNEL + startAddr - 1] = (byte)(intensity + 20);
                }

            }
            else if (direction == rotation_direction_t.CW)
            {
                lock (packet)
                {
                    packet[PRISM_CHANNEL + startAddr - 1] = (byte)(intensity + 90);
                }

            }
            //else not a valid direction
        }

        //no prism/gobo macros for now

        const int NEUTRAL = 128;
        const int MIN_PAN = -128;
        const int MAX_PAN = 127;
        const int PAN_CHANNEL = 8;

        //negative values go left, positive right, 0 is neutral
        /// <summary>
        /// Sets the pan(left to right spinning)
        /// </summary>
        /// <param name="panVal">int from -128 to 127, 0 is neutral</param>
        public void setPan(int panVal)
        {
            if (panVal >= MIN_PAN && panVal <= MAX_PAN)
            {
                lock (packet)
                {
                    packet[PAN_CHANNEL + startAddr - 1] = (byte)(panVal + 128);
                }

            }
        }

        const int MIN_TILT = -128;
        const int MAX_TILT = 127;
        const int TILT_CHANNEL = 10;

        /// <summary>
        /// Sets the tilt(up down motion).
        /// </summary>
        /// <param name="tiltVal">int from -128 to 127, 0 is neutral(pointing straight up)</param>
        public void setTilt(int tiltVal)
        {
            if (tiltVal >= MIN_TILT && tiltVal <= MAX_TILT)
            {
                lock (packet)
                {
                    packet[TILT_CHANNEL + startAddr - 1] = (byte)(tiltVal + 128);
                }

            }
        }


        const int FINE_PAN_CHANNEL = 9;

        /// <summary>
        /// Alternative function to setPan, this takes a 16 bit signed integer 
        /// valid range is -32,768 to +32,767
        /// </summary>
        /// <param name="panVal">integer from -32,768(full left) to +32,767(full right), 0 is neutral</param>
        public void setPan16Bit(short panVal)
        {
            ushort unsignedPan = (ushort)(panVal + (short.MinValue * -1));
            byte lsb = (byte)(unsignedPan & 255);
            byte msb = (byte)((unsignedPan & (255 << 8)) >> 8);
            lock (packet)
            {
                packet[FINE_PAN_CHANNEL + startAddr - 1] = lsb;
                packet[PAN_CHANNEL + startAddr - 1] = msb;
            }


        }

        const int FINE_TILT_CHANNEL = 11;
        /// <summary>
        /// Alternative function to setTilt, this takes a 16bit signed integer
        /// valid range is -32,768 to +32,767
        /// </summary>
        /// <param name="tiltVal">short that is -32,768 to +32,767, 0 being neutral</param>
        public void setTilt16Bit(short tiltVal)
        {
            ushort unsignedTilt = (ushort)(tiltVal + (short.MinValue * -1));
            byte lsb = (byte)(unsignedTilt & 255);
            byte msb = (byte)((unsignedTilt & (255 << 8)) >> 8);

            lock (packet)
            {
                packet[FINE_TILT_CHANNEL + startAddr - 1] = lsb;
                packet[TILT_CHANNEL + startAddr - 1] = msb;
            }

        }

        static volatile bool effectThreadRunning = false;
        Thread effectThread = null;

        public void threadedScanEffect(short min, short max, short speed)
        {
            effectThread = new Thread(() => scaneffectThread(packet, this, min, max, speed));
            effectThread.Start();
        }

        public void threadedScanEffect()
        {
            //panItr starts -16000
            //max = 0
            //speed is 5
            //defaults used for the studio performance

            effectThread = new Thread(() => scaneffectThread(packet, this, -16000, 0, 5));
            effectThread.Start();
        }

        public void endEffectThread(){
            effectThreadRunning = false;
        }
        public bool isEffectThreadRunning()
        {
            return effectThreadRunning;
        }

        static bool itrIncrease = true;
        
        private static void scaneffectThread(byte[] packet, DmxDriver device, short min, short max, short speed)
        {
            effectThreadRunning = true;
            device.setTilt(-80);

            short panItr = min;
            while (effectThreadRunning)
            {
                if (itrIncrease == true)
                {
                    if (panItr < max)
                    {
                        panItr += speed;
                    }
                    else
                    {
                        itrIncrease = false;
                    }
                }
                else
                {
                    if (panItr > min)
                    {
                        panItr -= speed;
                    }
                    else
                    {
                        itrIncrease = true;
                    }
                }
                device.setPan16Bit(panItr);
                Thread.Sleep(1);

            }

            effectThreadRunning = false;
        }

    }
}
