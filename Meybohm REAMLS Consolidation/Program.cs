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
            if (arrParameters.Length == 0 || (arrParameters[0].ToLower() != "inc" && arrParameters[0].ToLower() != "full"))
            {
                Console.WriteLine("You must specify a parameter of Full or Inc");
                return;
            }

            bool blnIsIncremental = (arrParameters.Length > 0 && arrParameters[0].ToLower() == "inc");
            string[] arrFileList;
            
            UtilityLibrary utilityLibrary = new UtilityLibrary(blnIsIncremental);
            
            utilityLibrary.WriteToLog("");

            //Send a test email to make sure it works
            if (arrParameters.Length > 1 && arrParameters[1].ToLower() == "testemail")
            {
                utilityLibrary.EmailLogStatus();
                return;
            }

            utilityLibrary.WriteToLog("<h2>Parsing and concatenation of files has been started: " + DateTime.Now.ToString("G") + "</h2>");
            utilityLibrary.WriteToLog("<h3>1) Starting to process Aiken Files...</h3>");

            // Begin Reading Aiken Files
            arrFileList = utilityLibrary.GetFilesList(CityType.Aiken, FeedType.Residential);
            utilityLibrary.ProcessAikenFiles(arrFileList, FeedType.Residential);

            arrFileList = utilityLibrary.GetFilesList(CityType.Aiken, FeedType.Land);
            utilityLibrary.ProcessAikenFiles(arrFileList, FeedType.Land);
            
            arrFileList = utilityLibrary.GetFilesList(CityType.Aiken, FeedType.Agent);
            utilityLibrary.ProcessAikenFiles(arrFileList, FeedType.Agent);
            
            arrFileList = utilityLibrary.GetFilesList(CityType.Aiken, FeedType.Office);
            utilityLibrary.ProcessAikenFiles(arrFileList, FeedType.Office);

            utilityLibrary.WriteToLog("<h3>1) Finished processing Aiken Files.</h3>");
            utilityLibrary.WriteToLog("<h3>2) Starting to process Augusta Files...</h3>");
            
            // Begin Reading Augusta Files
            arrFileList = utilityLibrary.GetFilesList(CityType.Augusta, FeedType.Residential);
            utilityLibrary.ProcessAugustaFiles(arrFileList, FeedType.Residential);

            arrFileList = utilityLibrary.GetFilesList(CityType.Augusta, FeedType.Land);
            utilityLibrary.ProcessAugustaFiles(arrFileList, FeedType.Land);

            arrFileList = utilityLibrary.GetFilesList(CityType.Augusta, FeedType.Agent);
            utilityLibrary.ProcessAugustaFiles(arrFileList, FeedType.Agent);

            arrFileList = utilityLibrary.GetFilesList(CityType.Augusta, FeedType.Office);
            utilityLibrary.ProcessAugustaFiles(arrFileList, FeedType.Office);

            utilityLibrary.FinishAllTasks();

            utilityLibrary.WriteToLog("<h3>2) Finished processing Augusta Files.</h3>");

            utilityLibrary.WriteToLog("<h3>3) Migrating Augusta MySQL Data... " + DateTime.Now.ToString("G") + "</h3>");

            utilityLibrary.MigrateAugustaMySQLData();

            utilityLibrary.CloseMySqlConnection();

            utilityLibrary.WriteToLog("<h3>3) Finished Migrating Augusta MySQL Data. " + DateTime.Now.ToString("G") + "</h3>");
            
            utilityLibrary.WriteToLog("<h3>4)Starting Archive Process...</h3>");
            utilityLibrary.ArchiveFiles();
            utilityLibrary.WriteToLog("<h3>4)Finished Archive Process.</h3>");

            utilityLibrary.WriteToLog("<h3>5)Running Import Process (UpdateFromXML) via URL Call: " + DateTime.Now.ToString("G") + "</h3>");
            utilityLibrary.ExecuteDataImportProcess();
            utilityLibrary.WriteToLog("<h3>5)Completed Import Process (UpdateFromXML) via URL Call: " + DateTime.Now.ToString("G") + "</h3>");

            utilityLibrary.WriteToLog("<h3>6)Running BuildFromFacts via URL Call: " + DateTime.Now.ToString("G") + "</h3>");
            utilityLibrary.ExecuteBuildFromFactsProcess();
            utilityLibrary.WriteToLog("<h3>6)Completed BuildFromFacts via URL Call: " + DateTime.Now.ToString("G") + "</h3>");

            if (!blnIsIncremental) { 
                utilityLibrary.WriteToLog("<h3>7)Running Photo Test: " + DateTime.Now.ToString("G") + "</h3>");
                utilityLibrary.ExecutePhotoTest();
                utilityLibrary.WriteToLog("<h3>7)Completed Running Photo Test: " + DateTime.Now.ToString("G") + "</h3>");
            }

            utilityLibrary.WriteToLog("<h2>Parsing and concatenation of files has been finished: " + DateTime.Now.ToString("G") + "</h2>");

            // Write out the statistics
            utilityLibrary.WriteStatistics();

            utilityLibrary.EmailLogStatus();
        }
    }
}
