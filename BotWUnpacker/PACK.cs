﻿using System;
using System.Linq;
using System.IO;
//Modified code From Uwizard SARC master branch as of 10/16/2017, 


namespace BotWUnpacker
{
    public struct PACK
    {
        public static string lerror = ""; // Gets the last error

        #region Structures
        private struct SarcNode //SARC node row (extract only)
        {
            public uint hash;
            public byte unknown;
            public uint offset, start, end;

            public SarcNode(uint fhash, byte unk, uint foffset, uint fstart, uint fend)
            {
                hash = fhash; //NOT USED for extracting
                unknown = unk; //NOT USED for extracting
                offset = foffset; //NOT USED for extracting
                start = fstart;
                end = fend;
            }
        }

        private struct NodeHash //Build only
        {
            public uint hash;
            public int index;

            public NodeHash(uint h, int i)
            {
                hash = h;
                index = i;
            }
        }

        private struct NodeInfo //Build only
        {
            public string filename, realname;
            public uint namesize; 

            public NodeInfo(string inFileName, string inRealName, uint inNameSize)
            {
                filename = inFileName;
                realname = inRealName;
                namesize = inNameSize;
            }

        }

        private struct NodeData //Build only
        {
            public byte[] data;
            public uint startPos, endPos;

            public NodeData(byte[] inData, uint inStartPos, uint inEndPos)
            {
                data = inData;
                startPos = inStartPos;
                endPos = inEndPos;
            }
        }
        #endregion

        #region Conversions
        private static ushort Makeu16(byte b1, byte b2) //16-bit change (ushort, 0xFFFF)
        {
            return (ushort)(((ushort)b1 << 8) | (ushort)b2);
        }

        private static uint Makeu32(byte b1, byte b2, byte b3, byte b4) //32-bit change (uint, 0xFFFFFFFF)
        {
            return ((uint)b1 << 24) | ((uint)b2 << 16) | ((uint)b3 << 8) | (uint)b4;
        }

        private static byte[] Breaku16(ushort u16) //Byte change from 16-bits (byte, 0xFF, 0xFF)
        {
            return new byte[] { (byte)(u16 >> 8), (byte)(u16 & 0xFF) };
        }

        private static byte[] Breaku32(uint u32) //Byte change from 32-bits (byte, 0xFF, 0xFF, 0xFF, 0xFF)
        {
            return new byte[] { (byte)(u32 >> 24), (byte)((u32 >> 16) & 0xFF), (byte)((u32 >> 8) & 0xFF), (byte)(u32 & 0xFF) };
        }

        static private string IntToHex(int num) 
        {
            return num.ToString("X");
        }

        static private int HexToInt(String hex)
        {
            return int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        }
        #endregion

        #region Constructors
        private static void MakeDirExist(string dir)
        {
            string dirPath = System.IO.Path.GetFullPath(dir);
            int numDirs = 0;
            for (int i = 0; i < dirPath.Length; i++)
                if (dirPath[i] == '\\') numDirs++;

            for (int j = numDirs; j >= 0; j--)
            {
                string tmp = dirPath;
                for (int k = 0; k < j; k++)
                    tmp = System.IO.Path.GetDirectoryName(tmp);
                if (!System.IO.Directory.Exists(tmp))
                    System.IO.Directory.CreateDirectory(tmp);
            }
        }

        private static uint CalcHash(string name)
        {
            ulong result = 0;
            for (int i = 0; i < name.Length; i++)
            {
                result = (((byte)name[i]) + (result * 0x65)) & 0xFFFFFFFF;
            }
            return (uint)(result & 0xFFFFFFFF);
        }

        private static string[] GetFiles(string dir)
        {
            if (dir == "") dir = System.Environment.CurrentDirectory;
            return System.IO.Directory.GetFiles(dir);
        }

