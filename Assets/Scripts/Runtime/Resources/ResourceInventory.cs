using System;
using UnityEngine;

namespace XianXia.Unity.Resources
{
    public sealed class ResourceInventory : MonoBehaviour
    {
        [SerializeField] private int wood;
        [SerializeField] private int food;
        [SerializeField] private int herb;
        [SerializeField] private int concealGrass;

        public event Action<ResourceType, int, int> ResourceChanged;

        public int GetAmount(ResourceType type)
        {
            return type switch
            {
                ResourceType.Wood => wood,
                ResourceType.Food => food,
                ResourceType.Herb => herb,
                ResourceType.ConcealGrass => concealGrass,
                _ => 0
            };
        }

        public void ConfigureStartingAmounts(int woodAmount, int foodAmount, int herbAmount, int concealGrassAmount)
        {
            wood = Mathf.Max(0, woodAmount);
            food = Mathf.Max(0, foodAmount);
            herb = Mathf.Max(0, herbAmount);
            concealGrass = Mathf.Max(0, concealGrassAmount);
        }

        public void Add(ResourceType type, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int current = GetAmount(type);
            int updated = current + amount;
            SetAmount(type, updated);
            ResourceChanged?.Invoke(type, amount, updated);
        }

        public bool TrySpend(ResourceType type, int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            int current = GetAmount(type);
            if (current < amount)
            {
                return false;
            }

            int updated = current - amount;
            SetAmount(type, updated);
            ResourceChanged?.Invoke(type, -amount, updated);
            return true;
        }

        private void SetAmount(ResourceType type, int amount)
        {
            int clamped = Mathf.Max(0, amount);
            switch (type)
            {
                case ResourceType.Wood:
                    wood = clamped;
                    break;
                case ResourceType.Food:
                    food = clamped;
                    break;
                case ResourceType.Herb:
                    herb = clamped;
                    break;
                case ResourceType.ConcealGrass:
                    concealGrass = clamped;
                    break;
            }
        }
    }
}
