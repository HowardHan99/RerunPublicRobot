using System.Collections.Generic;
using UnityEngine;
using UltimateReplay;

namespace Rerun
{
    /// <summary>
    /// Component to attach to objects that need state tracking
    /// </summary>
    public class TrackedObject : MonoBehaviour
    {
        // Unique ID for this object
        public string objectId;
        
        // Reference to the original ReplayObject (if any)
        public ReplayObject replayObject;
        
        // Properties to track
        [SerializeField]
        private bool trackTransform = true;
        
        [SerializeField]
        private bool trackRigidbody = true;
        
        [SerializeField]
        private bool trackAnimator = true;
        
        [SerializeField]
        private List<string> customProperties = new List<string>();
        
        // Debug settings
        [SerializeField]
        private bool debugMode = true;
        
        // Cached component references
        private Rigidbody cachedRigidbody;
        private Animator cachedAnimator;
        
        private void Awake()
        {
            // Use Debug.Log directly here for critical init logging, bypassing custom Log() if it exists in RerunStateManager
            if (string.IsNullOrEmpty(objectId))
            {
                try
                {
                    objectId = GenerateStableId();
                    Debug.Log($"[TrackedObject AWAKE] On '{gameObject.name}' (InstanceID: {gameObject.GetInstanceID()}): Generated ID: '{objectId}'. ActiveInHierarchy: {gameObject.activeInHierarchy}, Enabled: {this.enabled}, Parent: {(transform.parent ? transform.parent.name : "None")}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[TrackedObject AWAKE] ERROR generating ID for {gameObject.name}: {e.Message}\\n{e.StackTrace}");
                    // Potentially assign a fallback ID or disable if ID generation is critical and fails
                    objectId = $"{gameObject.name}_ERROR_{gameObject.GetInstanceID()}"; 
                }
            }
            else
            {
                Debug.Log($"[TrackedObject AWAKE] On '{gameObject.name}' (InstanceID: {gameObject.GetInstanceID()}): Using pre-assigned ID: '{objectId}'. ActiveInHierarchy: {gameObject.activeInHierarchy}, Enabled: {this.enabled}, Parent: {(transform.parent ? transform.parent.name : "None")}");
            }
            
            // Try to find ReplayObject if not set
            if (replayObject == null)
            {
                replayObject = GetComponent<ReplayObject>();
                if (replayObject != null)
                {
                    // Access the ReplayIdentity value from the inspector
                    // Debug.Log($"Found ReplayObject on {gameObject.name} with ID: {replayObject.GetInstanceID()}");
                }
            }
            
            // Cache component references
            if (trackRigidbody)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
                // if (cachedRigidbody != null)
                // {
                //     Debug.Log($"[{objectId}] Found Rigidbody on {gameObject.name}");
                // }
            }
                
            if (trackAnimator)
            {
                cachedAnimator = GetComponent<Animator>();
                // if (cachedAnimator != null)
                // {
                //     Debug.Log($"[{objectId}] Found Animator on {gameObject.name}");
                // }
            }
        }
        
        /// <summary>
        /// Generate a stable ID based on the object's hierarchy and position
        /// </summary>
        private string GenerateStableId()
        {
            string path = gameObject.name;
            // int siblingIndex = transform.GetSiblingIndex(); // Sibling index can change if objects are reordered.
            
            Transform currentParent = transform.parent;
            while (currentParent != null)
            {
                // Using name only for parent path. If names are not unique, this could be an issue.
                path = $"{currentParent.name}/{path}";
                currentParent = currentParent.parent;
            }
            // Adding instance ID to path makes it unique per instance even if names/paths are same, 
            // but this ID won't match between a source and a clone if that's ever a requirement for this ID.
            // For standalone objects, this is fine.
            return $"{path}_{gameObject.GetInstanceID().ToString("X8")}"; 
            // Original: return $"{gameObject.name}_{path.GetHashCode():X8}"; // Hash can have collisions.
        }
        
        /// <summary>
        /// Capture the current state of this object
        /// </summary>
        public ObjectState CaptureState(float timestamp)
        {
            ObjectState state = new ObjectState
            {
                objectId = this.objectId, // Ensure 'this.objectId' is used
                timestamp = timestamp,
                position = transform.position,
                rotation = transform.rotation,
                scale = transform.localScale,
                properties = new List<SerializedProperty>()
            };
            
            // Capture Rigidbody state
            if (trackRigidbody && cachedRigidbody != null)
            {
                AddProperty(state, "velocity", cachedRigidbody.velocity);
                AddProperty(state, "angularVelocity", cachedRigidbody.angularVelocity);
                AddProperty(state, "isKinematic", cachedRigidbody.isKinematic);
            }
            
            // Capture Animator state
            if (trackAnimator && cachedAnimator != null && cachedAnimator.runtimeAnimatorController != null)
            {
                var info = cachedAnimator.GetCurrentAnimatorStateInfo(0);
                AddProperty(state, "animatorStateHash", info.shortNameHash);
                AddProperty(state, "animatorNormalizedTime", info.normalizedTime);
                AddProperty(state, "animatorSpeed", cachedAnimator.speed);
            }
            
            // Build property cache
            state.BuildCache();
            
            return state;
        }
        
