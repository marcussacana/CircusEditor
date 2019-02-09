﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CircusEditor
{
    
    //Looks the MES script have a magic at 0x4, with 0x03000000, maybe MES version?
    
    /* 
    All Strings in the bytecode have 0x20 Decremented, 
    the 0x04 is the space (char: $)

    After some test i supose the jump labels as strings search, 
    and don't need update any offset, if you know about any jump error,
    create a issue and send the script.
    */
    public class MesEditor
    {
        private List<uint> Offsets;
        private List<string> Prefix;
        private List<string> Sufix;
        private byte[] Script;
        public Encoding Encoding = Encoding.GetEncoding(932);
        public bool Filter = true;
        public byte Obfuscation = 0x20;//General Games
        //public byte Obfuscation = 0x01;//Some Games, Newer?


        public MesEditor(byte[] Script, bool StringFilter) {
            Filter = StringFilter;
            this.Script = Script;
        }
        public MesEditor(byte[] Script) {
            this.Script = Script;
        }

        //This char bellow isn't a default space, if the game don't work with $ as space, try this char.
        //"　" 
        public string[] Import() {
            FindStrings();

            Again:;
            List<string> Strings = new List<string>();
            Prefix = new List<string>();
            Sufix = new List<string>();
            foreach (uint Offset in Offsets) {
                List<byte> Buffer = new List<byte>();
                while (Script[Offset + Buffer.Count()] != 0x00) {
                    byte Byte = Script[Offset + Buffer.Count()];
                    Buffer.Add((byte)((Byte + Obfuscation) & 0xFF));
                }
                string Str = Encoding.GetString(Buffer.ToArray());
                List<string> BlackList = new List<string>() { "_", "�" };
                if (string.IsNullOrWhiteSpace(Str)) {
                    Offsets.Remove(Offset);
                    goto Again;
                }
                foreach (string Corrupt in BlackList)
                    if (Str.Contains(Corrupt)) {
                        Offsets.Remove(Offset);
                        goto Again;
                    }

                string Tmp = Str.TrimStart('$');
                string StrPrefix = Str.Substring(0, Str.Length - Tmp.Length);

                Tmp = Str.TrimEnd('$', '　');
                string StrSufix = Str.Substring(Str.Length - (Str.Length - Tmp.Length), (Str.Length - Tmp.Length));

                if (Obfuscation >= 0x20)
                    Str = Str.Trim('$', '　').Replace("$", @" ");

                if (Filter) {
                    Str = Str.Replace("@-", "");//underlined?
                    Str = Str.Replace("@i", "");//Italic?
                    Str = Str.Replace("@b", "");//Bold?

                    while (Str.StartsWith("@")) {
                        int Len = 1;
                        while (!(Str[Len] >= '0' && Str[Len] <= '9'))
                            Len++;
                        while ((Str[Len] >= '0' && Str[Len] <= '9'))
                            Len++;
                        StrPrefix += Str.Substring(0, Len);
                        Str = Str.Substring(Len, Str.Length - Len);
                    }
                    while (Str.EndsWith("#h")) {
                        StrSufix = "#h" + StrSufix;
                        Str = Str.Substring(0, Str.Length - 2);
                    }
                }

                Strings.Add(Str);


                Prefix.Add(StrPrefix);
                Sufix.Add(StrSufix);
            }
            return Strings.ToArray();
        }

        public byte[] Export(string[] Strings) {
            byte[] OutScript = new byte[Script.LongLength];
            Script.CopyTo(OutScript, 0);

            //Allow long index
            string[] ArrPrefix = Prefix.ToArray();
            string[] ArrSufix = Sufix.ToArray();
            uint[] Offsets = this.Offsets.ToArray();

            //Reverse Replace to prevent miss the pointer
            for (long i = Strings.LongLength - 1; i >= 0; i--) {
                byte[] Buffer = Encoding.GetBytes(ArrPrefix[i] + (Obfuscation >= 0x20 ? Strings[i].Replace(@" ", "$") : Strings[i]) + ArrSufix[i]);
                for (uint x = 0; x < Buffer.LongLength; x++)
                    Buffer[x] -= Obfuscation;
                ReplaceStrAt(ref OutScript, Buffer, Offsets[i]);
            }

            return OutScript;
        }

        private void ReplaceStrAt(ref byte[] Script, byte[] String, uint At) {
            //Get Original String Length
            uint Len = 0;
            while (Script[At + Len] != 0x00)
                Len++;

            //Copy Everything before the string
            byte[] Prefix = new byte[At];
            for (uint i = 0; i < Prefix.Length; i++)
                Prefix[i] = Script[i];

            //Copy Everything after the string
            byte[] Sufix = new byte[Script.LongLength - (At + Len)];
            for (uint i = 0; i < Sufix.LongLength; i++) {
                Sufix[i] = Script[i + (At + Len)];
            }

            //Merge all Parts
            Script = new byte[Prefix.LongLength + String.Length + Sufix.LongLength];
            Prefix.CopyTo(Script, 0);
            String.CopyTo(Script, Prefix.LongLength);
            Sufix.CopyTo(Script, Prefix.LongLength + String.LongLength);
        }

        /*D.S.
         00 61 04 < String prefix, 0x04 isn't part of the bytecode
         08 01 00 53 < Choice prefix: //0x53 = First option part
         format: (label\x0062ChoiceText(\x0053 = more option, \0x0009 = no more option))
        */
        /*DC3
         00 50 61 < String Prefix, 0x61 isn't a part of the bytecode
         00 61 < Char Name
        */
        private void FindStrings() {
            var OffsetsBak = new List<uint>();
            Offsets = new List<uint>();
            uint ByteCodeStart = GetUintAt(0) * 4;
            if (ByteCodeStart >= Script.Length)
                throw new Exception("This isn't a valid MES Script.");

            //Search Strings - Type 1 (D.S)
            byte[] SrhPrx = new byte[] { 0x00, 0x61 };//Bytecode 0x0061
            for (uint i = ByteCodeStart; i < Script.Length; i++)
                if (EqualsAt(SrhPrx, i)) {
                    Offsets.Add((i + (uint)SrhPrx.Length));
                }

           //Search Strings - Type 2 (DC3)
            SrhPrx = new byte[] { 0x00, 0x50 };//Bytecode 0x0050
            for (uint i = ByteCodeStart; i < Script.Length-2; i++)
                if (EqualsAt(SrhPrx, i) && (Script[i+2] != 0x00 || Script[i+3] != 0x00)) {
                    Offsets.Add(i + (uint)SrhPrx.Length);
                }

            OffsetsBak = Offsets.ToList();
            Offsets.Clear();

            //Search Names - (DC3)
            SrhPrx = new byte[] { 0x00, 0x4F };//Bytecode 0x0061
            for (uint i = ByteCodeStart; i < Script.Length - 2; i++)
                if (EqualsAt(SrhPrx, i) && (Script[i + 2] != 0x00 || Script[i + 3] != 0x00)) {
                    i += 2;
                    if (Script[i] <= 0x3 || Script[i + 1] <= 0x3 || Script[i + 2] <= 0x3)
                        continue;
                    Offsets.Add(i);
                }

            bool Valid = (from x in OffsetsBak where ValidString(x) select x).Count() < (from x in Offsets where ValidString(x) select x).Count();
            if (!Valid)
                Offsets = OffsetsBak;


            OffsetsBak = Offsets.ToList();
            Offsets.Clear();


            //Search Text - (EXA)
            SrhPrx = new byte[] { 0x00, 0x4C };
            for (uint i = ByteCodeStart; i < Script.Length - 2; i++)
                if (EqualsAt(SrhPrx, i) && (Script[i + 2] != 0x00 || Script[i + 3] != 0x00)) {
                    i += 2;
                    if (Script[i] <= 0x3 || Script[i + 1] <= 0x3 || Script[i + 2] <= 0x3)
                        continue;
                    Offsets.Add(i);
                }

            Valid = (from x in OffsetsBak where ValidString(x) select x).Count() < (from x in Offsets where ValidString(x) select x).Count();
            if (!Valid)
                Offsets = OffsetsBak;


            OffsetsBak = Offsets.ToList();
            Offsets.Clear();

            //Search Text - (EXS)
            SrhPrx = new byte[] { 0x00, 0x4E };
            for (uint i = ByteCodeStart; i < Script.Length - 2; i++)
                if (EqualsAt(SrhPrx, i) && (Script[i + 2] != 0x00 || Script[i + 3] != 0x00)) {
                    i += 2;
                    if (Script[i] <= 0x3 || Script[i + 1] <= 0x3 || Script[i + 2] <= 0x3)
                        continue;
                    Offsets.Add(i);
                }


            Valid = (from x in OffsetsBak where ValidString(x) select x).Count() < (from x in Offsets where ValidString(x) select x).Count();
            if (!Valid)
                Offsets = OffsetsBak;


            OffsetsBak = Offsets.ToList();
            Offsets.Clear();

            //Search Text - (TPR)
            SrhPrx = new byte[] { 0x00, 0x00, 0x03 };
            for (uint i = ByteCodeStart; i < Script.Length - 2; i++)
                if (EqualsAt(SrhPrx, i) && (Script[i + 5] != 0x00 || Script[i + 6] != 0x00)) {
                    i += 5;
                    if (Script[i] <= 0x3 || Script[i + 1] <= 0x3 || Script[i + 2] <= 0x3)
                        continue;
                    Offsets.Add(i);
                }


            Valid = (from x in OffsetsBak where ValidString(x) select x).Count() < 
                (from x in Offsets
                 where ValidString(x) select x).Count();
            if (!Valid)
                Offsets = OffsetsBak;

            //Search Choice Strings
            SrhPrx = new byte[] { 0x08, 0x01, 0x00, 0x53 };//Bytecode 0x080100
            for (uint i = ByteCodeStart; i < Script.Length; i++)
                if (EqualsAt(SrhPrx, i)) {
                    uint Pos = (i + (uint)SrhPrx.Length) - 1;//0x53
                    while (Script[Pos] == 0x53) {

                        while (Script[Pos] != 0x00)//skip label name
                            Pos++;

                        Pos++;//Skip 0x00

                        if (Script[Pos++] != 0x62) //check choice text prefix
                            break;

                        Offsets.Add(Pos);

                        while (Script[Pos] != 0x00)//Skip choice text
                            Pos++;

                        Pos++;//Skip 0x00
                    }
                }
            uint[] offs = (from x in Offsets where ValidString(x) select x).ToArray();
            Array.Sort(offs);
            Offsets = new List<uint>(offs);
        }

        private bool EqualsAt(byte[] DataToCompare, uint At) {
            if (DataToCompare.Length + At >= Script.LongLength)
                return false;
            for (int i = 0; i < DataToCompare.Length; i++)
                if (DataToCompare[i] != Script[i + At])
                    return false;
            return true;
        }

        private uint GetUintAt(uint At) {
            byte[] Arr = new byte[4];
            for (int i = 0; i < Arr.Length; i++)
                Arr[i] = Script[i + At];
            return BitConverter.ToUInt32(Arr, 0);
        }



        //Check if the string contains bytes out of bounds from sjis encoding
        private bool ValidString(uint At) {
            bool Result = true;
            //return Result; //Enable the filter commenting this command
            byte Last = 0;
            int Len = 0;
            for (uint i = At; i < Script.Length; i++) {
                byte b = Script[i];
                if (b == 0)
                    break;
                byte Real = (byte)((b + Obfuscation) & 0xFF);
                if (!(Last >= 0x81 && Last <= 0xEE)) {
                    if (!(Real >= 0x81 || Real <= 0xEE))
                        if ((Real < 0x20 || Real > 0x7F) && Real != '\n') {
                            Result = false;
                            break;
                        }
                }
                Len++;
                Last = Real;
            }

            Result &= Len >= 2;

            byte[] Arr = (from x in Script.Skip((int)At).Take(Len) select (byte)(x + Obfuscation)).ToArray();
            string Str = Encoding.GetString(Arr);
            if (Result) {
                List<Range> AllowedRangesA = new List<Range>() {
                    Range.Katakana,
                    Range.Hiragana,
                    Range.KatakanaPhoneticExtensions,
                    Range.CJKUnifiedIdeographs,
                    Range.GeneralPunctuation,
                    Range.CJKSymbolsAndPunctuation,
                    Range.HalfwidthAndFullwidthForms
                };
                List<Range> AllowedRangesB = new List<Range>() {
                    Range.BasicLatin,
                    Range.LatinExtendedA
                };
                int Missmatch = 0;
                foreach (char c in Str) {
                    if (UnicodeRanges.IsUnprintable(c)) {
                        Result = false;
                        break;
                    }
                    if (!AllowedRangesA.Contains(UnicodeRanges.GetRange(c))) {
                        Missmatch++;
                    }
                }

                if (Missmatch >= Str.Length / 3)
                    Result = false;

                if (!Result) {
                    Result = true;
                    Missmatch = 0;

                    foreach (char c in Str) {
                        if (UnicodeRanges.IsUnprintable(c)) {
                            Result = false;
                            break;
                        }
                        if (!AllowedRangesB.Contains(UnicodeRanges.GetRange(c))) {
                            Missmatch++;
                            break;
                        }
                    }

                    if (Missmatch >= Str.Length / 3)
                        Result = false;
                }
            }

            return Result;
        }
    }
}
