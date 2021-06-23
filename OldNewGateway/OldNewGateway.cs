﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Runtime.InteropServices;
using System.Globalization;

namespace OldNewGateway
{
    public partial class OldNewGateway : ServiceBase
    {
        // CONSTANTS :
        static string version = "001";
        static int maxStationNumber = 100;
        static int maxErrorCount = 10;


        struct Station
        {
            public string name;
            public string ip;
            public string originPath;
            public string destinationPath;
            public string lastActivityDate;

            public Station(string name, string ip, string originPath, string destinationPath, string lastActivityDate)
            {
                this.name = name;
                this.ip = ip;
                this.originPath = originPath;
                this.destinationPath = destinationPath; 
                this.lastActivityDate = lastActivityDate;
            }
        }

        public enum SearchType
        {
            FirstOcurrence = 0,
            LastOcurrence = 1
        }

        private int errorCount;
        private int eventID;
        private string nextStep; 

     
      
        System.IO.StreamReader originFile;
        System.IO.StreamWriter destinationFile;
        string destinationString;

        System.IO.StreamReader modelFile;
        string modelString;
      
        Station[] stations = new Station[maxStationNumber];
        Timer myTimer = new Timer();

        public OldNewGateway()
        {
            InitializeComponent();

            myEventLog = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("OldNewGatewaySource"))
            {
                System.Diagnostics.EventLog.CreateEventSource("OldNewGatewaySource", "OldNewGatewayLog");
            }
            myEventLog.Source = "OldNewGatewaySource";
            myEventLog.Log = "OldNewGatewayLog";

            // set up a timer
            myTimer.Interval = 1000; // 1 second
            myTimer.Elapsed += new ElapsedEventHandler(this.OnTimer);
        }

        protected override void OnStart(string[] args)
        {      
            getStationInfo(); 
            myEventLog.WriteEntry($"Started - version {version}");

            // READ MODEL FILE - only once - improvement
            modelFile = System.IO.File.OpenText("C:\\OldNewGateway\\file models\\model.txt");
            modelString = modelFile.ReadToEnd(); // read as string

            errorCount = 0;
            myTimer.Start();
        }

        protected override void OnContinue()
        {
            errorCount = 0;
            eventID = 0;
            getStationInfo();
            myEventLog.WriteEntry($"Started again - version {version}");
        }

        protected override void OnStop()
        {
            myEventLog.WriteEntry($"Stopped - version {version}");
            myTimer.Stop();  
        }

        private void OnTimer (object sender, ElapsedEventArgs args)
        {
            // myEventLog.WriteEntry("monitoring the system", EventLogEntryType.Information, eventID++);
            if (errorCount > maxErrorCount)
            {
                myEventLog.WriteEntry("Too many errors, execution stopped. Please check errors and re-start the service", EventLogEntryType.Error);
                myTimer.Stop();         
            }
            else
            {
                readAndWriteStationData();
            }     
        }

        private void getStationInfo()
        {
            try
            {
                System.IO.StreamReader configFile;
                string configString;

                string[] lines;
                string[] fields;

                configFile = System.IO.File.OpenText("C:\\OldNewGateway\\config\\stations.csv");
                configString = configFile.ReadToEnd();
                lines = configString.Split(new char[] { '\x0D', '\x0A' }, StringSplitOptions.RemoveEmptyEntries);

                int i = -1;
                foreach (string line in lines)
                {
                    if (i == -1) i = 0;
                    else // skip the first line
                    {
                        fields = line.Split(';');
                        stations[i].name = fields[0];
                        stations[i].ip = fields[1];
                        stations[i].originPath = fields[2];
                        stations[i].destinationPath = fields[3];
                        i++;
                    }
                }
                configFile.Close();
            }
            catch (Exception theException) 
            {
                myEventLog.WriteEntry($"<getStationInfo> Error: {theException.Message} Source: {theException.Source}", EventLogEntryType.Error);
            }
        }

