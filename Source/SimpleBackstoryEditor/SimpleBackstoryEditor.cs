using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using RimWorld;
using Verse;

using SimpleBEUtilities;

namespace SimpleBE
{
    [StaticConstructorOnStartup]
    public static class SimpleBERun
    {
        static SimpleBERun()
        {
            Log.Message("SBE Starting");
            
            // Check that this is an expected OS and thus file system
            if( SimpleBEFileKnowledge.validOS )
            {
                CreateBackstoryXmlFile();

                CreateExampleReplacementBackstoryXmlFile();

                UpdateRimworldBackstoriesFromReplacementBacksoryXmlFile();

            }
            
            Log.Message("SBE Ending");
        }

        /// <summary>
        /// Creates a file which has all the fields the editor supports for each backstory
        /// </summary>
        static void CreateBackstoryXmlFile()
        {
            // Fill the mod's custom structure with data from the Rimworld Database
            SerializableBackstoryArray sba = BuildArrayFromRimworldBackstoryDatabase();

            // If successful
            if (null != sba)
            {
                // Write out the data for users to look through and use to make replacement stories
                SimpleBESerialization.WriteBackstory(sba, SimpleBEFileKnowledge.backstoriesFileName);                
            }
        }

        /// <summary>
        /// Creates a file which has all the the fields and all the replacement fields the editor supports for a subset of backstories to use as an example.
        /// </summary>
        static void CreateExampleReplacementBackstoryXmlFile()
        {
            // Fill the mod's custom structure with a few stories from the Rimworld Database with additional replacement fields thrown in
            SerializableBackstoryArray sba = BuildSampleReplacementBackstoryArrayFromRimworldBackstoryDatabase();

            // If successful
            if (null != sba)
            {
                // Set some example text into each of the replacement fields created by this mod
                foreach( ReplacementBackstory rb in sba.backstoriesArray.Cast<ReplacementBackstory>())
                {
                    rb.SetExampleText();
                }

                // Write out the example file
                SimpleBESerialization.WriteBackstory(sba, SimpleBEFileKnowledge.replacementsExampleFileName);
            }
        }

        /// <summary>
        /// Reads in a file with replacement backstories and uses that data to update the Rimworld database
        /// </summary>
        static void UpdateRimworldBackstoriesFromReplacementBacksoryXmlFile()
        {
            // Fill the mod's custom structure with the replacement stories provided by the user in a file
            SerializableBackstoryArray sba = BuildReplacementArrayFromFile();

            // If successful
            if (null != sba)
            {
                // Update the database that Rimworld will use for the rest of the play session
                UpdateRimworldBackstoryDatabase(sba);                
            }
        }


        /// <summary>
        /// Creates an array with supported fields of each backstory in the backstory database.  
        /// Wrapper function to make the code more self documenting.
        /// </summary>
        /// <returns>SerializableBackstoryArray</returns>
        static SerializableBackstoryArray BuildArrayFromRimworldBackstoryDatabase()
        {
            return BackstoriesUtil<SerializableBackstory>.Build(RimWorld.BackstoryDatabase.allBackstories.Count, SimpleBEFileKnowledge.backstoriesFileName);
        }

        /// <summary>
        /// Creates an array with a few backstories in it to be used to make example schemas for the example databaseEntry xml file.  
        /// Wrapper function to make the code more self documenting.
        /// </summary>
        /// <returns>SerializableBackstoryArray</returns>
        static SerializableBackstoryArray BuildSampleReplacementBackstoryArrayFromRimworldBackstoryDatabase()
        {
            return BackstoriesUtil<ReplacementBackstory>.Build(SimpleBEFileKnowledge.numberOfExamplesToGenerate, SimpleBEFileKnowledge.replacementsExampleFileName);
        }

        /// <summary>
        /// From an xml file, creates an array databaseEntry backstories that can be pushed to the backstory database.  
        /// Wrapper function to make the code more self documenting.
        /// </summary>
        /// <returns>SerializableBackstoryArray</returns>
        static SerializableBackstoryArray BuildReplacementArrayFromFile()
        {
            return SimpleBESerialization.ReadBackstory(typeof(ReplacementBackstory), SimpleBEFileKnowledge.replacementsFileName);
        }

