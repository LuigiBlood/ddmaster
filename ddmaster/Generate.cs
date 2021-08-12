using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ddmaster
{
    public static class Generate
    {
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

        public static string GetCfg(string line, string info)
        {
            return line.Substring(info.Length).Trim();
        }
    }
}