        private void readAndWriteStationData()
        {
            try
            {
                int i = 0;
                while (!String.IsNullOrEmpty(stations[i].name))
                {
                    IEnumerable<string> filePaths = System.IO.Directory.EnumerateFiles(stations[i].originPath, "*.json", System.IO.SearchOption.AllDirectories);
                    foreach (string originFilePath in filePaths) //all .json files in that folder and subfolders!
                    {
                        int index;
                        string result, prg, cycle, date, id, qc, row, column, step, Tmin, T, Tmax, Amin, A, Amax;
                        string originString; 
                        string destinationFilePath;

                        // READ ORIGIN FILE
                        originFile = System.IO.File.OpenText(originFilePath);
                               
                        originString = originFile.ReadToEnd();

                        nextStep = "trying to get result";
                        result = getData(originString, "result", 0, SearchType.FirstOcurrence);

                        nextStep = "trying to get prg nr";
                        prg = getData(originString, "prg nr", 0, SearchType.FirstOcurrence);
                        prg = expandAndShift(prg, 2);

                        nextStep = "trying to get cycle";
                        cycle = getData(originString, "cycle", 0, SearchType.FirstOcurrence);
                        cycle = expandAndShift(cycle, 7);

                        nextStep = "trying to get date";
                        date = getData(originString, "date", 0, SearchType.FirstOcurrence);
                        date = date.Insert(11, "H ");

                        nextStep = "trying to get id code";
                        id = getData(originString, "id code", 0, SearchType.FirstOcurrence);
                        id = id + "_xxx";

                        nextStep = "trying to get quality code";
                        qc = getData(originString, "quality code", 0, SearchType.FirstOcurrence);
                        qc = expandAndShift(qc, 3);

                        // ... last result
                        nextStep = "trying to get row";
                        row = getData(originString, "row", 0, SearchType.LastOcurrence);
                        row = expandAndShift(row, 2);

                        nextStep = "trying to get column";
                        column = getData(originString, "column", 0, SearchType.LastOcurrence);

                        step = row.Insert(row.Length, column);

                        nextStep = "trying to get torque";
                        T = getData(originString, "torque", 0, SearchType.LastOcurrence);
                        T = cutAndShift(T, 5);

                        nextStep = "trying to get angle";
                        A = getData(originString, "angle", 0, SearchType.LastOcurrence);
                        A = cutAndShift(A, 8);

                        nextStep = "trying to get MF TorqueMin";
                        index = originString.LastIndexOf("MF TorqueMin");

                        if (index != -1)
                        {
                            Tmin = getData(originString, "nom", index, SearchType.FirstOcurrence);
                            Tmin = cutAndShift(Tmin, 5);
                        }
                        else Tmin = null;

                        nextStep = "trying to get MFs TorqueMax";
                        index = originString.LastIndexOf("MFs TorqueMax");

                        //myEventLog.WriteEntry($"MFs TorqueMax index = {index}", EventLogEntryType.Warning);

                        if (index != -1)
                        {
                            Tmax = getData(originString, "nom", index, SearchType.FirstOcurrence);
                            Tmax = cutAndShift(Tmax, 5);
                        }
                        else Tmax = null;

                        nextStep = "trying to get MFU AngleMin";
                        index = originString.LastIndexOf("MF AngleMin");
                        if (index != -1)
                        {
                            Amin = getData(originString, "nom", index, SearchType.FirstOcurrence);
                            Amin = cutAndShift(Amin, 8);
                        }
                        else Amin = null;

                        nextStep = "trying to get MFs AngleMax";
                        index = originString.LastIndexOf("MFs AngleMax");
                        if (index != -1)
                        {
                            Amax = getData(originString, "nom", index, SearchType.FirstOcurrence);
                            Amax = cutAndShift(Amax, 8);
                        }
                        else Amax = null;




                        // WRITE DESTINATION STRING

                        destinationString = modelString;

                        //ID code souce and ID code

                        nextStep = "trying to write id";
                        destinationString = destinationString.Insert(12 - 1, id);

                        index = destinationString.IndexOf('\x0A');

                        index = destinationString.IndexOf('\x0A', index + 1); // date, time    
                        nextStep = "trying to write date";
                        destinationString = destinationString.Insert(index + 3, date);

                        index = destinationString.IndexOf('\x0A', index + 1); // measured values with result

                        nextStep = "trying to write T";
                        if (T != null) destinationString = destinationString.Insert(index + 6, T);
                        else destinationString = destinationString.Insert(index + 6, "     ");

                        nextStep = "trying to write A";
                        if (A != null) destinationString = destinationString.Insert(index + 14, A);
                        else destinationString = destinationString.Insert(index + 14, "        ");

                        nextStep = "trying to write G";
                        destinationString = destinationString.Insert(index + 28, "     "); // G gradient

                        nextStep = "trying to write result";
                        destinationString = destinationString.Insert(index + 34, result);

                        index = destinationString.IndexOf('\x0A', index + 1); // redundancy values (optional)

                        nextStep = "trying to write MR";
                        destinationString = destinationString.Insert(index + 6, "     "); // MR: 5 spaces 

                        nextStep = "trying to write WR";
                        destinationString = destinationString.Insert(index + 14, "        "); // WR: 8 spaces

                        nextStep = "trying to write QR";
                        destinationString = destinationString.Insert(index + 26, " 0"); // QR: " 0"

                       
                        index = destinationString.IndexOf('\x0A', index + 1); // angle limits

                        nextStep = "trying to write Amin";
                        if (Amin != null) destinationString = destinationString.Insert(index + 3, Amin);
                        else destinationString = destinationString.Insert(index + 3, "        ");

                        nextStep = "trying to write Amax";
                        if (Amax != null) destinationString = destinationString.Insert(index + 14, Amax);
                        else destinationString = destinationString.Insert(index + 14, "        ");

                        index = destinationString.IndexOf('\x0A', index + 1); // torque limits

                        nextStep = "trying to write Tmin";
                        if (Tmin != null) destinationString = destinationString.Insert(index + 6, Tmin);
                        else destinationString = destinationString.Insert(index + 6, "     ");

                        nextStep = "trying to write Tmax";
                        if (Tmax != null) destinationString = destinationString.Insert(index + 17, Tmax);
                        else destinationString = destinationString.Insert(index + 17, "     ");

                        index = destinationString.IndexOf('\x0A', index + 1); // gradient limits

                        nextStep = "trying to write G-";
                        destinationString = destinationString.Insert(index + 5, "      ");

                        nextStep = "trying to write G+";
                        destinationString = destinationString.Insert(index + 17, "     ");

                        index = destinationString.IndexOf('\x0A', index + 1); // step, quality code, stopped by

                        nextStep = "trying to write step";
                        destinationString = destinationString.Insert(index + 3, step);

                        nextStep = "trying to write qc";
                        destinationString = destinationString.Insert(index + 11, qc);

                        nextStep = "trying to write  X";
                        destinationString = destinationString.Insert(index + 18, " X");

                        index = destinationString.IndexOf('\x0A', index + 1); // consecutive no. and program no.

                        nextStep = "trying to write cycle";
                        destinationString = destinationString.Insert(index + 3, cycle);

                        nextStep = "trying to write prg";
                        destinationString = destinationString.Insert(index + 13, prg);

                        index = destinationString.IndexOf('\x0A', index + 1); // hardware ID and channel no.

                        string fileName = System.IO.Path.GetFileName(originFilePath);

                        fileName = fileName.Replace(".json", ".txt");

                        destinationFilePath = System.IO.Path.Combine(stations[i].destinationPath, fileName);
                        destinationFile = System.IO.File.CreateText(destinationFilePath);

                        destinationFile.Write(destinationString.ToCharArray());

                        destinationFile.Flush();

                        originFile.Close();
                        destinationFile.Close();

                        System.IO.File.Delete(originFilePath);

                        DateTime localDate = DateTime.Now;
                        var culture = new CultureInfo("en-GB");
                        stations[i].lastActivityDate = localDate.ToString(culture);
                        myEventLog.WriteEntry($"Station {stations[i].name} ({stations[i].ip}), last activity {stations[i].lastActivityDate}",
                            EventLogEntryType.Information, ++eventID);
                    }
                    i++; if (i >= maxStationNumber) break;
                }
            }
            catch (Exception theException) 
            {
                myEventLog.WriteEntry($"<readAndWriteStationData> Error {nextStep}. {theException.Message} Source: {theException.Source}", EventLogEntryType.Error);
                errorCount++;
            }
        }