        private static byte[] AddPadding(byte[] dataBuild, uint padding, NodeData nodeData, NodeInfo nodeInfo) //Add padding to adjust to Nintendo's logic
        {
            byte[] pad = { 0x00 };
            if (nodeData.data[nodeData.data.Length - 1] != 0x00) //if node is NOT pre padded
            {
                if ((dataBuild.Length % padding) != 0) //if build needs padding
                {
                    uint toAdd = padding % ((uint)dataBuild.Length % padding); //count of data to pad
                    for (int i = 0; i < toAdd; i++)
                    {
                        dataBuild = dataBuild.Concat(pad).ToArray(); //padding data
                    }
                    return dataBuild;
                }
                else
                {
                    return dataBuild; //build doesn't need padding
                }
            }
            else
            {
                if (nodeData.data[nodeData.data.Length - 2] != 0x00) //if node only 1 byte pre-padding
                    return dataBuild.Concat(pad).ToArray(); // add 1 pad to it... because Nintendo wants two bytes of padding
                else
                    return dataBuild; //build doesn't need padding
            }
        }
        #endregion

        #region Extract
        public static bool Extract(string inFile, string outDir) //EXTRACT ----------------------------------------------------------------------
        {
            try
            {
                return Extract(System.IO.File.ReadAllBytes(inFile), outDir, false, false, inFile); //default
            }
            catch (Exception e) //usually because file is in use
            {
                lerror = e.Message;
                return false;
            }
        }

        public static bool Extract(string inFile, string outDir, bool autoDecode, bool replaceFile) 
        {
            try
            {
                return Extract(System.IO.File.ReadAllBytes(inFile), outDir, autoDecode, replaceFile, inFile); 
            }
            catch (Exception e) //usually because file is in use
            {
                lerror = e.Message;
                return false;
            }
        }

