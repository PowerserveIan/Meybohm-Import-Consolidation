﻿using Microsoft.VisualBasic.FileIO;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Meybohm_REAMLS_Consolidation.Model
{
    public class UtilityLibrary
    {
        #region Fields

        private int intTotalAikenFiles;
        private int intTotalAugustaFiles;

        private bool blnIsIncremental;
        private bool blnGoogleMapsOverLimit;
        private bool blnErrorsFound;
        private bool bBlockPropertyPurge;

        private StringBuilder sbLogBuilder;

        private int intTotalAikenProperties = 0;
        private int intTotalAikenGeocodedProperties = 0;
        private int intTotalAikenOffices = 0;
        private int intTotalAikenAgents = 0;
        private int intTotalAugustaProperties = 0;
        private int intTotalAugustaGeocodedProperties = 0;
        private int intTotalAugustaOffices = 0;
        private int intTotalAugustaAgents = 0;

        private Task[] taskPool = new Task[25];

        private bool bFatalError = false;

        private Dictionary<string, string> dictMlsPhotos = new Dictionary<string,string>();

        private MySqlConnection connMySqlConnection;
        
        #endregion

        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        /// <param name="blnIsIncremental"></param>
        public UtilityLibrary(bool blnIsIncremental)
        {
            this.blnIsIncremental = blnIsIncremental;

            blnGoogleMapsOverLimit = false;
            blnErrorsFound = false;

            sbLogBuilder = new StringBuilder();

            this.CheckDirectoriesAndFiles();
            this.RemoveOldFiles();


            try { 
                connMySqlConnection = new MySqlConnection(ConfigurationManager.ConnectionStrings["MySQLServer"].ToString());
                connMySqlConnection.Open();

                if(!this.blnIsIncremental)
                {
                    this.ClearMySQLData();
                }
            }
            catch (Exception ex)
            {
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error Opening MySql connection - Details: " + ex.Message + "</i></b>");
            }

        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strMLSID"></param>
        /// <param name="strCoordinates"></param>
        private void AddMLSGeolocation(string strMLSID, string[] strCoordinates, MLSType intMLSType)
        {
            string strCommand = string.Format(@" INSERT INTO NavicaGeocodeCoordinates (MLSID, Latitude, Longitude, MLSType) 
                                                 VALUES ('{0}', {1}, {2}, {3})", strMLSID, strCoordinates[0], strCoordinates[1], (int)intMLSType);

            using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MeybohmServer"].ConnectionString))
            {
                using (SqlCommand command = new SqlCommand(strCommand, connection))
                {
                    command.CommandTimeout = 300;
                    connection.Open();
                    command.ExecuteNonQuery();
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ArchiveFiles()
        {
            string[] arrFilePaths = Directory.GetFiles(Constant.AIKEN_DOWNLOAD_FOLDER, "*.csv");
            List<string> listFilePaths = new List<string>();

            for (int intIndex = 0; intIndex < arrFilePaths.Length; intIndex++)
            {
                this.MoveFile(arrFilePaths[intIndex]);
            }

            arrFilePaths = Directory.GetFiles(Constant.AUGUSTA_DOWNLOAD_FOLDER, "*.csv");

            for (int intIndex = 0; intIndex < arrFilePaths.Length; intIndex++)
            {
                this.MoveFile(arrFilePaths[intIndex]);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void CheckDirectoriesAndFiles()
        {
            // Check the directories and make sure they are available.
            if (!Directory.Exists(Constant.AIKEN_DOWNLOAD_FOLDER))
            {
                Directory.CreateDirectory(Constant.AIKEN_DOWNLOAD_FOLDER);
            }

            if (!Directory.Exists(Constant.AUGUSTA_DOWNLOAD_FOLDER))
            {
                Directory.CreateDirectory(Constant.AUGUSTA_DOWNLOAD_FOLDER);
            }

            if (!Directory.Exists(Constant.AIKEN_ARCHIVE_FOLDER))
            {
                Directory.CreateDirectory(Constant.AIKEN_ARCHIVE_FOLDER);
            }

            if (!Directory.Exists(Constant.AUGUSTA_ARCHIVE_FOLDER))
            {
                Directory.CreateDirectory(Constant.AUGUSTA_ARCHIVE_FOLDER);
            }

            if (!Directory.Exists(Constant.CONSOLIDATION_FOLDER))
            {
                Directory.CreateDirectory(Constant.CONSOLIDATION_FOLDER);
            }

            if (!Directory.Exists(Constant.LOG_FOLDER))
            {
                Directory.CreateDirectory(Constant.LOG_FOLDER);
            }

            // Check the files for the day and make sure they are writable
            if (!File.Exists(Constant.LOG_FILE))
            {
                this.WriteToLog("<h1>**** Meybohm Consolidation Program - LOG FILE (" + DateTime.Today.ToShortDateString() + ") ****</h1>");
            }

            this.WriteToLog("<h1>**** Log Information ****</h1>");
        }

        /// <summary>
        /// 
        /// </summary>
        private void ClearMySQLData()
        {
            string strCommand = @" TRUNCATE TABLE prop_aik;
                                   TRUNCATE TABLE photo_links_aik; 
                                   TRUNCATE TABLE aik_agents;
                                   TRUNCATE TABLE aik_offices;  
                                   TRUNCATE TABLE aux_aik;
                                   TRUNCATE TABLE aux_aik_agents;
                                   TRUNCATE TABLE aux_aik_offices;   

                                   TRUNCATE TABLE prop_res;   
                                   TRUNCATE TABLE photo_links_aug;   
                                   TRUNCATE TABLE agents;   
                                   TRUNCATE TABLE offices;   
                                   TRUNCATE TABLE aux_aug;
                                   TRUNCATE TABLE aux_agents;
                                   TRUNCATE TABLE aux_offices; ";

            //using (MySqlConnection connection = new MySqlConnection(ConfigurationManager.ConnectionStrings["MySQLServer"].ToString()))
            if (connMySqlConnection.State == ConnectionState.Open)
            {
                using(MySqlCommand command = new MySqlCommand(strCommand, connMySqlConnection))
                {
                    command.CommandTimeout = 300;

                    try
                    {
                        //connMySqlConnection.Open();
                        command.ExecuteNonQuery();
                        //connection.Close();
                    }
                    catch (Exception ex)
                    {
                        if (!this.blnIsIncremental)
                            bFatalError = true;
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running ClearMySQLData SQL - Details: " + ex.Message + "</i></b>");
                    }
                }
            }
        }

        public void CloseMySqlConnection()
        {
            try
            {
                connMySqlConnection.Close();
            }
            catch (Exception ex)
            {

            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void EmailLogStatus()
        {
            try { 
                string strSubject;

                MailMessage mailMessage = new MailMessage();
                SmtpClient mailSMTPClient = new SmtpClient();
                StringBuilder sbMessage = new StringBuilder();
                MailAddress fromAddress; 
                
                if (Constant.EMAIL_SENDER == "localhost")
                    fromAddress = new MailAddress("realestate@powerserve.net");
                else
                    fromAddress = new MailAddress(Constant.EMAIL_SENDER);

                //mailSMTPClient.DeliveryMethod = SmtpDeliveryMethod.PickupDirectoryFromIis;

                if (Constant.EMAIL_SENDER == "localhost")
                {
                    mailSMTPClient.Port = 25;
                    mailSMTPClient.Host = "localhost";
                }
                else
                { 
                    mailSMTPClient.Credentials = new NetworkCredential("allen@powerserve.net", "powell3$5");
                    mailSMTPClient.Port = 587;
                    mailSMTPClient.Host = "smtp.gmail.com";
                    mailSMTPClient.EnableSsl = true;
                }
                mailMessage.From = fromAddress;
                mailMessage.IsBodyHtml = true;

                if (this.blnIsIncremental) {
                    if (!bFatalError) //Don't send incremental success
                        return;
                    foreach (string strEmailAddress in Constant.EMAIL_RECEIVER_INC)
                    {
                        mailMessage.To.Add(strEmailAddress);
                    }
                }
                else 
                { 
                    foreach (string strEmailAddress in Constant.EMAIL_RECEIVER)
                    {
                        mailMessage.To.Add(strEmailAddress);
                    }
                }

                strSubject = string.Format("{2} Meybohm Import Process ({0}) - {1}", (blnIsIncremental) ? "Incremental" : "Full", DateTime.Now.ToString("G"), (bFatalError) ? "FAILURE" : "SUCCESS");

                sbMessage.Append("Hello,");
                sbMessage.Append("<br />");
                sbMessage.Append("<br />The following is the the log report for the recent execution of the Meybohm Import Process. Please see the file below for more information.");
                sbMessage.Append("<h2>File: " + Constant.LOG_FILE + "</h2>");
                sbMessage.Append(this.sbLogBuilder.ToString());

                mailMessage.Subject = strSubject;
                mailMessage.Body = sbMessage.ToString();

                mailSMTPClient.Send(mailMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ExecuteDataImportProcess()
        {
            string strMeybohmImportURL = Constant.MEYBOHM_IMPORT_URL;
            string strServiceResponse = "";

            if(!this.blnIsIncremental)
            {
                strMeybohmImportURL += "?version=full&resetLock=1";

                if (this.bBlockPropertyPurge)
                {
                    strMeybohmImportURL += "&blockpurge=1";
                }
            }

            this.WriteToLog("Running URL: " + strMeybohmImportURL);

            try
            {
                WebRequest webRequest = WebRequest.Create(new Uri(strMeybohmImportURL));
                webRequest.Timeout = 4 * 60 * 60 * 1000; // 4 hours
                WebResponse webResponse = webRequest.GetResponse();
                Stream streamData = webResponse.GetResponseStream();
                using (StreamReader srReader = new StreamReader(streamData))
                {
                    strServiceResponse = srReader.ReadToEnd();
                }
                this.WriteToLog("<br />Web Service Response: " + strServiceResponse);
            }
            catch(Exception ex)
            {
                bFatalError = true;
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running UpdateFromXML Web Service: " + ex.Message + "</i></b>");
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running UpdateFromXML Web Service Details: " + ex.StackTrace + "</i></b>");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ExecuteBuildFromFactsProcess()
        {
            string strMeybohmImportURL = Constant.BUILD_FROM_FACTS_URL;
            string strServiceResponse = "";

            this.WriteToLog("Running URL: " + strMeybohmImportURL);

            try
            {
                WebRequest webRequest = WebRequest.Create(new Uri(Constant.BUILD_FROM_FACTS_URL));
                webRequest.Timeout = 4 * 60 * 60 * 1000; // 4 hours
                WebResponse webResponse = webRequest.GetResponse();
                Stream streamData = webResponse.GetResponseStream();
                using (StreamReader srReader = new StreamReader(streamData))
                {
                    strServiceResponse = srReader.ReadToEnd();
                }
                this.WriteToLog("<br />Web Service Response: " + strServiceResponse);
            }
            catch (Exception ex)
            {
                bFatalError = true;
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running BuildFromFacts Web Service: " + ex.Message + "</i></b>");
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running BuildFromFacts Web Service Details: " + ex.StackTrace + "</i></b>");
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void ExecutePhotoTest()
        {
            string strMeybohmURL = Constant.LISTING_URL;

            string strImageURL = "";
            string strServiceResponse = "";

            try
            {
                for (int intCounter = 0; intCounter < dictMlsPhotos.Keys.Count; intCounter++)
                {
                    List<string> listKeys = new List<string>(dictMlsPhotos.Keys);
                    int intDictSize = dictMlsPhotos.Count;

                    string strMlsID = listKeys[intCounter];
                    string strPhotoURL = dictMlsPhotos[strMlsID].ToString();
                    if (strPhotoURL.IndexOf(',') > -1)
                    {
                        strPhotoURL = strPhotoURL.Substring(0, strPhotoURL.IndexOf(','));
                    }
                    Uri strMlsURL = new Uri(Constant.PHOTO_TEST[strMlsID]);

                    this.WriteToLog("<br /><br />Testing Photo for MLS ID " + strMlsID + "<br />Photo URL: " + strPhotoURL + "<br/>MLS URL: " + strMlsURL + "");
                    try
                    {
                        WebRequest webRequest = WebRequest.Create(strMlsURL);
                        webRequest.Timeout = 4 * 60 * 60 * 1000; // 4 hours
                        WebResponse webResponse = webRequest.GetResponse();
                        Stream streamData = webResponse.GetResponseStream();
                        using (StreamReader srReader = new StreamReader(streamData))
                        {
                            strServiceResponse = srReader.ReadToEnd();

                            if (strServiceResponse.IndexOf(strPhotoURL) < 0)
                                this.WriteToLog("<br /><b><i style=\"color:red;\">PHOTO NOT FOUND</i></b>");
                        }
                    }
                    catch (Exception ex)
                    {
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running Photo Test: " + ex.Message + "</i></b>");
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running Photo Test Details: " + ex.StackTrace + "</i></b>");
                    }
                }
            }

            catch (Exception ex)
            {
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running Photo Test: " + ex.Message + "</i></b>");
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running Photo Test Details: " + ex.StackTrace + "</i></b>");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arrColumns"></param>
        /// <param name="intMLSType"></param>
        /// <param name="intFeedType"></param>
        private void ExportMySQLData(string[] arrColumns, MLSType intMLSType, FeedType intFeedType)
        {
            string strCommand = "";
            string strCommandSupport = "";

            string[] arrColumnHeaders = null;

            try
            {
                //using (MySqlConnection connection = new MySqlConnection(ConfigurationManager.ConnectionStrings["MySQLServer"].ToString()))
                if (connMySqlConnection.State == ConnectionState.Open)
                {
                    MySqlCommand command = connMySqlConnection.CreateCommand();
                    MySqlCommand commandSupport = connMySqlConnection.CreateCommand();

                    command.CommandTimeout = 300;
                    
                    string strAgentId = "";

                    if (intMLSType == MLSType.Aiken)
                    {
                        arrColumnHeaders = Constant.AIKEN_RESIDENTIAL_HEADER.Split(',');

                        switch (intFeedType)
                        {
                            case FeedType.Residential:
                                // Export Property Photos
                                this.ImportPropertyPhotos(arrColumns[(int)Aiken_RES_Fields.MLS_Number], arrColumns[arrColumns.Length - 1], intMLSType);

                                strCommand = @" INSERT INTO prop_aik (  propid,street_number,street_address,subdivision,city,state,zip,price_list,list_office,list_agentid,prop_type,
				 	                                                        public_remarks,year_built,style,ext_features,int_features,lot_size,lot_desc,sqft_total,baths,baths_half,bedrooms,foundation,flooring,garage,attic,
				 	                                                        hvac,school_elem,school_high,school_mid,photo_count,vt_url,allow_avm,builder,new_const,status,county) 
                                                    VALUES (@propid,@street_number,@street_name,@subdivision,@city,@state,@zip,
				 	                                        @price_list,@list_office,@list_agentid,@prop_type,@public_remarks,@year_built,@style,@ext_features,@int_features,@lot_size,
				 	                                        @lot_desc,@sqft_total,@baths,@baths_half,@bedrooms,@foundation,@flooring,@garage,@attic,@hvac,@school_elem,@school_high,
				 	                                        @school_mid,@photo_count,@vt_url,@allow_avm,@builder,@new_const,@status,@county)";
                                strCommandSupport = @"  INSERT INTO aux_aik (propid,updated,directions,acres_total) VALUES (@propid,'N',@directions,@acres_total)";

                                command.CommandText = strCommand;
                                commandSupport.CommandText = strCommandSupport;

                                if (arrColumns[(int)Aiken_RES_Fields.LA_ID].Split('-').Length >= 2)
                                {
                                    strAgentId = arrColumns[(int)Aiken_RES_Fields.LA_ID].Split('-')[1];
                                }

                                command.Parameters.AddWithValue("@propid", arrColumns[(int)Aiken_RES_Fields.MLS_Number]);
                                command.Parameters.AddWithValue("@street_number", arrColumns[(int)Aiken_RES_Fields.Street_Number]);
                                command.Parameters.AddWithValue("@street_name", arrColumns[(int)Aiken_RES_Fields.Address]);
                                command.Parameters.AddWithValue("@subdivision", arrColumns[(int)Aiken_RES_Fields.Town_Subdivision]);
                                command.Parameters.AddWithValue("@city", arrColumns[(int)Aiken_RES_Fields.City]);
                                command.Parameters.AddWithValue("@county", arrColumns[Array.IndexOf(arrColumnHeaders, "County")]);
                                command.Parameters.AddWithValue("@state", arrColumns[(int)Aiken_RES_Fields.State]);
                                command.Parameters.AddWithValue("@zip", arrColumns[(int)Aiken_RES_Fields.Zip_Code]);
                                command.Parameters.AddWithValue("@price_list", arrColumns[(int)Aiken_RES_Fields.List_Price]);
                                command.Parameters.AddWithValue("@list_office", arrColumns[(int)Aiken_RES_Fields.Listing_Office]);
                                command.Parameters.AddWithValue("@list_agentid", strAgentId);
                                command.Parameters.AddWithValue("@prop_type", "R");
                                command.Parameters.AddWithValue("@public_remarks", arrColumns[(int)Aiken_RES_Fields.Property_Description]);
                                command.Parameters.AddWithValue("@year_built", arrColumns[(int)Aiken_RES_Fields.Year_Built]);
                                command.Parameters.AddWithValue("@style", arrColumns[(int)Aiken_RES_Fields.Style]);
                                command.Parameters.AddWithValue("@ext_features", arrColumns[(int)Aiken_RES_Fields.Exterior_Features]);
                                command.Parameters.AddWithValue("@int_features", arrColumns[(int)Aiken_RES_Fields.Interior_Features]);
                                command.Parameters.AddWithValue("@flooring", arrColumns[(int)Aiken_RES_Fields.Floors]);
                                command.Parameters.AddWithValue("@lot_size", DBNull.Value);
                                command.Parameters.AddWithValue("@lot_desc", DBNull.Value);
                                command.Parameters.AddWithValue("@sqft_total", arrColumns[(int)Aiken_RES_Fields.Apx_Heated_SqFt]);
                                command.Parameters.AddWithValue("@baths", arrColumns[(int)Aiken_RES_Fields.Full_Baths]);
                                command.Parameters.AddWithValue("@baths_half", arrColumns[(int)Aiken_RES_Fields.Half_Baths]);
                                command.Parameters.AddWithValue("@bedrooms", arrColumns[(int)Aiken_RES_Fields.Bedrooms]);
                                command.Parameters.AddWithValue("@foundation", arrColumns[(int)Aiken_RES_Fields.Foundation_Basement]);
                                command.Parameters.AddWithValue("@garage", arrColumns[(int)Aiken_RES_Fields.Garage]);
                                command.Parameters.AddWithValue("@attic", arrColumns[(int)Aiken_RES_Fields.Attic]);
                                command.Parameters.AddWithValue("@hvac", arrColumns[(int)Aiken_RES_Fields.Air_Conditioning]);
                                command.Parameters.AddWithValue("@school_elem", arrColumns[(int)Aiken_RES_Fields.Elementary_School]);
                                command.Parameters.AddWithValue("@school_high", arrColumns[(int)Aiken_RES_Fields.High_School]);
                                command.Parameters.AddWithValue("@school_mid", arrColumns[(int)Aiken_RES_Fields.Middle_School]);
                                command.Parameters.AddWithValue("@photo_count", arrColumns[arrColumns.Length - 1].Split(',').Count());
                                command.Parameters.AddWithValue("@vt_url", arrColumns[(int)Aiken_RES_Fields.Virtual_Tour]);
                                command.Parameters.AddWithValue("@allow_avm", DBNull.Value);
                                command.Parameters.AddWithValue("@builder", arrColumns[(int)Aiken_RES_Fields.Builder_Name]);
                                command.Parameters.AddWithValue("@new_const", arrColumns[(int)Aiken_RES_Fields.New_Construction]);
                                command.Parameters.AddWithValue("@status", arrColumns[(int)Aiken_RES_Fields.Property_Status]);

                                commandSupport.Parameters.AddWithValue("@propid", arrColumns[(int)Aiken_RES_Fields.MLS_Number]);
                                commandSupport.Parameters.AddWithValue("@directions", arrColumns[(int)Aiken_RES_Fields.Directions]);
                                commandSupport.Parameters.AddWithValue("@acres_total", arrColumns[(int)Aiken_RES_Fields.Total_Acres]);

                                break;
                            case FeedType.Agent:
                                strCommand = @" INSERT INTO aik_agents (agentid,officeid,name_first,name_last,email,url,uid) 
			                                        VALUES (@agent_id,@agent_office,@agent_first,@agent_last,@agent_email,@agent_web,@agent_uid)";
                                strCommandSupport = @"  INSERT INTO aux_aik_agents (uid,phone_home,address,city,state,zip) 
			                                                VALUES (@agent_uid,@agent_home,@agent_street,@agent_city,@agent_state,@agent_zip)";

                                command.CommandText = strCommand;
                                commandSupport.CommandText = strCommandSupport;

                                if (arrColumns[(int)Aiken_Agent_Fields.AGENT_ID].Split('-').Length >= 2)
                                {
                                    strAgentId = arrColumns[(int)Aiken_Agent_Fields.AGENT_ID].Split('-')[1];
                                }

                                command.Parameters.AddWithValue("@agent_id", strAgentId);
                                command.Parameters.AddWithValue("@agent_office", arrColumns[(int)Aiken_Agent_Fields.Office_ID]);
                                command.Parameters.AddWithValue("@agent_first", arrColumns[(int)Aiken_Agent_Fields.First_Name]);
                                command.Parameters.AddWithValue("@agent_last", arrColumns[(int)Aiken_Agent_Fields.Last_Name]);
                                command.Parameters.AddWithValue("@agent_email", arrColumns[(int)Aiken_Agent_Fields.Agent_Email]);
                                command.Parameters.AddWithValue("@agent_web", arrColumns[(int)Aiken_Agent_Fields.Web_Address]);
                                command.Parameters.AddWithValue("@agent_uid", arrColumns[(int)Aiken_Agent_Fields.AGENT_ID]);

                                commandSupport.Parameters.AddWithValue("@agent_uid", arrColumns[(int)Aiken_Agent_Fields.AGENT_ID]);
                                commandSupport.Parameters.AddWithValue("@agent_home", arrColumns[(int)Aiken_Agent_Fields.Home]);
                                commandSupport.Parameters.AddWithValue("@agent_street", arrColumns[(int)Aiken_Agent_Fields.Mail_Address_1]);
                                commandSupport.Parameters.AddWithValue("@agent_city", arrColumns[(int)Aiken_Agent_Fields.Mail_City]);
                                commandSupport.Parameters.AddWithValue("@agent_state", arrColumns[(int)Aiken_Agent_Fields.Mail_State]);
                                commandSupport.Parameters.AddWithValue("@agent_zip", arrColumns[(int)Aiken_Agent_Fields.Mail_Zip_Code]);

                                break;
                            case FeedType.Office:
                                strCommand = @" INSERT INTO aik_offices (officeid,office_name,zip,url) 
			                                VALUES (@office_id,@office_name,@zip,@office_web)";
                                strCommandSupport = @"  INSERT INTO aux_aik_offices (officeid,address,city,state,zip,phone,fax) 
			                                        VALUES (@office_id,@office_street,@office_city,@office_state,@office_zip,@office_phone,@office_fax)";

                                command.CommandText = strCommand;
                                commandSupport.CommandText = strCommandSupport;

                                command.Parameters.AddWithValue("@office_id", arrColumns[(int)Office_Fields.Office_ID]);
                                command.Parameters.AddWithValue("@office_name", arrColumns[(int)Office_Fields.Office_Name]);
                                command.Parameters.AddWithValue("@zip", arrColumns[(int)Office_Fields.Mail_Zip_Code]);
                                command.Parameters.AddWithValue("@office_web", arrColumns[(int)Office_Fields.Web_Address]);

                                commandSupport.Parameters.AddWithValue("@office_id", arrColumns[(int)Office_Fields.Office_ID]);
                                commandSupport.Parameters.AddWithValue("@office_street", arrColumns[(int)Office_Fields.Mail_Address_1]);
                                commandSupport.Parameters.AddWithValue("@office_city", arrColumns[(int)Office_Fields.Mail_City]);
                                commandSupport.Parameters.AddWithValue("@office_state", arrColumns[(int)Office_Fields.Mail_State]);
                                commandSupport.Parameters.AddWithValue("@office_zip", arrColumns[(int)Office_Fields.Mail_Zip_Code]);
                                commandSupport.Parameters.AddWithValue("@office_phone", arrColumns[(int)Office_Fields.Main]);
                                commandSupport.Parameters.AddWithValue("@office_fax", arrColumns[(int)Office_Fields.Fax]);

                                break;
                        }
                    }
                    else
                    {
                        switch (intFeedType)
                        {
                            case FeedType.Residential:
                                this.ImportPropertyPhotos(arrColumns[(int)Augusta_RES_Fields.MLS_Number], arrColumns[arrColumns.Length - 1], intMLSType);

                                strCommand = @" INSERT INTO prop_res (  hvac,city,county,state,street_name,street_number,subdivision,zip,appliances,attic,basement,baths,b2_length,
				 	                                                b2_level,b2_width,b3_length,b3_level,b3_width,b4_length,b4_level,b4_width,b5_length,b5_level,b5_width,bedrooms,breakfast_length,breakfast_level,
				 	                                                breakfast_width,dining_length,dining_level,dining_width,driveway,ext_features,ext_finish,extra_rooms,family_length,family_level,family_width,
				 	                                                financing,fireplace,foundation,flooring,garage,great_length,great_level,great_width,baths_half,heat,int_features,kitchen_length,kitchen_level,
				 	                                                kitchen_width,price_list,list_agentid,list_agentname,list_office,list_firmid,living_length,living_level,living_width,master_length,master_level,
				 	                                                master_width,propid,amenities,new_cons,photo_count,pool,porch,prop_age,public_remarks,roof,school_elem,school_mid,school_high,sewer,show_instr,
				 	                                                sqft_total,status,style,water,year_built,cust_vt_url,prop_type,cust_directions,cust_builder_name,cust_dom,cust_list_date,lot_desc) 
                                            VALUES (@hvac,@city,@county,@state,@street_name,@street_number,
				 	                                @subdivision,@zip,@appliances,@attic,@basement,@baths,@b2_length,@b2_level,@b2_width,@b3_length,@b3_level,@b3_width,
				 	                                @b4_length,@b4_level,@b4_width,@b5_length,@b5_level,@b5_width,@bedrooms,@breakfast_length,@breakfast_level,@breakfast_width,
				 	                                @dining_length,@dining_level,@dining_width,@driveway,@ext_features,@ext_finish,@extra_rooms,@family_length,@family_level,
				 	                                @family_width,@financing,@fireplace,@foundation,@flooring,@garage,@great_length,@great_level,@great_width,@baths_half,
				 	                                @heat,@int_features,@kitchen_length,@kitchen_level,@kitchen_width,@price_list,@list_agentid,@list_agentname,@list_office,
				 	                                @list_firmid,@living_length,@living_level,@living_width,@master_length,@master_level,@master_width,@propid,@amenities,
				 	                                @new_cons,@photo_count,@pool,@porch,@prop_age,@public_remarks,@roof,@school_elem,@school_mid,@school_high,@sewer,
				 	                                @show_instr,@sqft_total,@status,@style,@water,@year_built,@vt_url,@prop_type,@directions,@builder,@dom,@list_date,@lot_desc)";
                                strCommandSupport = "INSERT INTO aux_aug (propid,fireplace,total_rooms,acres_total) VALUES (@propid,@fireplace_num,@total_rooms,@acres_total)";

                                command.CommandText = strCommand;
                                commandSupport.CommandText = strCommandSupport;

                                if (arrColumns[(int)Augusta_RES_Fields.LA_ID].Split('-').Length >= 2)
                                {
                                    strAgentId = arrColumns[(int)Augusta_RES_Fields.LA_ID].Split('-')[1];
                                }

                                command.Parameters.AddWithValue("@hvac", arrColumns[(int)Augusta_RES_Fields.AC_Ventilation]);
                                command.Parameters.AddWithValue("@attic", arrColumns[(int)Augusta_RES_Fields.Attic]);
                                command.Parameters.AddWithValue("@city", arrColumns[(int)Augusta_RES_Fields.City]);
                                command.Parameters.AddWithValue("@county", arrColumns[(int)Augusta_RES_Fields.County]);
                                command.Parameters.AddWithValue("@state", arrColumns[(int)Augusta_RES_Fields.State]);
                                command.Parameters.AddWithValue("@street_name", arrColumns[(int)Augusta_RES_Fields.Address]);
                                command.Parameters.AddWithValue("@street_number", arrColumns[(int)Augusta_RES_Fields.Street_Number]);
                                command.Parameters.AddWithValue("@subdivision", arrColumns[(int)Augusta_RES_Fields.Subdivision]);
                                command.Parameters.AddWithValue("@zip", arrColumns[(int)Augusta_RES_Fields.Zip_Code]);
                                command.Parameters.AddWithValue("@appliances", arrColumns[(int)Augusta_RES_Fields.Appliances]);
                                command.Parameters.AddWithValue("@basement", arrColumns[(int)Augusta_RES_Fields.Basement]);
                                command.Parameters.AddWithValue("@baths", arrColumns[(int)Augusta_RES_Fields.Full_Baths]);
                                command.Parameters.AddWithValue("@b2_length", arrColumns[(int)Augusta_RES_Fields.Bedroom_2_Length]);
                                command.Parameters.AddWithValue("@b2_level", arrColumns[(int)Augusta_RES_Fields.Bedroom_2_Level]);
                                command.Parameters.AddWithValue("@b2_width", arrColumns[(int)Augusta_RES_Fields.Bedroom_2_Width]);
                                command.Parameters.AddWithValue("@b3_length", arrColumns[(int)Augusta_RES_Fields.Bedroom_3_Length]);
                                command.Parameters.AddWithValue("@b3_level", arrColumns[(int)Augusta_RES_Fields.Bedroom_3_Level]);
                                command.Parameters.AddWithValue("@b3_width", arrColumns[(int)Augusta_RES_Fields.Bedroom_3_Width]);
                                command.Parameters.AddWithValue("@b4_length", arrColumns[(int)Augusta_RES_Fields.Bedroom_4_Length]);
                                command.Parameters.AddWithValue("@b4_level", arrColumns[(int)Augusta_RES_Fields.Bedroom_4_Level]);
                                command.Parameters.AddWithValue("@b4_width", arrColumns[(int)Augusta_RES_Fields.Bedroom_4_Width]);
                                command.Parameters.AddWithValue("@b5_length", arrColumns[(int)Augusta_RES_Fields.Bedroom_5_Length]);
                                command.Parameters.AddWithValue("@b5_level", arrColumns[(int)Augusta_RES_Fields.Bedroom_5_Level]);
                                command.Parameters.AddWithValue("@b5_width", arrColumns[(int)Augusta_RES_Fields.Bedroom_5_Width]);
                                command.Parameters.AddWithValue("@bedrooms", arrColumns[(int)Augusta_RES_Fields.Bedrooms]);
                                command.Parameters.AddWithValue("@breakfast_length", arrColumns[(int)Augusta_RES_Fields.Breakfast_Rm_Length]);
                                command.Parameters.AddWithValue("@breakfast_level", arrColumns[(int)Augusta_RES_Fields.Breakfast_Rm_Level]);
                                command.Parameters.AddWithValue("@breakfast_width", arrColumns[(int)Augusta_RES_Fields.Breakfast_Rm_Width]);
                                command.Parameters.AddWithValue("@dining_length", arrColumns[(int)Augusta_RES_Fields.Dining_Rm_Length]);
                                command.Parameters.AddWithValue("@dining_level", arrColumns[(int)Augusta_RES_Fields.Dining_Rm_Level]);
                                command.Parameters.AddWithValue("@dining_width", arrColumns[(int)Augusta_RES_Fields.Dining_Rm_Width]);
                                command.Parameters.AddWithValue("@driveway", arrColumns[(int)Augusta_RES_Fields.Driveway]);
                                command.Parameters.AddWithValue("@ext_features", arrColumns[(int)Augusta_RES_Fields.Exterior_Features]);
                                command.Parameters.AddWithValue("@ext_finish", arrColumns[(int)Augusta_RES_Fields.Exterior_Finish]);
                                command.Parameters.AddWithValue("@extra_rooms", arrColumns[(int)Augusta_RES_Fields.Extra_Rooms]);
                                command.Parameters.AddWithValue("@family_length", arrColumns[(int)Augusta_RES_Fields.Family_Rm_Length]);
                                command.Parameters.AddWithValue("@family_level", arrColumns[(int)Augusta_RES_Fields.Family_Rm_Level]);
                                command.Parameters.AddWithValue("@family_width", arrColumns[(int)Augusta_RES_Fields.Family_Rm_Width]);
                                command.Parameters.AddWithValue("@financing", arrColumns[(int)Augusta_RES_Fields.Financing_Type]);
                                command.Parameters.AddWithValue("@fireplace", arrColumns[(int)Augusta_RES_Fields.Number_Fireplaces]);
                                command.Parameters.AddWithValue("@foundation", arrColumns[(int)Augusta_RES_Fields.Foundation_Basement]);
                                command.Parameters.AddWithValue("@flooring", arrColumns[(int)Augusta_RES_Fields.Flooring]);
                                command.Parameters.AddWithValue("@garage", arrColumns[(int)Augusta_RES_Fields.Garage_Carport]);
                                command.Parameters.AddWithValue("@great_length", arrColumns[(int)Augusta_RES_Fields.Great_Rm_Length]);
                                command.Parameters.AddWithValue("@great_level", arrColumns[(int)Augusta_RES_Fields.Great_Rm_Level]);
                                command.Parameters.AddWithValue("@great_width", arrColumns[(int)Augusta_RES_Fields.Great_Rm_Width]);
                                command.Parameters.AddWithValue("@baths_half", arrColumns[(int)Augusta_RES_Fields.Half_Baths]);
                                command.Parameters.AddWithValue("@heat", arrColumns[(int)Augusta_RES_Fields.Heat_Delivery]);
                                command.Parameters.AddWithValue("@int_features", arrColumns[(int)Augusta_RES_Fields.Interior_Features]);
                                command.Parameters.AddWithValue("@kitchen_length", arrColumns[(int)Augusta_RES_Fields.Kitchen_Length]);
                                command.Parameters.AddWithValue("@kitchen_level", arrColumns[(int)Augusta_RES_Fields.Kitchen_Level]);
                                command.Parameters.AddWithValue("@kitchen_width", arrColumns[(int)Augusta_RES_Fields.Kitchen_Width]);
                                command.Parameters.AddWithValue("@price_list", arrColumns[(int)Augusta_RES_Fields.List_Price]);
                                command.Parameters.AddWithValue("@list_agentid", strAgentId);
                                command.Parameters.AddWithValue("@list_agentname", DBNull.Value);
                                command.Parameters.AddWithValue("@list_office", arrColumns[(int)Augusta_RES_Fields.Listing_Office]);
                                command.Parameters.AddWithValue("@list_firmid", DBNull.Value);
                                command.Parameters.AddWithValue("@living_length", arrColumns[(int)Augusta_RES_Fields.Living_Rm_Length]);
                                command.Parameters.AddWithValue("@living_level", arrColumns[(int)Augusta_RES_Fields.Living_Rm_Level]);
                                command.Parameters.AddWithValue("@living_width", arrColumns[(int)Augusta_RES_Fields.Living_Rm_Width]);
                                command.Parameters.AddWithValue("@master_length", arrColumns[(int)Augusta_RES_Fields.Owner_Bedroom_Length]);
                                command.Parameters.AddWithValue("@master_level", arrColumns[(int)Augusta_RES_Fields.Owner_Bedroom_Level]);
                                command.Parameters.AddWithValue("@master_width", arrColumns[(int)Augusta_RES_Fields.Owner_Bedroom_Width]);
                                command.Parameters.AddWithValue("@propid", arrColumns[(int)Augusta_RES_Fields.MLS_Number]);
                                command.Parameters.AddWithValue("@amenities", arrColumns[(int)Augusta_RES_Fields.Neighborhood_Amenities]);
                                command.Parameters.AddWithValue("@new_cons", arrColumns[(int)Augusta_RES_Fields.New_Construction]);
                                command.Parameters.AddWithValue("@photo_count", arrColumns[arrColumns.Length - 1]);
                                command.Parameters.AddWithValue("@pool", arrColumns[(int)Augusta_RES_Fields.Pool]);
                                command.Parameters.AddWithValue("@porch", DBNull.Value);
                                command.Parameters.AddWithValue("@prop_age", DBNull.Value);
                                command.Parameters.AddWithValue("@public_remarks", arrColumns[(int)Augusta_RES_Fields.Property_Description]);
                                command.Parameters.AddWithValue("@roof", arrColumns[(int)Augusta_RES_Fields.Roof]);
                                command.Parameters.AddWithValue("@school_elem", arrColumns[(int)Augusta_RES_Fields.Elementary_School]);
                                command.Parameters.AddWithValue("@school_mid", arrColumns[(int)Augusta_RES_Fields.Middle_School]);
                                command.Parameters.AddWithValue("@school_high", arrColumns[(int)Augusta_RES_Fields.High_School]);
                                command.Parameters.AddWithValue("@sewer", arrColumns[(int)Augusta_RES_Fields.Sewer]);
                                command.Parameters.AddWithValue("@show_instr", arrColumns[(int)Augusta_RES_Fields.Showing_Instructions]);
                                command.Parameters.AddWithValue("@sqft_total", arrColumns[(int)Augusta_RES_Fields.Apx_Total_Heated_SqFt]);
                                command.Parameters.AddWithValue("@status", arrColumns[(int)Augusta_RES_Fields.Property_Status]);
                                command.Parameters.AddWithValue("@style", arrColumns[(int)Augusta_RES_Fields.Style]);
                                command.Parameters.AddWithValue("@water", arrColumns[(int)Augusta_RES_Fields.Water]);
                                command.Parameters.AddWithValue("@year_built", arrColumns[(int)Augusta_RES_Fields.Apx_Year_Built]);
                                command.Parameters.AddWithValue("@prop_type", "R");
                                command.Parameters.AddWithValue("@directions", arrColumns[(int)Augusta_RES_Fields.Directions]);
                                command.Parameters.AddWithValue("@builder", arrColumns[(int)Augusta_RES_Fields.Builder_Name]);
                                command.Parameters.AddWithValue("@vt_url", arrColumns[(int)Augusta_RES_Fields.Virtual_Tour]);
                                command.Parameters.AddWithValue("@dom", DBNull.Value);
                                command.Parameters.AddWithValue("@list_date", DBNull.Value);
                                command.Parameters.AddWithValue("@lot_desc", arrColumns[(int)Augusta_RES_Fields.Lot_Description]);

                                commandSupport.Parameters.AddWithValue("@propid", arrColumns[(int)Augusta_RES_Fields.MLS_Number]);
                                commandSupport.Parameters.AddWithValue("@fireplace_num", arrColumns[(int)Augusta_RES_Fields.Number_Fireplaces]);
                                commandSupport.Parameters.AddWithValue("@total_rooms", arrColumns[(int)Augusta_RES_Fields.Total_Number_Rooms]);
                                commandSupport.Parameters.AddWithValue("@acres_total", arrColumns[(int)Augusta_RES_Fields.Total_Acres]);

                                break;
                            case FeedType.Agent:
                                strCommand = @" INSERT INTO agents (agentid,email,name_first,name_last,phone_office,officeid,address,web) 
			                                VALUES (@agent_id,@agent_email,@agent_first,@agent_last,@agent_phone,@agent_office,@agent_address,@agent_web)";
                                strCommandSupport = @"  INSERT INTO aux_agents (agentid,phone_home,address,city,state,zip) 
			                                        VALUES (@agent_id,@agent_home,@agent_street,@agent_city,@agent_state,@agent_zip)";

                                command.CommandText = strCommand;
                                commandSupport.CommandText = strCommandSupport;

                                command.Parameters.AddWithValue("@agent_id", arrColumns[(int)Augusta_Agent_Fields.AGENT_ID]);
                                command.Parameters.AddWithValue("@agent_email", arrColumns[(int)Augusta_Agent_Fields.Agent_Email]);
                                command.Parameters.AddWithValue("@agent_first", arrColumns[(int)Augusta_Agent_Fields.First_Name]);
                                command.Parameters.AddWithValue("@agent_last", arrColumns[(int)Augusta_Agent_Fields.Last_Name]);
                                command.Parameters.AddWithValue("@agent_phone", arrColumns[(int)Augusta_Agent_Fields.Home]);
                                command.Parameters.AddWithValue("@agent_office", arrColumns[(int)Augusta_Agent_Fields.Office_ID]);
                                command.Parameters.AddWithValue("@agent_address", arrColumns[(int)Augusta_Agent_Fields.Mail_Address_1]);
                                command.Parameters.AddWithValue("@agent_web", arrColumns[(int)Augusta_Agent_Fields.Web_Address]);

                                commandSupport.Parameters.AddWithValue("@agent_id", arrColumns[(int)Augusta_Agent_Fields.AGENT_ID]);
                                commandSupport.Parameters.AddWithValue("@agent_home", arrColumns[(int)Augusta_Agent_Fields.Home]);
                                commandSupport.Parameters.AddWithValue("@agent_street", arrColumns[(int)Augusta_Agent_Fields.Mail_Address_1]);
                                commandSupport.Parameters.AddWithValue("@agent_city", arrColumns[(int)Augusta_Agent_Fields.Mail_City]);
                                commandSupport.Parameters.AddWithValue("@agent_state", arrColumns[(int)Augusta_Agent_Fields.Mail_State]);
                                commandSupport.Parameters.AddWithValue("@agent_zip", arrColumns[(int)Augusta_Agent_Fields.Mail_Zip_Code]);

                                break;
                            case FeedType.Office:
                                strCommand = @" INSERT INTO offices (office_id,office_name,office_phone,address,url) 
			                                VALUES (@office_id,@office_name,@office_phone,@office_address,@office_web)";
                                strCommandSupport = @"  INSERT INTO aux_offices (officeid,address,city,state,zip,fax) 
			                                        VALUES (@office_id,@office_street,@office_city,@office_state,@office_zip,@office_fax)";

                                command.CommandText = strCommand;
                                commandSupport.CommandText = strCommandSupport;

                                command.Parameters.AddWithValue("@office_id", arrColumns[(int)Office_Fields.Office_ID]);
                                command.Parameters.AddWithValue("@office_name", arrColumns[(int)Office_Fields.Office_Name]);
                                command.Parameters.AddWithValue("@office_phone", arrColumns[(int)Office_Fields.Main]);
                                command.Parameters.AddWithValue("@office_address", arrColumns[(int)Office_Fields.Mail_Address_1]);
                                command.Parameters.AddWithValue("@office_web", arrColumns[(int)Office_Fields.Web_Address]);

                                commandSupport.Parameters.AddWithValue("@office_id", arrColumns[(int)Office_Fields.Office_ID]);
                                commandSupport.Parameters.AddWithValue("@office_street", arrColumns[(int)Office_Fields.Mail_Address_1]);
                                commandSupport.Parameters.AddWithValue("@office_city", arrColumns[(int)Office_Fields.Mail_City]);
                                commandSupport.Parameters.AddWithValue("@office_state", arrColumns[(int)Office_Fields.Mail_State]);
                                commandSupport.Parameters.AddWithValue("@office_zip", arrColumns[(int)Office_Fields.Mail_Zip_Code]);
                                commandSupport.Parameters.AddWithValue("@office_fax", arrColumns[(int)Office_Fields.Fax]);

                                break;
                        }
                    }

                    try
                    {
                        //connection.Open();
                        command.ExecuteNonQuery();
                        commandSupport.ExecuteNonQuery();
                        //connection.Close();
                    }
                    catch (MySqlException ex)
                    {
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running MySQL Export SQL - Details: " + ex.Message + "</i></b>");
                    }
                }
            }
            catch (Exception ex)
            {
                if (!this.blnIsIncremental)
                    bFatalError = true;
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running ExportMySQLData: " + ex.Message + "</i></b>");
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running ExportMySQLData - Details: " + ex.StackTrace + "</i></b>");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strSource"></param>
        /// <returns></returns>
        private string FixSentenceCase(string strSource)
        {
            var lowerCase = strSource.ToLower();
            var r = new Regex(@"(^[a-z])|\.\s+(.)", RegexOptions.ExplicitCapture);
            var result = r.Replace(lowerCase, s => s.Value.ToUpper());

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strSource"></param>
        /// <returns></returns>
        private string FixTitleCase(string strSource)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(strSource.ToLower());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strFolder"></param>
        /// <param name="blnIsIncremental"></param>
        /// <returns></returns>
        public string[] GetFilesList(CityType intCityType, FeedType intFeedType)
        {
            string strFolder = intCityType == CityType.Aiken ? Constant.AIKEN_DOWNLOAD_FOLDER : Constant.AUGUSTA_DOWNLOAD_FOLDER;
            string[] arrFilePaths = Directory.GetFiles(strFolder, "*.csv");
            List<string> listFilePaths = new List<string>();
            
            for(int intIndex = 0; intIndex < arrFilePaths.Length; intIndex++)
            {
                if(arrFilePaths[intIndex].Contains("Inc.csv") && this.blnIsIncremental)
                {
                    if ((intFeedType == FeedType.Residential && arrFilePaths[intIndex].Contains("_RES"))
                     || (intFeedType == FeedType.Land && arrFilePaths[intIndex].Contains("_LAND")))
                    {
                        listFilePaths.Add(arrFilePaths[intIndex]);
                    }
                }
                else if (!arrFilePaths[intIndex].Contains("Inc.csv") && !this.blnIsIncremental)
                {
                    if ((intFeedType == FeedType.Residential && arrFilePaths[intIndex].Contains("_RES"))
                     || (intFeedType == FeedType.Land && arrFilePaths[intIndex].Contains("_LAND")))
                    {
                        listFilePaths.Add(arrFilePaths[intIndex]);
                    }
                    else if (intFeedType == FeedType.Agent && arrFilePaths[intIndex].Contains("_Agents"))
                    {
                        listFilePaths.Add(arrFilePaths[intIndex]);
                    }
                    else if (intFeedType == FeedType.Office && arrFilePaths[intIndex].Contains("_Offices"))
                    {
                        listFilePaths.Add(arrFilePaths[intIndex]);
                    }
                }
            }

            if (!this.blnIsIncremental)
            {
                if (Constant.FILE_COUNT.ContainsKey(intCityType + "_" + intFeedType)) { 
                    if (Constant.FILE_COUNT[intCityType + "_" + intFeedType] != listFilePaths.Count)
                    {
                        if (!this.blnIsIncremental)
                            bFatalError = true;
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Missing file for " + Enum.GetName(typeof(CityType), intCityType) + " " + Enum.GetName(typeof(FeedType), intFeedType) + "</i></b>");
                        this.bBlockPropertyPurge = true;
                    }
                }
            }

            return listFilePaths.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strMLSID"></param>
        /// <param name="strCoordinates"></param>
        private string[] GetMLSGeolocation(string strMLSID, MLSType intMLSType)
        {
            string strCommand = string.Format(@" SELECT Latitude, Longitude 
                                                 FROM NavicaGeocodeCoordinates
                                                 WHERE MLSID = '{0}'
                                                 AND MLSType = {1} ", strMLSID, (int)intMLSType);
            string[] strCoordinates = new string[3];

            using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["MeybohmServer"].ConnectionString))
            {
                using (SqlCommand command = new SqlCommand(strCommand, connection))
                {
                    command.CommandTimeout = 300;
                    connection.Open();

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            strCoordinates[0] = reader["Latitude"].ToString();
                            strCoordinates[1] = reader["Longitude"].ToString();
                        }
                        else
                        {
                            strCoordinates = null;
                        }
                    }

                    connection.Close();
                }
            }

            return strCoordinates;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strMLSID"></param>
        /// <param name="strPhotoLocations"></param>
        /// <param name="intMLSType"></param>
        public void ImportPropertyPhotos(string strMLSID, string strPhotoLocations, MLSType intFeedType)
        {
            string[] arrPhotoLocations = strPhotoLocations.Split(',');
            string strCommand = string.Format(@" INSERT INTO photo_links_aik (link,propid,label,sequence,timestamp,portrait) 
                                                 VALUES (@photo_url,@propid,'',@photo_seq,@time,'False')");

            if(intFeedType == MLSType.Augusta)
            {
                strCommand = string.Format(@" INSERT INTO photo_links_aug (link,propid,label,sequence,timestamp,portrait) 
                                                 VALUES (@photo_url,@propid,'',@photo_seq,@time,'False')");
            }

            //using (MySqlConnection connection = new MySqlConnection(ConfigurationManager.ConnectionStrings["MySQLServer"].ConnectionString))
            if (connMySqlConnection.State == ConnectionState.Open)
            {
                for (int intIndex = 0; intIndex < arrPhotoLocations.Length; intIndex++)
                {
                    if (!string.IsNullOrEmpty(arrPhotoLocations[intIndex]))
                    {
                        using (MySqlCommand command = new MySqlCommand(strCommand, connMySqlConnection))
                        {
                            command.CommandTimeout = 300;

                            command.Parameters.AddWithValue("@photo_url", arrPhotoLocations[intIndex]);
                            command.Parameters.AddWithValue("@photo_seq", (intIndex + 1));
                            command.Parameters.AddWithValue("@propid", strMLSID);
                            command.Parameters.AddWithValue("@time", DateTime.Now.ToString("G"));

                            //connection.Open();
                            command.ExecuteNonQuery();
                            //connection.Close();
                        }


                    }
                }
            }

            //Check if photos are expired
            /*
            if (Constant.PHOTO_TEST.ContainsKey(strMLSID)) { 
                for (int intIndex = 0; intIndex < arrPhotoLocations.Length; intIndex++)
                {
                    try
                    {
                        WebRequest webRequest = WebRequest.Create(arrPhotoLocations[intIndex]);
                        WebResponse webResponse = webRequest.GetResponse();
                        string strWebResponse = webResponse.Headers["Content-Disposition"];
                        string strFile = strWebResponse.Substring(strWebResponse.IndexOf("filename=") + "filename=".Length).Trim();
                        string strExpiredFileName = "photo_expired.gif";

                        //Check if this is the expired file
                        if (strFile == strExpiredFileName)
                        {
                            this.WriteToLog("<br /><b><i style=\"color:red;\">Photo Expired (" + arrPhotoLocations[intIndex] + ").</i></b>");
                        }
                    }
                    catch (WebException ex)
                    {
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Error in WebResponse for Photo (" + arrPhotoLocations[intIndex] + "): " + ex.Message + ".</i></b>");
                    }
                    catch (Exception ex)
                    {
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Error in Request for Photo (" + arrPhotoLocations[intIndex] + "): " + ex.Message + ".</i></b>");
                    }
                }
            }
             */
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="strAddress"></param>
        /// <param name="strCity"></param>
        /// <param name="strState"></param>
        /// <param name="strZipCode"></param>
        private string[] MapAddress(string strMLSID, string strStreetNumber, string strStreetAddress, string strCity, string strState, string strZipCode, MLSType intMLSType)
        {
            string strFullAddress = string.Format("{0} {1}, {2}, {3}, {4}", strStreetNumber, strStreetAddress, strCity, strState, strZipCode);
            string strRequestURI = string.Format("http://maps.googleapis.com/maps/api/geocode/xml?address={0}&sensor=false", Uri.EscapeDataString(strFullAddress));
            string[] strCoordinates = new string[3];

            XElement eleResult = null;
            XElement eleStatus = null;
            XElement eleErrorMessage = null;
            XElement eleLocation = null;
            XElement eleLatitude = null;
            XElement eleLongitude = null;

            try
            {
                WebRequest webRequest = WebRequest.Create(strRequestURI);
                WebResponse webResponse = webRequest.GetResponse();
                XDocument xDocument = XDocument.Load(webResponse.GetResponseStream());

                eleResult = xDocument.Element("GeocodeResponse").Element("result");
                eleStatus = xDocument.Element("GeocodeResponse").Element("status");
                eleErrorMessage = xDocument.Element("GeocodeResponse").Element("error_message");
                eleLocation = eleResult.Element("geometry").Element("location");
                eleLatitude = eleLocation.Element("lat");
                eleLongitude = eleLocation.Element("lng");
                
                strCoordinates[0] = eleLatitude.Value;
                strCoordinates[1] = eleLongitude.Value;

                AddMLSGeolocation(strMLSID, strCoordinates, intMLSType);
            }
            catch(WebException ex)
            {
                this.WriteToLog("<br /><b><i style=\"color:red;\">Error in Webresponse for MLSID (" + strMLSID + "): Skipping MLSID.</i></b>");
            }
            catch (Exception ex)
            {
                if (eleStatus.Value == Constant.GOOGLE_API_STATUS_OVER_LIMIT)
                {
                    if (eleErrorMessage.Value == "You have exceeded your rate-limit for this API.")
                    {
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Error Geocoding MLSID (" + strMLSID + "): Rate-limit exceeded, waiting one second to resume...</i></b>");
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Error Geocoding MLSID (" + strMLSID + "): GOOGLE API GEOCODING DAILY QUOTA HAS BEEN REACHED</i></b>");
                        this.WriteToLog("<br /><b><i style=\"color:red;\">NOTE: SKIPPING GEOCODING PROCESS FOR NEW PROPERTIES</i></b>");
                        this.blnGoogleMapsOverLimit = true;
                    }
                }
                else if (eleStatus.Value == Constant.GOOGLE_API_ZERO_RESULTS)
                {
                    this.WriteToLog("<br /><b><i style=\"color:red;\">Error Geocoding MLSID (" + strMLSID + "): Unable to geocode property address (" + strFullAddress + ").</i></b>");
                }
                else
                {
                    this.WriteToLog("<br /><b><i style=\"color:red;\">Error Geocoding MLSID (" + strMLSID + "): " + ex.Message + "</i></b>");
                    this.WriteToLog("<br /><b><i style=\"color:red;\">Error Geocoding MLSID (" + strMLSID + ") Details: " + ex.StackTrace + "</i></b>");
                }

                strCoordinates = null;
                this.blnErrorsFound = true;
            }

            return strCoordinates;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strFile"></param>
        private void MoveFile(string strFile)
        {
            int intCount = 0;
            string strTempFile = strFile.Replace(Constant.AIKEN_DOWNLOAD_FOLDER, Constant.AIKEN_ARCHIVE_FOLDER).Replace(Constant.AUGUSTA_DOWNLOAD_FOLDER, Constant.AUGUSTA_ARCHIVE_FOLDER);

            if(this.blnIsIncremental)
            {
                if(strFile.Contains("Inc"))
                {
                    File.Delete(strFile);
                    this.WriteToLog("<br />Deleting: <b>" + strFile + "</b>");
                }
            }
            else
            {
                while (File.Exists(strTempFile))
                {
                    if (DateTime.Now > File.GetCreationTime(strTempFile).AddDays(3))
                    {
                        File.Delete(strTempFile);
                    }

                    intCount++;

                    strTempFile = strFile + "_" + intCount;
                    strTempFile = strTempFile.Replace(Constant.AIKEN_DOWNLOAD_FOLDER, Constant.AIKEN_ARCHIVE_FOLDER).Replace(Constant.AUGUSTA_DOWNLOAD_FOLDER, Constant.AUGUSTA_ARCHIVE_FOLDER);
                }

                this.WriteToLog("<br />Moving: <b>" + strFile + "</b> to <b>" + strTempFile + "</b>");
                File.Move(strFile, strTempFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arrFilePaths"></param>
        public void ProcessAikenFiles(string[] arrFilePaths, FeedType intFeedType)
        {
            string strConsolidationFolder = Constant.CONSOLIDATION_FOLDER;
            string strArchiveFolder = Constant.AIKEN_ARCHIVE_FOLDER;
            string strFileName, strFullFilePath;
            string strFeedType;
            string[] strGeoLocation;
            bool blnWriteHeader = false;
            bool blnSkipHeader = true;
            bool bRecordFound = false;

            string[] arrColumns = null;
            string[] arrColumnHeaders = null;

            if (arrFilePaths.Length == 0)
            {
                strFeedType = "";

                switch(intFeedType)
                {
                    case FeedType.Agent:
                        strFeedType = "Agent";
                        break;
                    case FeedType.Land:
                        strFeedType = "Land";
                        break;
                    case FeedType.Office:
                        strFeedType = "Office";
                        break;
                    case FeedType.Residential:
                        strFeedType = "Residential";
                        break;
                }

                this.WriteToLog(String.Format("<br /><b>No Files Found for Aiken {0}.</b>", strFeedType));
                return;
            }

            // Determine the columns for the file feeds
            strFileName = "";
            if (intFeedType == FeedType.Residential || intFeedType == FeedType.Land)
            {
                arrColumns = Constant.AIKEN_RESIDENTIAL_HEADER.Split(',');
                arrColumnHeaders = Constant.AIKEN_RESIDENTIAL_HEADER.Split(',');
                strFileName = "Meybohm-Aiken-ALL" + (this.blnIsIncremental ? "" : "Full") + ".csv";
            }
            else if (intFeedType == FeedType.Agent)
            {
                arrColumns = Constant.AIKEN_AGENT_HEADER.Split(',');
                arrColumnHeaders = Constant.AIKEN_AGENT_HEADER.Split(',');
                strFileName = "Meybohm-Aiken-Agents.csv";
            }
            else if (intFeedType == FeedType.Office)
            {
                arrColumns = Constant.OFFICE_HEADER.Split(',');
                arrColumnHeaders = Constant.OFFICE_HEADER.Split(',');
                strFileName = "Meybohm-Aiken-Offices.csv";
            }

            strFullFilePath = Constant.CONSOLIDATION_FOLDER + strFileName;

            // If the file does not already exist, write out the headers
            if (!File.Exists(strFullFilePath))
            {
                blnWriteHeader = true;
            }

            // Start writing the file
            using (StreamWriter file = new StreamWriter(strFullFilePath, true))
            {
                if(blnWriteHeader)
                {
                    // Write out the columns in sequential order, the columns should have been defined sequentially
                    for (int intIndex = 0; intIndex < arrColumns.Length; intIndex++)
                    {
                        file.Write("\"" + arrColumns[intIndex] + "\"");

                        if (intIndex < arrColumns.Length - 1)
                        {
                            file.Write(",");
                        }
                        else
                        {
                            file.WriteLine("");
                        }
                    }

                    blnWriteHeader = false;
                }

                // For each file, go through and download/pull the fields
                for (int intFileIndex = 0; intFileIndex < arrFilePaths.Length; intFileIndex++)
                {
                    blnSkipHeader = true;

                    bRecordFound = false;

                    using (TextFieldParser parser = new TextFieldParser(arrFilePaths[intFileIndex]))
                    {
                        parser.Delimiters = new string[] { "," };

                        while (true)
                        {
                            string[] BoundaryFields = parser.ReadFields();
                            strGeoLocation = null;

                            if (BoundaryFields == null || BoundaryFields.Length == 0)
                            {
                                break;
                            }

                            // Skip the first line of the file (header)
                            if (blnSkipHeader)
                            {
                                blnSkipHeader = false;
                            }
                            else
                            {
                                // Clear out old values in the columns array
                                for (int intIndex = 0; intIndex < arrColumns.Length; intIndex++)
                                {
                                    arrColumns[intIndex] = "";
                                }

                                try 
                                { 
                                    if (intFeedType == FeedType.Residential)
                                    {
                                        arrColumns[(int)Aiken_RES_Fields.Address] = BoundaryFields[(int)Aiken_RES_Fields.Address];
                                        arrColumns[(int)Aiken_RES_Fields.Air_Conditioning] = BoundaryFields[(int)Aiken_RES_Fields.Air_Conditioning];
                                        arrColumns[(int)Aiken_RES_Fields.Apx_Heated_SqFt] = BoundaryFields[(int)Aiken_RES_Fields.Apx_Heated_SqFt];
                                        arrColumns[(int)Aiken_RES_Fields.Attic] = BoundaryFields[(int)Aiken_RES_Fields.Attic];
                                        arrColumns[(int)Aiken_RES_Fields.Bedrooms] = BoundaryFields[(int)Aiken_RES_Fields.Bedrooms];
                                        arrColumns[(int)Aiken_RES_Fields.Builder_Name] = BoundaryFields[(int)Aiken_RES_Fields.Builder_Name];
                                        arrColumns[(int)Aiken_RES_Fields.City] = BoundaryFields[(int)Aiken_RES_Fields.City];
                                        arrColumns[Array.IndexOf(arrColumnHeaders, "County")] = BoundaryFields[(int)Aiken_RES_Fields.County];
                                        arrColumns[(int)Aiken_RES_Fields.Directions] = BoundaryFields[(int)Aiken_RES_Fields.Directions];
                                        arrColumns[(int)Aiken_RES_Fields.Elementary_School] = BoundaryFields[(int)Aiken_RES_Fields.Elementary_School];
                                        arrColumns[(int)Aiken_RES_Fields.Exterior_Features] = BoundaryFields[(int)Aiken_RES_Fields.Exterior_Features];
                                        arrColumns[(int)Aiken_RES_Fields.Floors] = BoundaryFields[(int)Aiken_RES_Fields.Floors];
                                        arrColumns[(int)Aiken_RES_Fields.Foundation_Basement] = BoundaryFields[(int)Aiken_RES_Fields.Foundation_Basement];
                                        arrColumns[(int)Aiken_RES_Fields.Full_Baths] = BoundaryFields[(int)Aiken_RES_Fields.Full_Baths];
                                        arrColumns[(int)Aiken_RES_Fields.Garage] = BoundaryFields[(int)Aiken_RES_Fields.Garage];
                                        arrColumns[(int)Aiken_RES_Fields.Half_Baths] = BoundaryFields[(int)Aiken_RES_Fields.Half_Baths];
                                        arrColumns[(int)Aiken_RES_Fields.High_School] = BoundaryFields[(int)Aiken_RES_Fields.High_School];
                                        arrColumns[(int)Aiken_RES_Fields.Interior_Features] = BoundaryFields[(int)Aiken_RES_Fields.Interior_Features];
                                        arrColumns[(int)Aiken_RES_Fields.LA_ID] = BoundaryFields[(int)Aiken_RES_Fields.LA_ID];
                                        arrColumns[(int)Aiken_RES_Fields.Latitude] = BoundaryFields[(int)Aiken_RES_Fields.Latitude];
                                        arrColumns[(int)Aiken_RES_Fields.List_Date] = BoundaryFields[(int)Aiken_RES_Fields.List_Date];
                                        arrColumns[(int)Aiken_RES_Fields.List_Price] = BoundaryFields[(int)Aiken_RES_Fields.List_Price];
                                        arrColumns[(int)Aiken_RES_Fields.Listing_Office] = BoundaryFields[(int)Aiken_RES_Fields.Listing_Office];
                                        arrColumns[(int)Aiken_RES_Fields.Longitude] = BoundaryFields[(int)Aiken_RES_Fields.Longitude];
                                        arrColumns[(int)Aiken_RES_Fields.Middle_School] = BoundaryFields[(int)Aiken_RES_Fields.Middle_School];
                                        arrColumns[(int)Aiken_RES_Fields.MLS_Number] = BoundaryFields[(int)Aiken_RES_Fields.MLS_Number];
                                        arrColumns[(int)Aiken_RES_Fields.New_Construction] = BoundaryFields[(int)Aiken_RES_Fields.New_Construction];
                                        arrColumns[Array.IndexOf(arrColumnHeaders, "Photo Location")] = BoundaryFields[(int)Aiken_RES_Fields.Photo_Location];
                                        arrColumns[(int)Aiken_RES_Fields.Property_Description] = BoundaryFields[(int)Aiken_RES_Fields.Property_Description];
                                        arrColumns[(int)Aiken_RES_Fields.Property_Status] = BoundaryFields[(int)Aiken_RES_Fields.Property_Status];
                                        arrColumns[(int)Aiken_RES_Fields.Property_Type] = BoundaryFields[(int)Aiken_RES_Fields.Property_Type];
                                        arrColumns[(int)Aiken_RES_Fields.State] = BoundaryFields[(int)Aiken_RES_Fields.State];
                                        arrColumns[(int)Aiken_RES_Fields.Street_Number] = BoundaryFields[(int)Aiken_RES_Fields.Street_Number];
                                        arrColumns[(int)Aiken_RES_Fields.Style] = BoundaryFields[(int)Aiken_RES_Fields.Style];
                                        arrColumns[(int)Aiken_RES_Fields.Total_Acres] = BoundaryFields[(int)Aiken_RES_Fields.Total_Acres];
                                        arrColumns[(int)Aiken_RES_Fields.Town_Subdivision] = BoundaryFields[(int)Aiken_RES_Fields.Town_Subdivision];
                                        arrColumns[(int)Aiken_RES_Fields.Virtual_Tour] = BoundaryFields[(int)Aiken_RES_Fields.Virtual_Tour];
                                        arrColumns[(int)Aiken_RES_Fields.Year_Built] = BoundaryFields[(int)Aiken_RES_Fields.Year_Built];
                                        arrColumns[(int)Aiken_RES_Fields.Zip_Code] = BoundaryFields[(int)Aiken_RES_Fields.Zip_Code];

                                        // Set Equestrian
                                        /*
                                         *  *   HORSES ALLOWED is set to "Y"
                                            *   LOT DESCRIPTION includes Pasture Land
                                            -   EXTERIOR FEATURES includes includes Barn or Stables
                                         */
                                        arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "";
                                        if (BoundaryFields[(int)Aiken_RES_Fields.Horses_Allowed].ToLower() == "y")
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_RES_Fields.Lot_Description].ToLower().IndexOf("pasture land") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_RES_Fields.Exterior_Features].ToLower().IndexOf("barn") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_RES_Fields.Exterior_Features].ToLower().IndexOf("stables") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                    }
                                    else if (intFeedType == FeedType.Land)
                                    {
                                        arrColumns[(int)Aiken_RES_Fields.Address] = BoundaryFields[(int)Aiken_LAND_Fields.Address];
                                        arrColumns[(int)Aiken_RES_Fields.Apx_Heated_SqFt] = BoundaryFields[(int)Aiken_LAND_Fields.Apx_Heated_SqFt];
                                        arrColumns[(int)Aiken_RES_Fields.Total_Acres] = BoundaryFields[(int)Aiken_LAND_Fields.Apx_Total_Acreage];
                                        arrColumns[(int)Aiken_RES_Fields.City] = BoundaryFields[(int)Aiken_LAND_Fields.City];
                                        arrColumns[Array.IndexOf(arrColumnHeaders, "County")] = BoundaryFields[(int)Aiken_LAND_Fields.County];
                                        arrColumns[(int)Aiken_RES_Fields.Directions] = BoundaryFields[(int)Aiken_LAND_Fields.Directions];
                                        arrColumns[(int)Aiken_RES_Fields.Elementary_School] = BoundaryFields[(int)Aiken_LAND_Fields.Elementary_School];
                                        arrColumns[(int)Aiken_RES_Fields.High_School] = BoundaryFields[(int)Aiken_LAND_Fields.High_School];
                                        arrColumns[(int)Aiken_RES_Fields.LA_ID] = BoundaryFields[(int)Aiken_LAND_Fields.LA_ID];
                                        arrColumns[(int)Aiken_RES_Fields.Latitude] = BoundaryFields[(int)Aiken_LAND_Fields.Latitude];
                                        arrColumns[(int)Aiken_RES_Fields.List_Date] = BoundaryFields[(int)Aiken_LAND_Fields.List_Date];
                                        arrColumns[(int)Aiken_RES_Fields.List_Price] = BoundaryFields[(int)Aiken_LAND_Fields.List_Price];
                                        arrColumns[(int)Aiken_RES_Fields.Listing_Office] = BoundaryFields[(int)Aiken_LAND_Fields.Listing_Office];
                                        arrColumns[(int)Aiken_RES_Fields.Longitude] = BoundaryFields[(int)Aiken_LAND_Fields.Longitude];
                                        arrColumns[(int)Aiken_RES_Fields.Middle_School] = BoundaryFields[(int)Aiken_LAND_Fields.Middle_School];
                                        arrColumns[(int)Aiken_RES_Fields.MLS_Number] = BoundaryFields[(int)Aiken_LAND_Fields.MLS_Number];
                                        arrColumns[(int)Aiken_RES_Fields.New_Construction] = BoundaryFields[(int)Aiken_LAND_Fields.New_Construction];
                                        arrColumns[Array.IndexOf(arrColumnHeaders, "Photo Location")] = BoundaryFields[(int)Aiken_LAND_Fields.Photo_Location];
                                        arrColumns[(int)Aiken_RES_Fields.Property_Status] = BoundaryFields[(int)Aiken_LAND_Fields.Property_Status];
                                        arrColumns[(int)Aiken_RES_Fields.Property_Type] = BoundaryFields[(int)Aiken_LAND_Fields.Property_Type];
                                        arrColumns[(int)Aiken_RES_Fields.Property_Description] = BoundaryFields[(int)Aiken_LAND_Fields.Remarks];
                                        arrColumns[(int)Aiken_RES_Fields.State] = BoundaryFields[(int)Aiken_LAND_Fields.State];
                                        arrColumns[(int)Aiken_RES_Fields.Street_Number] = BoundaryFields[(int)Aiken_LAND_Fields.Street_Number];
                                        arrColumns[(int)Aiken_RES_Fields.Town_Subdivision] = BoundaryFields[(int)Aiken_LAND_Fields.Town_Subdivision];
                                        arrColumns[(int)Aiken_RES_Fields.Virtual_Tour] = BoundaryFields[(int)Aiken_LAND_Fields.Virtual_Tour];
                                        arrColumns[(int)Aiken_RES_Fields.Zip_Code] = BoundaryFields[(int)Aiken_LAND_Fields.Zip_Code];

                                        // Set Equestrian
                                        /*
                                         *    *   HORSES is not null or set to "None" (May contain: Allowed, Racing Stables, Training Track, Riding Stables, Family Use, Polo, or Other - See Remarks)
                                              *   PRESENT USE includes Horses, Horses & Other or Pasture
                                              *   BEST USE includes Horse & Equestrian or Grazing
                                              *   TOPOGRAPHY includes Grass/Pasture
                                              *   CROPS includes Coastal Bermuda
                                              *   BUILDINGS ON PROPERTY includes Barns, Stable or Hay Shed
                                              *   OTHER IMPROVEMENTS includes Paddocks, Run-In Shed, Training Track, Wash Rack, or Hay Storage Building
                                              *   AMENITIES includes Equine Riding Easement or Community Riding Area
                                         */

                                        arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "";
                                        if (!string.IsNullOrEmpty(BoundaryFields[(int)Aiken_LAND_Fields.Horses]) && BoundaryFields[(int)Aiken_LAND_Fields.Horses].ToLower() != "none")
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Present_Use].ToLower().IndexOf("horses") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Present_Use].ToLower().IndexOf("pasture") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Best_Use].ToLower().IndexOf("horse & equestrian") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Best_Use].ToLower().IndexOf("grazing") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Topography].ToLower().IndexOf("grass/pasture") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Crops].ToLower().IndexOf("coastal bermuda") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Buildings_On_Property].ToLower().IndexOf("barns") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Buildings_On_Property].ToLower().IndexOf("stable") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Buildings_On_Property].ToLower().IndexOf("hay shed") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Other_Improvements].ToLower().IndexOf("paddocks") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Other_Improvements].ToLower().IndexOf("run-in shed") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Other_Improvements].ToLower().IndexOf("training track") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Other_Improvements].ToLower().IndexOf("wash rack") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Other_Improvements].ToLower().IndexOf("hay storage building") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Amenities].ToLower().IndexOf("equine riding easement") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Aiken_LAND_Fields.Amenities].ToLower().IndexOf("community riding area") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                    }
                                    else if (intFeedType == FeedType.Agent)
                                    {
                                        arrColumns[(int)Aiken_Agent_Fields.Agent_Email] = BoundaryFields[(int)Aiken_Agent_Fields.Agent_Email];
                                        arrColumns[(int)Aiken_Agent_Fields.AGENT_ID] = BoundaryFields[(int)Aiken_Agent_Fields.AGENT_ID];
                                        arrColumns[(int)Aiken_Agent_Fields.Contact_Number] = BoundaryFields[(int)Aiken_Agent_Fields.Contact_Number];
                                        arrColumns[(int)Aiken_Agent_Fields.First_Name] = BoundaryFields[(int)Aiken_Agent_Fields.First_Name];
                                        arrColumns[(int)Aiken_Agent_Fields.Home] = BoundaryFields[(int)Aiken_Agent_Fields.Home];
                                        arrColumns[(int)Aiken_Agent_Fields.Last_Name] = BoundaryFields[(int)Aiken_Agent_Fields.Last_Name];
                                        arrColumns[(int)Aiken_Agent_Fields.Mail_Address_1] = BoundaryFields[(int)Aiken_Agent_Fields.Mail_Address_1];
                                        arrColumns[(int)Aiken_Agent_Fields.Mail_City] = BoundaryFields[(int)Aiken_Agent_Fields.Mail_City];
                                        arrColumns[(int)Aiken_Agent_Fields.Mail_State] = BoundaryFields[(int)Aiken_Agent_Fields.Mail_State];
                                        arrColumns[(int)Aiken_Agent_Fields.Mail_Zip_Code] = BoundaryFields[(int)Aiken_Agent_Fields.Mail_Zip_Code];
                                        arrColumns[(int)Aiken_Agent_Fields.Office_ID] = BoundaryFields[(int)Aiken_Agent_Fields.Office_ID];
                                        arrColumns[(int)Aiken_Agent_Fields.Web_Address] = BoundaryFields[(int)Aiken_Agent_Fields.Web_Address];
                                    }
                                    else if (intFeedType == FeedType.Office)
                                    {
                                        arrColumns[(int)Office_Fields.Fax] = BoundaryFields[(int)Office_Fields.Fax];
                                        arrColumns[(int)Office_Fields.Mail_Address_1] = BoundaryFields[(int)Office_Fields.Mail_Address_1];
                                        arrColumns[(int)Office_Fields.Mail_City] = BoundaryFields[(int)Office_Fields.Mail_City];
                                        arrColumns[(int)Office_Fields.Mail_State] = BoundaryFields[(int)Office_Fields.Mail_State];
                                        arrColumns[(int)Office_Fields.Mail_Zip_Code] = BoundaryFields[(int)Office_Fields.Mail_Zip_Code];
                                        arrColumns[(int)Office_Fields.Main] = BoundaryFields[(int)Office_Fields.Main];
                                        arrColumns[(int)Office_Fields.Office_ID] = BoundaryFields[(int)Office_Fields.Office_ID];
                                        arrColumns[(int)Office_Fields.Office_Name] = BoundaryFields[(int)Office_Fields.Office_Name];
                                        arrColumns[(int)Office_Fields.Web_Address] = BoundaryFields[(int)Office_Fields.Web_Address];
                                    }


                                    // Do transformations on RES/LAND Data
                                    if (intFeedType == FeedType.Land || intFeedType == FeedType.Residential)
                                    {
                                        // Fix Title Type Columns
                                        arrColumns[(int)Aiken_RES_Fields.Address] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Address]);
                                        arrColumns[(int)Aiken_RES_Fields.Town_Subdivision] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Town_Subdivision]);
                                        arrColumns[(int)Aiken_RES_Fields.City] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.City]);
                                        arrColumns[Array.IndexOf(arrColumnHeaders, "County")] = this.FixTitleCase(arrColumns[Array.IndexOf(arrColumnHeaders, "County")]);
                                        arrColumns[(int)Aiken_RES_Fields.Exterior_Features] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Exterior_Features]);
                                        arrColumns[(int)Aiken_RES_Fields.Interior_Features] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Interior_Features]);
                                        arrColumns[(int)Aiken_RES_Fields.Foundation_Basement] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Foundation_Basement]);
                                        arrColumns[(int)Aiken_RES_Fields.Floors] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Floors]);
                                        arrColumns[(int)Aiken_RES_Fields.Garage] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Garage]);
                                        arrColumns[(int)Aiken_RES_Fields.Attic] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Attic]);
                                        arrColumns[(int)Aiken_RES_Fields.Air_Conditioning] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Air_Conditioning]);
                                        arrColumns[(int)Aiken_RES_Fields.Elementary_School] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Elementary_School]);
                                        arrColumns[(int)Aiken_RES_Fields.Middle_School] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.Middle_School]);
                                        arrColumns[(int)Aiken_RES_Fields.High_School] = this.FixTitleCase(arrColumns[(int)Aiken_RES_Fields.High_School]);

                                        // Fix Capitalize Columns
                                        arrColumns[(int)Aiken_RES_Fields.State] = arrColumns[(int)Aiken_RES_Fields.State].ToUpper();

                                        // Fix Sentence Type Columns
                                        // arrColumns[(int)Aiken_RES_Fields.Property_Description] = this.FixSentenceCase(arrColumns[(int)Aiken_RES_Fields.Property_Description]);

                                        // Update Agent Id Format.
                                        arrColumns[(int)Aiken_RES_Fields.LA_ID] = arrColumns[(int)Aiken_RES_Fields.LA_ID].Replace("_", "-");

                                        // Update New Construction
                                        arrColumns[(int)Aiken_RES_Fields.New_Construction] = (arrColumns[(int)Aiken_RES_Fields.New_Construction] == "1" ? "Y" : "N");

                                        if(intFeedType == FeedType.Land)
                                        {
                                            arrColumns[(int)Aiken_RES_Fields.Property_Type] = "Lots/Land";

                                            if (arrColumns[(int)Aiken_RES_Fields.Apx_Heated_SqFt] == "0")
                                            {
                                                arrColumns[(int)Aiken_RES_Fields.Apx_Heated_SqFt] = "";
                                            }
                                        }

                                        // Attempt to get the Geolocation for properties you already have.
                                        strGeoLocation = this.GetMLSGeolocation(arrColumns[(int)Aiken_RES_Fields.MLS_Number], MLSType.Aiken);
                                    
                                        if(strGeoLocation == null && !this.blnGoogleMapsOverLimit)
                                        {
                                            strGeoLocation = this.MapAddress(arrColumns[(int)Aiken_RES_Fields.MLS_Number], arrColumns[(int)Aiken_RES_Fields.Street_Number], arrColumns[(int)Aiken_RES_Fields.Address], arrColumns[(int)Aiken_RES_Fields.City], arrColumns[(int)Aiken_RES_Fields.State], arrColumns[(int)Aiken_RES_Fields.Zip_Code], MLSType.Aiken);
                                        }
                                    

                                        // Increment the total properties consolidated
                                        this.intTotalAikenProperties++;

                                        // If GeoLocation is not null, then add it to the columns.
                                        if(strGeoLocation != null)
                                        {
                                            this.intTotalAikenGeocodedProperties++;

                                            arrColumns[(int)Aiken_RES_Fields.Latitude] = strGeoLocation[0];
                                            arrColumns[(int)Aiken_RES_Fields.Longitude] = strGeoLocation[1];
                                        }

                                        if (intFeedType == FeedType.Residential)
                                        {
                                            this.ExportMySQLData(arrColumns, MLSType.Aiken, intFeedType);
                                        }
                                    }
                                    else if (intFeedType == FeedType.Agent)
                                    {
                                        arrColumns[(int)Aiken_Agent_Fields.First_Name] = this.FixTitleCase(arrColumns[(int)Aiken_Agent_Fields.First_Name]);
                                        arrColumns[(int)Aiken_Agent_Fields.Last_Name] = this.FixTitleCase(arrColumns[(int)Aiken_Agent_Fields.Last_Name]);
                                        arrColumns[(int)Aiken_Agent_Fields.Mail_Address_1] = this.FixTitleCase(arrColumns[(int)Aiken_Agent_Fields.Mail_Address_1]);
                                        arrColumns[(int)Aiken_Agent_Fields.Mail_City] = this.FixTitleCase(arrColumns[(int)Aiken_Agent_Fields.Mail_City]);

                                        // Update Agent Id Format.
                                        arrColumns[(int)Aiken_Agent_Fields.AGENT_ID] = arrColumns[(int)Aiken_Agent_Fields.AGENT_ID].Replace("_", "-");

                                        // Clear out invalid Contact Number
                                        if (arrColumns[(int)Aiken_Agent_Fields.Contact_Number] == "0")
                                        {
                                            arrColumns[(int)Aiken_Agent_Fields.Contact_Number] = "";
                                        }

                                        if(arrColumns[(int)Aiken_Agent_Fields.Home] == "0")
                                        {
                                            arrColumns[(int)Aiken_Agent_Fields.Home] = "";
                                        }

                                        this.intTotalAikenAgents++;

                                        this.ExportMySQLData(arrColumns, MLSType.Aiken, FeedType.Agent);
                                    }
                                    else if (intFeedType == FeedType.Office)
                                    {
                                        arrColumns[(int)Office_Fields.Mail_Address_1] = this.FixTitleCase(arrColumns[(int)Office_Fields.Mail_Address_1]);
                                        arrColumns[(int)Office_Fields.Mail_City] = this.FixTitleCase(arrColumns[(int)Office_Fields.Mail_City]);
                                        arrColumns[(int)Office_Fields.Office_Name] = this.FixTitleCase(arrColumns[(int)Office_Fields.Office_Name]);

                                        this.intTotalAikenOffices++;

                                        this.ExportMySQLData(arrColumns, MLSType.Aiken, FeedType.Office);
                                    }

                                    bRecordFound = true;

                                    // TODO: Save information in Linux Server

                                    //Add data mls id, photo url to dictionary
                                    if (intFeedType == FeedType.Land || intFeedType == FeedType.Residential) 
                                    {
                                        if (Constant.PHOTO_TEST.ContainsKey(arrColumns[(int)Aiken_RES_Fields.MLS_Number]) && !dictMlsPhotos.ContainsKey(arrColumns[(int)Aiken_RES_Fields.MLS_Number]))
                                        { 
                                            dictMlsPhotos.Add(arrColumns[(int)Aiken_RES_Fields.MLS_Number], arrColumns[Array.IndexOf(arrColumnHeaders, "Photo Location")]);
                                        }
                                    }

                                    // Write out the columns in sequential order, the columns should have been defined sequentially
                                    for (int intIndex = 0; intIndex < arrColumns.Length; intIndex++)
                                    {
                                        file.Write("\"" + arrColumns[intIndex] + "\"");
                                    
                                        if (intIndex < arrColumns.Length - 1)
                                        {
                                            file.Write(",");
                                        }
                                        else
                                        {
                                            file.WriteLine("");
                                        }
                                    }

                                }
                                catch (Exception ex)
                                {
                                    bFatalError = true;
                                    bBlockPropertyPurge = true;
                                    this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running Import Aiken Files. - Details: " + ex.Message + "</i></b>");
                                }

                            }
                        }
                    }

                    if (!bRecordFound)
                    {
                        this.WriteToLog(String.Format("<br /><b><i style=\"color:red;\">No Records Found in File {0}.</i></b>", arrFilePaths[intFileIndex]));

                        if (intFeedType == FeedType.Land || intFeedType == FeedType.Residential) { 
                            bBlockPropertyPurge = true;
                            if (!this.blnIsIncremental)
                                bFatalError = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arrFilePaths"></param>
        public void ProcessAugustaFiles(string[] arrFilePaths, FeedType intFeedType)
        {
            string strConsolidationFolder = Constant.CONSOLIDATION_FOLDER;
            string strArchiveFolder = Constant.AUGUSTA_ARCHIVE_FOLDER;
            string strFileName, strFullFilePath;
            string strFeedType;
            bool blnWriteHeader = false;
            bool blnSkipHeader = true;
            bool bRecordFound = false;

            string[] arrColumns = null;
            string[] arrColumnHeaders = null;
            string[] strGeoLocation;

            if (arrFilePaths.Length == 0)
            {
                strFeedType = "";

                switch (intFeedType)
                {
                    case FeedType.Agent:
                        strFeedType = "Agent";
                        break;
                    case FeedType.Land:
                        strFeedType = "Land";
                        break;
                    case FeedType.Office:
                        strFeedType = "Office";
                        break;
                    case FeedType.Residential:
                        strFeedType = "Residential";
                        break;
                }

                this.WriteToLog(String.Format("<br /><b>No Files Found for Augusta {0}.</b>", strFeedType));
                return;
            }

            // Determine the columns for the file feeds
            strFileName = "";
            if (intFeedType == FeedType.Residential || intFeedType == FeedType.Land)
            {
                arrColumns = Constant.AUGUSTA_RESIDENTIAL_HEADER.Split(',');
                arrColumnHeaders = Constant.AUGUSTA_RESIDENTIAL_HEADER.Split(',');
                strFileName = "Meybohm-Augusta-ALL" + (this.blnIsIncremental ? "" : "Full") + ".csv";
            }
            else if (intFeedType == FeedType.Agent)
            {
                arrColumns = Constant.AUGUSTA_AGENT_HEADER.Split(',');
                arrColumnHeaders = Constant.AUGUSTA_AGENT_HEADER.Split(',');
                strFileName = "Meybohm-Augusta-Agents.csv";
            }
            else if (intFeedType == FeedType.Office)
            {
                arrColumns = Constant.OFFICE_HEADER.Split(',');
                arrColumnHeaders = Constant.OFFICE_HEADER.Split(',');
                strFileName = "Meybohm-Augusta-Offices.csv";
            }

            strFullFilePath = Constant.CONSOLIDATION_FOLDER + strFileName;

            // If the file does not already exist, write out the headers
            if (!File.Exists(strFullFilePath))
            {
                blnWriteHeader = true;
            }

            // Start writing the file
            using (StreamWriter file = new StreamWriter(strFullFilePath, true))
            {
                if (blnWriteHeader)
                {
                    // Write out the columns in sequential order, the columns should have been defined sequentially
                    for (int intIndex = 0; intIndex < arrColumns.Length; intIndex++)
                    {
                        file.Write("\"" + arrColumns[intIndex] + "\"");

                        if (intIndex < arrColumns.Length - 1)
                        {
                            file.Write(",");
                        }
                        else
                        {
                            file.WriteLine("");
                        }
                    }

                    blnWriteHeader = false;
                }

                // For each file, go through and download/pull the fields
                for (int intFileIndex = 0; intFileIndex < arrFilePaths.Length; intFileIndex++)
                {
                    blnSkipHeader = true;

                    bRecordFound = false;

                    using (TextFieldParser parser = new TextFieldParser(arrFilePaths[intFileIndex]))
                    {
                        parser.Delimiters = new string[] { "," };

                        while (true)
                        {
                            string[] BoundaryFields = parser.ReadFields();
                            strGeoLocation = null;

                            if (BoundaryFields == null || BoundaryFields.Length == 0)
                            {
                                break;
                            }

                            // Skip the first line of the file (header)
                            if (blnSkipHeader)
                            {
                                blnSkipHeader = false;
                            }
                            else
                            {
                                // Clear out old values in the columns array
                                for (int intIndex = 0; intIndex < arrColumns.Length; intIndex++)
                                {
                                    arrColumns[intIndex] = "";
                                }
                                try 
                                {
                                    if (intFeedType == FeedType.Residential)
                                    {
                                        arrColumns[(int)Augusta_RES_Fields.AC_Ventilation] = BoundaryFields[(int)Augusta_RES_Fields.AC_Ventilation];
                                        arrColumns[(int)Augusta_RES_Fields.Address] = BoundaryFields[(int)Augusta_RES_Fields.Address];
                                        arrColumns[(int)Augusta_RES_Fields.Appliances] = BoundaryFields[(int)Augusta_RES_Fields.Appliances];
                                        arrColumns[(int)Augusta_RES_Fields.Apx_Total_Heated_SqFt] = BoundaryFields[(int)Augusta_RES_Fields.Apx_Total_Heated_SqFt];
                                        arrColumns[(int)Augusta_RES_Fields.Apx_Year_Built] = BoundaryFields[(int)Augusta_RES_Fields.Apx_Year_Built];
                                        arrColumns[(int)Augusta_RES_Fields.Attic] = BoundaryFields[(int)Augusta_RES_Fields.Attic];
                                        arrColumns[(int)Augusta_RES_Fields.Basement] = BoundaryFields[(int)Augusta_RES_Fields.Basement];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_2_Length] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_2_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_2_Level] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_2_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_2_Width] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_2_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_3_Length] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_3_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_3_Level] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_3_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_3_Width] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_3_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_4_Length] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_4_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_4_Level] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_4_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_4_Width] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_4_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_5_Length] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_5_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_5_Level] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_5_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Bedroom_5_Width] = BoundaryFields[(int)Augusta_RES_Fields.Bedroom_5_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Bedrooms] = BoundaryFields[(int)Augusta_RES_Fields.Bedrooms];
                                        arrColumns[(int)Augusta_RES_Fields.Breakfast_Rm_Length] = BoundaryFields[(int)Augusta_RES_Fields.Breakfast_Rm_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Breakfast_Rm_Level] = BoundaryFields[(int)Augusta_RES_Fields.Breakfast_Rm_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Breakfast_Rm_Width] = BoundaryFields[(int)Augusta_RES_Fields.Breakfast_Rm_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Builder_Name] = BoundaryFields[(int)Augusta_RES_Fields.Builder_Name];
                                        arrColumns[(int)Augusta_RES_Fields.City] = BoundaryFields[(int)Augusta_RES_Fields.City];
                                        arrColumns[(int)Augusta_RES_Fields.County] = BoundaryFields[(int)Augusta_RES_Fields.County];
                                        arrColumns[(int)Augusta_RES_Fields.Dining_Rm_Length] = BoundaryFields[(int)Augusta_RES_Fields.Dining_Rm_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Dining_Rm_Level] = BoundaryFields[(int)Augusta_RES_Fields.Dining_Rm_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Dining_Rm_Width] = BoundaryFields[(int)Augusta_RES_Fields.Dining_Rm_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Directions] = BoundaryFields[(int)Augusta_RES_Fields.Directions];
                                        arrColumns[(int)Augusta_RES_Fields.Driveway] = BoundaryFields[(int)Augusta_RES_Fields.Driveway];
                                        arrColumns[(int)Augusta_RES_Fields.Elementary_School] = BoundaryFields[(int)Augusta_RES_Fields.Elementary_School];
                                        arrColumns[(int)Augusta_RES_Fields.Exterior_Features] = BoundaryFields[(int)Augusta_RES_Fields.Exterior_Features];
                                        arrColumns[(int)Augusta_RES_Fields.Exterior_Finish] = BoundaryFields[(int)Augusta_RES_Fields.Exterior_Finish];
                                        arrColumns[(int)Augusta_RES_Fields.Extra_Rooms] = BoundaryFields[(int)Augusta_RES_Fields.Extra_Rooms];
                                        arrColumns[(int)Augusta_RES_Fields.Family_Rm_Length] = BoundaryFields[(int)Augusta_RES_Fields.Family_Rm_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Family_Rm_Level] = BoundaryFields[(int)Augusta_RES_Fields.Family_Rm_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Family_Rm_Width] = BoundaryFields[(int)Augusta_RES_Fields.Family_Rm_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Financing_Type] = BoundaryFields[(int)Augusta_RES_Fields.Financing_Type];
                                        arrColumns[(int)Augusta_RES_Fields.Flooring] = BoundaryFields[(int)Augusta_RES_Fields.Flooring];
                                        arrColumns[(int)Augusta_RES_Fields.Foundation_Basement] = BoundaryFields[(int)Augusta_RES_Fields.Foundation_Basement];
                                        arrColumns[(int)Augusta_RES_Fields.Fuel_Source] = BoundaryFields[(int)Augusta_RES_Fields.Fuel_Source];
                                        arrColumns[(int)Augusta_RES_Fields.Full_Baths] = BoundaryFields[(int)Augusta_RES_Fields.Full_Baths];
                                        arrColumns[(int)Augusta_RES_Fields.Garage_Carport] = BoundaryFields[(int)Augusta_RES_Fields.Garage_Carport];
                                        arrColumns[(int)Augusta_RES_Fields.Great_Rm_Length] = BoundaryFields[(int)Augusta_RES_Fields.Great_Rm_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Great_Rm_Level] = BoundaryFields[(int)Augusta_RES_Fields.Great_Rm_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Great_Rm_Width] = BoundaryFields[(int)Augusta_RES_Fields.Great_Rm_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Half_Baths] = BoundaryFields[(int)Augusta_RES_Fields.Half_Baths];
                                        arrColumns[(int)Augusta_RES_Fields.Heat_Delivery] = BoundaryFields[(int)Augusta_RES_Fields.Heat_Delivery];
                                        arrColumns[(int)Augusta_RES_Fields.High_School] = BoundaryFields[(int)Augusta_RES_Fields.High_School];
                                        arrColumns[(int)Augusta_RES_Fields.Interior_Features] = BoundaryFields[(int)Augusta_RES_Fields.Interior_Features];
                                        arrColumns[(int)Augusta_RES_Fields.Kitchen_Length] = BoundaryFields[(int)Augusta_RES_Fields.Kitchen_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Kitchen_Level] = BoundaryFields[(int)Augusta_RES_Fields.Kitchen_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Kitchen_Width] = BoundaryFields[(int)Augusta_RES_Fields.Kitchen_Width];
                                        arrColumns[(int)Augusta_RES_Fields.LA_ID] = BoundaryFields[(int)Augusta_RES_Fields.LA_ID];
                                        arrColumns[(int)Augusta_RES_Fields.Latitude] = BoundaryFields[(int)Augusta_RES_Fields.Latitude];
                                        arrColumns[(int)Augusta_RES_Fields.List_Price] = BoundaryFields[(int)Augusta_RES_Fields.List_Price];
                                        arrColumns[(int)Augusta_RES_Fields.Listing_Office] = BoundaryFields[(int)Augusta_RES_Fields.Listing_Office];
                                        arrColumns[(int)Augusta_RES_Fields.Living_Rm_Length] = BoundaryFields[(int)Augusta_RES_Fields.Living_Rm_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Living_Rm_Level] = BoundaryFields[(int)Augusta_RES_Fields.Living_Rm_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Living_Rm_Width] = BoundaryFields[(int)Augusta_RES_Fields.Living_Rm_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Longitude] = BoundaryFields[(int)Augusta_RES_Fields.Longitude];
                                        arrColumns[(int)Augusta_RES_Fields.Lot_Description] = BoundaryFields[(int)Augusta_RES_Fields.Lot_Description];
                                        arrColumns[(int)Augusta_RES_Fields.Lot_Size] = BoundaryFields[(int)Augusta_RES_Fields.Lot_Size];
                                        arrColumns[(int)Augusta_RES_Fields.Middle_School] = BoundaryFields[(int)Augusta_RES_Fields.Middle_School];
                                        arrColumns[(int)Augusta_RES_Fields.MLS_Number] = BoundaryFields[(int)Augusta_RES_Fields.MLS_Number];
                                        arrColumns[(int)Augusta_RES_Fields.Neighborhood_Amenities] = BoundaryFields[(int)Augusta_RES_Fields.Neighborhood_Amenities];
                                        arrColumns[(int)Augusta_RES_Fields.New_Construction] = BoundaryFields[(int)Augusta_RES_Fields.New_Construction];
                                        arrColumns[(int)Augusta_RES_Fields.Number_Fireplaces] = BoundaryFields[(int)Augusta_RES_Fields.Number_Fireplaces];
                                        arrColumns[(int)Augusta_RES_Fields.Owner_Bedroom_Length] = BoundaryFields[(int)Augusta_RES_Fields.Owner_Bedroom_Length];
                                        arrColumns[(int)Augusta_RES_Fields.Owner_Bedroom_Level] = BoundaryFields[(int)Augusta_RES_Fields.Owner_Bedroom_Level];
                                        arrColumns[(int)Augusta_RES_Fields.Owner_Bedroom_Width] = BoundaryFields[(int)Augusta_RES_Fields.Owner_Bedroom_Width];
                                        arrColumns[(int)Augusta_RES_Fields.Photo_Count] = BoundaryFields[(int)Augusta_RES_Fields.Photo_Count];
                                        arrColumns[Array.IndexOf(arrColumnHeaders, "Photo Location")] = BoundaryFields[(int)Augusta_RES_Fields.Photo_location];
                                        arrColumns[(int)Augusta_RES_Fields.Pool] = BoundaryFields[(int)Augusta_RES_Fields.Pool];
                                        arrColumns[(int)Augusta_RES_Fields.Property_Description] = BoundaryFields[(int)Augusta_RES_Fields.Property_Description];
                                        arrColumns[(int)Augusta_RES_Fields.Property_Status] = BoundaryFields[(int)Augusta_RES_Fields.Property_Status];
                                        arrColumns[(int)Augusta_RES_Fields.Property_Type] = BoundaryFields[(int)Augusta_RES_Fields.Property_Type];
                                        arrColumns[(int)Augusta_RES_Fields.Roof] = BoundaryFields[(int)Augusta_RES_Fields.Roof];
                                        arrColumns[(int)Augusta_RES_Fields.Sewer] = BoundaryFields[(int)Augusta_RES_Fields.Sewer];
                                        arrColumns[(int)Augusta_RES_Fields.Showing_Instructions] = BoundaryFields[(int)Augusta_RES_Fields.Showing_Instructions];
                                        arrColumns[(int)Augusta_RES_Fields.State] = BoundaryFields[(int)Augusta_RES_Fields.State];
                                        arrColumns[(int)Augusta_RES_Fields.Street_Number] = BoundaryFields[(int)Augusta_RES_Fields.Street_Number];
                                        arrColumns[(int)Augusta_RES_Fields.Style] = BoundaryFields[(int)Augusta_RES_Fields.Style];
                                        arrColumns[(int)Augusta_RES_Fields.Subdivision] = BoundaryFields[(int)Augusta_RES_Fields.Subdivision];
                                        arrColumns[(int)Augusta_RES_Fields.Total_Acres] = BoundaryFields[(int)Augusta_RES_Fields.Total_Acres];
                                        arrColumns[(int)Augusta_RES_Fields.Total_Number_Rooms] = BoundaryFields[(int)Augusta_RES_Fields.Total_Number_Rooms];
                                        arrColumns[(int)Augusta_RES_Fields.Virtual_Tour] = BoundaryFields[(int)Augusta_RES_Fields.Virtual_Tour];
                                        arrColumns[(int)Augusta_RES_Fields.Water] = BoundaryFields[(int)Augusta_RES_Fields.Water];
                                        arrColumns[(int)Augusta_RES_Fields.Zip_Code] = BoundaryFields[(int)Augusta_RES_Fields.Zip_Code];


                                        // Set Equestrian
                                        /*
                                         *    -   'Property Description' includes case-insensitive string "horses allowed"
                                              -   EXTERIOR FEATURES includes Fence-Pasture
                                              -   EXTERIOR FEATURES includes Barn
                                         */

                                        arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "";
                                        if (BoundaryFields[(int)Augusta_RES_Fields.Property_Description].ToLower().IndexOf("horses allowed") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Augusta_RES_Fields.Exterior_Features].ToLower().IndexOf("fence-pasture") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Augusta_RES_Fields.Exterior_Features].ToLower().IndexOf("barn") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                    }
                                    else if (intFeedType == FeedType.Land)
                                    {
                                        arrColumns[(int)Augusta_RES_Fields.Address] = BoundaryFields[(int)Augusta_LAND_Fields.Address];
                                        arrColumns[(int)Augusta_RES_Fields.Apx_Total_Heated_SqFt] = BoundaryFields[(int)Augusta_LAND_Fields.Apx_Total_Heated_SqFt];
                                        arrColumns[(int)Augusta_RES_Fields.Bedrooms] = BoundaryFields[(int)Augusta_LAND_Fields.Bedrooms];
                                        arrColumns[(int)Augusta_RES_Fields.Builder_Name] = BoundaryFields[(int)Augusta_LAND_Fields.Builder_Name];
                                        arrColumns[(int)Augusta_RES_Fields.City] = BoundaryFields[(int)Augusta_LAND_Fields.City];
                                        arrColumns[(int)Augusta_RES_Fields.County] = BoundaryFields[(int)Augusta_LAND_Fields.County];
                                        arrColumns[(int)Augusta_RES_Fields.Directions] = BoundaryFields[(int)Augusta_LAND_Fields.Directions];
                                        arrColumns[(int)Augusta_RES_Fields.Elementary_School] = BoundaryFields[(int)Augusta_LAND_Fields.Elementary_School];
                                        arrColumns[(int)Augusta_RES_Fields.Exterior_Finish] = BoundaryFields[(int)Augusta_LAND_Fields.Exterior_Finish]; 
                                        arrColumns[(int)Augusta_RES_Fields.Financing_Type] = BoundaryFields[(int)Augusta_LAND_Fields.Financing_Type];
                                        arrColumns[(int)Augusta_RES_Fields.Full_Baths] = BoundaryFields[(int)Augusta_LAND_Fields.Full_Baths];
                                        arrColumns[(int)Augusta_RES_Fields.Half_Baths] = BoundaryFields[(int)Augusta_LAND_Fields.Half_Baths];
                                        arrColumns[(int)Augusta_RES_Fields.High_School] = BoundaryFields[(int)Augusta_LAND_Fields.High_School];
                                        arrColumns[(int)Augusta_RES_Fields.Interior_Features] = BoundaryFields[(int)Augusta_LAND_Fields.Interior_Features];
                                        arrColumns[(int)Augusta_RES_Fields.LA_ID] = BoundaryFields[(int)Augusta_LAND_Fields.LA_ID];
                                        arrColumns[(int)Augusta_RES_Fields.Latitude] = BoundaryFields[(int)Augusta_LAND_Fields.Latitude];
                                        arrColumns[(int)Augusta_RES_Fields.Longitude] = BoundaryFields[(int)Augusta_LAND_Fields.Longitude];
                                        arrColumns[(int)Augusta_RES_Fields.List_Price] = BoundaryFields[(int)Augusta_LAND_Fields.List_Price];
                                        arrColumns[(int)Augusta_RES_Fields.Listing_Office] = BoundaryFields[(int)Augusta_LAND_Fields.Listing_Office];
                                        arrColumns[(int)Augusta_RES_Fields.Lot_Description] = BoundaryFields[(int)Augusta_LAND_Fields.Lot_Description];
                                        arrColumns[(int)Augusta_RES_Fields.Lot_Size] = BoundaryFields[(int)Augusta_LAND_Fields.Lot_Size];
                                        arrColumns[(int)Augusta_RES_Fields.Middle_School] = BoundaryFields[(int)Augusta_LAND_Fields.Middle_School];
                                        arrColumns[(int)Augusta_RES_Fields.MLS_Number] = BoundaryFields[(int)Augusta_LAND_Fields.MLS_Number];
                                        arrColumns[(int)Augusta_RES_Fields.Neighborhood_Amenities] = BoundaryFields[(int)Augusta_LAND_Fields.Neighborhood_Amenities];
                                        arrColumns[(int)Augusta_RES_Fields.New_Construction] = BoundaryFields[(int)Augusta_LAND_Fields.New_Construction];
                                        arrColumns[(int)Augusta_RES_Fields.Number_Fireplaces] = BoundaryFields[(int)Augusta_LAND_Fields.Number_Fireplaces];
                                        arrColumns[(int)Augusta_RES_Fields.Photo_Count] = BoundaryFields[(int)Augusta_LAND_Fields.Photo_Count];
                                        arrColumns[Array.IndexOf(arrColumnHeaders, "Photo Location")] = BoundaryFields[(int)Augusta_LAND_Fields.Photo_location];
                                        arrColumns[(int)Augusta_RES_Fields.Pool] = BoundaryFields[(int)Augusta_LAND_Fields.Pool];
                                        arrColumns[(int)Augusta_RES_Fields.Property_Description] = BoundaryFields[(int)Augusta_LAND_Fields.Property_Description];
                                        arrColumns[(int)Augusta_RES_Fields.Property_Status] = BoundaryFields[(int)Augusta_LAND_Fields.Property_Status];
                                        arrColumns[(int)Augusta_RES_Fields.Property_Type] = BoundaryFields[(int)Augusta_LAND_Fields.Property_Type];
                                        arrColumns[(int)Augusta_RES_Fields.Showing_Instructions] = BoundaryFields[(int)Augusta_LAND_Fields.Showing_Instructions];
                                        arrColumns[(int)Augusta_RES_Fields.State] = BoundaryFields[(int)Augusta_LAND_Fields.State];
                                        arrColumns[(int)Augusta_RES_Fields.Street_Number] = BoundaryFields[(int)Augusta_LAND_Fields.Street_Number];
                                        arrColumns[(int)Augusta_RES_Fields.Style] = BoundaryFields[(int)Augusta_LAND_Fields.Style];
                                        arrColumns[(int)Augusta_RES_Fields.Subdivision] = BoundaryFields[(int)Augusta_LAND_Fields.Subdivision];
                                        arrColumns[(int)Augusta_RES_Fields.Total_Acres] = BoundaryFields[(int)Augusta_LAND_Fields.Total_Acres];
                                        arrColumns[(int)Augusta_RES_Fields.Virtual_Tour] = BoundaryFields[(int)Augusta_LAND_Fields.Virtual_Tour];
                                        arrColumns[(int)Augusta_RES_Fields.Zip_Code] = BoundaryFields[(int)Augusta_LAND_Fields.Zip_Code];


                                        // Set Equestrian
                                        /*
                                         *   *   LAND DESCRIPTION includes Horses Allowed
                                             -   LOT DESCRIPTION includes Established Pasture
                                         */
                                        arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "";
                                        if (BoundaryFields[(int)Augusta_LAND_Fields.Land_Description].ToLower().IndexOf("horses allowed") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                        else if (BoundaryFields[(int)Augusta_LAND_Fields.Lot_Description].ToLower().IndexOf("established pasture") > -1)
                                            arrColumns[Array.IndexOf(arrColumnHeaders, "Equestrian")] = "Y";
                                    }
                                    else if (intFeedType == FeedType.Agent)
                                    {
                                        arrColumns[(int)Augusta_Agent_Fields.Agent_Email] = BoundaryFields[(int)Augusta_Agent_Fields.Agent_Email];
                                        arrColumns[(int)Augusta_Agent_Fields.AGENT_ID] = BoundaryFields[(int)Augusta_Agent_Fields.AGENT_ID];
                                        arrColumns[(int)Augusta_Agent_Fields.Contact_Number] = BoundaryFields[(int)Augusta_Agent_Fields.Contact_Number];
                                        arrColumns[(int)Augusta_Agent_Fields.First_Name] = BoundaryFields[(int)Augusta_Agent_Fields.First_Name];
                                        arrColumns[(int)Augusta_Agent_Fields.Home] = BoundaryFields[(int)Augusta_Agent_Fields.Home];
                                        arrColumns[(int)Augusta_Agent_Fields.Last_Name] = BoundaryFields[(int)Augusta_Agent_Fields.Last_Name];
                                        arrColumns[(int)Augusta_Agent_Fields.Mail_Address_1] = BoundaryFields[(int)Augusta_Agent_Fields.Mail_Address_1];
                                        arrColumns[(int)Augusta_Agent_Fields.Mail_City] = BoundaryFields[(int)Augusta_Agent_Fields.Mail_City];
                                        arrColumns[(int)Augusta_Agent_Fields.Mail_State] = BoundaryFields[(int)Augusta_Agent_Fields.Mail_State];
                                        arrColumns[(int)Augusta_Agent_Fields.Mail_Zip_Code] = BoundaryFields[(int)Augusta_Agent_Fields.Mail_Zip_Code];
                                        arrColumns[(int)Augusta_Agent_Fields.Office_ID] = BoundaryFields[(int)Augusta_Agent_Fields.Office_ID];
                                        arrColumns[(int)Augusta_Agent_Fields.Web_Address] = BoundaryFields[(int)Augusta_Agent_Fields.Web_Address];
                                    }
                                    else if (intFeedType == FeedType.Office)
                                    {
                                        arrColumns[(int)Office_Fields.Fax] = BoundaryFields[(int)Office_Fields.Fax];
                                        arrColumns[(int)Office_Fields.Mail_Address_1] = BoundaryFields[(int)Office_Fields.Mail_Address_1];
                                        arrColumns[(int)Office_Fields.Mail_City] = BoundaryFields[(int)Office_Fields.Mail_City];
                                        arrColumns[(int)Office_Fields.Mail_State] = BoundaryFields[(int)Office_Fields.Mail_State];
                                        arrColumns[(int)Office_Fields.Mail_Zip_Code] = BoundaryFields[(int)Office_Fields.Mail_Zip_Code];
                                        arrColumns[(int)Office_Fields.Main] = BoundaryFields[(int)Office_Fields.Main];
                                        arrColumns[(int)Office_Fields.Office_ID] = BoundaryFields[(int)Office_Fields.Office_ID];
                                        arrColumns[(int)Office_Fields.Office_Name] = BoundaryFields[(int)Office_Fields.Office_Name];
                                        arrColumns[(int)Office_Fields.Web_Address] = BoundaryFields[(int)Office_Fields.Web_Address];
                                    }

                                    // Do transformations on RES/LAND Data
                                    if (intFeedType == FeedType.Land || intFeedType == FeedType.Residential)
                                    {
                                        arrColumns[(int)Augusta_RES_Fields.Address] = this.FixTitleCase(arrColumns[(int)Augusta_RES_Fields.Address]);
                                        arrColumns[(int)Augusta_RES_Fields.City] = this.FixTitleCase(arrColumns[(int)Augusta_RES_Fields.City]);
                                        arrColumns[(int)Augusta_RES_Fields.Subdivision] = this.FixTitleCase(arrColumns[(int)Augusta_RES_Fields.Subdivision]);
                                        arrColumns[(int)Augusta_RES_Fields.Elementary_School] = this.FixTitleCase(arrColumns[(int)Augusta_RES_Fields.Elementary_School]);
                                        arrColumns[(int)Augusta_RES_Fields.Middle_School] = this.FixTitleCase(arrColumns[(int)Augusta_RES_Fields.Middle_School]);
                                        arrColumns[(int)Augusta_RES_Fields.High_School] = this.FixTitleCase(arrColumns[(int)Augusta_RES_Fields.High_School]);
                                        arrColumns[(int)Augusta_RES_Fields.County] = this.FixTitleCase(arrColumns[(int)Augusta_RES_Fields.County]);

                                        // Fix Capitalize Columns
                                        arrColumns[(int)Augusta_RES_Fields.State] = arrColumns[(int)Augusta_RES_Fields.State].ToUpper();

                                        // Fix Sentence Type Columns
                                        //arrColumns[(int)Augusta_RES_Fields.Property_Description] = this.FixSentenceCase(arrColumns[(int)Augusta_RES_Fields.Property_Description]);

                                        // Update Agent Id Format.
                                        arrColumns[(int)Augusta_RES_Fields.LA_ID] = arrColumns[(int)Augusta_RES_Fields.LA_ID].Replace("_", "-");

                                        // Update New Construction
                                        arrColumns[(int)Augusta_RES_Fields.New_Construction] = (arrColumns[(int)Augusta_RES_Fields.New_Construction] == "1" ? "Y" : "N");
                                        arrColumns[(int)Augusta_RES_Fields.Pool] = (arrColumns[(int)Augusta_RES_Fields.Pool] == "1" ? "Y" : "N");

                                        if(intFeedType == FeedType.Land)
                                        {
                                            if (arrColumns[(int)Augusta_RES_Fields.Number_Fireplaces] == "0")
                                            {
                                                arrColumns[(int)Augusta_RES_Fields.Number_Fireplaces] = "";
                                            }

                                            if (arrColumns[(int)Augusta_RES_Fields.Apx_Total_Heated_SqFt] == "0")
                                            {
                                                arrColumns[(int)Augusta_RES_Fields.Apx_Total_Heated_SqFt] = "";
                                            }

                                            if (arrColumns[(int)Augusta_RES_Fields.Bedrooms] == "0")
                                            {
                                                arrColumns[(int)Augusta_RES_Fields.Bedrooms] = "";
                                            }

                                            if (arrColumns[(int)Augusta_RES_Fields.Half_Baths] == "0")
                                            {
                                                arrColumns[(int)Augusta_RES_Fields.Half_Baths] = "";
                                            }

                                            if (arrColumns[(int)Augusta_RES_Fields.Full_Baths] == "0")
                                            {
                                                arrColumns[(int)Augusta_RES_Fields.Full_Baths] = "";
                                            }

                                            arrColumns[(int)Augusta_RES_Fields.Property_Type] = "Lots/Land";
                                        }

                                        // Attempt to get the Geolocation for properties you already have.
                                    
                                        strGeoLocation = this.GetMLSGeolocation(arrColumns[(int)Augusta_RES_Fields.MLS_Number], MLSType.Augusta);
                                        if(strGeoLocation == null && !this.blnGoogleMapsOverLimit)
                                        {
                                            strGeoLocation = this.MapAddress(arrColumns[(int)Augusta_RES_Fields.MLS_Number], arrColumns[(int)Augusta_RES_Fields.Street_Number], arrColumns[(int)Augusta_RES_Fields.Address], arrColumns[(int)Augusta_RES_Fields.City], arrColumns[(int)Augusta_RES_Fields.State], arrColumns[(int)Augusta_RES_Fields.Zip_Code], MLSType.Augusta);
                                        }
                                    

                                        // Increment the total properties consolidated
                                        this.intTotalAugustaProperties++;

                                        // If GeoLocation is not null, then add it to the columns.
                                        if(strGeoLocation != null)
                                        {
                                            this.intTotalAugustaGeocodedProperties++;

                                            arrColumns[(int)Augusta_RES_Fields.Latitude] = strGeoLocation[0];
                                            arrColumns[(int)Augusta_RES_Fields.Longitude] = strGeoLocation[1];
                                        }

                                        if (intFeedType == FeedType.Residential)
                                        {
                                            this.ExportMySQLData(arrColumns, MLSType.Augusta, intFeedType);
                                        }
                                    }
                                    else if (intFeedType == FeedType.Agent)
                                    {
                                        arrColumns[(int)Augusta_Agent_Fields.First_Name] = this.FixTitleCase(arrColumns[(int)Augusta_Agent_Fields.First_Name]);
                                        arrColumns[(int)Augusta_Agent_Fields.Last_Name] = this.FixTitleCase(arrColumns[(int)Augusta_Agent_Fields.Last_Name]);
                                        arrColumns[(int)Augusta_Agent_Fields.Mail_Address_1] = this.FixTitleCase(arrColumns[(int)Augusta_Agent_Fields.Mail_Address_1]);
                                        arrColumns[(int)Augusta_Agent_Fields.Mail_City] = this.FixTitleCase(arrColumns[(int)Augusta_Agent_Fields.Mail_City]);

                                        // Update Agent Id Format.
                                        arrColumns[(int)Augusta_Agent_Fields.AGENT_ID] = arrColumns[(int)Augusta_Agent_Fields.AGENT_ID].Replace("_", "-");

                                        // Clear out invalid Contact Number
                                        if (arrColumns[(int)Augusta_Agent_Fields.Contact_Number] == "0")
                                        {
                                            arrColumns[(int)Augusta_Agent_Fields.Contact_Number] = "";
                                        }

                                        if (arrColumns[(int)Augusta_Agent_Fields.Home] == "0")
                                        {
                                            arrColumns[(int)Augusta_Agent_Fields.Home] = "";
                                        }

                                        this.intTotalAugustaAgents++;

                                        this.ExportMySQLData(arrColumns, MLSType.Augusta, FeedType.Agent);
                                    }
                                    else if (intFeedType == FeedType.Office)
                                    {
                                        arrColumns[(int)Office_Fields.Mail_Address_1] = this.FixTitleCase(arrColumns[(int)Office_Fields.Mail_Address_1]);
                                        arrColumns[(int)Office_Fields.Mail_City] = this.FixTitleCase(arrColumns[(int)Office_Fields.Mail_City]);
                                        arrColumns[(int)Office_Fields.Office_Name] = this.FixTitleCase(arrColumns[(int)Office_Fields.Office_Name]);

                                        this.intTotalAugustaOffices++;

                                        this.ExportMySQLData(arrColumns, MLSType.Augusta, FeedType.Office);
                                    }

                                    bRecordFound = true;

                                    // TODO: Save information in Linux Server

                                    //Add data mls id, photo url to dictionary
                                    if (intFeedType == FeedType.Land || intFeedType == FeedType.Residential)
                                    {
                                        if (Constant.PHOTO_TEST.ContainsKey(arrColumns[(int)Augusta_RES_Fields.MLS_Number]) && !dictMlsPhotos.ContainsKey(arrColumns[(int)Augusta_RES_Fields.MLS_Number]))
                                        {
                                            dictMlsPhotos.Add(arrColumns[(int)Augusta_RES_Fields.MLS_Number], arrColumns[Array.IndexOf(arrColumnHeaders, "Photo Location")]);
                                        }
                                    }

                                    // Write out the columns in sequential order, the columns should have been defined sequentially
                                    for (int intIndex = 0; intIndex < arrColumns.Length; intIndex++)
                                    {
                                        file.Write("\"" + arrColumns[intIndex] + "\"");

                                        if (intIndex < arrColumns.Length - 1)
                                        {
                                            file.Write(",");
                                        }
                                        else
                                        {
                                            file.WriteLine("");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    bFatalError = true;
                                    bBlockPropertyPurge = true;
                                    this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running Import Augusta Files. - Details: " + ex.Message + "</i></b>");
                                }
                            }
                        }
                    }

                    if (!bRecordFound)
                    {
                        this.WriteToLog(String.Format("<br /><b><i style=\"color:red;\">No Records Found in File {0}.</i></b>", arrFilePaths[intFileIndex]));

                        if (intFeedType == FeedType.Land || intFeedType == FeedType.Residential) { 
                            bBlockPropertyPurge = true;
                            if (!this.blnIsIncremental)
                                bFatalError = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void RemoveOldFiles()
        {
            string[] arrFilePaths = Directory.GetFiles(Constant.CONSOLIDATION_FOLDER, "*.csv");

            for (int intIndex = 0; intIndex < arrFilePaths.Length; intIndex++)
            {
                if(blnIsIncremental && arrFilePaths[intIndex].Contains("-ALL.csv"))
                {
                    File.Delete(arrFilePaths[intIndex]);
                }
                else if (!blnIsIncremental && !arrFilePaths[intIndex].Contains("-ALL.csv"))
                {
                    File.Delete(arrFilePaths[intIndex]);
                }
                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arrColumns"></param>
        /// <param name="intMLSType"></param>
        /// <param name="intFeedType"></param>
        private void RunExportMySQLData(string[] arrColumns, MLSType intMLSType, FeedType intFeedType)
        {
            bool isAssigned = false;

            string[] newArrayColumns = new string[arrColumns.Length];

            for (int intIndex = 0; intIndex < newArrayColumns.Length; intIndex++)
            {
                newArrayColumns[intIndex] = arrColumns[intIndex];
            }

            for (int intTaskIndex = 0; intTaskIndex < this.taskPool.Length; intTaskIndex++)
            {
                if (taskPool[intTaskIndex] == null || (taskPool[intTaskIndex] != null && taskPool[intTaskIndex].Status == TaskStatus.RanToCompletion))
                {
                    taskPool[intTaskIndex] = Task.Factory.StartNew(() => this.ExportMySQLData(newArrayColumns, intMLSType, intFeedType));
                    isAssigned = true;

                    break;
                }
            }

            if (!isAssigned)
            {
                int intTaskIndex = Task.WaitAny(taskPool);

                taskPool[intTaskIndex] = Task.Factory.StartNew(() => this.ExportMySQLData(newArrayColumns, intMLSType, intFeedType));
                isAssigned = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void FinishAllTasks()
        {
            if(this.taskPool.Any(task => task != null))
            {
                for (int intTaskIndex = 0; intTaskIndex < this.taskPool.Length; intTaskIndex++)
                {
                    if (taskPool[intTaskIndex] != null && taskPool[intTaskIndex].Status != TaskStatus.RanToCompletion)
                    {
                        taskPool[intTaskIndex].Wait();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void MigrateAugustaMySQLData()
        {
            string strCommand = @" TRUNCATE TABLE prop_res_soap;
                                   TRUNCATE TABLE photo_links_aug_soap; 
                                   TRUNCATE TABLE agents_soap;
                                   TRUNCATE TABLE offices_soap;     

                                   INSERT INTO prop_res_soap SELECT * FROM prop_res;   
                                   INSERT INTO photo_links_aug_soap SELECT * FROM photo_links_aug;   
                                   INSERT INTO agents_soap SELECT * FROM agents;   
                                   INSERT INTO offices_soap SELECT * FROM offices; ";

            //using (MySqlConnection connection = new MySqlConnection(ConfigurationManager.ConnectionStrings["MySQLServer"].ToString()))
            if (connMySqlConnection.State == ConnectionState.Open)
            {
                using (MySqlCommand command = new MySqlCommand(strCommand, connMySqlConnection))
                {
                    command.CommandTimeout = 300;

                    try
                    {
                        //connection.Open();
                        command.ExecuteNonQuery();
                        //connection.Close();
                    }
                    catch(Exception ex)
                    {
                        if (!this.blnIsIncremental)
                            bFatalError = true;
                        this.WriteToLog("<br /><b><i style=\"color:red;\">Error Running MigrateAugustaMySQLData SQL - Details: " + ex.Message + "</i></b>");
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strMessage"></param>
        public void WriteToLog(string strMessage)
        {
            try
            {
                using (StreamWriter file = new StreamWriter(Constant.LOG_FILE, true))
                {
                    file.WriteLine(strMessage);
                    sbLogBuilder.Append(strMessage);
                }
            }
            catch(Exception ex)
            {
                //Skip Exceptions on log writing
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void WriteStatistics()
        {
            this.WriteToLog("<h2>Statistics Information</h3>");
            this.WriteToLog("<h3>Aiken Stats</h3>");
            this.WriteToLog("<b>Properties Consolidated:</b> " + this.intTotalAikenProperties);
            this.WriteToLog("<br /><b>Properties Geocoded:</b> " + this.intTotalAikenGeocodedProperties + " of " + this.intTotalAikenProperties);
            this.WriteToLog("<br /><b>Agents Consolidated:</b> " + this.intTotalAikenAgents);
            this.WriteToLog("<br /><b>Offices Consolidated:</b> " + this.intTotalAikenOffices);

            this.WriteToLog("<h3>Augusta Stats</h3>");
            this.WriteToLog("<b>Properties Consolidated:</b> " + this.intTotalAugustaProperties);
            this.WriteToLog("<br /><b>Properties Geocoded:</b> " + this.intTotalAugustaGeocodedProperties + " of " + this.intTotalAugustaProperties);
            this.WriteToLog("<br /><b>Agents Consolidated:</b> " + this.intTotalAugustaAgents);
            this.WriteToLog("<br /><b>Offices Consolidated:</b> " + this.intTotalAugustaOffices);
        }

        public void ProcessCombinedFiles()
        {
            string aikenOutputPath = Constant.CONSOLIDATION_FOLDER + "Meybohm-Aiken-ALL" + (blnIsIncremental ? "" : "Full") + ".csv";
            string augustaOutputPath = Constant.CONSOLIDATION_FOLDER + "Meybohm-Augusta-ALL" + (blnIsIncremental ? "" : "Full") + ".csv";

            string[] files = Directory.GetFiles(Constant.DOWNLOAD_FOLDER, "*.csv");

            if (files.Length > 0)
            {
                // Create the Aiken output file if it doesn't exist.
                if (!File.Exists(aikenOutputPath))
                    File.WriteAllText(aikenOutputPath, Constant.AIKEN_RESIDENTIAL_HEADER + "\n");

                // Create the Augusta output file if it doesn't exist.
                if (!File.Exists(augustaOutputPath))
                    File.WriteAllText(augustaOutputPath, Constant.AUGUSTA_RESIDENTIAL_HEADER + "\n");
            }

            foreach (string path in files)
            {
                using (TextFieldParser parser = new TextFieldParser(path))
                using (StreamWriter aikenWriter = new StreamWriter(aikenOutputPath, true))
                using (StreamWriter augustaWriter = new StreamWriter(augustaOutputPath, true))
                {
                    parser.Delimiters = new string[] { "," };

                    // Get the index of each column in the file.
                    int[] columns = ParseColumnIndices(parser.ReadLine());

                    // Read the remainder of the lines to parse out data.
                    string[] fields = null;

                    while ((fields = parser.ReadFields()) != null)
                    {
                        // Trim spaces and quotation marks from each field.
                        for (int i = 0; i < fields.Length; i++)
                            fields[i] = fields[i].Trim().Trim('\"');

                        // Determine which city type to use.
                        CityType cityType = fields.Contains("SC") ? CityType.Aiken : CityType.Augusta;

                        // Parse the data from the fields.
                        string[] data = ParseData(columns, fields, cityType);

                        // Write the data to the correct file.
                        if (cityType == CityType.Aiken)
                            WriteAikenRecord(aikenWriter, columns, data);
                        else
                            WriteAugustaRecord(augustaWriter, columns, data);
                    }
                }
            }
        }

        private int[] ParseColumnIndices(string header)
        {
            int[] columns = new int[Enum.GetValues(typeof(Augusta_RENT_Fields)).Length];

            // Fields with -1 are not found in the import file.
            for (int i = 0; i < columns.Length; i++)
                columns[i] = -1;

            // Split the header fields by commas.
            string[] fields = header.Split(',');

            // Determine the index of each field based on the index in the header.
            for (int i = 0; i < fields.Length; i++)
            {
                // Ignore spaces and quotation marks surrounding each field.
                switch (fields[i].Trim().Trim('\"').ToLower())
                {
                    case "mls number":
                        columns[(int)Augusta_RENT_Fields.MLS_Number] = i;
                        break;

                    case "address":
                        columns[(int)Augusta_RENT_Fields.Address] = i;
                        break;

                    case "apx total heated sqft":
                        columns[(int)Augusta_RENT_Fields.Apx_Total_Heated_SqFt] = i;
                        break;

                    case "bedrooms":
                        columns[(int)Augusta_RENT_Fields.Bedrooms] = i;
                        break;

                    case "city":
                        columns[(int)Augusta_RENT_Fields.City] = i;
                        break;

                    case "county":
                        columns[(int)Augusta_RENT_Fields.County] = i;
                        break;

                    case "date available":
                            columns[(int)Augusta_RENT_Fields.Date_Available] = i;
                        break;

                    case "elementary school":
                        columns[(int)Augusta_RENT_Fields.Elementary_School] = i;
                        break;

                    case "full baths":
                        columns[(int)Augusta_RENT_Fields.Full_Baths] = i;
                        break;

                    case "half baths":
                        columns[(int)Augusta_RENT_Fields.Half_Baths] = i;
                        break;

                    case "high school":
                        columns[(int)Augusta_RENT_Fields.High_School] = i;
                        break;

                    case "middle school":
                        columns[(int)Augusta_RENT_Fields.Middle_School] = i;
                        break;

                    case "property description":
                        columns[(int)Augusta_RENT_Fields.Property_Description] = i;
                        break;

                    case "rent price":
                        columns[(int)Augusta_RENT_Fields.Rent_Price] = i;
                        break;

                    case "subdivision":
                        columns[(int)Augusta_RENT_Fields.Subdivision] = i;
                        break;

                    case "state":
                        columns[(int)Augusta_RENT_Fields.State] = i;
                        break;

                    case "street #":
                        columns[(int)Augusta_RENT_Fields.Street_Number] = i;
                        break;

                    case "zip code":
                        columns[(int)Augusta_RENT_Fields.Zip] = i;
                        break;

                    case "photo location":
                        columns[(int)Augusta_RENT_Fields.Photo_Location] = i;
                        break;

                    default:
                        break;
                }
            }

            return columns;
        }

        private string[] ParseData(int[] columns, string[] fields, CityType cityType)
        {
            string[] data = new string[columns.Length];
            
            data[(int)Augusta_RENT_Fields.MLS_Number] = fields[columns[(int)Augusta_RENT_Fields.MLS_Number]];
            data[(int)Augusta_RENT_Fields.Address] = fields[columns[(int)Augusta_RENT_Fields.Address]];
            data[(int)Augusta_RENT_Fields.Apx_Total_Heated_SqFt] = fields[columns[(int)Augusta_RENT_Fields.Apx_Total_Heated_SqFt]];
            data[(int)Augusta_RENT_Fields.Bedrooms] = fields[columns[(int)Augusta_RENT_Fields.Bedrooms]];
            data[(int)Augusta_RENT_Fields.City] = fields[columns[(int)Augusta_RENT_Fields.City]];
            data[(int)Augusta_RENT_Fields.County] = fields[columns[(int)Augusta_RENT_Fields.County]];
            data[(int)Augusta_RENT_Fields.Date_Available] = fields[columns[(int)Augusta_RENT_Fields.Date_Available]];
            data[(int)Augusta_RENT_Fields.Elementary_School] = fields[columns[(int)Augusta_RENT_Fields.Elementary_School]];
            data[(int)Augusta_RENT_Fields.Full_Baths] = fields[columns[(int)Augusta_RENT_Fields.Full_Baths]];
            data[(int)Augusta_RENT_Fields.Half_Baths] = fields[columns[(int)Augusta_RENT_Fields.Half_Baths]];
            data[(int)Augusta_RENT_Fields.High_School] = fields[columns[(int)Augusta_RENT_Fields.High_School]];
            data[(int)Augusta_RENT_Fields.Middle_School] = fields[columns[(int)Augusta_RENT_Fields.Middle_School]];
            data[(int)Augusta_RENT_Fields.Property_Description] = fields[columns[(int)Augusta_RENT_Fields.Property_Description]];
            data[(int)Augusta_RENT_Fields.Date_Available] = fields[columns[(int)Augusta_RENT_Fields.Date_Available]];
            data[(int)Augusta_RENT_Fields.Subdivision] = fields[columns[(int)Augusta_RENT_Fields.Subdivision]];
            data[(int)Augusta_RENT_Fields.State] = fields[columns[(int)Augusta_RENT_Fields.State]];
            data[(int)Augusta_RENT_Fields.Street_Number] = fields[columns[(int)Augusta_RENT_Fields.Street_Number]];
            data[(int)Augusta_RENT_Fields.Zip] = fields[columns[(int)Augusta_RENT_Fields.Zip]];
            data[(int)Augusta_RENT_Fields.Photo_Location] = fields[columns[(int)Augusta_RENT_Fields.Photo_Location]];

            return data;
        }

        private void WriteAikenRecord(StreamWriter writer, int[] columns, string[] data)
        {
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.MLS_Number]]);            // MLS Number
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Street_Number]]);         // Street #
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Address]]);               // Address
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Subdivision]]);           // Town/Subdivision
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.City]]);                  // City
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.State]]);                 // State
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Zip]]);                   // Zip Code
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Rent_Price]]);            // List Price
            writer.Write(",");                                                                       // Listing Office
            writer.Write(",");                                                                       // LA ID
            writer.Write("\"Rental\",");                                                             // Property Type
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Property_Description]]);  // Property Description
            writer.Write(",");                                                                       // Year Built
            writer.Write(",");                                                                       // Style
            writer.Write(",");                                                                       // Exterior Features
            writer.Write(",");                                                                       // Interior Features
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Apx_Total_Heated_SqFt]]); // Apx Heated SqFt
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Full_Baths]]);            // Full Baths
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Half_Baths]]);            // Half Baths
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Bedrooms]]);              // Bedrooms
            writer.Write(",");                                                                       // Foundation/Basement
            writer.Write(",");                                                                       // Floors
            writer.Write(",");                                                                       // Garage
            writer.Write(",");                                                                       // Attic
            writer.Write(",");                                                                       // Air Conditioning
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Elementary_School]]);     // Elementary School
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Middle_School]]);         // Middle School
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.High_School]]);           // High School
            writer.Write(",");                                                                       // Virtual Tour
            writer.Write(",");                                                                       // Builder Name
            writer.Write(",");                                                                       // New Construction
            writer.Write(",");                                                                       // Property Status
            writer.Write(",");                                                                       // Latitude
            writer.Write(",");                                                                       // Longitude
            writer.Write(",");                                                                       // Directions
            writer.Write(",");                                                                       // List Date
            writer.Write(",");                                                                       // Total Acres
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.County]]);                // County
            writer.Write(",");                                                                       // Equestrian
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Photo_Location]]);        // Photo Location
            writer.Write("\"{0}\"", data[columns[(int)Augusta_RENT_Fields.Date_Available]]);         // Date Available
            writer.WriteLine();
        }

        private void WriteAugustaRecord(StreamWriter writer, int[] columns, string[] data)
        {
            writer.Write(",");                                                                       // # Fireplaces
            writer.Write(",");                                                                       // AC/Ventilation
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Address]]);               // Address
            writer.Write(",");                                                                       // Appliances
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Apx_Total_Heated_SqFt]]); // Apx Total Heated SqFt
            writer.Write(",");                                                                       // Apx Year Built
            writer.Write(",");                                                                       // Attic
            writer.Write(",");                                                                       // Basement
            writer.Write(",");                                                                       // Bedroom 2 Length
            writer.Write(",");                                                                       // Bedroom 2 Level
            writer.Write(",");                                                                       // Bedroom 2 Width
            writer.Write(",");                                                                       // Bedroom 3 Length
            writer.Write(",");                                                                       // Bedroom 3 Level
            writer.Write(",");                                                                       // Bedroom 3 Width
            writer.Write(",");                                                                       // Bedroom 4 Length
            writer.Write(",");                                                                       // Bedroom 4 Level
            writer.Write(",");                                                                       // Bedroom 4 Width
            writer.Write(",");                                                                       // Bedroom 5 Length
            writer.Write(",");                                                                       // Bedroom 5 Level
            writer.Write(",");                                                                       // Bedroom 5 Width
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Bedrooms]]);              // Bedrooms
            writer.Write(",");                                                                       // Breakfast Rm Length
            writer.Write(",");                                                                       // Breakfast Rm Level
            writer.Write(",");                                                                       // Breakfast Rm Width
            writer.Write(",");                                                                       // Builder Name
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.City]]);                  // City
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.County]]);                // County
            writer.Write(",");                                                                       // Dining Rm Length
            writer.Write(",");                                                                       // Dining Rm Level
            writer.Write(",");                                                                       // Dining Rm Width
            writer.Write(",");                                                                       // Directions
            writer.Write(",");                                                                       // Driveway
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Elementary_School]]);     // Elementary School
            writer.Write(",");                                                                       // Exterior Features
            writer.Write(",");                                                                       // Exterior Finish
            writer.Write(",");                                                                       // Extra Rooms
            writer.Write(",");                                                                       // Family Rm Length
            writer.Write(",");                                                                       // Family Rm Level
            writer.Write(",");                                                                       // Family Rm Width
            writer.Write(",");                                                                       // Financing Type
            writer.Write(",");                                                                       // Flooring
            writer.Write(",");                                                                       // Foundation/Basement
            writer.Write(",");                                                                       // Fuel Source
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Full_Baths]]);            // Full Baths
            writer.Write(",");                                                                       // Garage/Carport
            writer.Write(",");                                                                       // Great Rm Length
            writer.Write(",");                                                                       // Great Rm Level
            writer.Write(",");                                                                       // Great Rm Width
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Half_Baths]]);            // Half Baths
            writer.Write(",");                                                                       // Heat Delivery
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.High_School]]);           // High School
            writer.Write(",");                                                                       // Interior Features
            writer.Write(",");                                                                       // Kitchen Length
            writer.Write(",");                                                                       // Kitchen Level
            writer.Write(",");                                                                       // Kitchen Width
            writer.Write(",");                                                                       // LA ID
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Rent_Price]]);            // List Price
            writer.Write(",");                                                                       // Listing Office
            writer.Write(",");                                                                       // Living Rm Length
            writer.Write(",");                                                                       // Living Rm Level
            writer.Write(",");                                                                       // Living Rm Width
            writer.Write(",");                                                                       // Lot Description
            writer.Write(",");                                                                       // Lot Size
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Middle_School]]);         // Middle School
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.MLS_Number]]);            // MLS Number
            writer.Write(",");                                                                       // Neighborhood Amenities
            writer.Write(",");                                                                       // New Construction
            writer.Write(",");                                                                       // Owner Bedroom Length
            writer.Write(",");                                                                       // Owner Bedroom Level
            writer.Write(",");                                                                       // Owner Bedroom Width
            writer.Write(",");                                                                       // Photo Count
            writer.Write(",");                                                                       // Pool
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Property_Description]]);  // Property Description
            writer.Write(",");                                                                       // Property Status
            writer.Write("\"Rental\",");                                                             // Property Type
            writer.Write(",");                                                                       // Roof
            writer.Write(",");                                                                       // Sewer
            writer.Write(",");                                                                       // Showing Instructions
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.State]]);                 // State
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Street_Number]]);         // Street #
            writer.Write(",");                                                                       // Style
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Subdivision]]);           // Subdivision
            writer.Write(",");                                                                       // Virtual Tour
            writer.Write(",");                                                                       // Water
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Zip]]);                   // Zip Code
            writer.Write(",");                                                                       // Total # Rooms
            writer.Write(",");                                                                       // Total Acres
            writer.Write(",");                                                                       // Latitude
            writer.Write(",");                                                                       // Longitude
            writer.Write(",");                                                                       // Equestrian
            writer.Write("\"{0}\",", data[columns[(int)Augusta_RENT_Fields.Photo_Location]]);        // Photo Location
            writer.Write("\"{0}\"", data[columns[(int)Augusta_RENT_Fields.Date_Available]]);         // Date Available
            writer.WriteLine();
        }

        #endregion
    }
}
