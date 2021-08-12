using System;
using System.Collections.Generic;
using System.Text;

namespace ddmaster
{
    public static class Leo
    {
        //64DD Functions
        public static int ByteToLBA(int disktype, int nbytes, int startlba)
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
                    if ((init_flag != 0) || (VZONE_LBA_TBL[disktype, vzone] == lba))
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

        public static int LBAToByte(int disktype, int nlbas, int startlba)
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
                    if ((init_flag != 0) || (VZONE_LBA_TBL[disktype, vzone] == lba))
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
                    if ((lba_count != 0) && (lba > (0x10db + 1)))
                    {
                        return -1;
                    }
                }
            }
            return totalbytes;
        }

        public static int LBAToPhys(int lba, byte[] sys_data)
        {
            int start_block = 0;
            int vzone, pzone = 0;
            int param_head, param_zone, param_cylinder = 0;
            int vzone_lba, cylinder_zone_start, defect_offset, defect_amount = 0;

            int disktype = sys_data[5] & 0xF;

            //Skip System Area to correspond LBAs to SDK manual
            //lba += 24;

            //Unused here, get Block 0/1 on Disk Track
            if (((lba & 3) == 0) || ((lba & 3) == 3))
                start_block = 0;
            else
                start_block = 1;

            //Get Virtual & Physical Disk Zones
            vzone = LBAToVZone(lba, disktype);
            pzone = VZoneToPZone(vzone, disktype);

            //Get Disk Head
            param_head = (7 < pzone) ? 1 : 0;

            //Get Disk Zone
            param_zone = pzone;
            if (param_head != 0)
                param_zone = pzone - 7;

            //Get Virtual Zone LBA start, if Zone 0, it's LBA 0
            if (vzone == 0)
                vzone_lba = 0;
            else
                vzone_lba = VZONE_LBA_TBL[disktype, vzone - 1];

            //Calculate Physical Cylinder
            param_cylinder = (lba - vzone_lba) >> 1;

            //Get the start cylinder from current zone
            cylinder_zone_start = SCYL_PZONE_TBL[pzone];
            if (param_head != 0)
            {
                //If Head 1, count from the other way around
                param_cylinder = -param_cylinder;
                cylinder_zone_start = OUTERCYL_TBL[param_zone - 1];
            }
            param_cylinder += SCYL_PZONE_TBL[pzone];

            //Get the relative offset to defect tracks for the current zone (if Zone 0, then it's 0)
            if (pzone == 0)
                defect_offset = 0;
            else
                defect_offset = sys_data[8 + pzone - 1];

            //Get amount of defect tracks for the current zone
            defect_amount = sys_data[8 + pzone] - defect_offset;

            //Skip defect tracks
            while ((defect_amount != 0) && ((sys_data[0x20 + defect_offset] + cylinder_zone_start) <= param_cylinder))
            {
                param_cylinder++;
                defect_offset++;
                defect_amount--;
            }

            //Return Cylinder and Head data for 64DD Seek command
            return param_cylinder | (param_head * 0x1000) | (start_block * 0x2000);
        }

        public static int[] GenLBAToPhysTable(byte[] sys_data)
        {
            int[] table = new int[Leo.LBA_COUNT];

            for (int i = 0; i < Leo.LBA_COUNT; i++)
                table[i] = LBAToPhys(i, sys_data);

            return table;
        }

        public static int PhysToLBA(int head, int track, int block, int[] table)
        {
            int expectedvalue = track | (head * 0x1000) | (block * 0x2000);

            for (int lba = 0; lba < Leo.LBA_COUNT; lba++)
            {
                if (table[lba] == expectedvalue)
                {
                    return lba;
                }
            }
            return -1;
        }

        public static int LBAToPhys(int lba, int[] table)
        {
            return table[lba];
        }

        /* Sector Size in bytes [zone] */
        public static byte[] SECTOR_SIZES = { 0xE8, 0xD8, 0xD0, 0xC0, 0xB0, 0xA0, 0x90, 0x80, 0x70 };

        /* Block Size in bytes [zone] */
        public static ushort[] BLOCK_SIZES = { 0x4D08, 0x47B8, 0x4510, 0x3FC0, 0x3A70, 0x3520, 0x2FD0, 0x2A80, 0x2530 };

        /* LBA to VZone [type, vzone] */
        public static ushort[,] VZONE_LBA_TBL = {
            {0x0124, 0x0248, 0x035A, 0x047E, 0x05A2, 0x06B4, 0x07C6, 0x08D8, 0x09EA, 0x0AB6, 0x0B82, 0x0C94, 0x0DA6, 0x0EB8, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x06A2, 0x07C6, 0x08D8, 0x09EA, 0x0AFC, 0x0BC8, 0x0C94, 0x0DA6, 0x0EB8, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x08C6, 0x09EA, 0x0AFC, 0x0C0E, 0x0CDA, 0x0DA6, 0x0EB8, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x08B4, 0x09C6, 0x0AEA, 0x0C0E, 0x0D20, 0x0DEC, 0x0EB8, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x08B4, 0x09C6, 0x0AD8, 0x0BEA, 0x0D0E, 0x0E32, 0x0EFE, 0x0FCA, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x086E, 0x0980, 0x0A92, 0x0BA4, 0x0CB6, 0x0DC8, 0x0EEC, 0x1010, 0x10DC},
            {0x0124, 0x0248, 0x035A, 0x046C, 0x057E, 0x0690, 0x07A2, 0x086E, 0x093A, 0x0A4C, 0x0B5E, 0x0C70, 0x0D82, 0x0E94, 0x0FB8, 0x10DC}
        };

        /* VZone to PZone [type, vzone] */
        public static byte[,] VZONE_PZONE_TBL = {
            {0x0, 0x1, 0x2, 0x9, 0x8, 0x3, 0x4, 0x5, 0x6, 0x7, 0xF, 0xE, 0xD, 0xC, 0xB, 0xA},
            {0x0, 0x1, 0x2, 0x3, 0xA, 0x9, 0x8, 0x4, 0x5, 0x6, 0x7, 0xF, 0xE, 0xD, 0xC, 0xB},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0xB, 0xA, 0x9, 0x8, 0x5, 0x6, 0x7, 0xF, 0xE, 0xD, 0xC},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0xC, 0xB, 0xA, 0x9, 0x8, 0x6, 0x7, 0xF, 0xE, 0xD},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0xD, 0xC, 0xB, 0xA, 0x9, 0x8, 0x7, 0xF, 0xE},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0xE, 0xD, 0xC, 0xB, 0xA, 0x9, 0x8, 0xF},
            {0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0xF, 0xE, 0xD, 0xC, 0xB, 0xA, 0x9, 0x8}
        };

        /* Cylinder Number - Zone Start [head, zone] */
        public static ushort[,] SCYL_ZONE_TBL = {
            {0x000, 0x09E, 0x13C, 0x1D1, 0x266, 0x2FB, 0x390, 0x425},
            {0x091, 0x12F, 0x1C4, 0x259, 0x2EE, 0x383, 0x418, 0x48A}
        };

        /* Cylinder Number - PZone Start [pzone] */
        public static ushort[] SCYL_PZONE_TBL = {
            0x000, 0x09E, 0x13C, 0x1D1, 0x266, 0x2FB, 0x390, 0x425, 0x091, 0x12F, 0x1C4, 0x259, 0x2EE, 0x383, 0x418, 0x48A
        };

        /* Cylinder Number - Zone Start (if Head 1) [zone] */
        /* Used to count from the other way around */
        public static ushort[] OUTERCYL_TBL = { 0x000, 0x09E, 0x13C, 0x1D1, 0x266, 0x2FB, 0x390, 0x425 };

        /* MAME Start Offsets [zone] */
        public static int[] MAMEStartOffset =
            { 0x0, 0x5F15E0, 0xB79D00, 0x10801A0, 0x1523720, 0x1963D80, 0x1D414C0, 0x20BBCE0,
            0x23196E0, 0x28A1E00, 0x2DF5DC0, 0x3299340, 0x36D99A0, 0x3AB70E0, 0x3E31900, 0x4149200 };

        /* Amount of User Sectors */
        public static int USER_SECTORS_COUNT = 85;

        /* Amount of LBA */
        public static int LBA_COUNT = 4316;

        public static int LBAToVZone(int lba, int disktype)
        {
            for (int vzone = 0; vzone < 16; vzone++)
            {
                if (lba < VZONE_LBA_TBL[disktype, vzone])
                {
                    return vzone;
                }
            }
            return -1;
        }

        public static byte VZoneToPZone(int vzone, int disktype)
        {
            return VZONE_PZONE_TBL[disktype, vzone];
        }

        public static int LBAToPZone(int lba, int disktype)
        {
            return VZoneToPZone(LBAToVZone(lba, disktype), disktype);
        }
    }
}
