using System;
using System.Reflection;
using System.Xml.Serialization;

using Verse;

using SimpleBE;

namespace SimpleBEUtilities
{
    /// <summary>
    /// Deals with XML aspects of Serialization
    /// </summary>
    internal static class SimpleBESerialization
    {
        /// <summary>
        /// Given a container type, generates an XmlAttributeOverrides object replacing overriddenType with overrideType
        /// </summary>
        /// <param name="containerType"></param>
        /// <param name="overriddenType"></param>
        /// <param name="overrideType"></param>
        /// <returns>XmlAttributeOverrides</returns>
        internal static XmlAttributeOverrides GetOveridesForType(Type containerType, Type overriddenType, Type overrideType)
        {
            Console.WriteLine($"Creating override of type {overrideType.Name} to override {overriddenType.Name} within {containerType.Name} ");

            // Create the XmlAttributeOverrides object.
            XmlAttributeOverrides attrOverrides = new XmlAttributeOverrides();

            //Look through all the fields for a field with overriddenType
            FieldInfo[] myFieldInfo;

            // Get the information related to all public fields of containerType
            myFieldInfo = containerType.GetFields();

            //Look through each field to see if it matches the overridden type
            foreach (FieldInfo field in myFieldInfo)
            {
                string fieldName = null;
                bool typeFound = false;

                // If a field matches the overriden type
                if (field.FieldType == overriddenType)
                {
                    typeFound = true;
                }
                // or if it's an array of the overriden type
                else if (field.FieldType.HasElementType)
                {
                    if (field.FieldType.GetElementType() == overriddenType)
                    {
                        typeFound = true;
                    }

                }
                // use that fields Name when building the override
                if (typeFound)
                {
                    fieldName = field.Name;
                }

                // so if the name was found
                if (fieldName != null)
                {
                    /* Each overridden field, property, or type requires
                    an XmlAttributes object. */
                    XmlAttributes attrs = new XmlAttributes();

                    /* Create an XmlElementAttribute to override the
                    field that returns overriden objects. The overridden field
                    returns override type objects instead. */
                    XmlElementAttribute attr = new XmlElementAttribute();
                    attr.ElementName = overrideType.Name;
                    attr.Type = overrideType;

                    // Add the element to the collection of elements.
                    attrs.XmlElements.Add(attr);

                    /* Add the type of the class that contains the overridden
                    member and the XmlAttributes to override it with to the
                    XmlAttributeOverrides object. */
                    attrOverrides.Add(containerType, fieldName, attrs);
                    Console.WriteLine("Adding an override '{0}' within the type {1}", fieldName, containerType.Name);
                }
            }

            Console.WriteLine("\nThe field types of class '{0}' are :\n", containerType);
            for (int i = 0; i < myFieldInfo.Length; i++)
            {

                // Display name and type of the concerned member.
                Console.WriteLine("'{0}' is a {1}", myFieldInfo[i].Name, myFieldInfo[i].FieldType);

                if (myFieldInfo[i].FieldType.HasElementType)
                {
                    Type elementType = myFieldInfo[i].FieldType.GetElementType();
                    Console.WriteLine("An array with elements of type '{0}'", elementType);

                }
            }

            return attrOverrides;
        }

        /// <summary>
        /// Given a SerializableBackstoryArray and a file name, generates the XML file representing that array at the save file location.
        /// </summary>
        /// <param name="sba"></param>
        /// <param name="file_name"></param>
        internal static void WriteBackstory(SerializableBackstoryArray sba, string file_name)
        {
            // The full path and file name
            string file_location = SimpleBEFileKnowledge.GetFullPathAndFileName(file_name);            

            // As long as the file does not already exist
            if (!System.IO.File.Exists(file_location))
            {
                // Create the file
                System.IO.FileStream file = System.IO.File.Create(file_location);

                // Determine the class of backstory array passed in
                Type arrayType = sba.backstoriesArray.GetType().GetElementType();

                // Generate the override needed to properly write out the arrayType
                XmlAttributeOverrides attrOverrides = SimpleBESerialization.GetOveridesForType(sba.GetType(), typeof(SerializableBackstory), arrayType);

                // Create a serializer, which knows how to make XML files
                XmlSerializer serialiazer = new XmlSerializer(typeof(SerializableBackstoryArray), attrOverrides);

                Log.Message($"Creating {file_location}.");                

                // Make the file
                serialiazer.Serialize(file, sba);

                // Close the file
                file.Close();
            }
            else
            {
                Log.Message($"{file_location} already exists.");
                Log.Message("Delete file to regenerate.");
            }

        }

        /// <summary>
        /// Given an array type and file name, read a SerializableBackstoryArray of that type from the save file location.
        /// </summary>
        /// <param name="arrayType"></param>
        /// <param name="file_name"></param>
        /// <returns>SerializableBackstoryArray</returns>
        internal static SerializableBackstoryArray ReadBackstory(Type arrayType, string file_name)
        {
            // The full path and file name
            string file_location = SimpleBEFileKnowledge.GetFullPathAndFileName(file_name);

            // Object to return
            SerializableBackstoryArray sba = null;

            // As long as the file already exists
            if (System.IO.File.Exists(file_location))
            {
                // Open the file
                System.IO.FileStream file = System.IO.File.OpenRead(file_location);

                // Generate the override needed to properly read the arrayType
                XmlAttributeOverrides attrOverrides = SimpleBESerialization.GetOveridesForType(typeof(SerializableBackstoryArray), typeof(SerializableBackstory), arrayType);

                // Create a serializer, which knows how to read XML files
                XmlSerializer serialiazer = new XmlSerializer(typeof(SerializableBackstoryArray), attrOverrides);

                Log.Message($"Reading {file_location}.");

                // Read the file
                sba = (SerializableBackstoryArray)serialiazer.Deserialize(file);

                // Close the file
                file.Close();
            }
            else
            {
                Log.Message($"{file_location} did not exist.");
                Log.Message("You must provide file to read.");
            }

            // Return the array
            return sba;
        }
    }
}
