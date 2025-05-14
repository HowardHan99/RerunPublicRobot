using UnityEngine;
using UltimateReplay;

namespace Rerun
{
    public class RerunInputManager : MonoBehaviour
    {
        private KeyCode m_LiveModeShortcut = KeyCode.L;
        private KeyCode m_RecordModeShortcut = KeyCode.R;
        private KeyCode m_PlayModeShortcut = KeyCode.P;
        private KeyCode m_OpenShortcut = KeyCode.O;

        private RerunManager m_RerunManager;
        private RerunStateManager m_StateManager;

        void Start()
        {
            // First get the RerunManager
            m_RerunManager = GetComponent<RerunManager>();
            
            if (m_RerunManager == null)
            {
                Debug.LogError("RerunInputManager: RerunManager not found! This script must be attached to the same GameObject as RerunManager.");
                m_RerunManager = FindObjectOfType<RerunManager>();
                
                if (m_RerunManager == null)
                {
                    Debug.LogError("RerunInputManager: No RerunManager found in scene! Input handling will not work.");
                    return;
                }
            }
            
            // Use the new method to find or create a state manager
            Debug.Log("Calling FindStateManager on RerunManager");
            m_StateManager = m_RerunManager.FindStateManager();
            
            if (m_StateManager == null)
            {
                Debug.LogError("StateManager still not found after FindStateManager call!");
            }
            else
            {
                Debug.Log($"Successfully found StateManager: {m_StateManager.name}");
            }
            
            Debug.Log($"RerunInputManager initialized: StateManager {(m_StateManager != null ? "found" : "not found")}");
        }
        
        void Update()
        {
            // TODO - Improve input handling. This is mostly for testing
            
            if (IsReplayKeyPressed(m_RecordModeShortcut))
            {
                m_RerunManager.ToggleRecording();
            }
            if (IsReplayKeyPressed(m_LiveModeShortcut))
            {
                // Use our custom LiveMode method that will use the state manager
                Debug.Log("Live mode key pressed - using custom implementation");
                
                // Check if playback is active
                bool isReplaying = false;
                if (m_RerunManager != null)
                {
                    isReplaying = ReplayManager.IsReplaying(m_RerunManager.playbackHandle);
                    Debug.Log($"Playback active: {isReplaying}");
                }
                
                if (m_StateManager != null && isReplaying)
                {
                    // Use our custom state-based live mode if we're currently in playback
                    Debug.Log("Using state-based LiveFromCurrentPosition");
                    m_StateManager.LiveFromCurrentPosition();
                }
                else if (m_RerunManager != null)
                {
                    // Log why we're falling back
                    if (m_StateManager == null) 
                        Debug.LogError("StateManager is null! Using default Live method.");
                    if (!isReplaying)
                        Debug.Log("No active playback - using default Live method");
                        
                    // Fall back to default implementation
                    Debug.Log("Using default Live method");
                m_RerunManager.Live();
                }
                else
                {
                    Debug.LogError("Both RerunManager and StateManager are null! Cannot transition to Live mode.");
                }
            }
            if (IsReplayKeyPressed(m_PlayModeShortcut))
            {
                m_RerunManager.Play();
            }
            if (IsReplayKeyPressed(m_OpenShortcut))
            {
                m_RerunManager.Open();
            }
            
            if (IsReplayKeyPressed(KeyCode.K))
            {
                m_RerunManager.BeginRecording();
            }
            if (IsReplayKeyPressed(KeyCode.J))
            {
                m_RerunManager.StopRecording();
            }
            
            // Debug helper to find state files
            if (IsReplayKeyPressed(KeyCode.F))
            {
                Debug.Log("F key pressed - attempting to find existing state files");
                if (m_StateManager != null)
                {
                    Debug.Log("Finding existing state files via StateManager...");
                    m_StateManager.FindExistingStateFiles();
                }
                else
                {
                    Debug.LogError("Cannot find state files - StateManager is null!");
                    
                    // Fallback implementation
                    Debug.Log("Using fallback implementation to search for state files");
                    string persistentPath = Application.persistentDataPath;
                    Debug.Log($"Checking in persistentDataPath: {persistentPath}");
                    
                    try
                    {
                        string[] files = System.IO.Directory.GetFiles(persistentPath, "*.rerunstate", System.IO.SearchOption.AllDirectories);
                        foreach (string file in files)
                        {
                            Debug.Log($"Found state file: {file}");
                        }
                        
                        if (files.Length == 0)
                        {
                            Debug.Log("No state files found in persistentDataPath");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error searching for state files: {e.Message}");
                    }
                }
            }
            
            // Debug helper to test state file creation
            if (IsReplayKeyPressed(KeyCode.T))
            {
                Debug.Log("T key pressed - attempting to test state file creation");
                if (m_StateManager != null)
                {
                    Debug.Log("Testing state file creation via StateManager...");
                    m_StateManager.TestSaveStateFile();
                }
                else
                {
                    Debug.LogError("Cannot test state file creation - StateManager is null!");
                    
                    // Fallback implementation
                    Debug.Log("Creating test state file directly...");
                    try
                    {
                        string testFilePath = System.IO.Path.Combine(Application.persistentDataPath, "test.rerunstate");
                        System.IO.File.WriteAllText(testFilePath, "Test state file content");
                        Debug.Log($"Created test file at: {testFilePath}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error creating test file: {e.Message}");
                    }
                }
            }
        }

        private bool IsReplayKeyPressed(KeyCode key)
        {
            // Check for key press
            return Input.GetKeyDown(key);
        }
    }
}