        /// <summary>
        /// Apply a state to this object
        /// </summary>
        public void ApplyState(ObjectState state)
        {
            if (state == null || state.objectId != this.objectId)
            {
                Debug.LogError($"[{this.objectId}] Attempted to apply null state or mismatched ID (state for '{state?.objectId}').");
                return;
            }
            
            if (debugMode) Debug.Log($"[{this.objectId}] Applying state at timestamp {state.timestamp:F3}. CurrentPos: {transform.position}, TargetPos: {state.position}");
            
            // Store initial transform state for debugging
            Vector3 initialPosition = transform.position;
            Quaternion initialRotation = transform.rotation;
            Vector3 initialScale = transform.localScale;
            
            // Check for parent that might be affecting transform
            if (transform.parent != null)
            {
                Debug.Log($"[{this.objectId}] Has parent: {transform.parent.name}, which could affect world transformations");
                
                // Check if we need to convert to local space
                Debug.Log($"[{this.objectId}] World position={initialPosition}, Local position={transform.localPosition}");
                
                // Also log parent's transform
                Debug.Log($"[{this.objectId}] Parent world position={transform.parent.position}, rotation={transform.parent.rotation.eulerAngles}");
            }
            
            // Log the state we're about to apply
            Debug.Log($"[{this.objectId}] Before applying state - Current: pos={initialPosition}, rot={initialRotation.eulerAngles}, scale={initialScale}");
            Debug.Log($"[{this.objectId}] About to apply state - Target: pos={state.position}, rot={state.rotation.eulerAngles}, scale={state.scale}");
            
            // Try two different approaches to set position/rotation
            
            // Approach 1: Direct assignment (the original way)
            transform.position = state.position;
            transform.rotation = state.rotation;
            transform.localScale = state.scale;
            
            // Check if transform was set correctly
            if (Vector3.Distance(transform.position, state.position) > 0.001f)
            {
                // If direct assignment failed, try alternative approach
                Debug.LogWarning($"[{this.objectId}] Direct position assignment failed. Position={transform.position}, Target={state.position}");
                
                // Approach 2: Try setting local position by calculation
                if (transform.parent != null)
                {
                    Debug.Log($"[{this.objectId}] Attempting to set position via local position");
                    
                    // Convert world position to local
                    Vector3 localPos = transform.parent.InverseTransformPoint(state.position);
                    Debug.Log($"[{this.objectId}] Calculated local position: {localPos}");
                    
                    // Set the local position
                    transform.localPosition = localPos;
                    
                    // Check if that worked
                    if (Vector3.Distance(transform.position, state.position) > 0.001f)
                    {
                        Debug.LogError($"[{this.objectId}] Both position setting methods failed! Current pos={transform.position}, Target={state.position}");
                    }
                    else
                    {
                        Debug.Log($"[{this.objectId}] Successfully set position via local position");
                    }
                }
            }
            
            // Verify the transform was changed
            Vector3 afterPosition = transform.position;
            Quaternion afterRotation = transform.rotation;
            Vector3 afterScale = transform.localScale;
            
            // Debug position difference
            if (Vector3.Distance(afterPosition, state.position) > 0.001f)
            {
                Debug.LogError($"[{this.objectId}] Position was not applied correctly! Target={state.position}, Actual={afterPosition}, Difference={Vector3.Distance(afterPosition, state.position)}");
                // Try one more forceful approach
                Debug.Log($"[{this.objectId}] Attempting one more forced position set");
                transform.position = state.position;
            }
            
            // Debug rotation difference
            if (Quaternion.Angle(afterRotation, state.rotation) > 0.1f)
            {
                Debug.LogError($"[{this.objectId}] Rotation was not applied correctly! Target={state.rotation.eulerAngles}, Actual={afterRotation.eulerAngles}, Difference={Quaternion.Angle(afterRotation, state.rotation)}");
                
                // Try using eulerAngles instead
                Debug.Log($"[{this.objectId}] Attempting to set rotation via eulerAngles");
                transform.eulerAngles = state.rotation.eulerAngles;
                
                // Check if that worked
                if (Quaternion.Angle(transform.rotation, state.rotation) > 0.1f)
                {
                    Debug.LogError($"[{this.objectId}] Euler angle approach also failed. Current rotation={transform.rotation.eulerAngles}, Target={state.rotation.eulerAngles}");
                }
            }
            
            // Debug scale difference
            if (Vector3.Distance(afterScale, state.scale) > 0.001f)
            {
                Debug.LogError($"[{this.objectId}] Scale was not applied correctly! Target={state.scale}, Actual={afterScale}, Difference={Vector3.Distance(afterScale, state.scale)}");
                // Force scale once more
                transform.localScale = state.scale;
            }
            
            // Make sure property cache is built
            if (state.propertyCache == null)
            {
                Debug.Log($"[{this.objectId}] Building property cache");
                state.BuildCache();
            }
            
            // Apply Rigidbody properties
            if (trackRigidbody && cachedRigidbody != null)
            {
                if (state.propertyCache.TryGetValue("velocity", out object velocityObj) && velocityObj is Vector3 velocity)
                {
                    Debug.Log($"[{this.objectId}] Setting rigidbody velocity: {velocity}");
                    cachedRigidbody.velocity = velocity;
                }
                
                if (state.propertyCache.TryGetValue("angularVelocity", out object angVelObj) && angVelObj is Vector3 angularVelocity)
                {
                    Debug.Log($"[{this.objectId}] Setting rigidbody angular velocity: {angularVelocity}");
                    cachedRigidbody.angularVelocity = angularVelocity;
                }
                
                if (state.propertyCache.TryGetValue("isKinematic", out object isKinematicObj) && isKinematicObj is bool isKinematic)
                {
                    Debug.Log($"[{this.objectId}] Setting rigidbody isKinematic: {isKinematic}");
                    cachedRigidbody.isKinematic = isKinematic;
                }
            }
            
            // Apply Animator properties
            if (trackAnimator && cachedAnimator != null && cachedAnimator.runtimeAnimatorController != null)
            {
                if (state.propertyCache.TryGetValue("animatorStateHash", out object stateHashObj) && 
                    state.propertyCache.TryGetValue("animatorNormalizedTime", out object normalizedTimeObj))
                {
                    int stateHash = (int)stateHashObj;
                    float normalizedTime = (float)normalizedTimeObj;
                    
                    Debug.Log($"[{this.objectId}] Setting animator state: hash={stateHash}, time={normalizedTime}");
                    cachedAnimator.Play(stateHash, 0, normalizedTime);
                }
                
                if (state.propertyCache.TryGetValue("animatorSpeed", out object speedObj) && speedObj is float speed)
                {
                    Debug.Log($"[{this.objectId}] Setting animator speed: {speed}");
                    cachedAnimator.speed = speed;
                }
            }
            
            // Debug final state after all properties are applied
            Debug.Log($"[{this.objectId}] After applying state - Final: pos={transform.position}, rot={transform.rotation.eulerAngles}, scale={transform.localScale}");
            
            // Calculate final differences for logging
            float finalPosDiff = Vector3.Distance(transform.position, state.position);
            float finalRotDiff = Quaternion.Angle(transform.rotation, state.rotation);
            Debug.Log($"[{this.objectId}] Final differences - Position: {finalPosDiff:F6}, Rotation: {finalRotDiff:F6} degrees");
            
            // Log parent and local position to detect hierarchy issues
            if (transform.parent != null)
            {
                Debug.Log($"[{this.objectId}] Parent: {transform.parent.name}, LocalPosition: {transform.localPosition}");
                
                // Print global vs local transform to identify any issues with coordinate spaces
                Debug.Log($"[{this.objectId}] World position={transform.position}, Local position={transform.localPosition}");
            }
        }
        
