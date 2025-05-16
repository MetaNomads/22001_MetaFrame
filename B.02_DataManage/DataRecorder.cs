using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using Sirenix.OdinInspector;

namespace MetaFrame.Data
{
    /// <summary>
    /// save data to csv files, in the folder named by application start time
    /// WIP: data should save to a database
    /// </summary>
    public class DataRecorder : MonoBehaviour
    {
        [BoxGroup("Output Settings")] [SerializeField] private DataSourceManager _dataManager;
        [BoxGroup("Output Settings")] [FolderPath] public string _savePath;
        [BoxGroup("Output Settings")] [SerializeField] [Tooltip("record interval in milliseconds")] private int interval = 250;

        public bool startRecord = true;
        private string startTime;
        private string sessionPath;
        /*=========================================================================================================================*/

        /*=========================================================================================================================*/
        void Start()
        {
            startTime = DateTime.Now.ToString("MMdd_HH_mm");
            sessionPath = Path.Combine(_savePath, startTime);
            Directory.CreateDirectory(sessionPath);  // Ensure directory exists at start
            
            StartCoroutine(Record());            
        }
        /*=========================================================================================================================*/

        /*=========================================================================================================================*/
        private IEnumerator Record()
        {
            yield return null;
            //yield return new WaitForSeconds(5);
            // This loop runs indefinitely
            while (startRecord)
            {
                try
                {
                    WriteToCSV(Path.Combine(sessionPath, "FACS.csv"), _dataManager.FACSNameList(), _dataManager.FACSValueList());
                    WriteToCSV(Path.Combine(sessionPath, "Body.csv"), _dataManager.BodyNameList(), _dataManager.BodyValueList());
                    WriteToCSV(Path.Combine(sessionPath, "LeftHand.csv"), _dataManager.LeftHandNameList(), _dataManager.LeftHandValueList());
                    WriteToCSV(Path.Combine(sessionPath, "RightHand.csv"), _dataManager.RightHandNameList(), _dataManager.RightHandValueList());
                }
                catch (Exception e)
                {
                    print("Likely an issue with initialization is causing the functions not to complete properly.");
                    Debug.Log("Recording Data Still Initializing. Did not write to CSV.");
                }



                // Wait for "interval" second before the next write
                yield return new WaitForSeconds(interval / 1000f);
            }
        }

        public void WriteToCSV(string filePath, List<string>? nameList, List<object>? valueList)
        {


            
            if (_dataManager.BodyNameList() != null)
            {

                // Prepend "time" and encapsulate fields as necessary
                string headerLine = "time," + string.Join(",", string.Join(",", nameList.Select(o => ConvertAndEncapsulate(o))));
                string valueLine = Time.time.ToString() + "," + string.Join(",", valueList.Select(o => ConvertAndEncapsulate(o)));

                // Write to CSV file
                using (StreamWriter writer = new StreamWriter(filePath, append: true))
                {
                    if (new FileInfo(filePath).Length == 0)  // File is new, write header
                    {
                        writer.WriteLine(headerLine);
                    }
                    writer.WriteLine(valueLine);
                }

            }
        }

        /// <summary>
        /// Encapsulates a field in double quotes if it contains commas, quotes, or newlines.
        /// </summary>
        /// <param name="field">The field to encapsulate.</param>
        /// <returns>The encapsulated field.</returns>
        private string EncapsulateForCSV(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains(";"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\""; // Escape double quotes and encapsulate
            }
            return field;
        }

        private string ConvertAndEncapsulate(object obj)
        {
            // Check if the object is a list and handle accordingly
            if (obj is IEnumerable enumerable && !(obj is string))
            {
                List<string> items = new List<string>();
                if (enumerable != null)
                {
                    foreach (var item in enumerable)
                    {
                        // Check if the item is not null before calling ToString()
                        if (item != null)
                        {
                            // Recursively encapsulate each item in the list
                            items.Add(EncapsulateForCSV(item.ToString()));
                        }
                    }
                    // Join list items
                    return "\"" + string.Join(",", items) + "\""; 
                }
                else {return null;}
                
            }
            else
            {
                // Handle the case when the object itself is null
                return EncapsulateForCSV(obj?.ToString() ?? "");
            }
        }
        /*=========================================================================================================================*/
    }
}
