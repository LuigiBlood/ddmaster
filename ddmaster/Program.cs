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

                //Get Sys Data (assumes dev disk for now)
                byte[] sys_data = new byte[256];
                file_conv.Seek(0x9A10, SeekOrigin.Begin);
                file_conv.Read(sys_data, 0, 0xE8);

                byte disk_type = (byte)(sys_data[5] & 0x0F);

                string orig_format;
                if (file_conv.Length == 0x3DEC800)
                    orig_format = "ndd";
                else if (file_conv.Length == 0x435B0C0)
                    orig_format = "mame";
                else
                    orig_format = "d64";

                if (set_conv == orig_format)
                {
                    //Nothing to do
                    file_conv.Close();
                }
                else if (set_conv == "mame" && orig_format == "ndd")
                {
                    //NDD to MAME
                    byte[] output_mame = new byte[0x435B0C0];

                    int[] table = Leo.GenLBAToPhysTable(sys_data);

                    for (int i = 0; i < 4316; i++)
                    {
                        int pzone = Leo.LBAToPZone(i, disk_type);
                        int physinfo = Leo.LBAToPhys(i, table);
                        int cylinder = physinfo & 0xFFF;
                        int cylinder_zone = cylinder - Leo.OUTERCYL_TBL[(pzone < 8) ? pzone % 8 : pzone - 8];
                        int head = (physinfo & 0x1000) >> 12;
                        int block = (physinfo & 0x2000) >> 13;
                        int blocksize = Leo.BLOCK_SIZES[(pzone < 8) ? pzone % 8 : pzone - 7];

                        //PZone Offset
                        int mameoffset = Leo.MAMEStartOffset[pzone];
                        //Track Offset
                        mameoffset += ((blocksize * 2) * cylinder_zone);
                        //Block Offset
                        mameoffset += (blocksize * block);

                        int lbaoffset = Leo.LBAToByte(disk_type, i, 0);

                        file_conv.Seek(lbaoffset, SeekOrigin.Begin);
                        file_conv.Read(output_mame, mameoffset, blocksize);
                    }
                    file_conv.Close();

                    //Write D64 File
                    FileStream file_out = new FileStream(set_o, FileMode.Create);
                    file_out.Write(output_mame, 0, output_mame.Length);

                    file_out.Close();
                }
                else if (set_conv == "ndd" && orig_format == "mame")
                {
                    //NDD to MAME
                    byte[] output_ndd = new byte[0x3DEC800];

                    int[] table = Leo.GenLBAToPhysTable(sys_data);

                    for (int i = 0; i < 4316; i++)
                    {
                        int pzone = Leo.LBAToPZone(i, disk_type);
                        int physinfo = Leo.LBAToPhys(i, table);
                        int cylinder = physinfo & 0xFFF;
                        int cylinder_zone = cylinder - Leo.OUTERCYL_TBL[(pzone < 8) ? pzone % 8 : pzone - 8];
                        int head = (physinfo & 0x1000) >> 12;
                        int block = (physinfo & 0x2000) >> 13;
                        int blocksize = Leo.BLOCK_SIZES[(pzone < 8) ? pzone % 8 : pzone - 7];

                        //PZone Offset
                        int mameoffset = Leo.MAMEStartOffset[pzone];
                        //Track Offset
                        mameoffset += ((blocksize * 2) * cylinder_zone);
                        //Block Offset
                        mameoffset += (blocksize * block);

                        int lbaoffset = Leo.LBAToByte(disk_type, i, 0);

                        file_conv.Seek(mameoffset, SeekOrigin.Begin);
                        file_conv.Read(output_ndd, lbaoffset, blocksize);
                    }
                    file_conv.Close();

                    //Write D64 File
                    FileStream file_out = new FileStream(set_o, FileMode.Create);
                    file_out.Write(output_ndd, 0, output_ndd.Length);

                    file_out.Close();
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
