using UltimateReplay.Storage;
using UnityEngine;
using UltimateReplay;
using UltimateReplay.Storage;
using UnityEditor;
using UnityEngine.UI;

#if (UNITY_STANDALONE || UNITY_EDITOR)
using SimpleFileBrowser;  //https://assetstore.unity.com/packages/tools/gui/runtime-file-browser-113006#description
#endif


namespace Rerun
{
    /// <summary>
    /// The main Rerun class.
    /// </summary>
    [RequireComponent(typeof(RerunPlaybackCameraManager))]
    public class RerunManager : MonoBehaviour
    {
        private ReplayStorageTarget m_MemoryTarget = new ReplayMemoryTarget();
        private ReplayHandle m_RecordHandle = ReplayHandle.invalid;
        private ReplayHandle m_PlaybackHandle = ReplayHandle.invalid;
        private ReplayFileTarget m_FileTarget;
        private RerunPlaybackCameraManager m_RerunPlaybackCameraManager;

        /// <summary>
        /// Property for accessing the record ReplayHandle from Ultimate Replay
        /// </summary>
        public ReplayHandle recordHandle => m_RecordHandle;

        /// <summary>
        /// Property for accessing the playback ReplayHandle from Ultimate Replay
        /// </summary>
        public ReplayHandle playbackHandle => m_PlaybackHandle;

        // Set to true for now, but his could be exposed in editor for flexibility
        private bool m_RecordToFile = true;

        [SerializeField] public bool _DontDestroyOnLoad = true;

        // String prefix for file name. Use inspector, or property to set programmatically
        // For example, use to store session ID, user name, scene name etc., in the file name
        // TODO - Store information like this in the recording itself, or JSON
        [SerializeField] private string m_RecordingPrefix = "";

        private string folderName = "temp";

        /// <summary>
        /// String prefix for filenames of recordings
        /// </summary>
        public string recordingPrefix
        {
            get => m_RecordingPrefix;
            set => m_RecordingPrefix = value;
        }

        // This is the main simulation object root. This should reference a prefab or scene object containing a ReplayObject.
        [Tooltip("The root GameObject of the simulation to be recorded. Must contain a ReplayObject component.")]
        [SerializeField]
        private ReplayObject m_SimulationSource;
        
        /// <summary>
        /// Property for accessing the simulation source object
        /// </summary>
        public ReplayObject SimulationSource => m_SimulationSource;

        // This is the clone simulation object root, that will be replayed using data captured from the source.
        // See Ultimate Replay documentation on clones.
        [Tooltip("A clone GameObject of the simulation source, used for playback. Must contain a ReplayObject component.")]
        [SerializeField]
        private ReplayObject m_SimulationClone;
        
        /// <summary>
        /// Property for accessing the simulation clone object
        /// </summary>
        public ReplayObject SimulationClone => m_SimulationClone;

        // Information about the active replay mode, name of file being recorded/played etc.
        private string m_InfoString;
        
        /// <summary>
        /// Property for setting the info string
        /// </summary>
        public void SetInfoString(string value)
        {
            m_InfoString = value;
            // We don't need to update the UI since RerunGUI is not being used
        }

        /// <summary>
        /// String containing information about the active replay mode, name of file being recorded/played etc.
        /// </summary>
        public string infoString
        {
            get => m_InfoString;
        }

        // Reference to our custom state manager
        [SerializeField]
        public RerunStateManager stateManager;

