using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ddmaster
{
    public static class Util
    {
        //Process mwrite Disk Configuration file
        public static void ProcessCfg(string filepath, out int disktype, out int destcode, out byte[] diskid)
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

            //Get Configuration Information
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

            //Make Disk ID Info
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

        public static string GetCfg(string line, string info)
        {
            return line.Substring(info.Length).Trim();
        }


        //Find Correct System Data (Return Block)
        public static int FindSystemData(FileStream ndd)
        {
            //Verify if Data repeats in the following blocks (works for NDD and MAME formats)
            int[] blocks = { 0, 1, 8, 9 };
            bool found = false;
            byte[] data = new byte[Leo.BLOCK_SIZES[0]];

            //Retail Check
            foreach (int i in blocks)
            {
                ndd.Seek(i * Leo.BLOCK_SIZES[0], SeekOrigin.Begin);
                ndd.Read(data, 0, Leo.BLOCK_SIZES[0]);

                found = IsDataRepeating(data, Leo.SECTOR_SIZES[0], Leo.USER_SECTORS_COUNT);
                if (found)
                    return i;
            }

            //Development Check
            foreach (int i in blocks)
            {
                ndd.Seek((i + 2) * Leo.BLOCK_SIZES[0], SeekOrigin.Begin);
                ndd.Read(data, 0, Leo.BLOCK_SIZES[0]);

                found = IsDataRepeating(data, Leo.SECTOR_SIZES[3], Leo.USER_SECTORS_COUNT);
                if (found)
                    return i + 2;
            }

            return -1;
        }

        //Return System Data Info Byte Array
        public static byte[] GetSystemData(FileStream ndd)
        {
            int block = FindSystemData(ndd);

            //Return null if all System Data info is invalid
            if (block < 0)
                return null;

            //Get System Data Info
            byte[] sys;
            if ((block & 2) == 0)
                sys = new byte[Leo.SECTOR_SIZES[0]];    //Retail
            else
                sys = new byte[Leo.SECTOR_SIZES[3]];    //Development

            ndd.Seek(block * Leo.BLOCK_SIZES[0], SeekOrigin.Begin);
            ndd.Read(sys, 0, sys.Length);

            return sys;
        }

        public static int FindDiskIDInfo(FileStream ndd)
        {
            //Verify if Data repeats in the following blocks (works for NDD and MAME formats)
            int[] blocks = { 14, 15 };
            bool found = false;
            byte[] data = new byte[Leo.BLOCK_SIZES[0]];

            //Check
            foreach (int i in blocks)
            {
                ndd.Seek(i * Leo.BLOCK_SIZES[0], SeekOrigin.Begin);
                ndd.Read(data, 0, Leo.BLOCK_SIZES[0]);

                found = IsDataRepeating(data, Leo.SECTOR_SIZES[0], Leo.USER_SECTORS_COUNT);
                if (found)
                    return i;
            }

            return -1;
        }

        public static byte[] GetDiskIDInfo(FileStream ndd)
        {
            int block = FindDiskIDInfo(ndd);

            //Return null if all System Data info is invalid
            if (block < 0)
                return null;

            //Get Disk ID Info
            byte[] diskid = new byte[Leo.SECTOR_SIZES[0]];

            ndd.Seek(block * Leo.BLOCK_SIZES[0], SeekOrigin.Begin);
            ndd.Read(diskid, 0, diskid.Length);

            return diskid;
        }

        public static bool IsDataRepeating(byte[] data, int sectorsize, int sectors)
        {
            for (int j = 1; j < sectors; j++)
            {
                for (int k = 0; k < sectorsize; k++)
                {
                    if (data[k] != data[k + (j * sectorsize)])
                        return false;
                }
            }
            return true;
        }
    }
}
