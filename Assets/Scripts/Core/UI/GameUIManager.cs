using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Profiling;
using System.Collections.Generic;

namespace MarbleMaker.Core.UI
{
    /// <summary>
    /// Main UI Manager for the game
    /// From UI docs: "GameUIManager (MonoB) <-- Main Thread • Holds UIDocument (root) • Injects ViewModels • Subscribes to UIBus"
    /// "GameUIManager lives in the Authoring World (main thread) and owns one UIDocument with a single UXML root (Root.uxml)"
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        
        [Header("ViewModels")]
        [SerializeField] private EconomyViewModel economyViewModel;
        [SerializeField] private PartsTrayViewModel partsTrayViewModel;
        [SerializeField] private InspectorViewModel inspectorViewModel;
        [SerializeField] private SimulationController simulationController;
        
        [Header("Performance Settings")]
        [SerializeField] private bool enablePerformanceProfiling = true;
        [SerializeField] private float uiUpdateInterval = 0.016f; // ~60 FPS
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogging = false;
        
        // UI Elements (cached for performance)
        private VisualElement root;
        private VisualElement topControlsRegion;
        private VisualElement partsTrayRegion;
        private VisualElement inspectorRegion;
        private VisualElement modalRegion;
        
        // Top Controls
        private Button playButton;
        private Button pauseButton;
        private Button resetButton;
        private Button undoButton;
        private Label coinLabel;
        private Label partTokenLabel;
        
        // Parts Tray
        private ListView partsList;
        private VisualTreeAsset traySlotTemplate;
        
        // Inspector
        private Label selectedPartLabel;
        private Label upgradeLevelLabel;
        private Button upgradeButton;
        private Label interactionHintLabel;
        
        // Modal
        private VisualElement tooltipElement;
        
        // State
        private bool isInitialized = false;
        private float lastUIUpdateTime = 0f;
        
        private void Awake()
        {
            // Ensure UIDocument is available
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
            
            if (uiDocument == null)
            {
                Debug.LogError("GameUIManager: UIDocument component not found");
                enabled = false;
                return;
            }
        }
        
        private void Start()
        {
            InitializeUI();
        }
        
        private void OnEnable()
        {
            // Subscribe to UIBus events for UI updates
            UIBus.OnSimulationSnapshot += HandleSimulationSnapshot;
            UIBus.OnEconomySnapshot += HandleEconomySnapshot;
            UIBus.OnSelectionSnapshot += HandleSelectionSnapshot;
            UIBus.OnTooltipShow += HandleTooltipShow;
            UIBus.OnTooltipHide += HandleTooltipHide;
        }
        
        private void OnDisable()
        {
            // Unsubscribe from UIBus events
            UIBus.OnSimulationSnapshot -= HandleSimulationSnapshot;
            UIBus.OnEconomySnapshot -= HandleEconomySnapshot;
            UIBus.OnSelectionSnapshot -= HandleSelectionSnapshot;
            UIBus.OnTooltipShow -= HandleTooltipShow;
            UIBus.OnTooltipHide -= HandleTooltipHide;
        }
        
        private void Update()
        {
            if (!isInitialized)
                return;
            
            // Update simulation controller
            if (simulationController != null)
            {
                simulationController.UpdateController();
            }
            
            // Update UI at specified interval
            if (Time.time - lastUIUpdateTime >= uiUpdateInterval)
            {
                UpdateUI();
                lastUIUpdateTime = Time.time;
            }
        }
        
        /// <summary>
        /// Initializes the UI system
        /// From UI docs checklist: "Create Root.uxml with empty regions. Implement GameUIManager: loads UIDocument, injects VMs, hooks UIBus"
        /// </summary>
        private void InitializeUI()
        {
            if (isInitialized)
                return;
            
            // Get root element
            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("GameUIManager: Root visual element not found");
                return;
            }
            
            // Cache UI regions
            CacheUIRegions();
            
            // Cache UI elements
            CacheUIElements();
            
