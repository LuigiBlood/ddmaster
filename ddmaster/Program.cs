using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace ddmaster
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ddmaster v0.4");
            Console.WriteLine("---- by LuigiBlood");
            Console.WriteLine("");

            string set_cfg = "";
            string set_rom = "";
            string set_ram = "";
            string set_o = "";
            string set_ipladdr = "";
            string set_iplsize = "";
            string set_ndd = "";
            string set_conv = "";
            string set_convpath = "";

            if (args.Length == 0 && ((args.Length & 1) == 1))
            {
                Usage();
                return;
            }

            //Check all arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-cfg")
                {
                    //64DD Disk Configuration
                    i++;
                    set_cfg = args[i];
                }
                else if (args[i] == "-rom")
                {
                    //64DD Disk User ROM Area Binary
                    i++;
                    set_rom = args[i];
                }
                else if (args[i] == "-ram")
                {
                    //64DD Disk User RAM Area Binary
                    i++;
                    set_ram = args[i];
                }
                else if (args[i] == "-ipladdr")
                {
                    //Address for IPL to load and execute
                    i++;
                    set_ipladdr = args[i];
                }
                else if (args[i] == "-iplsize")
                {
                    //Size in bytes to load
                    i++;
                    set_iplsize = args[i];
                }
                else if (args[i] == "-o")
                {
                    //Output Filename
                    i++;
                    set_o = args[i];
                }
                else if (args[i] == "-ndd")
                {
                    //NDD (64DD Dump format) Filename
                    i++;
                    set_ndd = args[i];
                }
                else if (args[i] == "-conv")
                {
                    //Conversion to Format and Filename
                    i++;
                    set_conv = args[i];
                    i++;
                    set_convpath = args[i];
                }
            }

            if (set_o == "")
                set_o = "master.d64";

            //Check if required ones are found
            if (set_ndd == "" && set_cfg == "" && set_rom == "" && set_conv == "")
            {
                Usage();
                return;
            }

            //Make Disk Code
            if (set_ndd == "" && set_conv == "")
            {
                int ret = Convert.CFGtoD64(set_cfg, set_rom, set_ipladdr, set_iplsize, set_o);
                if (ret < 0)
                    return;
            }
            else if (set_conv != "")
            {
                //Convert Disk
                FileStream file_conv = new FileStream(set_convpath, FileMode.Open);

                //Get Size
                string orig_format;
                if (file_conv.Length == 0x3DEC800)
                    orig_format = "ndd";
                else if (file_conv.Length == 0x435B0C0)
                    orig_format = "mame";
                else
                    orig_format = "d64";

                file_conv.Close();

                if (set_conv == "mame" && orig_format == "ndd")
                {
                    //NDD to MAME
                    int ret = Convert.NDDtoMAME(set_convpath, set_o);
                    if (ret < 0)
                        return;
                }
                else if (set_conv == "ndd" && orig_format == "mame")
                {
                    //MAME to NDD
                    int ret = Convert.MAMEtoNDD(set_convpath, set_o);
                    if (ret < 0)
                        return;
                }
            }
            else
            {
                int ret = Convert.NDDtoD64(set_ndd, set_o);
                if (ret < 0)
                    return;
            }
        }

        static void Usage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine(" ddmaster <arguments>");
            Console.WriteLine("Arguments:");
            Console.WriteLine("To make Disk with Config:");
            Console.WriteLine(" -cfg <filepath> = Use file as Disk Info configuration (required)");
            Console.WriteLine(" -rom <filepath> = Use file as the Disk ROM content (required)");
            //Console.WriteLine(" -ram <filepath> = Use file as the default Disk RAM content (optional)");
            Console.WriteLine(" -ipladdr <RAM address> = Start RAM Address of the main boot code in hex");
            Console.WriteLine(" -iplsize <Size> = Size in bytes (decimal) of the main boot code");
            Console.WriteLine("");
            Console.WriteLine("To make Disk with NDD Image:");
            Console.WriteLine(" -ndd <filepath> = Use file as retail disk file to convert to d64 (required)");
            Console.WriteLine("");
            Console.WriteLine("To convert Disk Formats:");
            Console.WriteLine(" -conv <toformat> <filepath> = Use file as disk file to convert to <format> (required)");
            Console.WriteLine("    toformat = ndd, mame");
            Console.WriteLine("");
            Console.WriteLine(" -o <filepath> = Use filepath as the output disk file (optional, will make master.d64 by default)");
        }
    }
}