        /// <summary>
        /// Iterates over the the data in an array and updates the Rimworld Backstory Database
        /// </summary>
        /// <param name="sba">A SerializableBackstoryArray with a ReplacementBackstory array of data</param>
        static void UpdateRimworldBackstoryDatabase(SerializableBackstoryArray sba)
        {
            // Check that the array count reported in the file and the array length generated by parsing the file match
            if (sba.count == sba.backstoriesArray.Length)
            {
                Log.Message($"Updating {sba.count} stories with details from {SimpleBEFileKnowledge.replacementsFileName}.");

                // Iterated over the replacement backstories
                foreach (ReplacementBackstory rb in sba.backstoriesArray.Cast<ReplacementBackstory>())
                {
                    // Get the story that needs to be updated based on the identifier provided
                    BackstoryDatabase.TryGetWithIdentifier(rb.identifier, out Backstory databaseEntry, false);
                    
                    // If the description isn't null, use it, otherwise default back to the original
                    databaseEntry.baseDesc = rb.replacementDesc ?? databaseEntry.baseDesc;

                    // If the tiltes aren't null, use them, otherwise default back to the originals
                    databaseEntry.SetTitle(
                        rb.replacementTitle ?? databaseEntry.title, 
                        rb.replacementTitleFemale ?? databaseEntry.titleFemale 
                        );

                    // If the short tiltes aren't null, use them, otherwise default back to the originals
                    databaseEntry.SetTitleShort(
                        rb.replacementTitleShort ?? databaseEntry.titleShort, 
                        rb.replacementTitleShortFemale ?? databaseEntry.titleShortFemale
                        );

                    // Now that the data is filled in appropriately, write it back to the database
                    BackstoryDatabase.allBackstories.SetOrAdd(rb.key, databaseEntry);

                }

            }
            else
            {
                // The user may have added more stories than they meant to or failed to update the count.
                Log.Message($"{SimpleBEFileKnowledge.replacementsFileName} reports {sba.count} stories, but contains {sba.backstoriesArray.Length} stories.");
                Log.Message("Errors assumed. Fix file and try again.");
            }

        }
    }

    /// <summary>
    /// Utility class for creating SerializableBackstoryArray.  Takes the internal type of the array, 
    /// which can differ based on use case. e.g. Writing out the database vs. Writing or reading replacement stories
    /// </summary>
    /// <typeparam name="ArrayBaseType"></typeparam>
    public static class BackstoriesUtil<ArrayBaseType> where ArrayBaseType : SerializableBackstory , new()
    {
        /// <summary>
        /// Produces a backstory array of ArrayBaseType which is a SerializableBackstory or Inheritting Class.  If fileName is not null, 
        /// and filename already exists, processing is pre-emptively skipped, improving script run time.  If arraySize is less than 
        /// BackstoryDatabase.allBackstories, a smaller array is produced and looping over the database is cut short, improving script run time.
        /// </summary>
        /// <param name="fileName">Name of file that backstory array may be destined for.  Processing skipped if file exists.</param>
        /// <param name="arraySize">Size of SerializableBackstoryArray to be produced.  Processing cut short if smaller than BackstoryDatabase.allBackstories.</param>
        /// <returns>SerializableBackstoryArray</returns>
        public static SerializableBackstoryArray Build(int arraySize, string fileName = null)
        {
            // Generate a full path filename
            string file_location = SimpleBEFileKnowledge.GetFullPathAndFileName(fileName);

            // If a file name was provide, skip this entire process if the file already exists
            if (!System.IO.File.Exists(file_location) || fileName == null)
            {
                bool build_error = false;

                int i = 0;

                // Setup an array for the size passed in
                ArrayBaseType[] backstoriesArray = new ArrayBaseType[arraySize];

                Log.Message($"Building {typeof(ArrayBaseType).Name} array with {backstoriesArray.Length} backstories.");

                // Iterate over the database
                foreach (KeyValuePair<string, Backstory> story in BackstoryDatabase.allBackstories)
                {
                    // fill in the fields that this mod will support with data from the database
                    ArrayBaseType backstory = new ArrayBaseType
                    {
                        key = story.Key,
                        identifier = story.Value.identifier,
                        title = story.Value.title,
                        titleFemale = story.Value.titleFemale,
                        titleShort = story.Value.titleShort,
                        titleShortFemale = story.Value.titleShortFemale,
                        baseDesc = story.Value.baseDesc
                    };

                    // allow the process to continue as long as the database size isn't exceeded
                    if (i >= 0 && i < backstoriesArray.Length)
                    {
                        backstoriesArray[i] = backstory;
                        i++;
                        
                        // or the predetermined length, whichever comes first
                        if(i==backstoriesArray.Length)
                        {
                            /* While this funtion runs fine when the intention is to fill an allBackstories.Count array,
                             * in the case where the intent is to stop early,
                             * breaking here minimizes the amount of extra work that is done. 
                             */
                            break;
                        }
                    }                    
                    else
                    {
                        build_error = true;
                        break;
                    }                    

                }

                // as long as there's no error
                if (!build_error)
                {
                    // create the mod's custom structure and fill it with data from the backstory database
                    Log.Message($"Loaded {i} {typeof(ArrayBaseType).Name}.");
                    return new SerializableBackstoryArray(backstoriesArray, i);
                }
                else
                {
                    Log.Message($"Error building {typeof(ArrayBaseType).Name} set.");
                    return null;
                }

            }
            else
            {
                Log.Message($"{file_location} already exists.");
                Log.Message("Delete to regenerate.");
                return null;
            }
        }
    }

