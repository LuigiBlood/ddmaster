using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ddmaster
{
    public static class Convert
    {
        public static int NDDtoD64(string set_ndd, string set_o)
        {
            //Make Disk from NDD Disk File
            FileStream file_ndd = new FileStream(set_ndd, FileMode.Open);

            //Make sure it is the right size
            if (file_ndd.Length != 0x3DEC800)
            {
                Console.WriteLine("ERROR: NDD FILE SIZE IS NOT VALID");
                file_ndd.Close();
                return -1;
            }

            byte[] sys_data = Util.GetSystemData(file_ndd);
            //Make sure it is a retail disk
            if (sys_data == null || sys_data.Length != Leo.SECTOR_SIZES[0])
            {
                Console.WriteLine("ERROR: COULD NOT FIND A VALID RETAIL SYSTEM DATA BLOCK");
                file_ndd.Close();
                return -1;
            }

            //Test Region Code
            uint regioncode = 0;
            regioncode = (uint)(sys_data[0] & 0xFF);
            regioncode = (regioncode << 8) | (uint)(sys_data[1] & 0xFF);
            regioncode = (regioncode << 8) | (uint)(sys_data[2] & 0xFF);
            regioncode = (regioncode << 8) | (uint)(sys_data[3] & 0xFF);

            if (regioncode != 0xE848D316 && regioncode != 0x2263EE56)
            {
                Console.WriteLine("ERROR: NDD SYSTEM DATA DOES NOT HAVE A VALID REGION CODE");
                file_ndd.Close();
                return -1;
            }

            //Sys Data
            byte[] d64_sys_data = new byte[256];
            sys_data.CopyTo(d64_sys_data, 0);

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

            byte[] sys_id = Util.GetDiskIDInfo(file_ndd);

            //Make sure the Disk ID info is valid
            if (sys_id == null)
            {
                Console.WriteLine("ERROR: COULD NOT FIND A VALID DISK ID BLOCK");
                file_ndd.Close();
                return -1;
            }
            sys_id.CopyTo(d64_sys_id, 0);

            if (regioncode == 0xE848D316)
                d64_sys_id[0xE8] = 0;   //JAPAN
            else if (regioncode == 0x2263EE56)
                d64_sys_id[0xE8] = 1;   //USA

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
            return 0;
        }

        public static int CFGtoD64(string set_cfg, string set_rom, string set_ipladdr, string set_iplsize, string set_o)
        {
            //Make Disk from CFG and ROM
            int cfg_disktype = -1;
            int cfg_destcode = 0;
            byte[] cfg_diskid;
            Util.ProcessCfg(set_cfg, out cfg_disktype, out cfg_destcode, out cfg_diskid);

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
                return -1;
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
            return 0;
        }

        public static int NDDtoMAME(string set_convpath, string set_o)
        {
            FileStream file_conv = new FileStream(set_convpath, FileMode.Open);

            byte[] sys_data = Util.GetSystemData(file_conv);
            if (sys_data == null)
            {
                Console.WriteLine("ERROR: COULD NOT FIND A VALID SYSTEM DATA BLOCK");
                file_conv.Close();
                return -1;
            }

            byte[] output_mame = new byte[0x435B0C0];
            byte disk_type = (byte)(sys_data[5] & 0x0F);
            int[] table = Leo.GenLBAToPhysTable(sys_data);

            for (int i = 0; i < Leo.LBA_COUNT; i++)
            {
                int blocksizemame = 0;
                int mameoffset = Util.GetMAMEBlockInfo(i, table, disk_type, out blocksizemame);

                int blocksizendd = 0;
                int nddoffset = Util.GetNDDLBAInfo(i, disk_type, out blocksizendd);

                if (blocksizemame != blocksizendd)
                {
                    Console.WriteLine("ERROR: BLOCK SIZE COULD NOT BE CALCULATED CORRECTLY");
                    file_conv.Close();
                    return -1;
                }

                file_conv.Seek(nddoffset, SeekOrigin.Begin);
                file_conv.Read(output_mame, mameoffset, blocksizemame);
            }
            file_conv.Close();

            //Write D64 File
            FileStream file_out = new FileStream(set_o, FileMode.Create);
            file_out.Write(output_mame, 0, output_mame.Length);

            file_out.Close();

            return 0;
        }

        public static int MAMEtoNDD(string set_convpath, string set_o)
        {
            FileStream file_conv = new FileStream(set_convpath, FileMode.Open);

            byte[] sys_data = Util.GetSystemData(file_conv);
            if (sys_data == null)
            {
                Console.WriteLine("ERROR: COULD NOT FIND A VALID SYSTEM DATA BLOCK");
                file_conv.Close();
                return -1;
            }

            byte[] output_ndd = new byte[0x3DEC800];
            byte disk_type = (byte)(sys_data[5] & 0x0F);
            int[] table = Leo.GenLBAToPhysTable(sys_data);

            for (int i = 0; i < Leo.LBA_COUNT; i++)
            {
                int blocksizemame = 0;
                int mameoffset = Util.GetMAMEBlockInfo(i, table, disk_type, out blocksizemame);

                int blocksizendd = 0;
                int nddoffset = Util.GetNDDLBAInfo(i, disk_type, out blocksizendd);

                if (blocksizemame != blocksizendd)
                {
                    Console.WriteLine("ERROR: BLOCK SIZE COULD NOT BE CALCULATED CORRECTLY");
                    file_conv.Close();
                    return -1;
                }

                file_conv.Seek(mameoffset, SeekOrigin.Begin);
                file_conv.Read(output_ndd, nddoffset, blocksizendd);
            }
            file_conv.Close();

            //Write D64 File
            FileStream file_out = new FileStream(set_o, FileMode.Create);
            file_out.Write(output_ndd, 0, output_ndd.Length);

            file_out.Close();

            return 0;
        }
    }
}
