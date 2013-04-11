using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DmxComm;

namespace DmxEffects
{
    class Effects
    {
        private static int tempc = 0;

       
        public static void exampleEffect1(DmxDriver device)
        {
            Console.WriteLine("DO TEST FUNCTION1");
            tempc += 1;
            device.setLampOn();
            device.setDimmerLevel((byte)(tempc & 0xff));
            device.setPan((byte)((tempc % 255) - 128));
            device.setTilt((byte)((tempc % 255) - 128));
            device.setColorContinuous(DmxDriver.color_t.PINK);
        }

        public static void exampleEffect2(DmxDriver device)
        {
            Console.WriteLine("DO TEST FUNCTION2");
            tempc -= 1;
            device.setLampOn();
            device.setDimmerLevel((byte)(tempc & 0xff));
            device.setPan((byte)((tempc % 255) - 128));
            device.setTilt((byte)((tempc % 255) - 128));
            device.setColorContinuous(DmxDriver.color_t.BLUE_101);
            device.setGoboStandard(3);
        }
    }
}