    /// <summary>
    /// A structure representing the fields this mod supports.  The class is setup to be serializable.
    /// </summary>
    public class SerializableBackstory
    {
        public string key;
        public string identifier;
        public string title;
        public string titleFemale;
        public string titleShort;
        public string titleShortFemale;
        public string baseDesc; 

        public SerializableBackstory()
        {
            
        }
    }

    /// <summary>
    /// An extension of SerializableBackstory, this structure adds replacement fields for updating the database.
    /// </summary>
    public class ReplacementBackstory : SerializableBackstory
    {
        public string replacementTitle;
        public string replacementTitleFemale;
        public string replacementTitleShort;
        public string replacementTitleShortFemale;
        public string replacementDesc;

        public ReplacementBackstory()
        {
            
        }

        /// <summary>
        /// If the fields are null, they will not be written out when an example file is generated.  This puts some filler text in the fields.
        /// </summary>
        public void SetExampleText()
        {
            replacementTitle = "Replacement Title";
            replacementTitleFemale = "Remove Unused Tags";
            replacementTitleShort = "Rplcmnt Ttl";
            replacementTitleShortFemale = "Rmv Unsd Tgs";
            replacementDesc = "Replacement Description";
        }
    }

    /// <summary>
    /// An array of SerializableBackstoryArray or one of its subclasses.  A count is included for 
    /// cross-checking purposes.  This class is designed to be serializable.
    /// </summary>
    public class SerializableBackstoryArray
    {
        public int count;

        public SerializableBackstory[] backstoriesArray;

        public SerializableBackstoryArray()
        {
            count = 0;
            backstoriesArray = null;
        }

        public SerializableBackstoryArray(SerializableBackstory[] array, int arrayCount)
        {
            count = arrayCount;
            backstoriesArray = array;
        }
    }

    /// <summary>
    /// File structure related constants and methods.
    /// </summary>
    static class SimpleBEFileKnowledge
    {
        // Enumeration of possible OS situations
        private enum SimpleBEOS{
            SimpleBEUnknown = 0,
            SimpleBEOther = 1,
            SimpleBEWindows = 2,
            SimpleBELinux = 3,
            SimpleBEMacOS = 4,
        }
                
        public static readonly bool validOS; // Initialized by constructor
        private static readonly SimpleBEOS operatingSystem; // Initialized by constructor
        private static readonly string path; // Initialized by constructor

        // File Names
        public static readonly string backstoriesFileName = "backstories.xml";
        public static readonly string replacementsFileName = "replacements.xml";
        public static readonly string replacementsExampleFileName = "replacementsExample.xml";

        public static readonly int numberOfExamplesToGenerate = 10;

        //public static readonly string currentdirectory = Directory.GetCurrentDirectory();

        // Base Paths
        private static readonly string localLowPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace("Roaming", "LocalLow");
        private static readonly string linuxLowPath = "~/.config/unity3d";
        private static readonly string macOSLowPath = "~/Library/Application Support";

        private static readonly string osPathDivider; // Initialized by constructor

        // Game Name
        private static readonly string gameName = "Rimworld";
        private static readonly string authorship = " by ";
        private static readonly string studioName = "Ludeon Studios";

