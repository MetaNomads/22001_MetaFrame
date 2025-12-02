using UnityEngine;
using System.Collections.Generic;
using MetaNomads.Interaction;
using MetaNomads.Data;
using System;


namespace MetaNomads.Data
{

    public class DataSource_Interactable : DataSourceBase<DataSource_Interactable.DataStructure, DataSource_Interactable.RecordingConfig>
    {


        public override string SourceName => "Interactable";

        [SerializeField] internal Gaze_Interactor _gazeInteractor;

        protected override DataStructure CreateData()
        {
            return new DataStructure(this, _gazeInteractor);
        }

        public override Dictionary<string, object> CollectData()
        {

            var data = new Dictionary<string, object>();

            //In case there is a way to ensure the eye tracker and raycast is functional, add it here.
            if (true)
            {
                //Currently gazed object
                if (RecordConfig.GazedObject)
                {
                    data["GazedObject"] = Data.GazedObject.name;
                }


            }

            return data;
        }


        /*=========================================================================================================================*/
        /// <summary>
        /// Interactable Data Structure - Clean property-based access for consistent typing
        /// </summary>

        public class DataStructure
        {
            private readonly DataSource_Interactable _source;
            private readonly Gaze_Interactor _gazeInteractor;



            //Data referenced from the GazeRayCombined script as to avoid overlap and ensure updates are occuring in a single script.
            public DataStructure(DataSource_Interactable source, Gaze_Interactor gaze_Interactor)
            {
                _source = source;
                _gazeInteractor = gaze_Interactor;
            }

            //Gazed Object
            public GameObject GazedObject => _gazeInteractor.GetCurrentlyGazedObject();

        }


        /*=========================================================================================================================*/
        /// <summary>
        /// Eye Recording Configuration
        /// </summary>

        [Serializable]
        public class RecordingConfig
        {
            [Header("Eye Options")]
            [Tooltip("Gazed Object")]
            public bool GazedObject = true;

        }


    }


}




