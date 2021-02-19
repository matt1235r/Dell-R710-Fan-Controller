using OpenHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace r710_Fan_Control
{
    class Program
    {        
        //IPMI SETTINGS - originally hardcoded here but now loaded from the config file for ease.
        static string ipAddress = Properties.Settings.Default.idracHostname;
        static string username = Properties.Settings.Default.idracUsername;
        static string password = Properties.Settings.Default.idracPassword;

        static Computer thisComputer;
        static bool log = Properties.Settings.Default.log;

        static void Main(string[] args)
        {
            //open new instance of hardware monitor, only looking at CPU to aid performance.
            thisComputer = new Computer() { CPUEnabled = true };
            thisComputer.Open();

            //checks if the log flag was used and enables verbose logging if so.
            if(args.Contains("-log")){ log = true; }


            //set fans to manual speed control - so we can control ourselves.
            if (log) { Console.WriteLine("Setting fans to manual control..."); }
            Transmit("0x30 0x30 0x01 0x00");
            
            //start of main program loop. terminate with ctrl-c from cmd.
            while (true)
            {

                float currentTemp = GetHottestCore();

                //Here is the main logic
                if(currentTemp < 40)
                {
                    if (log) { Console.WriteLine("Setting fans to 5%..."); }
                    Transmit("0x30 0x30 0x02 0xff 0x05"); //5% fan speed
                }
                else if(currentTemp < 50)
                {
                    if (log) { Console.WriteLine("Setting fans to 20%..."); }
                    Transmit("0x30 0x30 0x02 0xff 0x14"); // 20% fan speed
                }

                else if (currentTemp < 65)
                {
                    if (log) { Console.WriteLine("Setting fans to high speed..."); }
                    Transmit("0x30 0x30 0x02 0xff 0x23"); // 35% fan speed 
                }
                else
                {
                    if (log) { Console.WriteLine("System getting hot! Giving control back to server until temperatures drop..."); }
                    Transmit("0x30 0x30 0x01 0x01"); //break out, set fan control back to automatic
                }

                //holds the program here, until the specified time has elapsed.
                Thread.Sleep(Properties.Settings.Default.waitTime);
            }
        }     

        static void Transmit(string message) //function written to handle all communication with the servers idrac.
        {
            Process process = new Process();
            process.StartInfo.FileName = "ipmitool.exe";
            process.StartInfo.Arguments = $"-I lanplus -H {ipAddress} -U {username} -P {password} raw {message}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true; 
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string err = process.StandardError.ReadToEnd();
            Console.WriteLine(err); 
            if (log) { Console.WriteLine("================= FAN SETTINGS UPDATED ================="); }
        }

        //uses open hardware monitor, iterates over all the CPU cores in the system, returning the highest temp.
        static float GetHottestCore()
        {
            String temp = "";
            float highestTemp = 0;

            foreach (var hardwareItem in thisComputer.Hardware)
            {
                if (hardwareItem.HardwareType == HardwareType.CPU)
                {
                    hardwareItem.Update();
                    foreach (IHardware subHardware in hardwareItem.SubHardware)
                        subHardware.Update();

                    foreach (var sensor in hardwareItem.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            if (sensor.Value.Value > highestTemp) { highestTemp = sensor.Value.Value; }
                            temp += String.Format("{0} Temperature = {1}\r\n", sensor.Name, sensor.Value.HasValue ? sensor.Value.Value.ToString() : "no value");

                        }
                    }
                }
            }
            if (log) { Console.WriteLine(temp); }
            if (log){ Console.WriteLine($"hottest core is: {highestTemp.ToString()}"); }

            return highestTemp;
        }

    }
}
