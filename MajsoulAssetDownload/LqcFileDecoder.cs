using System;
using System.IO;
using Lq.Config;
using System.Linq;
using System.Collections.Generic;
using Google.Protobuf;

namespace MajsoulAssetDownload
{
    public class LqcFileDecoder
    {
        /*
        Decodes lqc.lqbin, which is a protobuf file storing most of the game's text and other files
        in the form of csv files. The csv data inside the file is itself also stored as protobuf data,
        with the schemas for it also being stored in the file. The proto file that the game uses has been
        converted to a C# script using Google's compiler tool (see Config.cs). This should maybe be changed
        so it can use the proto file directly, either by compiling it during runtime to a C# file, or
        some other way.
        */
        public static void DecodeConfigTablesFile()
        {
            //Load the protobuf file, which contains most of the game's text
            FileStream input = File.OpenRead("Assets/v" + Program.version + "/res/config/lqc.lqbin");

            if (!Directory.Exists("csvfiles")) Directory.CreateDirectory("csvfiles");

            //Use the compiled C# script to parse the data from the file
            ConfigTables configTables = ConfigTables.Parser.ParseFrom(input);

            SheetData[] sheetDataArray = configTables.Datas.ToArray();
            TableSchema[] tableSchemas = configTables.Schemas.ToArray();
            List<SheetSchema> sheetSchemasList = new List<SheetSchema>();

            for (int i = 0; i < tableSchemas.Length; i++)
            {
                TableSchema tableSchema = tableSchemas[i];

                //Console.WriteLine(tableSchema.Name);

                for (int j = 0; j < tableSchema.Sheets.Count; j++)
                {
                    SheetSchema sheetSchema = tableSchema.Sheets[j];
                    sheetSchemasList.Add(sheetSchema);
                }
            }

            for (int i = 0; i < sheetDataArray.Length; i++)
            {
                ConvertSheetDataToCSV(sheetDataArray[i], sheetSchemasList[i]);
            }

        }

        public static void ConvertSheetDataToCSV(SheetData sheetData, SheetSchema sheetSchema)
        {
            List<string> fileLines = new List<string>();
            string table = sheetData.Table;
            string sheet = sheetData.Sheet;
            ByteString[] byteStrings = sheetData.Data.ToArray();
            Field[] fields = sheetSchema.Fields.ToArray();
            int fieldsAmount = fields.Length;
            List<byte[]> sheetRowsData = new List<byte[]>();

            foreach (ByteString byteString in byteStrings)
            {
                sheetRowsData.Add(byteString.ToByteArray());
            }

            int numberOfEntries = sheetRowsData.Count;

            string csvFieldsString = "";

            for (int i = 0; i < fieldsAmount; i++)
            {
                csvFieldsString += fields[i].FieldName + (i < fieldsAmount - 1 ? ", " : "");
            }

            //Add the top category csv row
            fileLines.Add(csvFieldsString);

            //Decrypt each row
            for (int i = 0; i < numberOfEntries; i++)
            {
                byte[] rowData = sheetRowsData[i];
                int index = 0;


                //Console.WriteLine("Bytes: " + Convert.ToHexString(rowData));

                string rowString = "";

                for (int j = 0; j < fieldsAmount; j++)
                {
                    Field field = fields[j];

                    string name = field.FieldName;
                    string type = field.PbType;
                    uint pbIndex = field.PbIndex;
                    uint arrayLength = field.ArrayLength;
                    uint typeVal = 0;

                    //Console.WriteLine("name: {0}, type: {1}, array length: {2}, current offset: {3}", name, type, arrayLength, index);

                    //If the array length is 0, there's only a single element
                    if (arrayLength == 0){
                        typeVal = ReadUInt32(rowData, ref index);
                        string s = ReadAndConvertVariableToString(rowData, ref index, type);
                        rowString += s;
                    }else{
                        //If it's an array, but not a string array, there is an extra byte storing the length of the array
                        if (type != "string")
                        {
                            typeVal = ReadUInt32(rowData, ref index);
                            index++;
                        }

                        rowString += "[";

                        for (int k = 0; k < arrayLength; k++){
                            //For some reason, string arrays are stored as strings one after another with the type bytes, so skip it each time 
                            if (type == "string")
                            {
                                typeVal = ReadUInt32(rowData, ref index);
                            }
                            string s = ReadAndConvertVariableToString(rowData, ref index, type);
                            rowString += s + (k < arrayLength - 1 ? ", " : "");
                        }

                        rowString += "]";
                    }

                    if (j < fieldsAmount - 1) rowString += ", ";
                }

                //Add the line to the list
                fileLines.Add(rowString);
            }


            File.WriteAllLines("csvfiles/" + sheet + ".csv", fileLines);
        }

        static string ReadAndConvertVariableToString(byte[] rowData, ref int index, string type)
        {
            string result = "";

            switch (type)
            {
                case "uint32":
                case "int32":
                        uint val = ReadUInt32(rowData, ref index);
                        result = val.ToString();
                    break;
                case "string":
                        string s = ReadString(rowData, ref index);
                        result = "\"" + s + "\"";
                    break;
                case "float":
                    float f = ReadFloat(rowData, ref index);
                    result = f.ToString();
                    break;
                default:
                    throw new NotImplementedException();
            }

            return result;
        }

        //Reads a uint32 value in the protobuf format
        static uint ReadUInt32(byte[] bytes, ref int index)
        {
            int num = 0;
            bool lastGroup = false;
            int offset = 0;

            while (lastGroup == false)
            {
                lastGroup = true;

                //If the highest bit is 1, there are more bytes to come
                if((bytes[index] & 0x80) == 0x80)
                {
                    lastGroup = false;

                    //Toggle the highest bit
                    bytes[index] ^= 0x80;

                    num += bytes[index] << offset;
                    offset += 7;
                }
                else
                {
                    //Otherwise, this is the last group
                    num += bytes[index] << offset;
                }

                index++;

            }

            return (uint)num;
        }

        static string ReadString(byte[] bytes, ref int index)
        {
            int length = (int)ReadUInt32(bytes,ref index);
            string s = System.Text.Encoding.Default.GetString(bytes.Skip(index).Take(length).ToArray());
            index += length;
            return s;
        }

        static float ReadFloat(byte[] bytes, ref int index)
        {
            uint intVal = BitConverter.ToUInt32(bytes.Skip(index).ToArray(),0);
            float floatVal = BitConverter.UInt32BitsToSingle(intVal);
            index += 4;
            return floatVal;
        }

    }
}