        public static bool Extract(byte[] inFile, string outDir, bool autoDecode, bool replaceFile, string inFileName)
        {

            //SARC header 0x00 - 0x13
            if (inFile[0] != 'S' || inFile[1] != 'A' || inFile[2] != 'R' || inFile[3] != 'C')
            {
                if (inFile[0] == 'Y' && inFile[1] == 'a' && inFile[2] == 'z' && inFile[3] == '0')
                {
                    if (autoDecode)
                    {
                        string outFile;
                        if (replaceFile)
                        {
                            //replace the file decoded and recursively run the extract
                            outFile = inFileName;
                            Yaz0.Decode(inFileName, outFile);
                        }
                        else
                        {
                            //create the decoded file and recursively run the extract
                            outFile = Path.GetDirectoryName(inFileName) + "\\" + Path.GetFileNameWithoutExtension(inFileName) + "Decoded" + Path.GetExtension(inFileName);
                            Yaz0.Decode(inFileName, outFile);
                        }
                        return Extract(outFile, outDir, autoDecode, replaceFile); //recursively run the code again
                    }
                    else
                    {
                        lerror = "Yaz0 file encoded (you don't have Auto Yaz0 Decode on!)";
                        return false;
                    }
                }
                else
                {
                    lerror = "Not a SARC archive! Missing SARC header at 0x00" + "\n" + "( Your file header is: " + ((char)inFile[0]) + ((char)inFile[1]) + ((char)inFile[2]) + ((char)inFile[3]) + " )";
                    return false;
                }
            }
            int pos = 4; //0x04
            ushort hdr = Makeu16(inFile[pos], inFile[pos + 1]); //SARC Header length
            pos += 2; //0x06
            ushort bom = Makeu16(inFile[pos], inFile[pos + 1]); //Byte Order Mark
            if (bom != 65279) //Check 0x06 for Byte Order Mark (if not 0xFEFF)
            {
                if (bom == 65518) lerror = "Unable to support Little Endian! (Byte Order Mark 0x06)";
                else lerror = "Unknown SARC header (Byte Order Mark 0x06)";
                return false;
            }
            pos += 2; //0x08
            uint fileSize = Makeu32(inFile[pos], inFile[pos + 1], inFile[pos + 2], inFile[pos + 3]); 
            pos += 4; //0x0C
            uint dataOffset = Makeu32(inFile[pos], inFile[pos + 1], inFile[pos + 2], inFile[pos + 3]); //Data offset start position
            pos += 4; //0x10
            uint unknown = Makeu32(inFile[pos], inFile[pos + 1], inFile[pos + 2], inFile[pos + 3]); //unknown, always 0x01?
            pos += 4; //0x14

            //SFAT header 0x14 - 0x1F
            if (inFile[pos] != 'S' || inFile[pos + 1] != 'F' || inFile[pos + 2] != 'A' || inFile[pos + 3] != 'T')
            {
                lerror = "Unknown file table! (Missing SFAT header at 0x14)";
                return false;
            }
            pos += 4; //0x18
            ushort hdr2 = Makeu16(inFile[pos], inFile[pos + 1]); //SFAT Header length
            pos += 2; //0x1A
            ushort nodeCount = Makeu16(inFile[pos], inFile[pos + 1]); //Node Cluster count
            pos += 2; //0x1C
            uint hashr = Makeu32(inFile[pos], inFile[pos + 1], inFile[pos + 2], inFile[pos + 3]); //Hash multiplier, always 0x65
            pos += 4; //0x20

            SarcNode[] nodes = new SarcNode[nodeCount];
            SarcNode tmpnode = new SarcNode();

            for (int i = 0; i < nodeCount; i++) //Node cluster 
            {
                tmpnode.hash = Makeu32(inFile[pos], inFile[pos + 1], inFile[pos + 2], inFile[pos + 3]);
                pos += 4; //0x?4
                tmpnode.unknown = inFile[pos]; //unknown, always 0x01? (not used in this case)
                pos += 1; //0x?5
                tmpnode.offset = Makeu32(0, inFile[pos], inFile[pos + 1], inFile[pos + 2]); //Node SFNT filename offset divided by 4 (not used)
                pos += 3; //0x?8
                tmpnode.start = Makeu32(inFile[pos], inFile[pos + 1], inFile[pos + 2], inFile[pos + 3]); //Start Data offset position
                pos += 4; //0x?C
                tmpnode.end = Makeu32(inFile[pos], inFile[pos + 1], inFile[pos + 2], inFile[pos + 3]); //End Data offset position
                pos += 4; //0x?0
                nodes[i] = tmpnode; //Store node data into array
            }

            if (inFile[pos] != 'S' || inFile[pos + 1] != 'F' || inFile[pos + 2] != 'N' || inFile[pos + 3] != 'T')
            {
                string posOffset = "0x" + pos.ToString("X");
                lerror = "Unknown file name table! (Missing SFNT header at " + posOffset +")";
                return false;
            }
            pos += 4; //0x?4
            ushort hdr3 = Makeu16(inFile[pos], inFile[pos + 1]); //SFNT Header length, always 0x08
            pos += 2; //0x?6
            ushort unk2 = Makeu16(inFile[pos], inFile[pos + 1]); //unknown, always 0x00?
            pos += 2; //0x?8

            string[] fileNames = new string[nodeCount];
            string tempName;

            for (int i = 0; i < nodeCount; i++) //Get file names for each node
            {
                tempName = ""; //reset for each file
                while (inFile[pos] != 0)
                {
                    tempName = tempName + ((char)inFile[pos]).ToString(); //Build temp string for each letter
                    pos += 1;
                }
                while (inFile[pos] == 0) //ignore every 0 byte, because why bother calculating the SFNT header offset anyway?
                    pos += 1;
                fileNames[i] = tempName; //Take built string and store it in the array
            }

            if (!System.IO.Directory.Exists(outDir)) System.IO.Directory.CreateDirectory(outDir); //folder creation
            System.IO.StreamWriter stream;
            byte[] nodeData;

            for (int i = 0; i < nodeCount; i++) //Write files based from node information
            {
                MakeDirExist(System.IO.Path.GetDirectoryName(outDir + "/" + fileNames[i]));
                if (autoDecode)
                {
                    nodeData = new byte[nodes[i].end - nodes[i].start];
                    Array.Copy(inFile, nodes[i].start + dataOffset, nodeData, 0, nodes[i].end - nodes[i].start);
                    if (replaceFile)
                    {
                        if (!(Yaz0.Decode(nodeData, outDir + "/" + fileNames[i]))) //attempt decode, but if it fails, write it anyway.
                        {
                            stream = new System.IO.StreamWriter(outDir + "/" + fileNames[i]);
                            stream.BaseStream.Write(inFile, (int)(nodes[i].start + dataOffset), (int)(nodes[i].end - nodes[i].start)); //Write 
                            stream.Close();
                            stream.Dispose();
                        }
                    }
                    else
                    {
                        Yaz0.Decode(nodeData, outDir + "/" + fileNames[i].Split('.')[0] + "Decoded." + fileNames[i].Split('.')[1]);
                        stream = new System.IO.StreamWriter(outDir + "/" + fileNames[i]);
                        stream.BaseStream.Write(inFile, (int)(nodes[i].start + dataOffset), (int)(nodes[i].end - nodes[i].start)); //Write 
                        stream.Close();
                        stream.Dispose();
                    }
                }
                else
                {
                    stream = new System.IO.StreamWriter(outDir + "/" + fileNames[i]);
                    stream.BaseStream.Write(inFile, (int)(nodes[i].start + dataOffset), (int)(nodes[i].end - nodes[i].start)); //Write 
                    stream.Close();
                    stream.Dispose();
                }
            }
            GC.Collect();
            return true;
        } //--------------------------------------------------------------------------------------------------------------------------------------------
        #endregion