        /// <summary>
        /// Helper method to add a property to a state
        /// </summary>
        private void AddProperty(ObjectState state, string key, object value)
        {
            var prop = new SerializedProperty { key = key };
            prop.SetValue(value);
            state.properties.Add(prop);
        }

        void OnEnable()
        {
            Debug.Log($"[TrackedObject ONENABLE] On '{gameObject.name}' (ID: '{objectId}', InstanceID: {gameObject.GetInstanceID()}): Component Enabled. ActiveInHierarchy: {gameObject.activeInHierarchy}");
            
            // Attempt to self-register with RerunStateManager if available
            // Ensure RerunStateManager has initialized its Instance by this point (Script Execution Order might be needed)
            if (RerunStateManager.Instance != null)
            {
                RerunStateManager.Instance.RegisterTrackedObject(this);
            }
            else
            {
                // This might happen if TrackedObjects enable before RerunStateManager's Awake/Instance is set.
                // RerunStateManager's delayed scan can act as a backup.
                Debug.LogWarning($"[TrackedObject ONENABLE] For '{gameObject.name}': RerunStateManager.Instance is null. Cannot self-register. Will rely on manager's scan.");
            }
        }

        void OnDisable()
        {
            Debug.Log($"[TrackedObject ONDISABLE] On '{gameObject.name}' (ID: '{objectId}', InstanceID: {gameObject.GetInstanceID()}): Component Disabled.");
            if (RerunStateManager.Instance != null)
            {
                RerunStateManager.Instance.UnregisterTrackedObject(this);
            }
        }
    }
} 