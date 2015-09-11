using Meybohm_REAMLS_Consolidation.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meybohm_REAMLS_Consolidation
{
    class Program
    {
        public static void Main(string[] arrParameters)
        {
            bool blnIsIncremental = (arrParameters.Length > 0 && arrParameters[0] == "Inc");
            string[] arrFileList;
            UtilityLibrary utilityLibrary = new UtilityLibrary(blnIsIncremental);

            // Begin Reading Aiken Files
            utilityLibrary.WriteToLog("");
            utilityLibrary.WriteToLog("Parsing and concatenation of files has been started: " + DateTime.Now.ToString("G"));
            utilityLibrary.WriteToLog("Starting to process Aiken Files...");
            
            arrFileList = utilityLibrary.GetFilesList(CityType.Aiken, FeedType.Residential);
            utilityLibrary.ProcessAikenFiles(arrFileList, FeedType.Residential);

            arrFileList = utilityLibrary.GetFilesList(CityType.Aiken, FeedType.Land);
            utilityLibrary.ProcessAikenFiles(arrFileList, FeedType.Land);

            arrFileList = utilityLibrary.GetFilesList(CityType.Aiken, FeedType.Agent);
            utilityLibrary.ProcessAikenFiles(arrFileList, FeedType.Agent);

            arrFileList = utilityLibrary.GetFilesList(CityType.Aiken, FeedType.Office);
            utilityLibrary.ProcessAikenFiles(arrFileList, FeedType.Office);

            utilityLibrary.WriteToLog("Finished processing Aiken Files.");
            utilityLibrary.WriteToLog("Starting to process Augusta Files...");
            // Begin Reading Augusta Files
            arrFileList = utilityLibrary.GetFilesList(CityType.Augusta, FeedType.Residential);
            utilityLibrary.ProcessAugustaFiles(arrFileList, FeedType.Residential);

            arrFileList = utilityLibrary.GetFilesList(CityType.Augusta, FeedType.Land);
            utilityLibrary.ProcessAugustaFiles(arrFileList, FeedType.Land);

            arrFileList = utilityLibrary.GetFilesList(CityType.Augusta, FeedType.Agent);
            utilityLibrary.ProcessAugustaFiles(arrFileList, FeedType.Agent);

            arrFileList = utilityLibrary.GetFilesList(CityType.Augusta, FeedType.Office);
            utilityLibrary.ProcessAugustaFiles(arrFileList, FeedType.Office);

            utilityLibrary.WriteToLog("Finished processing Augusta Files.");
            utilityLibrary.WriteToLog("Finished processing all files for Aiken and Augusta.");

            utilityLibrary.WriteToLog("Starting Archive Process...");
            //utilityLibrary.ArchiveFiles();
            utilityLibrary.WriteToLog("Finished Archive Process...");

            utilityLibrary.WriteToLog("Parsing and concatenation of files has been finished: " + DateTime.Now.ToString("G"));
            utilityLibrary.WriteToLog("");

            utilityLibrary.WriteToLog("Running Import Process (UpdateFromXML) via URL Call: " + DateTime.Now.ToString("G"));
            //utilityLibrary.ExecuteDataImportProcess();
            utilityLibrary.WriteToLog("Completed Import Process (UpdateFromXML) via URL Call: " + DateTime.Now.ToString("G"));
            utilityLibrary.WriteToLog("");

            utilityLibrary.EmailLogStatus();
        }
    }
}
