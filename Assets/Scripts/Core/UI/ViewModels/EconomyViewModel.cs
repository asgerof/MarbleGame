using UnityEngine;
using System;

namespace MarbleMaker.Core.UI
{
    /// <summary>
    /// Economy ViewModel for coin counter and currency management
    /// From UI docs: "UserEconomy.Coins → EconomyVM (scriptable object)"
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyViewModel", menuName = "MarbleMaker/UI/Economy ViewModel")]
    public class EconomyViewModel : ScriptableObject
    {
        [Header("Economy State")]
        [SerializeField] private int coins = 100;
        [SerializeField] private int partTokens = 0;
        [SerializeField] private int idleIncomeRate = 10;
        
        [Header("Settings")]
        [SerializeField] private bool enableDebugLogging = false;
        
        // Events for data binding
        public event Action<int> OnCoinsChanged;
        public event Action<int> OnPartTokensChanged;
        public event Action<int> OnIdleIncomeRateChanged;
        
        /// <summary>
        /// Current coin amount
        /// </summary>
        public int Coins
        {
            get => coins;
            private set
            {
                if (coins != value)
                {
                    coins = value;
                    OnCoinsChanged?.Invoke(coins);
                    
                    if (enableDebugLogging)
                        Debug.Log($"EconomyViewModel: Coins updated to {coins}");
                }
            }
        }
        
        /// <summary>
        /// Current part tokens amount
        /// </summary>
        public int PartTokens
        {
            get => partTokens;
            private set
            {
                if (partTokens != value)
                {
                    partTokens = value;
                    OnPartTokensChanged?.Invoke(partTokens);
                    
                    if (enableDebugLogging)
                        Debug.Log($"EconomyViewModel: Part tokens updated to {partTokens}");
                }
            }
        }
        
        /// <summary>
        /// Current idle income rate
        /// </summary>
        public int IdleIncomeRate
        {
            get => idleIncomeRate;
            private set
            {
                if (idleIncomeRate != value)
                {
                    idleIncomeRate = value;
                    OnIdleIncomeRateChanged?.Invoke(idleIncomeRate);
                    
                    if (enableDebugLogging)
                        Debug.Log($"EconomyViewModel: Idle income rate updated to {idleIncomeRate}");
                }
            }
        }
        
        private void OnEnable()
        {
            // Subscribe to UIBus economy events
            UIBus.OnEconomySnapshot += HandleEconomySnapshot;
        }
        
        private void OnDisable()
        {
            // Unsubscribe from UIBus events
            UIBus.OnEconomySnapshot -= HandleEconomySnapshot;
        }
        
        /// <summary>
        /// Handles economy snapshot updates from the simulation
        /// </summary>
        /// <param name="snapshot">Economy state snapshot</param>
        private void HandleEconomySnapshot(UIBus.EconomySnapshot snapshot)
        {
            Coins = snapshot.coins;
            PartTokens = snapshot.partTokens;
            IdleIncomeRate = snapshot.idleIncomeRate;
        }
        
        /// <summary>
        /// Attempts to spend coins
        /// </summary>
        /// <param name="amount">Amount to spend</param>
        /// <returns>True if successful, false if insufficient funds</returns>
        public bool TrySpendCoins(int amount)
        {
            if (coins >= amount)
            {
                Coins = coins - amount;
                return true;
            }
            
            if (enableDebugLogging)
                Debug.LogWarning($"EconomyViewModel: Insufficient coins. Need {amount}, have {coins}");
            
            return false;
        }
        
        /// <summary>
        /// Attempts to spend part tokens
        /// </summary>
        /// <param name="amount">Amount to spend</param>
        /// <returns>True if successful, false if insufficient tokens</returns>
        public bool TrySpendPartTokens(int amount)
        {
            if (partTokens >= amount)
            {
                PartTokens = partTokens - amount;
                return true;
            }
            
            if (enableDebugLogging)
                Debug.LogWarning($"EconomyViewModel: Insufficient part tokens. Need {amount}, have {partTokens}");
            
            return false;
        }
        
        /// <summary>
        /// Adds coins to the economy
        /// </summary>
        /// <param name="amount">Amount to add</param>
        public void AddCoins(int amount)
        {
            if (amount > 0)
            {
                Coins = coins + amount;
            }
        }
        
        /// <summary>
        /// Adds part tokens to the economy
        /// </summary>
        /// <param name="amount">Amount to add</param>
        public void AddPartTokens(int amount)
        {
            if (amount > 0)
            {
                PartTokens = partTokens + amount;
            }
        }
        
        /// <summary>
        /// Gets formatted coin display string
        /// </summary>
        /// <returns>Formatted coin string</returns>
        public string GetCoinsDisplayString()
        {
            if (coins >= 1000000)
                return $"{coins / 1000000.0f:F1}M";
            else if (coins >= 1000)
                return $"{coins / 1000.0f:F1}K";
            else
                return coins.ToString();
        }
        
        /// <summary>
        /// Gets formatted part tokens display string
        /// </summary>
        /// <returns>Formatted part tokens string</returns>
        public string GetPartTokensDisplayString()
        {
            return partTokens.ToString();
        }
        
        /// <summary>
        /// Checks if player can afford a purchase
        /// </summary>
        /// <param name="coinCost">Coin cost</param>
        /// <param name="tokenCost">Token cost</param>
        /// <returns>True if affordable</returns>
        public bool CanAfford(int coinCost, int tokenCost = 0)
        {
            return coins >= coinCost && partTokens >= tokenCost;
        }
        
        /// <summary>
        /// Resets economy to initial state
        /// </summary>
        public void Reset()
        {
            Coins = 100;
            PartTokens = 0;
            IdleIncomeRate = 10;
            
            if (enableDebugLogging)
                Debug.Log("EconomyViewModel: Reset to initial state");
        }
    }
}