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
                    i++;
                    set_cfg = args[i];
                }
                else if (args[i] == "-rom")
                {
                    i++;
                    set_rom = args[i];
                }
                else if (args[i] == "-ram")
                {
                    i++;
                    set_ram = args[i];
                }
                else if (args[i] == "-ipladdr")
                {
                    i++;
                    set_ipladdr = args[i];
                }
                else if (args[i] == "-iplsize")
                {
                    i++;
                    set_iplsize = args[i];
                }
                else if (args[i] == "-o")
                {
                    i++;
                    set_o = args[i];
                }
                else if (args[i] == "-ndd")
                {
                    i++;
                    set_ndd = args[i];
                }
                else if (args[i] == "-conv")
                {
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
                //Make Disk from CFG and ROM
                int cfg_disktype = -1;
                int cfg_destcode = 0;
                byte[] cfg_diskid;
                Generate.ProcessCfg(set_cfg, out cfg_disktype, out cfg_destcode, out cfg_diskid);

                uint cfg_ipladdr = uint.Parse(set_ipladdr, System.Globalization.NumberStyles.HexNumber);
                int cfg_iplsize = Leo.ByteToLBA(cfg_disktype, int.Parse(set_iplsize), 24);

                //Take ROM size
                FileStream file_rom = new FileStream(set_rom, FileMode.Open);
                if (cfg_disktype == -1)
                {
                    if (file_rom.Length < 26014080) cfg_disktype = 0;
                    else if (file_rom.Length < 34957440) cfg_disktype = 1;
                    else if (file_rom.Length < 43155520) cfg_disktype = 2;
                    else if (file_rom.Length < 50608320) cfg_disktype = 3;
                    else if (file_rom.Length < 57315840) cfg_disktype = 4;
                    else if (file_rom.Length < 62516480) cfg_disktype = 5;
                    else if (file_rom.Length < 64458560) cfg_disktype = 6;
                }

                if (file_rom.Length > 64458560)
                {
                    //TOO BIG!
                    Console.WriteLine("ERROR: ROM FILE IS TOO BIG! (MAX SIZE 64458560 bytes)");
                    return;
                }

                int block_rom_size = Leo.ByteToLBA(cfg_disktype, (int)file_rom.Length, 24);
                int block_rom_byte_size = Leo.LBAToByte(cfg_disktype, block_rom_size, 24);

                byte[] rom_data = new byte[block_rom_byte_size];
                Array.Fill<byte>(rom_data, 0xFF);

                file_rom.Read(rom_data, 0, (int)file_rom.Length);

                //Make D64 file
                FileStream file_out = new FileStream(set_o, FileMode.Create);

                byte[] d64_sys_data = new byte[256];
                byte[] d64_sys_id = new byte[256];
                d64_sys_data[0x05] = (byte)cfg_disktype;
                d64_sys_data[0x06] = (byte)(cfg_iplsize >> 8);
                d64_sys_data[0x07] = (byte)(cfg_iplsize & 0xFF);

                d64_sys_data[0x1C] = (byte)(cfg_ipladdr >> 24);
                d64_sys_data[0x1D] = (byte)(cfg_ipladdr >> 16);
                d64_sys_data[0x1E] = (byte)(cfg_ipladdr >> 8);
                d64_sys_data[0x1F] = (byte)(cfg_ipladdr & 0xFF);

                d64_sys_data[0xE0] = (byte)((block_rom_size - 1) >> 8);
                d64_sys_data[0xE1] = (byte)((block_rom_size - 1) & 0xFF);
                d64_sys_data[0xE2] = 0xFF;
                d64_sys_data[0xE3] = 0xFF;
                d64_sys_data[0xE4] = 0xFF;
                d64_sys_data[0xE5] = 0xFF;

                Array.Copy(cfg_diskid, d64_sys_id, cfg_diskid.Length);
                d64_sys_id[0xE8] = (byte)cfg_destcode;

                file_out.Write(d64_sys_data, 0, d64_sys_data.Length);
                file_out.Write(d64_sys_id, 0, d64_sys_id.Length);
                file_out.Write(rom_data, 0, rom_data.Length);

                file_out.Close();
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
                //Make Disk from NDD Disk File
                FileStream file_ndd = new FileStream(set_ndd, FileMode.Open);

                //Make sure it is the right size
                if (file_ndd.Length != 0x3DEC800)
                {
                    Console.WriteLine("ERROR: NDD FILE SIZE IS NOT VALID");
                    file_ndd.Close();
                    return;
                }

                //Make sure it is a retail disk (assumes the first block is correct for now)
                for (int i = 0; i < 85; i++)
                {
                    file_ndd.Seek(i * 0xE8, SeekOrigin.Begin);
                    uint temp = 0;
                    temp = (uint)(file_ndd.ReadByte() & 0xFF);
                    temp = (temp << 8) | (uint)(file_ndd.ReadByte() & 0xFF);
                    temp = (temp << 8) | (uint)(file_ndd.ReadByte() & 0xFF);
                    temp = (temp << 8) | (uint)(file_ndd.ReadByte() & 0xFF);

                    if (temp != 0xE848D316 && temp != 0x2263EE56)
                    {
                        Console.WriteLine("ERROR: NDD SYSTEM DATA BLOCK 0 IS NOT VALID");
                        file_ndd.Close();
                        return;
                    }
                }

                //Sys Data
                byte[] d64_sys_data = new byte[256];
                file_ndd.Seek(0, SeekOrigin.Begin);
                file_ndd.Read(d64_sys_data, 0, 0xE8);

                //Zeroes Useless Data
                for (int i = 0; i < 5; i++)
                    d64_sys_data[i] = 0;
                d64_sys_data[5] &= 0x0F;
                for (int i = 8; i < 0x1C; i++)
                    d64_sys_data[i] = 0;
                for (int i = 0x20; i < 0xE0; i++)
                    d64_sys_data[i] = 0;
                for (int i = 0xE6; i < 0xE8; i++)
                    d64_sys_data[i] = 0;

                byte disk_type = d64_sys_data[5];
                ushort lba_rom_end = (ushort)((d64_sys_data[0xE0] << 8) | d64_sys_data[0xE1]);
                ushort lba_ram_start = (ushort)((d64_sys_data[0xE2] << 8) | d64_sys_data[0xE3]);
                ushort lba_ram_end = (ushort)((d64_sys_data[0xE4] << 8) | d64_sys_data[0xE5]);

                //Disk ID
                byte[] d64_sys_id = new byte[256];
                file_ndd.Seek(0x43670, SeekOrigin.Begin);
                file_ndd.Read(d64_sys_id, 0, 0xE8);

                //ROM Area
                byte[] d64_rom = new byte[Leo.LBAToByte(disk_type, lba_rom_end + 1, 24)];
                file_ndd.Seek(0x738C0, SeekOrigin.Begin);
                file_ndd.Read(d64_rom, 0, d64_rom.Length);

                //RAM Area
                byte[] d64_ram = new byte[Leo.LBAToByte(disk_type, lba_ram_end - lba_ram_start + 1, lba_ram_start + 24)];
                file_ndd.Seek(0x738C0 + Leo.LBAToByte(disk_type, lba_ram_start, 24), SeekOrigin.Begin);
                file_ndd.Read(d64_ram, 0, d64_ram.Length);
                file_ndd.Close();

                //Write D64 File
                FileStream file_out = new FileStream(set_o, FileMode.Create);
                file_out.Write(d64_sys_data, 0, d64_sys_data.Length);
                file_out.Write(d64_sys_id, 0, d64_sys_id.Length);
                file_out.Write(d64_rom, 0, d64_rom.Length);
                file_out.Write(d64_ram, 0, d64_ram.Length);

                file_out.Close();
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