            // Setup event handlers
            SetupEventHandlers();
            
            // Inject ViewModels
            InjectViewModels();
            
            // Apply initial styling
            ApplyInitialStyling();
            
            isInitialized = true;
            
            if (enableDebugLogging)
                Debug.Log("GameUIManager: UI initialized successfully");
        }
        
        /// <summary>
        /// Caches references to UI regions
        /// From UI docs: "Each major HUD region sits under its own <VisualElement class='region'>"
        /// </summary>
        private void CacheUIRegions()
        {
            topControlsRegion = root.Q<VisualElement>("top-controls");
            partsTrayRegion = root.Q<VisualElement>("parts-tray");
            inspectorRegion = root.Q<VisualElement>("inspector");
            modalRegion = root.Q<VisualElement>("modal");
            
            if (topControlsRegion == null) Debug.LogWarning("GameUIManager: top-controls region not found");
            if (partsTrayRegion == null) Debug.LogWarning("GameUIManager: parts-tray region not found");
            if (inspectorRegion == null) Debug.LogWarning("GameUIManager: inspector region not found");
            if (modalRegion == null) Debug.LogWarning("GameUIManager: modal region not found");
        }
        
        /// <summary>
        /// Caches references to UI elements for performance
        /// </summary>
        private void CacheUIElements()
        {
            // Top Controls elements
            playButton = root.Q<Button>("play-button");
            pauseButton = root.Q<Button>("pause-button");
            resetButton = root.Q<Button>("reset-button");
            undoButton = root.Q<Button>("undo-button");
            coinLabel = root.Q<Label>("coin-label");
            partTokenLabel = root.Q<Label>("part-token-label");
            
            // Parts Tray elements
            partsList = root.Q<ListView>("parts-list");
            
            // Inspector elements
            selectedPartLabel = root.Q<Label>("selected-part-label");
            upgradeLevelLabel = root.Q<Label>("upgrade-level-label");
            upgradeButton = root.Q<Button>("upgrade-button");
            interactionHintLabel = root.Q<Label>("interaction-hint-label");
            
            // Modal elements
            tooltipElement = root.Q<VisualElement>("tooltip");
            
            // Load templates
            // traySlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/TraySlot");
        }
        
        /// <summary>
        /// Sets up event handlers for UI elements
        /// From UI docs: "TopControls (▶ ⏸ ⟲ ⭮) Button .clickable.clicked += UI → UIBus.SimCommand"
        /// </summary>
        private void SetupEventHandlers()
        {
            // Top Controls button handlers
            if (playButton != null)
                playButton.clicked += () => UIBus.PublishSimCommand(UIBus.SimCommand.Play);
            
            if (pauseButton != null)
                pauseButton.clicked += () => UIBus.PublishSimCommand(UIBus.SimCommand.Pause);
            
            if (resetButton != null)
                resetButton.clicked += () => UIBus.PublishSimCommand(UIBus.SimCommand.Reset);
            
            if (undoButton != null)
                undoButton.clicked += () => UIBus.PublishSimCommand(UIBus.SimCommand.Undo);
            
            // Inspector button handler
            if (upgradeButton != null)
            {
                upgradeButton.clicked += () =>
                {
                    if (inspectorViewModel != null)
                        inspectorViewModel.TryUpgradeSelected();
                };
            }
            
            // Parts List setup
            SetupPartsList();
        }
        
        /// <summary>
        /// Sets up the parts list ListView
        /// From UI docs: "PartsTray ListView with makeItem/bindItem → ObservableList<PartDef>"
        /// </summary>
        private void SetupPartsList()
        {
            if (partsList == null || partsTrayViewModel == null)
                return;
            
            // Set up ListView data source
            partsList.itemsSource = partsTrayViewModel.UnlockedParts;
            
            // Set up item creation
            partsList.makeItem = () =>
            {
                var slot = new VisualElement();
                slot.AddToClassList("tray-slot");
                
                var icon = new VisualElement();
                icon.name = "icon";
                icon.AddToClassList("part-icon");
                slot.Add(icon);
                
                var label = new Label();
                label.name = "label";
                label.AddToClassList("part-label");
                slot.Add(label);
                
                return slot;
            };
            
            // Set up item binding
            // From UI docs: "trayListView.bindItem = (ve, i) => { var slot = ve.Q<VisualElement>("icon"); slot.style.backgroundImage = unlockedParts[i].IconTex; ve.userData = unlockedParts[i]; }"
            partsList.bindItem = (element, index) =>
            {
                if (index >= partsTrayViewModel.UnlockedParts.Count)
                    return;
                
                var partDef = partsTrayViewModel.UnlockedParts[index];
                var icon = element.Q<VisualElement>("icon");
                var label = element.Q<Label>("label");
                
                if (icon != null && partDef.iconTexture != null)
                {
                    icon.style.backgroundImage = new StyleBackground(partDef.iconTexture);
                }
                
                if (label != null)
                {
                    label.text = partDef.displayName;
                }
                
                // Store part data for click handling
                element.userData = partDef;
                
                // Add click handler
                element.RegisterCallback<ClickEvent>(evt =>
                {
                    if (partDef != null)
                    {
                        UIBus.PublishPartSelected(partDef.partID);
                    }
                });
            };
            
            // Subscribe to parts list changes
            if (partsTrayViewModel != null)
            {
                partsTrayViewModel.OnPartsRefreshed += RefreshPartsList;
            }
        }
        
        /// <summary>
        /// Injects ViewModels into UI binding contexts
        /// From UI docs: "assign BindingContext manually in C# to keep GC-alloc free"
        /// </summary>
        private void InjectViewModels()
        {
            // Subscribe to ViewModel events for data binding
            if (economyViewModel != null)
            {
                economyViewModel.OnCoinsChanged += UpdateCoinDisplay;
                economyViewModel.OnPartTokensChanged += UpdatePartTokenDisplay;
            }
            
            if (inspectorViewModel != null)
            {
                inspectorViewModel.OnSelectionChanged += UpdateInspectorVisibility;
                inspectorViewModel.OnSelectedPartChanged += UpdateSelectedPartDisplay;
                inspectorViewModel.OnUpgradeLevelChanged += UpdateUpgradeLevelDisplay;
                inspectorViewModel.OnCanUpgradeChanged += UpdateUpgradeButtonState;
            }
        }
        
        /// <summary>
        /// Applies initial styling and themes
        /// </summary>
        private void ApplyInitialStyling()
        {
            // Apply theme classes
            root.AddToClassList("marble-maker-theme");
            
            // Set initial visibility states
            if (tooltipElement != null)
                tooltipElement.style.visibility = Visibility.Hidden;
        }
        
        /// <summary>
        /// Updates UI elements
        /// From UI docs: "Profiling budget – UITK CPU ≤ 1 ms on Deck"
        /// </summary>
        private void UpdateUI()
        {
            if (!isInitialized)
                return;
            
            if (enablePerformanceProfiling)
                Profiler.BeginSample("GameUIManager.UpdateUI");
            
            try
            {
                // Update would happen through data binding events
                // This method is kept for any manual updates needed
            }
            finally
            {
                if (enablePerformanceProfiling)
                    Profiler.EndSample();
            }
        }
        
        // Event Handlers for UIBus
        
        private void HandleSimulationSnapshot(UIBus.SimulationSnapshot snapshot)
        {
            // Update simulation state displays
            // Would update tick counter, marble count, etc.
        }
        
        private void HandleEconomySnapshot(UIBus.EconomySnapshot snapshot)
        {
            // Economy updates are handled through EconomyViewModel events
        }
        
        private void HandleSelectionSnapshot(UIBus.SelectionSnapshot snapshot)
        {
            // Selection updates are handled through InspectorViewModel events
        }
        
        private void HandleTooltipShow(string text)
        {
            if (tooltipElement != null)
            {
                var tooltipLabel = tooltipElement.Q<Label>("tooltip-text");
                if (tooltipLabel != null)
                    tooltipLabel.text = text;
                
                tooltipElement.style.visibility = Visibility.Visible;
            }
        }
        
        private void HandleTooltipHide()
        {
            if (tooltipElement != null)
            {
                tooltipElement.style.visibility = Visibility.Hidden;
            }
        }
        
        // Data Binding Update Methods
        
        private void UpdateCoinDisplay(int coins)
        {
            if (coinLabel != null && economyViewModel != null)
            {
                coinLabel.text = economyViewModel.GetCoinsDisplayString();
            }
        }
        
        private void UpdatePartTokenDisplay(int partTokens)
        {
            if (partTokenLabel != null && economyViewModel != null)
            {
                partTokenLabel.text = economyViewModel.GetPartTokensDisplayString();
            }
        }
        
        private void UpdateInspectorVisibility(bool hasSelection)
        {
            if (inspectorRegion != null)
            {
                inspectorRegion.style.visibility = hasSelection ? Visibility.Visible : Visibility.Hidden;
            }
        }
        
        private void UpdateSelectedPartDisplay(string partId)
        {
            if (selectedPartLabel != null && inspectorViewModel != null)
            {
                selectedPartLabel.text = inspectorViewModel.GetSelectedPartDisplayName();
            }
            
            if (interactionHintLabel != null && inspectorViewModel != null)
            {
                var hint = inspectorViewModel.GetInteractionHint();
                interactionHintLabel.text = hint;
                interactionHintLabel.style.visibility = string.IsNullOrEmpty(hint) ? Visibility.Hidden : Visibility.Visible;
            }
        }
        
        private void UpdateUpgradeLevelDisplay(int level)
        {
            if (upgradeLevelLabel != null && inspectorViewModel != null)
            {
                upgradeLevelLabel.text = inspectorViewModel.GetUpgradeLevelDisplay();
            }
        }
        
        private void UpdateUpgradeButtonState(bool canUpgrade)
        {
            if (upgradeButton != null && inspectorViewModel != null)
            {
                upgradeButton.SetEnabled(canUpgrade);
                upgradeButton.text = inspectorViewModel.GetUpgradeButtonText();
            }
        }
        
        /// <summary>
        /// Refreshes the parts list
        /// From UI docs: ".schedule.Execute(() => trayListView.RefreshItems())"
        /// </summary>
        private void RefreshPartsList()
        {
            if (partsList != null)
            {
                partsList.schedule.Execute(() => partsList.RefreshItems());
            }
        }
        
        /// <summary>
        /// Gets UI performance statistics
        /// </summary>
        /// <returns>Performance info string</returns>
        public string GetPerformanceInfo()
        {
            var info = $"UI Update Interval: {uiUpdateInterval:F3}s\n";
            info += $"Is Initialized: {isInitialized}\n";
            info += $"Root Element: {(root != null ? "OK" : "Missing")}";
            return info;
        }
        
        /// <summary>
        /// For debugging - logs all cached UI elements status
        /// </summary>
        [ContextMenu("Debug UI Elements")]
        public void DebugUIElements()
        {
            Debug.Log($"GameUIManager Debug:\n" +
                     $"Root: {(root != null ? "✓" : "✗")}\n" +
                     $"Play Button: {(playButton != null ? "✓" : "✗")}\n" +
                     $"Parts List: {(partsList != null ? "✓" : "✗")}\n" +
                     $"Inspector: {(inspectorRegion != null ? "✓" : "✗")}\n" +
                     $"Economy VM: {(economyViewModel != null ? "✓" : "✗")}\n" +
                     $"Parts Tray VM: {(partsTrayViewModel != null ? "✓" : "✗")}\n" +
                     $"Inspector VM: {(inspectorViewModel != null ? "✓" : "✗")}\n" +
                     $"Simulation Controller: {(simulationController != null ? "✓" : "✗")}");
        }
    }
}