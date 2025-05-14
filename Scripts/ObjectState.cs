using System.Collections.Generic;
using UnityEngine;

namespace Rerun
{
    /// <summary>
    /// Container for a single object's state at a point in time
    /// </summary>
    [System.Serializable]
    public class ObjectState
    {
        public string objectId;
        public float timestamp;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        
        // Store additional properties as serialized string data
        public List<SerializedProperty> properties = new List<SerializedProperty>();
        
        // Non-serialized runtime dictionary for easier access
        [System.NonSerialized]
        public Dictionary<string, object> propertyCache;
        
        public void BuildCache()
        {
            propertyCache = new Dictionary<string, object>();
            foreach (var prop in properties)
            {
                propertyCache[prop.key] = prop.GetValue();
            }
        }
    }
    
    /// <summary>
    /// Serializable property container to store different value types
    /// </summary>
    [System.Serializable]
    public class SerializedProperty
    {
        public string key;
        public SerializedPropertyType type;
        public string stringValue;
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public Vector3 vector3Value;
        public Quaternion quaternionValue;
        public Color colorValue;
        
        public object GetValue()
        {
            switch (type)
            {
                case SerializedPropertyType.String: return stringValue;
                case SerializedPropertyType.Float: return floatValue;
                case SerializedPropertyType.Int: return intValue;
                case SerializedPropertyType.Bool: return boolValue;
                case SerializedPropertyType.Vector3: return vector3Value;
                case SerializedPropertyType.Quaternion: return quaternionValue;
                case SerializedPropertyType.Color: return colorValue;
                default: return null;
            }
        }
        
        public void SetValue(object value)
        {
            if (value is string stringVal)
            {
                type = SerializedPropertyType.String;
                stringValue = stringVal;
            }
            else if (value is float floatVal)
            {
                type = SerializedPropertyType.Float;
                floatValue = floatVal;
            }
            else if (value is int intVal)
            {
                type = SerializedPropertyType.Int;
                intValue = intVal;
            }
            else if (value is bool boolVal)
            {
                type = SerializedPropertyType.Bool;
                boolValue = boolVal;
            }
            else if (value is Vector3 vector3Val)
            {
                type = SerializedPropertyType.Vector3;
                vector3Value = vector3Val;
            }
            else if (value is Quaternion quaternionVal)
            {
                type = SerializedPropertyType.Quaternion;
                quaternionValue = quaternionVal;
            }
            else if (value is Color colorVal)
            {
                type = SerializedPropertyType.Color;
                colorValue = colorVal;
            }
        }
    }
    
    /// <summary>
    /// Enum to define the type of serialized property
    /// </summary>
    public enum SerializedPropertyType
    {
        String,
        Float,
        Int,
        Bool,
        Vector3,
        Quaternion,
        Color
    }
    
    /// <summary>
    /// Timeline of states for a single object
    /// </summary>
    [System.Serializable]
    public class ObjectStateTimeline
    {
        public string objectId;
        public List<ObjectState> states = new List<ObjectState>();
    }
    
    /// <summary>
    /// Container for the entire recording
    /// </summary>
    [System.Serializable]
    public class StateRecording
    {
        public float totalDuration;
        public List<ObjectStateTimeline> timelines = new List<ObjectStateTimeline>();
        
        // Dictionary for easier lookup (built at runtime)
        [System.NonSerialized]
        public Dictionary<string, ObjectStateTimeline> timelineDict;
        
        public void BuildCache()
        {
            timelineDict = new Dictionary<string, ObjectStateTimeline>();
            foreach (var timeline in timelines)
            {
                timelineDict[timeline.objectId] = timeline;
                
                // Build cache for each state
                foreach (var state in timeline.states)
                {
                    state.BuildCache();
                }
            }
        }
    }
} 