using System;
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
using System.Threading;
using System.IO;

namespace OldNewGateway
{
    public partial class OldNewGateway : ServiceBase
    {
        // CONSTANTS :
        static string version = "001";
        static int maxErrorCount = 10;
        static int timerInterval = 3000; // in miliseconds
            
        struct  Station
        {
            public string name;
            public string ip;
            public string originPath;
            public string destinationPath;
            public string lastActivityDate;
        }

        public enum SearchType
        {
            FirstOcurrence = 0,
            LastOcurrence = 1
        }

        // GLOBAL VARIABLES :
        private int errorCount;
        private string nextStep;
        string modelString;
        Station[] stations;
        int totalCycleTime;
        System.Timers.Timer myTimer = new System.Timers.Timer();

        public OldNewGateway()
        {
            InitializeComponent();

            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);

            myEventLog = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("OldNewGatewaySource"))
            {
                System.Diagnostics.EventLog.CreateEventSource("OldNewGatewaySource", "OldNewGatewayLog");
            }
            myEventLog.Source = "OldNewGatewaySource";
            myEventLog.Log = "OldNewGatewayLog";

            myTimer.Interval = timerInterval; 
            myTimer.Elapsed += new ElapsedEventHandler(this.OnTimer); 
        }

        protected override void OnStart(string[] args)
        {
            stations = getStationsInfo("C:\\OldNewGateway\\config\\stations.csv");
            modelString = getStringFromFile("C:\\OldNewGateway\\file models\\model.txt");
            errorCount = 0;
            myTimer.Start();
            myEventLog.WriteEntry($"Started - version {version}");
        }

        protected override void OnContinue()
        {
            errorCount = 0;
            stations = getStationsInfo("C:\\OldNewGateway\\config\\stations.csv");
            myEventLog.WriteEntry($"Started again - version {version}");
        }

        protected override void OnStop()
        {
            myEventLog.WriteEntry($"Stopped - version {version}");
            myTimer.Stop();
        }

        private void OnTimer(object sender, ElapsedEventArgs args)
        {        
            if (errorCount > maxErrorCount)
            {
                myEventLog.WriteEntry("Too many errors, execution stopped. Please check errors and re-start the service", EventLogEntryType.Error);
                myTimer.Stop();
            }
            else
            {
                if (worker.IsBusy != true)
                {
                    worker.RunWorkerAsync();
                }
                else
                {
                    myEventLog.WriteEntry($"Overloaded - version {version}", EventLogEntryType.Warning);
                }
            }
        }

        private Station[] getStationsInfo(string path)
        {     
            try
            {     
                string configString;

                string[] lines;
                string[] fields;

                using (StreamReader r = File.OpenText(path))
                {
                    configString = r.ReadToEnd();
                }

                lines = configString.Split(new char[] { '\x0D', '\x0A' }, StringSplitOptions.RemoveEmptyEntries);

                Station[] sts = new Station[lines.Length-1]; //the title of each column is not part of a station

                int i = -1;
                foreach (string line in lines)
                {
                    if (i == -1) i = 0;
                    else // skip the first line
                    {
                        fields = line.Split(';');
                        sts[i].name = fields[0];
                        sts[i].ip = fields[1];
                        sts[i].originPath = fields[2];
                        sts[i].destinationPath = fields[3];
                        i++;
                    }
                } 
                return sts;
            }
            catch (Exception theException)
            {
                myEventLog.WriteEntry($"<getStationInfo> Error: {theException.Message} Source: {theException.Source}", EventLogEntryType.Error);
                return null;
            }
        }

        private string getStringFromFile(string path)
        {
            /*
            System.IO.StreamReader theFile = System.IO.File.OpenText(path);
            string theString = theFile.ReadToEnd();
            theFile.Close();
            */

            using (StreamReader r = File.OpenText(path))
            {
                string theString = r.ReadToEnd();
                return theString;
            }
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {       
            processStationsData();  
        }

        private void worker_RunWorkerCompleted (object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                //myEventLog.WriteEntry($"<RunWorkerCompleted> Error!! {e.Error.Message}", EventLogEntryType.Error);
            }
            else
            {
                //myEventLog.WriteEntry($"<RunWorkerCompleted> Done!", EventLogEntryType.SuccessAudit);
            }
        }

        private void processStationsData() 
        {  
            try
            {
                DateTime localDateStart = DateTime.Now;
                for (int i = 0; i< stations.Length; i++)
                {
                    stations[i] = readAndWriteStationData(stations[i]);
                }
                DateTime localDateFinish = DateTime.Now;
                totalCycleTime = localDateFinish.Millisecond - localDateStart.Millisecond;
                logStatus(totalCycleTime, stations);
            }
            catch (Exception theException)
            {
                myEventLog.WriteEntry($"<processStationsData> Error {nextStep}. {theException.Message} Source: {theException.Source}", EventLogEntryType.Error);
                errorCount++;
            }
        }

        private void logStatus (int totalTime, Station[] sts)
        {
            using (StreamWriter w = new StreamWriter("C:\\OldNewGateway\\log\\log.txt"))
            {
                w.WriteLine($"{totalTime}");
                foreach (Station st in sts)
                {
                    w.WriteLine($"{st.name};{st.ip};{st.lastActivityDate}");
                }          
            }
        }

        private Station readAndWriteStationData(Object obj)
        {
            //System.IO.StreamReader originFile;
            //System.IO.StreamWriter destinationFile;

        

            int index;
            string result, prg, cycle, date, id, qc, row, column, step, Tmin, T, Tmax, Amin, A, Amax;

            Station station = (Station) obj;

            try
            {
                IEnumerable<string> filePaths = System.IO.Directory.EnumerateFiles(station.originPath, "*.json", System.IO.SearchOption.AllDirectories);
                foreach (string originFilePath in filePaths) //all .json files in that folder and subfolders!
                {
                    // READ ORIGIN FILE

                    /*originFile = System.IO.File.OpenText(originFilePath); // OLD WAY
                    originString = originFile.ReadToEnd();
                    originFile.Close();
                    string fileName = System.IO.Path.GetFileName(originFilePath);
                    System.IO.File.Delete(originFilePath);*/

                    string originString;
                    using (StreamReader r = File.OpenText(originFilePath))
                    {
                        originString = r.ReadToEnd();
                    }
                    string fileName = System.IO.Path.GetFileName(originFilePath); // get the file name and delete file
                    System.IO.File.Delete(originFilePath);


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

                    string destinationString = modelString;

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
                          

                    fileName = fileName.Replace(".json", ".txt");
                    string destinationFilePath = System.IO.Path.Combine(station.destinationPath, fileName);
                    using (StreamWriter w = new StreamWriter(destinationFilePath))
                    {
                        w.Write(destinationString);
                    }

                    /*
                    destinationFile = System.IO.File.CreateText(destinationFilePath); // OLD WAY
                    destinationFile.Write(destinationString.ToCharArray());
                    destinationFile.Flush();
                    destinationFile.Close();
                    */

                    DateTime localDate = DateTime.Now;
                    var culture = new CultureInfo("en-GB");
                    station.lastActivityDate = localDate.ToString(culture);

                    return station;
                }
            }
            catch (Exception theException)
            {
                myEventLog.WriteEntry($"<readAndWriteStationData> Error {nextStep}. {theException.Message} Source: {theException.Source}", EventLogEntryType.Error);
                errorCount++;
                return station;
            }
            return station;
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


/* OLD: USING THREAD POOL
  
 //ThreadPool.QueueUserWorkItem(readAndWriteStationData, stations[i]); // thread pool

    */


/* OLD: USING THREADS
 * 
        private void processStationsData() 
        {
            try
            {
                int i = 0;
                while (!String.IsNullOrEmpty(stations[i].name))
                {
                    stations[i].thread = new Thread(readAndWriteStationData);
                    stations[i].thread.Start(stations[i]);

                    i++; if (i >= maxStationNumber) break;
                }
                i = 0;
                while (!String.IsNullOrEmpty(stations[i].name))
                {
                    stations[i].thread.Join();
                    i++; if (i >= maxStationNumber) break;
                }
            }
            catch (Exception theException)
            {
                myEventLog.WriteEntry($"<processStationsData> Error {nextStep}. {theException.Message} Source: {theException.Source}", EventLogEntryType.Error);
                errorCount++;
            }
        }

        private void readAndWriteStationData(Object obj)
        {
            Station station;

            System.IO.StreamReader originFile;
            System.IO.StreamWriter destinationFile;

            string originString;
            string destinationString;

            string destinationFilePath;
            int index;
            string result, prg, cycle, date, id, qc, row, column, step, Tmin, T, Tmax, Amin, A, Amax;

            try
            {
                station = (Station)obj;

                IEnumerable<string> filePaths = System.IO.Directory.EnumerateFiles(station.originPath, "*.json", System.IO.SearchOption.AllDirectories);
                foreach (string originFilePath in filePaths) //all .json files in that folder and subfolders!
                {
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

                    destinationFilePath = System.IO.Path.Combine(station.destinationPath, fileName);
                    destinationFile = System.IO.File.CreateText(destinationFilePath);

                    destinationFile.Write(destinationString.ToCharArray());

                    destinationFile.Flush();

                    originFile.Close();
                    destinationFile.Close();

                    System.IO.File.Delete(originFilePath);

                    DateTime localDate = DateTime.Now;
                    var culture = new CultureInfo("en-GB");
                    station.lastActivityDate = localDate.ToString(culture);
                    myEventLog.WriteEntry($"Station {station.name} ({station.ip}), last activity {station.lastActivityDate}",
                    EventLogEntryType.Information, ++eventID);
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
 * 
 * 
 * 
 * 
 * 
 */