        public void Awake()
        {
            // Find or create a replay manager
            ReplayManager.ForceAwake();
            m_RerunPlaybackCameraManager = GetComponent<RerunPlaybackCameraManager>();

            m_InfoString = "";
            if (_DontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            
            // Ensure we have a state manager
            EnsureStateManager();
            
            // Make sure object references are correct
            ValidateSimulationReferences();
        }
        
        /// <summary>
        /// Ensures a state manager exists, creating one if necessary
        /// </summary>
        public void EnsureStateManager()
        {
            // Try to find the state manager first
            if (stateManager == null)
            {
                stateManager = GetComponent<RerunStateManager>();
                
                // If still not found, check other objects
                if (stateManager == null)
                {
                    stateManager = FindObjectOfType<RerunStateManager>();
                    
                    // If still not found, create a new one
                    if (stateManager == null)
                    {
                        Debug.LogWarning("No RerunStateManager found in scene. Creating one now.");
                        stateManager = gameObject.AddComponent<RerunStateManager>();
                        
                        // Initialize the state manager
                        stateManager.enabled = true;
                    }
                }
            }
            
            // Log the result
            if (stateManager != null)
            {
                Debug.Log($"RerunManager using StateManager: {stateManager.name}");
            }
            else
            {
                Debug.LogError("Failed to create or find RerunStateManager!");
            }
        }
        
        /// <summary>
        /// Gets the current state manager (useful for other scripts to find it)
        /// </summary>
        public RerunStateManager FindStateManager()
        {
            // Ensure we have a state manager
            if (stateManager == null)
            {
                EnsureStateManager();
            }
            
            return stateManager;
        }

        /// <summary>
        /// Validate and log the state of simulation references to help debugging
        /// </summary>
        private void ValidateSimulationReferences()
        {
            if (m_SimulationSource == null)
            {
                Debug.LogError("RerunManager: SimulationSource is not set!");
            }
            else
            {
                Debug.Log($"RerunManager: SimulationSource is set to {m_SimulationSource.name}");
            }
            
            if (m_SimulationClone == null)
            {
                Debug.LogError("RerunManager: SimulationClone is not set!");
            }
            else
            {
                Debug.Log($"RerunManager: SimulationClone is set to {m_SimulationClone.name}");
            }
        }

        /// <summary>
        /// Enter Live mode starting from the current playback timeframe.
        /// </summary>
        public void Live()
        {
            // If recording then do nothing (recording must be stopped first)
            if (ReplayManager.IsRecording(m_RecordHandle))
            {
                Debug.Log("Cannot switch to Live mode while recording");
                return;
            }

            // If we have an active playback and state manager, use our enhanced Live mode
            if (ReplayManager.IsReplaying(m_PlaybackHandle) && stateManager != null)
            {
                Debug.Log("Using custom state manager for Live transition");
                // Use our custom state manager to handle the transition
                stateManager.LiveFromCurrentPosition();
                return;
            }
            
            // Otherwise use the traditional method
            Debug.Log("Using traditional method for Live transition");
            
            // Stop playback
            StopPlayback();
            
            // Activate source and deactivate clone
            if (m_SimulationSource != null && m_SimulationSource.gameObject != null)
            {
                Debug.Log($"Activating source: {m_SimulationSource.name}");
                m_SimulationSource.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogError("[RerunManager Live] m_SimulationSource is null! Cannot set active.");
            }
            
            if (m_SimulationClone != null && m_SimulationClone.gameObject != null)
            {
                Debug.Log($"Deactivating clone: {m_SimulationClone.name}");
                m_SimulationClone.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("[RerunManager Live] m_SimulationClone is null! Cannot set inactive.");
            }

            m_InfoString = "Live view";
        }

        /// <summary>
        /// Toggles the recording state. Can be called from a single button used to start and stop recording.
        /// </summary>
        public void ToggleRecording()
        {
            // Start a fresh recording
            if (!ReplayManager.IsRecording(m_RecordHandle))
            {
                BeginRecording();
            }
            else
            {
                // --- DEBUG ---
                Debug.Log($"[RerunManager ToggleRecording - Before Stop] m_SimulationClone is {(m_SimulationClone == null ? "NULL" : "Assigned")}");
                // -------------
                // Stop recording and begin playback
                StopRecording();
                Play();
            }
        }

        /// <summary>
        /// Enter Play mode. This will play back any recorded data, from file or memory.
        /// </summary>
        public void Play()
        {
            // If recording then do nothing (recording must be stopped first)
            if (ReplayManager.IsRecording(m_RecordHandle))
            {
                return;
            }

            StopPlayback();

            // m_RerunPlaybackCameraManager.EnableCameras();

            // Begin playback, based on target
            if (m_RecordToFile)
            {
                m_PlaybackHandle = ReplayManager.BeginPlayback(m_FileTarget, null, true);
                string[] filePath = m_FileTarget.FilePath.Split('/');
                m_InfoString = "Playing file: " + filePath[filePath.Length - 1];
            }
            else
            {
                m_PlaybackHandle = ReplayManager.BeginPlayback(m_MemoryTarget, null, true);
                m_InfoString = "Playing from memory";
            }
        }


        /// <summary>
        /// Should some other part of your software need to know what replayfile is being loaded you can register (and de_register)
        /// callback delegates here that get called before the file is loaded. Mostly used to load the scene before the file is loaded.
        /// </summary>
        public delegate void preLoadDelegate(string fileToBeLoaded);

        private preLoadDelegate handlers;

        public void RegisterPreLoadHandler(preLoadDelegate del)
        {
            handlers += del;
        }

        public void DeRegisterPreLoadHandler(preLoadDelegate del)
        {
            handlers -= del;
        }


        /// <summary>
        /// Open a file dialog to load .replay recordings. Starts playback immediately after opening.
        /// </summary>
        public void Open()
        {
            // If recording then do nothing (recording must be stopped first)
            if (ReplayManager.IsRecording(m_RecordHandle))
            {
                return;
            }
/* // Implementatio without using the file browser
            #if UNITY_EDITOR
            var filePath = EditorUtility.OpenFilePanel("Choose Input Event Trace to Load", string.Empty, "replay");
            InternalOpenFile(filePath);
*/


#if UNITY_STANDALONE || UNITY_EDITOR
            FileBrowser.SetDefaultFilter( ".replay" );
            FileBrowser.ShowLoadDialog((paths) => { InternalOpenFile(paths[0]); },
                () => { Debug.Log("Canceled file loading"); },
                FileBrowser.PickMode.Files,
                false,
                Application.persistentDataPath,
                null,
                "Select one ReRun file",
                "Select");

#else
// on Android (Oculus Headset) we do not require ReRun so we exclude the execution here.
//  var filePath = "";
//  InternalOpenFile(filePath);
#endif
        }

        private void InternalOpenFile(string filePath)
        {
            if (handlers != null)
            {
                handlers.Invoke(filePath);
            }


            m_FileTarget = ReplayFileTarget.ReadReplayFile(filePath);
           
            // Load state data if we have a state manager
            if (stateManager != null)
            {
                stateManager.LoadStateRecording(filePath);
            }
            
            Play();
        }

        public bool IsRecording()
        {
            return ReplayManager.IsRecording(m_RecordHandle);
        }

        /// <summary>
        /// Stop playback.
        /// </summary>
        private void StopPlayback()
        {
            // If recording then do nothing (recording must be stopped first)
            if (ReplayManager.IsRecording(m_RecordHandle))
            {
                return;
            }

            // If not playing then do nothing
            if (!ReplayManager.IsReplaying(m_PlaybackHandle))
            {
                return;
            }

            ReplayManager.StopPlayback(ref m_PlaybackHandle);
            m_RerunPlaybackCameraManager.DisableCameras();
        }

        /// <summary>
        /// Stop recording.
        /// </summary>
        public void StopRecording()
        {
            // If not recording then do nothing
            if (!ReplayManager.IsRecording(m_RecordHandle))
            {
                return;
            }
           
            ReplayManager.StopRecording(ref m_RecordHandle);
            if (m_FileTarget != null) 
            {
                Debug.Log("Stopped Recording with length: "+m_FileTarget.Duration);
            }
            else if (m_MemoryTarget != null)
            {
                 Debug.Log("Stopped Recording with length: "+m_MemoryTarget.Duration);
            }
            else
            {
                Debug.Log("Stopped Recording. Target was null, unable to get duration.");
            }
            
            // --- DEBUG ---
            Debug.Log($"[RerunManager StopRecording - Before SetActive] m_SimulationClone is {(m_SimulationClone == null ? "NULL" : "Assigned")}");
            // -------------
            
            if (m_SimulationClone != null && m_SimulationClone.gameObject != null)
            {
                m_SimulationClone.gameObject.SetActive(true); 
            } else {
                 Debug.LogError("[RerunManager StopRecording] m_SimulationClone is null! Cannot set active.");
            }

            if (m_SimulationSource != null && m_SimulationClone != null)
            {
                ReplayObject.CloneReplayObjectIdentity(m_SimulationSource, m_SimulationClone); 
            } else {
                 Debug.LogError("[RerunManager StopRecording] m_SimulationSource or m_SimulationClone is null! Cannot clone identity.");
            }
            
            // Stop state recording if we have a state manager
            if (stateManager != null)
            {
                stateManager.StopStateRecording();
            }
            
            m_InfoString = "Live view";
        }

        public void SetRecordingFolder(string val)
        {
            folderName = val;
        }

        public string GetRecordingFolder()
        {
            return folderName;
        }

        public void BeginRecording(string Prefix)
        {
            m_RecordingPrefix = Prefix;
            BeginRecording();
        }

        public string GetCurrentFolderPath()
        {
            return Application.persistentDataPath + "/" + folderName + "/";
        }
        
        public string GetCurrentFilePath()
        {
            return LastRecordedFilePath;
        }

        private string LastRecordedFilePath;

        /// <summary>
        /// Begin recording.
        /// </summary>
        public void BeginRecording()
        {
            // If recording then do nothing (recording must be stopped first)
            if (ReplayManager.IsRecording(m_RecordHandle))
            {
                return;
            }

            StopPlayback();

            if (m_RecordToFile)
            {
                string fileName = m_RecordingPrefix + "_Rerun_" +
                                  System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".replay";


                string path = Application.persistentDataPath + "/" + folderName + "/";
                System.IO.Directory.CreateDirectory(path);

                m_FileTarget = ReplayFileTarget.CreateReplayFile(path + fileName);
                LastRecordedFilePath = m_FileTarget.FilePath;
                Debug.Log("RecordingToFile" + path + fileName);
                if (m_FileTarget.MemorySize > 0)
                {
                    m_FileTarget.PrepareTarget(ReplayTargetTask.Discard);
                }

                m_RecordHandle = ReplayManager.BeginRecording(m_FileTarget, null, false, true);
                m_InfoString = "Recording file: " + fileName;
                
                // Begin state recording if we have a state manager
                if (stateManager != null)
                {
                    stateManager.BeginStateRecording();
                }
            }
            else
            {
                // Clear old data
                if (m_MemoryTarget.MemorySize > 0)
                {
                    m_MemoryTarget.PrepareTarget(ReplayTargetTask.Discard);
                }

                m_RecordHandle = ReplayManager.BeginRecording(m_MemoryTarget, null, false, true);
                m_InfoString = "Recording into memory";
                
                // Begin state recording if we have a state manager
                if (stateManager != null)
                {
                    stateManager.BeginStateRecording();
                }
            }
        }

        /// <summary>
        /// Safely stop playback without raising exceptions
        /// </summary>
        public void SafeStopPlayback()
        {
            try
            {
                if (ReplayManager.IsReplaying(m_PlaybackHandle))
                {
                    ReplayManager.StopPlayback(ref m_PlaybackHandle);
                    Debug.Log("Playback stopped safely");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error stopping playback: {e.Message}");
            }
        }

        // Add this method to hook into the default live mode transition and use our custom one instead
        public void LiveMode()
        {
            Debug.Log("RerunManager.LiveMode() was called - Redirecting to custom implementation");
            
            // Find our custom state manager
            RerunStateManager stateManager = GetComponent<RerunStateManager>();
            if (stateManager != null)
            {
                // Use our custom implementation
                Debug.Log("Calling custom LiveFromCurrentPosition implementation");
                stateManager.LiveFromCurrentPosition();
            }
            else
            {
                Debug.LogWarning("RerunStateManager not found! Using traditional method for Live transition");
                
                // Fall back to traditional implementation
                // TODO: Add traditional implementation here if needed
            }
        }
    }
}