        // Mod Name
        private static readonly string assemblyName = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title;

        /// <summary>
        /// Static constructor fills out the the operatingSystem, path, osPathDivider, and validOS variables.
        /// </summary>
        static SimpleBEFileKnowledge()
        {
            validOS = GetSimpleBEOS( out operatingSystem );

            if( validOS )
            {
                ComposePath(operatingSystem, out path, out osPathDivider);
                // Ensure path exists for future file operations
                System.IO.Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Composes an appropriate save file location string depending on the os
        /// </summary>
        /// <param name="os"></param>
        /// <param name="composedPath"></param>
        /// <param name="pathDivider"></param>
        private static void ComposePath(SimpleBEOS os, out string composedPath, out string pathDivider)
        {
            // https://www.rimworldwiki.com/wiki/Save_file
            // Windows : %USERPROFILE%\Appdata\LocalLow    \ Ludeon Studios \ RimWorld by Ludeon Studios \
            // Linux : ~/.config/unity3d                   / Ludeon Studios / RimWorld by Ludeon Studios /
            // MacOS: ~/Library/Application Support        / RimWorld

            composedPath = null;
            pathDivider = "/";

            if( validOS )
            {
                if (os == SimpleBEOS.SimpleBEWindows)
                {
                    pathDivider = "\\";
                    composedPath = localLowPath + osPathDivider + studioName + osPathDivider + gameName + authorship + studioName + osPathDivider + assemblyName;
                }
                else if (os == SimpleBEOS.SimpleBELinux)
                {
                    composedPath = linuxLowPath + osPathDivider + studioName + osPathDivider + gameName + authorship + studioName + osPathDivider + assemblyName;
                }
                else // os == SimpleBEOS.SimpleBEMacOS
                {
                    composedPath = macOSLowPath + osPathDivider + gameName + osPathDivider + assemblyName;
                }
            }                                   
        }

        /// <summary>
        /// Determines which Operating System the mod is operating on
        /// </summary>
        /// <param name="os"></param>
        /// <returns></returns>
        private static bool GetSimpleBEOS(out SimpleBEOS os)
        {
            // System.OperatingSystem is the future
            /*
            bool isLinux = System.OperatingSystem.IsLinux();
            bool isWindows = System.OperatingSystem.IsWindows();
            bool isMacOS = System.OperatingSystem.IsMacOS();
            */

            bool isWindows, isLinux, isMacOS, isValid;

            isWindows = isLinux = isMacOS = true;
            isValid = false;
            os = SimpleBEOS.SimpleBEUnknown;

            // RuntimeInformation and InteropServices is the now
            isWindows &= RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            isLinux &= RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            isMacOS &= RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

            // PlatformID is essentially deprecated.
            /*
            System.OperatingSystem osInfo = System.Environment.OSVersion;
            System.PlatformID platformID = osInfo.Platform;

            isWindows &= (platformID == PlatformID.Win32NT);
            isLinux &= (platformID == PlatformID.Other);
            isMacOS &= (platformID == PlatformID.MacOSX);
            */


            // if more than one is true
            if (  (isWindows & isLinux ) | (isWindows & isMacOS) | (isLinux & isMacOS) )
            {
                // Something is wrong
                Log.Message("Detected multiple Operating Systems");
                os = SimpleBEOS.SimpleBEUnknown;
            }
            // if none of the options are true
            else if ( !( isWindows | isMacOS | isWindows) )
            {
                // Some unexpected OS might be true
                Log.Message("Unexpected Operating System.  Mod may need updating.");
                os = SimpleBEOS.SimpleBEOther;
            }
            else if(isWindows)
            {                
                Log.Message("Detected Windows");
                os = SimpleBEOS.SimpleBEWindows;
                isValid = true;
            }
            else if(isMacOS)
            {
                Log.Message("Detected Mac");
                os = SimpleBEOS.SimpleBEMacOS;
                isValid = true;

            }
            else if (isLinux)
            {
                Log.Message("Detected Linux");
                os = SimpleBEOS.SimpleBELinux;
                isValid = true;

            }
            else
            {
                Log.Message("Unexpected exit while determining OS.");
            }
                        
            return isValid;
        }


        /// <summary>
        /// Given a filename, generates the full path to where this mod stores data on Windows.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static string GetFullPathAndFileName(string filename)
        {
            return path + osPathDivider + filename;
        }
    }
}
