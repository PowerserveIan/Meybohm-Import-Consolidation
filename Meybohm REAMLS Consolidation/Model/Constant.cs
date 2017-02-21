using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meybohm_REAMLS_Consolidation.Model
{
    public class Constant
    {
        public static string AIKEN_DOWNLOAD_FOLDER = ConfigurationManager.AppSettings["AIKEN_DOWNLOAD_FOLDER"];
        public static string AUGUSTA_DOWNLOAD_FOLDER = ConfigurationManager.AppSettings["AUGUSTA_DOWNLOAD_FOLDER"];
        public static string AIKEN_ARCHIVE_FOLDER = ConfigurationManager.AppSettings["AIKEN_ARCHIVE_FOLDER"];
        public static string AUGUSTA_ARCHIVE_FOLDER = ConfigurationManager.AppSettings["AUGUSTA_ARCHIVE_FOLDER"];

        public static string CONSOLIDATION_FOLDER = ConfigurationManager.AppSettings["CONSOLIDATION_FOLDER"];
        public static string LOG_FOLDER = ConfigurationManager.AppSettings["LOG_FOLDER"] + DateTime.Today.ToString("MMyyyy") + "\\";

        public static string LOG_FILE = LOG_FOLDER + DateTime.Today.ToString("MMddyyyy") + ".html";

        public static string AIKEN_RESIDENTIAL_HEADER = ConfigurationManager.AppSettings["AIKEN_RESIDENTIAL_HEADER"];
        public static string AUGUSTA_RESIDENTIAL_HEADER = ConfigurationManager.AppSettings["AUGUSTA_RESIDENTIAL_HEADER"];
        public static string AIKEN_AGENT_HEADER = ConfigurationManager.AppSettings["AIKEN_AGENT_HEADER"];
        public static string AUGUSTA_AGENT_HEADER = ConfigurationManager.AppSettings["AUGUSTA_AGENT_HEADER"];
        public static string OFFICE_HEADER = ConfigurationManager.AppSettings["OFFICE_HEADER"];

        public static string MEYBOHM_IMPORT_URL = ConfigurationManager.AppSettings["MEYBOHM_IMPORT_URL"];
        public static string BUILD_FROM_FACTS_URL = ConfigurationManager.AppSettings["BUILD_FROM_FACTS_URL"];

        public static string LISTING_URL = ConfigurationManager.AppSettings["LISTING_URL"];
        
        public static string GOOGLE_API_STATUS_OVER_LIMIT = "OVER_QUERY_LIMIT";
        public static string GOOGLE_API_ZERO_RESULTS = "ZERO_RESULTS";
        public static string[] EMAIL_RECEIVER = ConfigurationManager.AppSettings["EMAIL_RECEIVER"].Split(';');
        public static string[] EMAIL_RECEIVER_INC = ConfigurationManager.AppSettings["EMAIL_RECEIVER_INC"].Split(';');
        public static string EMAIL_SENDER = ConfigurationManager.AppSettings["EMAIL_SENDER"];

        public static Dictionary<string, string> PHOTO_TEST = new Dictionary<string, string>() {
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_1"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_1"]},
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_2"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_2"]},
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_3"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_3"]},
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_4"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_4"]},
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_5"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_5"]},
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_6"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_6"]},
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_7"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_7"]},
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_8"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_8"]},
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_9"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_9"]},
            {ConfigurationManager.AppSettings["PHOTO_TEST_MLSID_10"], ConfigurationManager.AppSettings["PHOTO_TEST_URL_10"]}
        };

        public static Dictionary<string, int> FILE_COUNT = new Dictionary<string, int>() {
            {CityType.Aiken + "_" + FeedType.Residential, Int32.Parse(ConfigurationManager.AppSettings["AIKEN_RES_FILE_COUNT"])},
            {CityType.Aiken + "_" + FeedType.Land, Int32.Parse(ConfigurationManager.AppSettings["AIKEN_LAND_FILE_COUNT"])},
            {CityType.Aiken + "_" + FeedType.Agent, Int32.Parse(ConfigurationManager.AppSettings["AIKEN_AGENT_FILE_COUNT"])},
            {CityType.Aiken + "_" + FeedType.Office, Int32.Parse(ConfigurationManager.AppSettings["AIKEN_OFFICE_FILE_COUNT"])},
            {CityType.Augusta + "_" + FeedType.Residential, Int32.Parse(ConfigurationManager.AppSettings["AUGUSTA_RES_FILE_COUNT"])},
            {CityType.Augusta + "_" + FeedType.Land, Int32.Parse(ConfigurationManager.AppSettings["AUGUSTA_LAND_FILE_COUNT"])},
            {CityType.Augusta + "_" + FeedType.Agent, Int32.Parse(ConfigurationManager.AppSettings["AUGUSTA_AGENT_FILE_COUNT"])},
            {CityType.Augusta + "_" + FeedType.Office, Int32.Parse(ConfigurationManager.AppSettings["AUGUSTA_OFFICE_FILE_COUNT"])}
        };
    }

    public enum MLSType
    {
        Aiken = 1,
        Augusta = 2
    }

    public enum CityType
    {
        Aiken = 0,
        Augusta = 1
    }

    public enum FeedType
    {
        Residential = 0,
        Land = 1,
        Agent = 2,
        Office = 3
    }

    /// <summary>
    /// 
    /// </summary>
    public enum Aiken_RES_Fields
    {
        MLS_Number = 0,
        Street_Number = 1,
        Address = 2,
        Town_Subdivision = 3,
        City = 4,
        State = 5,
        Zip_Code = 6,
        List_Price = 7,
        Listing_Office = 8,
        LA_ID = 9,
        Property_Type = 10,
        Property_Description = 11,
        Year_Built = 12,
        Style = 13,
        Exterior_Features = 14,
        Interior_Features = 15,
        Apx_Heated_SqFt = 16,
        Full_Baths = 17,
        Half_Baths = 18,
        Bedrooms = 19,
        Foundation_Basement = 20,
        Floors = 21,
        Garage = 22,
        Attic = 23,
        Air_Conditioning = 24,
        Elementary_School = 25,
        Middle_School = 26,
        High_School = 27,
        Virtual_Tour = 28,
        Builder_Name = 29,
        New_Construction = 30,
        Property_Status = 31,
        Latitude = 32,
        Longitude = 33,
        Directions = 34,
        List_Date = 35,
        Total_Acres = 36,
        Horses_Allowed = 37,
        Lot_Description = 38,
        County = 39,
        Photo_Location = 40
    }

    /// <summary>
    /// 
    /// </summary>
    public enum Aiken_LAND_Fields
    {
        MLS_Number,
        Street_Number,
        Address,
        Town_Subdivision,
        City,
        State,
        Zip_Code,
        List_Price,
        Listing_Office,
        LA_ID,
        Property_Type,
        Remarks,
        Apx_Heated_SqFt,
        Elementary_School,
        Middle_School,
        High_School,
        Virtual_Tour,
        New_Construction,
        Property_Status,
        Latitude,
        Longitude,
        Directions,
        List_Date,
        Apx_Total_Acreage,
        Horses,
        Present_Use,
        Best_Use,
        Topography,
        Crops,
        Buildings_On_Property,
        Other_Improvements,
        Amenities,
        County,
        Photo_Location
    }

    /// <summary>
    /// 
    /// </summary>
    public enum Aiken_Agent_Fields
    {
        Agent_Email,
        AGENT_ID,
        First_Name,
        Last_Name,
        Office_ID,
        Web_Address,
        Contact_Number,
        Home,
        Mail_Address_1,
        Mail_City,
        Mail_State,
        Mail_Zip_Code
    }

    public enum Augusta_RES_Fields
    {
        Number_Fireplaces,
        AC_Ventilation,
        Address,
        Appliances,
        Apx_Total_Heated_SqFt,
        Apx_Year_Built,
        Attic,
        Basement,
        Bedroom_2_Length,
        Bedroom_2_Level,
        Bedroom_2_Width,
        Bedroom_3_Length,
        Bedroom_3_Level,
        Bedroom_3_Width,
        Bedroom_4_Length,
        Bedroom_4_Level,
        Bedroom_4_Width,
        Bedroom_5_Length,
        Bedroom_5_Level,
        Bedroom_5_Width,
        Bedrooms,
        Breakfast_Rm_Length,
        Breakfast_Rm_Level,
        Breakfast_Rm_Width,
        Builder_Name,
        City,
        County,
        Dining_Rm_Length,
        Dining_Rm_Level,
        Dining_Rm_Width,
        Directions,
        Driveway,
        Elementary_School,
        Exterior_Features,
        Exterior_Finish,
        Extra_Rooms,
        Family_Rm_Length,
        Family_Rm_Level,
        Family_Rm_Width,
        Financing_Type,
        Flooring,
        Foundation_Basement,
        Fuel_Source,
        Full_Baths,
        Garage_Carport,
        Great_Rm_Length,
        Great_Rm_Level,
        Great_Rm_Width,
        Half_Baths,
        Heat_Delivery,
        High_School,
        Interior_Features,
        Kitchen_Length,
        Kitchen_Level,
        Kitchen_Width,
        LA_ID,
        List_Price,
        Listing_Office,
        Living_Rm_Length,
        Living_Rm_Level,
        Living_Rm_Width,
        Lot_Description,
        Lot_Size,
        Middle_School,
        MLS_Number,
        Neighborhood_Amenities,
        New_Construction,
        Owner_Bedroom_Length,
        Owner_Bedroom_Level,
        Owner_Bedroom_Width,
        Photo_Count,
        Pool,
        Property_Description,
        Property_Status,
        Property_Type,
        Roof,
        Sewer,
        Showing_Instructions,
        State,
        Street_Number,
        Style,
        Subdivision,
        Virtual_Tour,
        Water,
        Zip_Code,
        Total_Number_Rooms,
        Total_Acres,
        Latitude,
        Longitude,
        Photo_location
    }

    public enum Augusta_LAND_Fields
    {
        Number_Fireplaces,
        Address,
        Apx_Total_Heated_SqFt,
        Bedrooms,
        Builder_Name,
        City,
        County,
        Directions,
        Elementary_School,
        Exterior_Finish,
        Financing_Type,
        Full_Baths,
        Half_Baths,
        High_School,
        Interior_Features,
        LA_ID,
        List_Price,
        Listing_Office,
        Lot_Description,
        Lot_Size,
        Middle_School,
        MLS_Number,
        Neighborhood_Amenities,
        New_Construction,
        Photo_Count,
        Pool,
        Property_Description,
        Property_Status,
        Property_Type,
        Showing_Instructions,
        State,
        Street_Number,
        Style,
        Subdivision,
        Virtual_Tour,
        Zip_Code,
        Total_Acres,
        Latitude,
        Longitude,
        Land_Description,
        Photo_location
    }

    public enum Augusta_RENT_Fields
    {
        MLS_Number,
        Address,
        Apx_Total_Heated_SqFt,
        Bedrooms,
        City,
        County,
        Date_Available,
        Elementary_School,
        Full_Baths,
        Half_Baths,
        High_School,
        Middle_School,
        Property_Description,
        Rent_Price,
        Subdivision,
        State,
        Zip,
        Photo_Location
    }

    public enum Augusta_Agent_Fields
    {
        Agent_Email,
        AGENT_ID,
        Contact_Number,
        First_Name,
        Last_Name,
        Office_ID,
        Web_Address,
        Home,
        Mail_Address_1,
        Mail_City,
        Mail_State,
        Mail_Zip_Code
    }

    /// <summary>
    /// 
    /// </summary>
    public enum Office_Fields
    {
        Office_ID = 0,
        Office_Name = 1,
        Web_Address = 2,
        Main = 3,
        Fax = 4,
        Mail_Address_1 = 5,
        Mail_City = 6,
        Mail_State = 7,
        Mail_Zip_Code = 8
    }
}