        private string getData(string source, string name, int fromIndex, SearchType t)
        {
            try
            { 
                int index = 0, i;
                string result = "";

                char[] charArray = source.ToCharArray();

                if (t == SearchType.FirstOcurrence)
                    index = source.IndexOf("\"" + name + "\":", fromIndex); //e.g. '"result": '

                if (t == SearchType.LastOcurrence)
                    index = source.LastIndexOf("\"" + name + "\":");

                if (index == 0) // NO MATCHING FOUND
                {
                    myEventLog.WriteEntry($"Error trying to get: {name}", EventLogEntryType.Warning);
                    return null;
                }

                index = index + name.Length + 4; // two quotation marks, one colon and a space

                if (charArray[index] == '"') // STRING CASE!
                {
                    i = 1; // offset of the quotation mark
                    while (charArray[i + index] != '"')
                    {
                        result = result.Insert(result.Length, charArray[i + index].ToString());
                        i++;
                    }
                }
                else // NUMBER CASE!
                {
                    i = 0; // no offset
                    while (charArray[i + index] != ',' && charArray[i + index] != ' ')
                    {
                        result = result.Insert(result.Length, charArray[i + index].ToString());
                        i++;
                    }
                }
                return result;
            }
            catch (Exception theException) 
            {
                myEventLog.WriteEntry($"<getData> Error trying to get: {name}. {theException.Message}  Source: {theException.Source}", EventLogEntryType.Error);
                errorCount++;
                return null;
            }
        }

        private string cutAndShift(string s, int n)
        {
            try
            {
                int indexOfPoint = s.IndexOf(".");
                if (indexOfPoint == -1)
                {
                    s = s.Insert(s.Length, ".");
                    s = s.Insert(s.Length, "0");
                    s = s.Insert(s.Length, "0");
                }

                indexOfPoint = s.IndexOf(".");
        
                for (int i = 0; i < (n - indexOfPoint - 3); i++) // round in 2 decimals
                {
                    s = s.Insert(0, " ");
                }

                //TODO: add a point and two decimals when the value has no decimals

                s = s.Substring(0, n); // last cut
                return s;
            }
            catch (Exception theException)
            {
                myEventLog.WriteEntry($"<cutAndShift> Error: {theException.Message} Source: {theException.Source}", EventLogEntryType.Error);
                errorCount++;
                return null;
            }
        }

        private string expandAndShift(string s, int n)
        {
            try
            {
                int len = s.Length;   
                for (int i = 0; i < (n - len); i++)
                {
                    s = s.Insert(0, " ");
                }
                return s;
            }
            catch (Exception theException)
            {
                myEventLog.WriteEntry($"<expandAndShift> Error: {theException.Message} Source: {theException.Source}", EventLogEntryType.Error);
                errorCount++;
                return null;
            }
        }

    }
}
