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
            Console.WriteLine("ddmaster v0.2");
            Console.WriteLine("---- by LuigiBlood");
            Console.WriteLine("");

            string set_cfg = "";
            string set_rom = "";
            string set_ram = "";
            string set_o = "";
            string set_ipladdr = "";
            string set_iplsize = "";
            string set_ndd = "";

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
            }

            if (set_o == "")
                set_o = "master.d64";

            //Check if required ones are found
            if (set_ndd == "" && set_cfg == "" && set_rom == "")
            {
                Usage();
                return;
            }

            //Make Disk Code
            if (set_ndd == "")
            {
                //Make Disk from CFG and ROM
                int cfg_disktype = -1;
                int cfg_destcode = 0;
                byte[] cfg_diskid;
                ProcessCfg(set_cfg, out cfg_disktype, out cfg_destcode, out cfg_diskid);

                uint cfg_ipladdr = uint.Parse(set_ipladdr, System.Globalization.NumberStyles.HexNumber);
                int cfg_iplsize = bytetolba(cfg_disktype, int.Parse(set_iplsize), 24);

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

                int block_rom_size = bytetolba(cfg_disktype, (int)file_rom.Length, 24);
                int block_rom_byte_size = lbatobyte(cfg_disktype, block_rom_size, 24);

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
                byte[] d64_rom = new byte[lbatobyte(disk_type, lba_rom_end + 1, 24)];
                file_ndd.Seek(0x738C0, SeekOrigin.Begin);
                file_ndd.Read(d64_rom, 0, d64_rom.Length);

                //RAM Area
                byte[] d64_ram = new byte[lbatobyte(disk_type, lba_ram_end - lba_ram_start, lba_ram_start + 24)];
                file_ndd.Seek(0x738C0 + lbatobyte(disk_type, lba_ram_start, 24), SeekOrigin.Begin);
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

        static void ProcessCfg(string filepath, out int disktype, out int destcode, out byte[] diskid)
        {
            //FileStream file = new FileStream(filepath, FileMode.Open);
            StreamReader reader = new StreamReader(filepath);
            List<string> cfg = new List<string>();

            string s_type = "";
            string s_code = "";
            string s_ver = "";
            string s_diskno = "";
            string s_ramuse = "";
            string s_diskuse = "";
            string s_dest = "";
            string s_company = "";
            string s_freearea = "";

            string s = "";
            while (s != null)
            {
                s = reader.ReadLine();
                if (s != null)
                {
                    if (s.ToUpperInvariant().StartsWith("DISK TYPE"))
                        s_type = GetCfg(s, "DISK TYPE");
                    else if (s.ToUpperInvariant().StartsWith("INITIAL CODE"))
                        s_code = GetCfg(s, "INITIAL CODE");
                    else if (s.ToUpperInvariant().StartsWith("GAME VERSION"))
                        s_ver = GetCfg(s, "GAME VERSION");
                    else if (s.ToUpperInvariant().StartsWith("DISK NUMBER"))
                        s_diskno = GetCfg(s, "DISK NUMBER");
                    else if (s.ToUpperInvariant().StartsWith("RAM USE"))
                        s_ramuse = GetCfg(s, "RAM USE");
                    else if (s.ToUpperInvariant().StartsWith("DISK USE"))
                        s_diskuse = GetCfg(s, "DISK USE");
                    else if (s.ToUpperInvariant().StartsWith("DESTINATION CODE"))
                        s_dest = GetCfg(s, "DESTINATION CODE");
                    else if (s.ToUpperInvariant().StartsWith("COMPANY CODE"))
                        s_company = GetCfg(s, "COMPANY CODE");
                    else if (s.ToUpperInvariant().StartsWith("FREE AREA"))
                        s_freearea = GetCfg(s, "FREE AREA");
                }
            }

            reader.Close();

            //REALLY BAD CODE
            if (s_type.ToUpperInvariant() == "AUTO")
                disktype = -1;
            else
                disktype = int.Parse(s_type);

            List<byte> id = new List<byte>();
            id.Add((byte)s_code[0]);
            id.Add((byte)s_code[1]);
            id.Add((byte)s_code[2]);
            id.Add((byte)s_code[3]);

            id.Add(byte.Parse(s_ver));
            id.Add(byte.Parse(s_diskno));
            id.Add(byte.Parse(s_ramuse));
            id.Add(byte.Parse(s_diskuse));

            if (s_dest == "JAPAN")
                destcode = 0;
            else
                destcode = int.Parse(s_dest);

            id.Add(0); id.Add(0); id.Add(0); id.Add(0);
            id.Add(0); id.Add(0); id.Add(0); id.Add(0);

            id.Add(0); id.Add(0); id.Add(0); id.Add(0);
            id.Add(0); id.Add(0); id.Add(0); id.Add(0);

            id.Add((byte)s_company[0]);
            id.Add((byte)s_company[1]);

            id.Add(byte.Parse(s_freearea.Substring(2, 2), System.Globalization.NumberStyles.HexNumber));
            id.Add(byte.Parse(s_freearea.Substring(4, 2), System.Globalization.NumberStyles.HexNumber));
            id.Add(byte.Parse(s_freearea.Substring(6, 2), System.Globalization.NumberStyles.HexNumber));
            id.Add(byte.Parse(s_freearea.Substring(8, 2), System.Globalization.NumberStyles.HexNumber));
            id.Add(byte.Parse(s_freearea.Substring(10, 2), System.Globalization.NumberStyles.HexNumber));
            id.Add(byte.Parse(s_freearea.Substring(12, 2), System.Globalization.NumberStyles.HexNumber));

            diskid = id.ToArray();
        }

        static string GetCfg(string line, string info)
        {
            return line.Substring(info.Length).Trim();
        }

        //64DD shit
        static int bytetolba(int disktype, int nbytes, int startlba)
        {
            byte init_flag = 1;
            int vzone = 1;
            int pzone = 0;
            int lba = startlba;
            int lba_count = 0;
            int byte_count = nbytes;
            int blkbytes = 0;
            if (nbytes != 0)
            {
                do
                {
                    if ((init_flag != 0) || (VZONE_LBA_TBL[disktype,vzone] == lba))
                    {
                        vzone = LBAToVZone(lba, disktype);
                        pzone = VZoneToPZone(vzone, disktype);
                        if (7 < pzone)
                        {
                            pzone -= 7;
                        }
                        blkbytes = BLOCK_SIZES[pzone];
                    }
                    if (byte_count < blkbytes)
                    {
                        byte_count = 0;
                    }
                    else
                    {
                        byte_count -= blkbytes;
                    }
                    lba++;
                    lba_count++;
                    init_flag = 0;
                    if ((byte_count != 0) && (lba > 0x10db))
                    {
                        return -1;
                    }
                } while (byte_count != 0);
            }
            return lba_count;
        }

        static int lbatobyte(int disktype, int nlbas, int startlba)
        {
            int totalbytes = 0;
            byte init_flag = 1;
            int vzone = 1;
            int pzone = 0;
            int lba = startlba;
            int lba_count = nlbas;
            int blkbytes = 0;
            if (nlbas != 0)
            {
                for (; lba_count != 0; lba_count--)
                {
                    if ((init_flag != 0) || (VZONE_LBA_TBL[disktype,vzone] == lba))
                    {
                        vzone = LBAToVZone(lba, disktype);
                        pzone = VZoneToPZone(vzone, disktype);
                        if (7 < pzone)
                        {
                            pzone -= 7;
                        }
                        blkbytes = BLOCK_SIZES[pzone];
                    }
                    totalbytes += blkbytes;
                    lba++;
                    init_flag = 0;
                    if ((lba_count != 0) && (lba > 0x10db))
                    {
                        return -1;
                    }
                }
            }
            return totalbytes;
        }

        static void Usage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine(" ddmaster <arguments>");
            Console.WriteLine("Arguments:");
            Console.WriteLine("Make Disk with Config:");
            Console.WriteLine(" -cfg <filepath> = Use file as Disk Info configuration (required)");
            Console.WriteLine(" -rom <filepath> = Use file as the Disk ROM content (required)");
            //Console.WriteLine(" -ram <filepath> = Use file as the default Disk RAM content (optional)");
            Console.WriteLine(" -ipladdr <RAM address> = Start RAM Address of the main boot code in hex");
            Console.WriteLine(" -iplsize <Size> = Size in bytes (decimal) of the main boot code");
            Console.WriteLine("");
            Console.WriteLine("Make Disk with NDD Image:");
            Console.WriteLine(" -ndd <filepath> = Use file as retail disk file to convert to d64 (required)");
            Console.WriteLine("");
            Console.WriteLine(" -o <filepath> = Use filepath as the output disk file (optional, will make master.d64 by default)");
        }

        static byte[] SECTOR_SIZES = { 0xE8, 0xD8, 0xD0, 0xC0, 0xB0, 0xA0, 0x90, 0x80, 0x70 };
        static ushort[] BLOCK_SIZES = { 0x4D08, 0x47B8, 0x4510, 0x3FC0, 0x3A70, 0x3520, 0x2FD0, 0x2A80, 0x2530 };

        static ushort[,] VZONE_LBA_TBL = {
            {0x0124, 0x0248, 0x035A, 0x047E, 0x05A2, 0x06B4, 0x07C6, 0x08D8, 0x09EA, 0x0AB6, 0x0B82, 0x0C94, 0x0DA6, 0x0EB8, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x06A2, 0x07C6, 0x08D8, 0x09EA, 0x0AFC, 0x0BC8, 0x0C94, 0x0DA6, 0x0EB8, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x08C6, 0x09EA, 0x0AFC, 0x0C0E, 0x0CDA, 0x0DA6, 0x0EB8, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x08B4, 0x09C6, 0x0AEA, 0x0C0E, 0x0D20, 0x0DEC, 0x0EB8, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x08B4, 0x09C6, 0x0AD8, 0x0BEA, 0x0D0E, 0x0E32, 0x0EFE, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x086E, 0x0980, 0x0A92, 0x0BA4, 0x0CB6, 0x0DC8, 0x0EEC, 0x1010, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x086E, 0x093A, 0x0A4C, 0x0B5E, 0x0C70, 0x0D82, 0x0E94, 0x0FB8, 0x10DC}
        };

        static byte[,] VZONE_PZONE_TBL = {
            {0x0, 0x1, 0x2, 0x9, 0x8, 0x3, 0x4, 0x5, 0x6, 0x7, 0xF, 0xE, 0xD, 0xC, 0xB, 0xA},
            {0x0, 0x1, 0x2, 0x3, 0xA, 0x9, 0x8, 0x4, 0x5, 0x6, 0x7, 0xF, 0xE, 0xD, 0xC, 0xB},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0xB, 0xA, 0x9, 0x8, 0x5, 0x6, 0x7, 0xF, 0xE, 0xD, 0xC},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0xC, 0xB, 0xA, 0x9, 0x8, 0x6, 0x7, 0xF, 0xE, 0xD},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0xD, 0xC, 0xB, 0xA, 0x9, 0x8, 0x7, 0xF, 0xE},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0xE, 0xD, 0xC, 0xB, 0xA, 0x9, 0x8, 0xF},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0xF, 0xE, 0xD, 0xC, 0xB, 0xA, 0x9, 0x8}
        };

        static int LBAToVZone(int lba, int disktype)
        {
            for (int vzone = 0; vzone < 16; vzone++)
            {
                if (lba < VZONE_LBA_TBL[disktype,vzone])
                {
                    return vzone;
                }
            }
            return -1;
        }

        static byte VZoneToPZone(int x, int y)
        {
            return VZONE_PZONE_TBL[y,x];
        }
    }
}