        #region Build
        public static bool Build(string inDir, string outFile)
        {
            return Build(inDir, outFile, 0); //if no fixed data offset is set.
        }

        public static bool Build(string inDir, string outFile, uint dataFixedOffset) //BUILD ----------------------------------------------------------------------
        {
            try
            {
                //Setup
                string[] inDirFiles = System.IO.Directory.GetFiles(inDir == "" ? System.Environment.CurrentDirectory : inDir, "*.*", System.IO.SearchOption.AllDirectories);
                NodeInfo[] nodeInfo = new NodeInfo[inDirFiles.Length];

                //Node storage logic
                uint totalNamesLength = 0;
                uint numFiles = (uint)inDirFiles.Length;
                for (int i = 0; i < numFiles; i++) //collect node information
                {
                    string realname = inDirFiles[i];
                    string fileName = inDirFiles[i].Replace(inDir + System.IO.Path.DirectorySeparatorChar.ToString(), "");
                    fileName = fileName.Replace("\\","/"); //HURP DERP NINTENDUR
                    uint namesize = (uint)fileName.Length;
                    namesize += (4 - (namesize % 4));
                    totalNamesLength += namesize;
                    nodeInfo[i] = new NodeInfo(fileName, realname, namesize);
                }
                uint namePaddingToAdd = 0;
                if (totalNamesLength % 0x10 != 0)
                {
                    namePaddingToAdd = 0x10 % (totalNamesLength % 0x10); //padding to add for 
                    totalNamesLength += namePaddingToAdd;
                }

                //Node hash calculation and sorting logic
                NodeHash[] hashesUnsorted = new NodeHash[numFiles];
                for (int i = 0; i < numFiles; i++)
                {
                    hashesUnsorted[i] = new NodeHash(CalcHash(nodeInfo[i].filename), i); //Store and calculate unsorted hashes
                }
                uint lastHash;
                bool[] hashSortedFlag = new bool[hashesUnsorted.Length];
                NodeHash[] hashes = new NodeHash[hashesUnsorted.Length];
                int dhi = 0;
                for (int i = 0; i < hashes.Length; i++) //sort nodes by hash calculation
                {
                    lastHash = uint.MaxValue;
                    for (int j = 0; j < hashesUnsorted.Length; j++)
                    {
                        if (hashSortedFlag[j]) continue;
                        if (hashesUnsorted[j].hash < lastHash)
                        {
                            dhi = j;
                            lastHash = hashesUnsorted[j].hash;
                        }
                    }
                    hashSortedFlag[dhi] = true;
                    hashes[i] = hashesUnsorted[dhi];
                }

                //Node data build and position logic
                NodeData[] nodeData = new NodeData[numFiles];
                nodeData[hashes[0].index].data = System.IO.File.ReadAllBytes(nodeInfo[hashes[0].index].realname);
                nodeData[hashes[0].index].startPos = 0; //first starting positon
                nodeData[hashes[0].index].endPos = (uint)nodeData[hashes[0].index].data.Length; //first end position before padding
                byte[] nodeBuild = AddPadding(nodeData[hashes[0].index].data, 0x10, nodeData[hashes[0].index], nodeInfo[hashes[0].index]); //Prep first node for building
                for (int i = 1; i < numFiles; i++)
                {
                    nodeData[hashes[i].index].startPos = (uint)nodeBuild.Length; //start position after padding
                    nodeData[hashes[i].index].data = System.IO.File.ReadAllBytes(nodeInfo[hashes[i].index].realname);
                    nodeBuild = nodeBuild.Concat(nodeData[hashes[i].index].data).ToArray(); //Concatenate next unpadded node
                    nodeData[hashes[i].index].endPos = (uint)nodeBuild.Length; //end position before padding
                    if (i != numFiles - 1) //As long as it's not the last node, add padding
                    {
                        nodeBuild = AddPadding(nodeBuild, 0x10, nodeData[hashes[i].index], nodeInfo[hashes[i].index]);
                    }
                    GC.Collect();
                }
                

                uint fileSize = 0;
                fileSize += 0x20; //SARC + SFAT reserve
                fileSize += 0x10 * (uint)numFiles; //nodeInfo reserve 
                fileSize += 0x08; //SFNT reserve
                fileSize += totalNamesLength; //names of files 
                uint nodeDataStart = fileSize; //node data table offset
                if (dataFixedOffset > nodeDataStart) //if fixed data offset if larger than generated start...
                {
                    namePaddingToAdd += (dataFixedOffset - nodeDataStart);
                    fileSize += (dataFixedOffset - nodeDataStart);
                    nodeDataStart = dataFixedOffset;
                }
                fileSize += (uint)nodeBuild.Length; //finish calculating expected filesize 


                //Write logic
                System.IO.StreamWriter stream = new System.IO.StreamWriter(outFile);
                //SARC ---
                stream.BaseStream.Write(new byte[] { 83, 65, 82, 67, 0x00, 0x14, 0xFE, 0xFF }, 0, 8); //Write Fixed SARC Big Endian header
                stream.BaseStream.Write(Breaku32(fileSize), 0, 4); //Write 0x08 split bytes of file size
                stream.BaseStream.Write(Breaku32(nodeDataStart), 0, 4); //Write 0x0C split bytes of data table start offset
                //SFAT ---
                stream.BaseStream.Write(new byte[] { 0x01, 0x00, 0x00, 0x00, 83, 70, 65, 84, 0x00, 0x0C }, 0, 10); //Write Fixed SFAT header
                stream.BaseStream.Write(Breaku16((ushort)numFiles), 0, 2); //Write 0x1A split bytes of number of nodes/files
                stream.BaseStream.Write(Breaku32(0x65), 0, 4); //Write Fixed Hash Multiplier 
                uint strpos = 0;
                //Node ---
                for (int i = 0; i < numFiles; i++)
                {
                    stream.BaseStream.Write(Breaku32(hashes[i].hash), 0, 4); //Node Hash 
                    stream.BaseStream.WriteByte(0x01); //Node Fixed Unknown
                    stream.BaseStream.Write(Breaku32((strpos >> 2)), 1, 3); //Node filename offset position (divided by 4)
                    strpos += nodeInfo[hashes[i].index].namesize;
                    stream.BaseStream.Write(Breaku32(nodeData[hashes[i].index].startPos), 0, 4); //Node start data offset position
                    stream.BaseStream.Write(Breaku32(nodeData[hashes[i].index].endPos), 0, 4); //Node end data offset position
                }
                GC.Collect();
                //SFNT ---
                stream.BaseStream.Write(new byte[] { 83, 70, 78, 84, 0x00, 0x08, 0x00, 0x00 }, 0, 8); //Write fixed SFNT header
                for (int i = 0; i < numFiles; i++)
                {
                    string fileName = nodeInfo[hashes[i].index].filename;
                    for (int j = 0; j < fileName.Length; j++)
                    {
                        stream.BaseStream.WriteByte((byte)fileName[j]); //Write file names
                    }
                    int namePadding = (int)nodeInfo[hashes[i].index].namesize - nodeInfo[hashes[i].index].filename.Length; //short padding for file offset location (to be divisible by 4)
                    for (int j = 0; j < namePadding; j++)
                        stream.BaseStream.WriteByte(0); 
                }

                for (int i = 0; i < namePaddingToAdd; i++)
                {
                    stream.BaseStream.WriteByte(0); //pad end of names
                }
                //Data ---
                stream.BaseStream.Write(nodeBuild, 0, nodeBuild.Length); //Write node data

                stream.Close();
                stream.Dispose();
                GC.Collect();
            }
            catch (System.Exception e)
            {
                lerror = "An error occurred: " + e.Message;
                return false;
            }
            
            return true;
        } //--------------------------------------------------------------------------------------------------------------------------------------------
        #endregion
    }
}