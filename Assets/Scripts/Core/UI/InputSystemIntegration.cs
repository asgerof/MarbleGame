using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace MarbleMaker.Core.UI
{
    /// <summary>
    /// Input System integration for UI navigation
    /// From UI docs: "Gamepad (Deck, Switch Joy-Cons) ActionAsset clone with Navigate, Submit, Cancel → UITK focus engine"
    /// "Navigation order tagged via FocusController.SetFocusIndex()"
    /// </summary>
    [CreateAssetMenu(fileName = "InputSystemIntegration", menuName = "MarbleMaker/UI/Input System Integration")]
    public class InputSystemIntegration : ScriptableObject
    {
        [Header("Input Settings")]
        [SerializeField] private bool enableGamepadNavigation = true;
        [SerializeField] private bool enableTouchInput = true;
        [SerializeField] private bool enableMouseInput = true;
        
        [Header("Navigation Settings")]
        [SerializeField] private float navigationRepeatDelay = 0.5f;
        [SerializeField] private float navigationRepeatRate = 0.1f;
        
        [Header("3D World Interaction")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask raycastLayers = -1;
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogging = false;
        
        // Input Action References
        private InputAction navigateAction;
        private InputAction submitAction;
        private InputAction cancelAction;
        private InputAction pointAction;
        private InputAction clickAction;
        
        // UI References
        private UIDocument uiDocument;
        private GameUIManager gameUIManager;
        
        // State
        private bool isInitialized = false;
        private VisualElement currentFocusedElement;
        private int currentFocusIndex = 0;
        
        /// <summary>
        /// Gets whether gamepad navigation is enabled
        /// </summary>
        public bool IsGamepadNavigationEnabled => enableGamepadNavigation;
        
        /// <summary>
        /// Initializes the input system integration
        /// </summary>
        /// <param name="uiDocument">UI Document to integrate with</param>
        /// <param name="gameUIManager">Game UI Manager instance</param>
        public void Initialize(UIDocument uiDocument, GameUIManager gameUIManager)
        {
            if (isInitialized)
                return;
            
            this.uiDocument = uiDocument;
            this.gameUIManager = gameUIManager;
            
            SetupInputActions();
            SetupUINavigation();
            SetupWorldInteraction();
            
            isInitialized = true;
            
            if (enableDebugLogging)
                Debug.Log("InputSystemIntegration: Initialized");
        }
        
        /// <summary>
        /// Cleans up input system integration
        /// </summary>
        public void Cleanup()
        {
            if (!isInitialized)
                return;
            
            CleanupInputActions();
            isInitialized = false;
            
            if (enableDebugLogging)
                Debug.Log("InputSystemIntegration: Cleaned up");
        }
        
        /// <summary>
        /// Sets up input actions for UI navigation
        /// From UI docs: "ActionAsset clone with Navigate, Submit, Cancel"
        /// </summary>
        private void SetupInputActions()
        {
            // Create input actions (in a full implementation, these would come from an Input Action Asset)
            navigateAction = new InputAction("Navigate", binding: "<Gamepad>/dpad");
            navigateAction.AddCompositeBinding("2DVector")
                .With("Up", "<Gamepad>/dpad/up")
                .With("Down", "<Gamepad>/dpad/down")
                .With("Left", "<Gamepad>/dpad/left")
                .With("Right", "<Gamepad>/dpad/right");
            
            submitAction = new InputAction("Submit", binding: "<Gamepad>/buttonSouth");
            submitAction.AddBinding("<Keyboard>/enter");
            submitAction.AddBinding("<Keyboard>/space");
            
            cancelAction = new InputAction("Cancel", binding: "<Gamepad>/buttonEast");
            cancelAction.AddBinding("<Keyboard>/escape");
            
            pointAction = new InputAction("Point", binding: "<Mouse>/position");
            pointAction.AddBinding("<Touchscreen>/primaryTouch/position");
            
            clickAction = new InputAction("Click", binding: "<Mouse>/leftButton");
            clickAction.AddBinding("<Touchscreen>/primaryTouch/tap");
            
            // Enable actions
            navigateAction.Enable();
            submitAction.Enable();
            cancelAction.Enable();
            pointAction.Enable();
            clickAction.Enable();
            
            // Setup callbacks
            navigateAction.performed += OnNavigate;
            submitAction.performed += OnSubmit;
            cancelAction.performed += OnCancel;
            clickAction.performed += OnClick;
        }
        
        /// <summary>
        /// Cleans up input actions
        /// </summary>
        private void CleanupInputActions()
        {
            navigateAction?.Disable();
            submitAction?.Disable();
            cancelAction?.Disable();
            pointAction?.Disable();
            clickAction?.Disable();
            
            navigateAction?.Dispose();
            submitAction?.Dispose();
            cancelAction?.Dispose();
            pointAction?.Dispose();
            clickAction?.Dispose();
        }
        
        /// <summary>
        /// Sets up UI navigation focus system
        /// From UI docs: "Navigation order tagged via FocusController.SetFocusIndex()"
        /// </summary>
        private void SetupUINavigation()
        {
            if (uiDocument?.rootVisualElement == null)
                return;
            
            var root = uiDocument.rootVisualElement;
            
            // Setup focus indices for navigable elements
            SetupFocusIndices(root);
            
            // Set initial focus
            if (enableGamepadNavigation)
            {
                SetFocusToFirstElement();
            }
        }
        
        /// <summary>
        /// Sets up focus indices for UI elements
        /// </summary>
        /// <param name="root">Root visual element</param>
        private void SetupFocusIndices(VisualElement root)
        {
            int focusIndex = 0;
            
            // Find all focusable elements and assign indices
            var buttons = root.Query<Button>().ToList();
            var listViews = root.Query<ListView>().ToList();
            
            foreach (var button in buttons)
            {
                button.focusable = true;
                button.tabIndex = focusIndex++;
                
                if (enableDebugLogging)
                    Debug.Log($"InputSystemIntegration: Set focus index {button.tabIndex} for button {button.name}");
            }
            
            foreach (var listView in listViews)
            {
                listView.focusable = true;
                listView.tabIndex = focusIndex++;
                
                if (enableDebugLogging)
                    Debug.Log($"InputSystemIntegration: Set focus index {listView.tabIndex} for ListView {listView.name}");
            }
        }
        
        /// <summary>
        /// Sets up world interaction for 3D raycasting
        /// From UI docs: "pointer clicks must bubble to simulation only when over 3-D world"
        /// "PanelRaycaster that consumes clicks if pointerEvent.target.panel is UI; otherwise dispatches to RaycastWorld()"
        /// </summary>
        private void SetupWorldInteraction()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null)
                {
                    if (enableDebugLogging)
                        Debug.LogWarning("InputSystemIntegration: No world camera found for 3D interaction");
                    return;
                }
            }
            
            // Setup world interaction will be handled by the main UI manager
            // This is a placeholder for the full implementation
        }
        
        /// <summary>
        /// Handles navigation input
        /// </summary>
        /// <param name="context">Input action context</param>
        private void OnNavigate(InputAction.CallbackContext context)
        {
            if (!enableGamepadNavigation || !isInitialized)
                return;
            
            var navigationVector = context.ReadValue<Vector2>();
            
            if (navigationVector.y > 0.5f) // Up
            {
                NavigateUp();
            }
            else if (navigationVector.y < -0.5f) // Down
            {
                NavigateDown();
            }
            else if (navigationVector.x < -0.5f) // Left
            {
                NavigateLeft();
            }
            else if (navigationVector.x > 0.5f) // Right
            {
                NavigateRight();
            }
        }
        
        /// <summary>
        /// Handles submit input (confirm/activate)
        /// </summary>
        /// <param name="context">Input action context</param>
        private void OnSubmit(InputAction.CallbackContext context)
        {
            if (!isInitialized)
                return;
            
            if (currentFocusedElement is Button button)
            {
                // Simulate button click
                button.Focus();
                var clickEvent = ClickEvent.GetPooled();
                button.SendEvent(clickEvent);
                
                if (enableDebugLogging)
                    Debug.Log($"InputSystemIntegration: Submitted button {button.name}");
            }
            else if (currentFocusedElement is ListView listView)
            {
                // Handle ListView selection
                var selectedIndex = listView.selectedIndex;
                if (selectedIndex >= 0)
                {
                    // Trigger selection event
                    listView.onItemsChosen?.Invoke(new[] { listView.itemsSource[selectedIndex] });
                }
            }
        }
        
        /// <summary>
        /// Handles cancel input
        /// </summary>
        /// <param name="context">Input action context</param>
        private void OnCancel(InputAction.CallbackContext context)
        {
            if (!isInitialized)
                return;
            
            // Clear selection or go back
            UIBus.PublishSelectionCleared();
            
            if (enableDebugLogging)
                Debug.Log("InputSystemIntegration: Cancel pressed");
        }
        
        /// <summary>
        /// Handles click input
        /// </summary>
        /// <param name="context">Input action context</param>
        private void OnClick(InputAction.CallbackContext context)
        {
            if (!isInitialized)
                return;
            
            var pointerPosition = pointAction.ReadValue<Vector2>();
            
            // Check if click is over UI or world
            if (IsPointerOverUI(pointerPosition))
            {
                // UI handles the click
                if (enableDebugLogging)
                    Debug.Log("InputSystemIntegration: Click consumed by UI");
            }
            else
            {
                // Raycast into 3D world
                RaycastWorld(pointerPosition);
            }
        }
        
        /// <summary>
        /// Checks if pointer is over UI elements
        /// </summary>
        /// <param name="screenPosition">Screen position</param>
        /// <returns>True if over UI</returns>
        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (uiDocument?.rootVisualElement == null)
                return false;
            
            var panelPosition = RuntimePanelUtils.ScreenToPanel(
                uiDocument.rootVisualElement.panel,
                screenPosition
            );
            
            var elementUnderPointer = uiDocument.rootVisualElement.panel.Pick(panelPosition);
            return elementUnderPointer != null;
        }
        
        /// <summary>
        /// Performs raycast into 3D world
        /// </summary>
        /// <param name="screenPosition">Screen position</param>
        private void RaycastWorld(Vector2 screenPosition)
        {
            if (worldCamera == null)
                return;
            
            var ray = worldCamera.ScreenPointToRay(screenPosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, raycastLayers))
            {
                // Handle world interaction
                HandleWorldClick(hit);
                
                if (enableDebugLogging)
                    Debug.Log($"InputSystemIntegration: World click at {hit.point}");
            }
        }
        
        /// <summary>
        /// Handles clicks on 3D world objects
        /// </summary>
        /// <param name="hit">Raycast hit information</param>
        private void HandleWorldClick(RaycastHit hit)
        {
            // In a full implementation, this would:
            // 1. Convert world position to grid position
            // 2. Check if there's a module at that position
            // 3. Either select the module or place a new part
            // 4. Send appropriate commands via UIBus
            
            // Placeholder implementation
            var gridPosition = WorldToGridPosition(hit.point);
            UIBus.PublishModuleSelected(gridPosition);
        }
        
        /// <summary>
        /// Converts world position to grid position
        /// </summary>
        /// <param name="worldPosition">World position</param>
        /// <returns>Grid position</returns>
        private Unity.Mathematics.int3 WorldToGridPosition(Vector3 worldPosition)
        {
            // Convert world coordinates to grid coordinates
            // This would use the actual grid cell size from GameConstants
            return new Unity.Mathematics.int3(
                Mathf.RoundToInt(worldPosition.x),
                Mathf.RoundToInt(worldPosition.y),
                Mathf.RoundToInt(worldPosition.z)
            );
        }
        
        // Navigation methods
        
        private void NavigateUp()
        {
            NavigateToIndex(currentFocusIndex - 1);
        }
        
        private void NavigateDown()
        {
            NavigateToIndex(currentFocusIndex + 1);
        }
        
        private void NavigateLeft()
        {
            // In a grid layout, this might navigate horizontally
            NavigateToIndex(currentFocusIndex - 1);
        }
        
        private void NavigateRight()
        {
            // In a grid layout, this might navigate horizontally
            NavigateToIndex(currentFocusIndex + 1);
        }
        
        private void NavigateToIndex(int index)
        {
            if (uiDocument?.rootVisualElement == null)
                return;
            
            var focusableElements = uiDocument.rootVisualElement.Query()
                .Where(e => e.focusable && e.tabIndex >= 0)
                .OrderBy(e => e.tabIndex)
                .ToList();
            
            if (focusableElements.Count == 0)
                return;
            
            // Wrap around navigation
            index = (index + focusableElements.Count) % focusableElements.Count;
            currentFocusIndex = index;
            
            var elementToFocus = focusableElements[index];
            elementToFocus.Focus();
            currentFocusedElement = elementToFocus;
            
            if (enableDebugLogging)
                Debug.Log($"InputSystemIntegration: Focused element {elementToFocus.name} at index {index}");
        }
        
        private void SetFocusToFirstElement()
        {
            NavigateToIndex(0);
        }
    }
}